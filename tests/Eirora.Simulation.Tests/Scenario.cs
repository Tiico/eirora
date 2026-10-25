using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
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
/// it, now and then off the home island, a producing building that can still be upgraded, given a road to its depot,
/// worked or paused, a tile that takes a road, a storehouse or a jetty to work or to cap the carriers of, a processing
/// building's minimum stock, an island's reservation and import order, a paused building to resume, a building and a
/// place in the staffing order, a project that is not first in the queue, a building to demolish, a tile at the fog's
/// edge, self-care within the window), and three in four commands that would come
/// while the villagers rest wait until a little after dawn, so most commands are accepted and change the world. The
/// rest are made blind, so every rejection stays covered. To know when the villagers rest, generating plays the inputs
/// into a world of its own.
/// <para>
/// A scenario is played on the prototype's map, or on the <see cref="Archipelago"/>, where three islands can each get a
/// store and a sea route home (TASK-045).
/// </para>
/// </remarks>
internal sealed class Scenario
{
    private const long Hour = 3600;

    // The tiles the design names, where most of the economy happens; blind commands also go to tiles around Hemön.
    private static readonly Hex[] s_named =
    [
        Tiles.S, Tiles.L1, Tiles.L2, Tiles.L3, Tiles.E1, Tiles.E2, Tiles.K1, Tiles.K2, Tiles.H1, Tiles.O1, Tiles.O2,
        Tiles.F1, Tiles.F2, Tiles.F3, Tiles.M1, Tiles.M2, Tiles.M3, Tiles.P1, Tiles.N0, Tiles.N1, Tiles.N2, Tiles.N9,
    ];

    // Where a suited building is sought on the prototype's map: Hemön and its waters, and Nyön.
    private static readonly Hex[] s_sites = [.. Hex.Origin.Within(6), .. PrototypeHome.NyonCentre.Within(2)];
    private static readonly BuildingKind[] s_buildings = Enum.GetValues<BuildingKind>();

    private Scenario(TimeReading install, RestSchedule schedule, bool archipelago, IReadOnlyList<Input> inputs)
    {
        Install = install;
        Schedule = schedule;
        OnArchipelago = archipelago;
        Inputs = inputs;
    }

    public TimeReading Install { get; }

    public RestSchedule Schedule { get; }

    /// <summary>Whether the world is on the <see cref="Archipelago"/> rather than the prototype's map.</summary>
    public bool OnArchipelago { get; }

    public IReadOnlyList<Input> Inputs { get; }

    /// <param name="stockRules">
    /// Whether the player's stock rules are among the commands (TASK-046); <see cref="BuiltOutWorld"/> leaves them out and
    /// sets them itself, so that its worlds are drawn as they were before the rules.
    /// </param>
    public static Scenario Generate(int seed, int length = 60, bool archipelago = false, bool stockRules = true)
    {
        var random = new Random(seed);
        var install = new TimeReading(At("2026-01-01 00:00", 1).UtcSeconds + (random.Next(360 * 24) * Hour), Stockholm, 3600 * (1 + random.Next(2)));
        var schedule = new RestSchedule(random.Next(96) * 15, (6 * 60) + (random.Next(9) * 30));
        return new Scenario(install, schedule, archipelago, Play(random, Start(install, schedule, archipelago), length, "k", stockRules));
    }

    /// <summary>
    /// Inputs that go on from <paramref name="world"/>, as one device plays after the save was shared (TASK-023);
    /// the keys carry <paramref name="device"/> so that two devices' keys never collide. The world is left as it is.
    /// </summary>
    public static IReadOnlyList<Input> Continue(int seed, World world, string device, int length = 20) =>
        Play(new Random(seed), SimulationSnapshot.Take(world).Restore(world.Map), length, device);

