using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests;

/// <summary>
/// A random start and sequence of inputs, as the generators in invariants.md describe: commands of every kind,
/// duplicate keys, self-care late and out of range, and time across zones, offsets and absences.
/// </summary>
/// <remarks>
/// Three in four commands are made to suit the world when they are applied (an affordable building on a site that takes
/// it, a producing building that can still be upgraded, given a road or worked, a tile at the fog's edge, self-care
/// within the window), and three in four commands that would come
/// while the villagers rest wait until a little after dawn, so most commands are accepted and change the world. The
/// rest are made blind, so every rejection stays covered. To know when the villagers rest, generating plays the inputs
/// into a world of its own.
/// </remarks>
internal sealed class Scenario
{
    private const long Hour = 3600;

    private static readonly string[] s_siteIds = PrototypeIsland.Sites.Select(s => s.Id).ToArray();
    private static readonly BuildingKind[] s_buildings = Enum.GetValues<BuildingKind>();

    private Scenario(TimeReading install, RestSchedule schedule, IReadOnlyList<Input> inputs)
    {
        Install = install;
        Schedule = schedule;
        Inputs = inputs;
    }

    public TimeReading Install { get; }

    public RestSchedule Schedule { get; }

    public IReadOnlyList<Input> Inputs { get; }

    public static Scenario Generate(int seed, int length = 60)
    {
        var random = new Random(seed);
        var install = new TimeReading(At("2026-01-01 00:00", 1).UtcSeconds + (random.Next(360 * 24) * Hour), Stockholm, 3600 * (1 + random.Next(2)));
        var schedule = new RestSchedule(random.Next(96) * 15, (6 * 60) + (random.Next(9) * 30));

        var inputs = new List<Input>(length);
        var world = World.Create(install, schedule);
        long utc = install.UtcSeconds;
        string zone = install.ZoneId;
        int offset = install.OffsetSeconds;
        for (int i = 0; i < length; i++)
        {
            if (random.Next(100) < 35)
            {
                utc += Span(random);

                if (random.Next(10) == 0)
                {
                    zone = zone == Stockholm ? NewYork : Stockholm;
                }

                if (random.Next(10) == 0 || (zone == Stockholm) != (offset > 0))
                {
                    offset = zone == Stockholm ? 3600 * (1 + random.Next(2)) : -3600 * (4 + random.Next(2));
                }

                Add(new Advance(new TimeReading(utc, zone, offset)));
            }
            else
            {
                if (random.Next(4) > 0 && world.Phase == Phase.Resting)
                {
                    utc += UntilAwake(world, random);
                    Add(new Advance(new TimeReading(utc, zone, offset)));
                }

                // Now and then a key is sent again, as a client does after a crash or a retry.
                string key = i > 0 && random.Next(100) < 15 ? $"k{random.Next(i)}" : $"k{i}";
                Add(new Command(key, Maker(random)));
            }
        }

        void Add(Input input)
        {
            inputs.Add(input);
            input.ApplyTo(world);
        }

        return new Scenario(install, schedule, inputs);
    }

    public World Start() => World.Create(Install, Schedule);

    /// <summary>Plays every input into a new world.</summary>
    public World Run()
    {
        var world = Start();
        foreach (var input in Inputs)
        {
            input.ApplyTo(world);
        }

        return world;
    }

    private static long Span(Random random) => random.Next(10) switch
    {
        < 6 => random.Next((int)(6 * Hour)),
        < 9 => (6 * Hour) + random.Next((int)(66 * Hour)),
        _ => (3 * 24 * Hour) + random.Next((int)(11 * 24 * Hour)),
    };

    /// <summary>The seconds from the world's time to a little after its rest ends by the local clock.</summary>
    private static long UntilAwake(World world, Random random)
    {
        const int Day = 24 * 60;
        var schedule = world.RestSchedule;
        int minute = (int)(((world.Now.UtcSeconds + world.Now.OffsetSeconds) % (Day * 60) + (Day * 60)) % (Day * 60) / 60);
        int intoRest = ((minute - schedule.StartMinuteOfDay) % Day + Day) % Day;
        return (Math.Max(schedule.DurationMinutes - intoRest, 0) + 1 + random.Next(60)) * 60L;
    }