    private static List<Input> Play(Random random, World world, int length, string keyPrefix, bool stockRules = true)
    {
        var inputs = new List<Input>(length);
        long utc = world.Now.UtcSeconds;
        string zone = world.Now.ZoneId;
        int offset = world.Now.OffsetSeconds;
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
                string key = i > 0 && random.Next(100) < 15 ? $"{keyPrefix}{random.Next(i)}" : $"{keyPrefix}{i}";
                Add(new Command(key, Maker(random, stockRules)));
            }
        }

        void Add(Input input)
        {
            inputs.Add(input);
            input.ApplyTo(world);
        }

        return inputs;
    }

    public World Start() => Start(Install, Schedule, OnArchipelago);

    /// <summary>A save of the world the scenario starts from, to play the inputs into.</summary>
    public Save Begin() => Save.Begin(Start());

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

    private static World Start(TimeReading install, RestSchedule schedule, bool archipelago) =>
        archipelago ? Archipelago.Create(install, schedule) : World.Create(install, schedule);

    /// <summary>Where a suited building is sought: the islands that can be built on and their waters.</summary>
    internal static IReadOnlyList<Hex> SitesOf(World world) => world.Map.Hash == Archipelago.Map.Hash ? Archipelago.Sites : s_sites;

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
    private static Func<World, GameCommand> Maker(Random random, bool stockRules)
    {
        bool suited = random.Next(4) > 0;
        int pick = random.Next(int.MaxValue);
        var site = random.Next(10) < 7 ? s_named[random.Next(s_named.Length)] : new Hex(random.Next(-6, 7), random.Next(-6, 7));
        switch (random.Next(11))
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
                bool away = (pick >> 16) % 3 == 0; // From a draw already made, so that the other draws stay as they were.
                return world => suited && FreeSite(world, pick, away) is { } free ? new Build(free.Kind, free.Site) : new Build(building, site);
            case 5:
                return world => new Upgrade(suited ? BuiltSite(world, pick, b => b.Level < world.Content.For(b.Kind).MaxLevel && world.CanAfford(world.Content.Upgrade.Cost, b.Tile)) ?? site : site);
            case 6:
                int road = random.Next(stockRules ? 6 : 3);
                int cap = suited ? 1 + random.Next(Logistics.MostCarriers) : random.Next(-1, Logistics.MostCarriers + 2);

                // The stock rules (TASK-046), from the same draws: an amount up to the store's capacity or past it, any
                // resource, and a place in the import order or past it.
                int units = suited ? pick % 61 : (pick % 400) - 50;
                var resource = (Resource)(pick / 7 % Enum.GetValues<Resource>().Length);
                int import = suited ? pick / 11 % Enum.GetValues<Resource>().Length : (pick / 11 % 12) - 2;
                return world => road switch
                {
                    0 => new BuildRoad(suited ? BuiltSite(world, pick, b => world.RoadToDepot(b.Tile).Count > 0) ?? site : site),
                    1 => new LayRoad(suited ? RoadSite(world, pick) ?? site : site),
                    2 => new SetCarrierCap(suited ? DepotSite(world, pick, d => d.CarrierCap != cap) ?? site : site, cap),
                    3 => new SetReserve(suited ? BuiltSite(world, pick, b => b.Held is not null && b.Reserve != units) ?? site : site, units),
                    4 => new SetReservation(suited ? world.Islands[pick % world.Islands.Count] : "nowhere", resource, units),
                    _ => new MoveImport(suited ? world.Islands.LastOrDefault(i => i != world.HomeIsland) ?? world.HomeIsland : world.HomeIsland, resource, import),
                };
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
                bool depot = random.Next(4) == 0;
                return world => new Workday(suited ? (depot ? DepotSite(world, pick, d => d.Staffed && !d.WorkdayToday) : BuiltSite(world, pick, b => b.Staffed && !b.WorkdayToday)) ?? site : site);
            case 9:
                int work = random.Next(4);
                int place = random.Next(-1, 13);
                var project = (ProjectKind)random.Next(3);
                return world => work switch
                {
                    0 => new PauseBuilding(suited ? BuiltSite(world, pick, b => !b.Paused) ?? site : site),
                    1 => new ResumeBuilding(suited ? BuiltSite(world, pick, b => b.Paused) ?? site : site),
                    2 => suited && world.StaffingOrder.Count > 1
                        ? new Prioritize(world.StaffingOrder[pick % world.StaffingOrder.Count].Tile, place % world.StaffingOrder.Count)
                        : new Prioritize(world.StaffingOrder.Count > 0 && pick % 2 == 0 ? world.StaffingOrder[pick % world.StaffingOrder.Count].Tile : site, place),
                    _ => suited && world.Projects.Count > 1
                        ? new QueueFirst(world.Projects[1 + (pick % (world.Projects.Count - 1))].Kind, world.Projects[1 + (pick % (world.Projects.Count - 1))].Tile)
                        : new QueueFirst(project, site),
                };
            default:
                // A demolition in one case of three, drawn from what is already drawn, so the rest of the sequence stays.
                var change = new RestSchedule(random.Next(96) * 15, (5 * 60) + (random.Next(13) * 30));
                return world => pick % 3 > 0 ? new ChangeRestSchedule(change)
                    : new Demolish(suited && world.Buildings.Count > 0 ? world.Buildings[pick / 3 % world.Buildings.Count].Tile : site);
        }
    }

    /// <summary>
    /// One of the affordable buildings that the world would take on a tile, or none; <paramref name="away"/> prefers one
    /// off the home island, whose settlement's villagers then have to move there and whose materials may take room on
    /// the sea route (TASK-050).
    /// </summary>
    private static (BuildingKind Kind, Hex Site)? FreeSite(World world, int pick, bool away)
    {
        var free = SitesOf(world)
            .SelectMany(tile => s_buildings.Select(k => (Kind: k, Site: tile)))
            .Where(c => world.CheckBuild(c.Kind, c.Site) == Rejection.None && world.CanAfford(world.Content.For(c.Kind).Cost, c.Site))
            .ToList();
        var offHome = free.Where(c => world.Settlement.IslandOf(c.Site) != world.HomeIsland).ToList();
        free = away && offHome.Count > 0 ? offHome : free;
        return free.Count > 0 ? free[pick % free.Count] : null;
    }

    /// <summary>One of the tiles whose fog can be lifted now, or none.</summary>
    private static Hex? FogEdge(World world, int pick)
    {
        var edge = Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i]).Where(world.CanLiftFog).ToList();
        return edge.Count > 0 ? edge[pick % edge.Count] : null;
    }

    /// <summary>One of the tiles a road can be laid on now, or none.</summary>
    private static Hex? RoadSite(World world, int pick)
    {
        var free = SitesOf(world).Where(tile => world.CheckRoad(tile) == Rejection.None).ToList();
        return free.Count > 0 ? free[pick % free.Count] : null;
    }

    /// <summary>The tile of one of the world's storehouses and jetties that <paramref name="can"/> take the command, or none.</summary>
    private static Hex? DepotSite(World world, int pick, Func<Building, bool> can)
    {
        var depots = world.Buildings.Where(b => Logistics.IsDepot(b.Kind) && can(b)).ToList();
        return depots.Count > 0 ? depots[pick % depots.Count].Tile : null;
    }

    /// <summary>The tile of one of the world's producing buildings that <paramref name="can"/> take the command, or none.</summary>
    private static Hex? BuiltSite(World world, int pick, Func<Building, bool> can)
    {
        var built = world.Buildings.Where(b => world.Content.For(b.Kind).Produces && can(b)).ToList();
        return built.Count > 0 ? built[pick % built.Count].Tile : null;
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