    // Commands are made against the world when applied, so that they can suit it; everything random is drawn here, so
    // a scenario is the same whichever world it is applied to.
    private static Func<World, GameCommand> Maker(Random random)
    {
        bool suited = random.Next(4) > 0;
        int pick = random.Next(int.MaxValue);
        string site = s_siteIds[random.Next(s_siteIds.Length)];
        switch (random.Next(10))
        {
            case 0 or 1 or 2:
                int back = suited ? random.Next(RecordSelfCare.DaysBack) : random.Next(-1, RecordSelfCare.DaysBack + 1);
                var habit = (Habit)random.Next(3);
                var source = (ContributionSource)random.Next(2);
                int total = habit switch
                {
                    Habit.Steps => random.Next(10) == 0 ? 70_000 : random.Next(25_000),
                    Habit.Workouts => random.Next(4),
                    _ => random.Next(10 * 60),
                };
                return world => new RecordSelfCare(world.GameDay - back, habit, source, total);
            case 3 or 4:
                var building = s_buildings[random.Next(s_buildings.Length)];
                return world => suited && FreeSite(world, pick) is { } free ? new Build(free.Kind, free.Site) : new Build(building, site);
            case 5:
                return world => new Upgrade(suited ? BuiltSite(world, pick, b => b.Level < BuildingSpec.For(b.Kind).MaxLevel && world.Settlement.CanAfford(BuildingSpec.Upgrade.Cost)) ?? site : site);
            case 6:
                return world => new BuildRoad(suited ? BuiltSite(world, pick, b => !b.HasRoad && world.Settlement.DepotDistance(b.Site) > 1 && !world.Settlement.IsQueued(b.Site.Id, ProjectKind.Road)) ?? site : site);
            case 7:
                int which = random.Next(4);
                // Around Hemön and out to Nyön, and now and then off the map.
                int q = random.Next(-14, 15) * (random.Next(20) == 0 ? 3 : 1);
                int r = random.Next(-14, 15);
                return world => which switch
                {
                    0 => new Survey(),
                    1 => new SendExpedition(),
                    2 => suited && FogEdge(world, pick) is Hex edge ? new LiftFog(edge.Q, edge.R) : new LiftFog(q, r),
                    _ => new Rush(),
                };
            case 8:
                return world => new Workday(suited ? BuiltSite(world, pick, b => b.Staffed && !b.WorkdayToday) ?? site : site);
            default:
                var change = new RestSchedule(random.Next(96) * 15, (5 * 60) + (random.Next(13) * 30));
                return _ => new ChangeRestSchedule(change);
        }
    }

    /// <summary>One of the affordable buildings that the settlement would take on a site, or none.</summary>
    private static (BuildingKind Kind, string Site)? FreeSite(World world, int pick)
    {
        var free = PrototypeIsland.Sites
            .SelectMany(s => s_buildings.Select(k => (Kind: k, Site: s.Id)))
            .Where(c => world.Settlement.CheckSite(c.Kind, c.Site) == Rejection.None && world.Settlement.CanAfford(BuildingSpec.For(c.Kind).Cost))
            .ToList();
        return free.Count > 0 ? free[pick % free.Count] : null;
    }

    /// <summary>One of the tiles whose fog can be lifted now, or none.</summary>
    private static Hex? FogEdge(World world, int pick)
    {
        var edge = Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i]).Where(world.CanLiftFog).ToList();
        return edge.Count > 0 ? edge[pick % edge.Count] : null;
    }

    /// <summary>The site of one of the world's producing buildings that <paramref name="can"/> take the command, or none.</summary>
    private static string? BuiltSite(World world, int pick, Func<Building, bool> can)
    {
        var built = world.Buildings.Where(b => Upgrade.Produces(b.Kind) && can(b)).ToList();
        return built.Count > 0 ? built[pick % built.Count].Site.Id : null;
    }

    /// <summary>One input to a world: a command or a time reading.</summary>
    internal abstract class Input
    {
        public abstract void ApplyTo(World world);

        public abstract void ApplyTo(Journal journal);
    }

    internal sealed class Advance(TimeReading to) : Input
    {
        public TimeReading To { get; } = to;

        public override void ApplyTo(World world) => world.AdvanceTo(To);

        public override void ApplyTo(Journal journal) => journal.AdvanceTo(To);
    }

    internal sealed class Command(string key, Func<World, GameCommand> make) : Input
    {
        public string Key { get; } = key;

        public GameCommand For(World world) => make(world);

        public override void ApplyTo(World world) => world.Apply(Key, For(world));

        public override void ApplyTo(Journal journal) => journal.Apply(Key, For(journal.World));
    }
}
