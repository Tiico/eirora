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
internal sealed class Scenario
{
    private const long Hour = 3600;

    // The tiles the design names, where most of the economy happens; other tiles are drawn from around Hemön.
    private static readonly Hex[] s_named =
    [
        Tiles.S, Tiles.L1, Tiles.L2, Tiles.L3, Tiles.E1, Tiles.E2, Tiles.K1, Tiles.K2, Tiles.H1, Tiles.O1, Tiles.O2,
        Tiles.F1, Tiles.F2, Tiles.F3, Tiles.M1, Tiles.M2, Tiles.M3, Tiles.P1, Tiles.N0, Tiles.N1, Tiles.N2, Tiles.N9,
    ];

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

                inputs.Add(new Advance(new TimeReading(utc, zone, offset)));
            }
            else
            {
                // Now and then a key is sent again, as a client does after a crash or a retry.
                string key = i > 0 && random.Next(100) < 15 ? $"k{random.Next(i)}" : $"k{i}";
                inputs.Add(new Command(key, Maker(random)));
            }
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

    // Commands are made against the current game day, so that self-care lands both inside and outside the window.
    private static Func<int, GameCommand> Maker(Random random)
    {
        var site = random.Next(10) < 7 ? s_named[random.Next(s_named.Length)] : new Hex(random.Next(-6, 7), random.Next(-6, 7));
        switch (random.Next(10))
        {
            case 0 or 1 or 2:
                int back = random.Next(-1, RecordSelfCare.DaysBack + 1);
                var habit = (Habit)random.Next(3);
                var source = (ContributionSource)random.Next(2);
                int total = habit switch
                {
                    Habit.Steps => random.Next(10) == 0 ? 70_000 : random.Next(25_000),
                    Habit.Workouts => random.Next(4),
                    _ => random.Next(10 * 60),
                };
                return day => new RecordSelfCare(day - back, habit, source, total);
            case 3 or 4:
                var building = (BuildingKind)random.Next(Enum.GetValues<BuildingKind>().Length);
                return _ => new Build(building, site);
            case 5:
                return _ => new Upgrade(site);
            case 6:
                return _ => new BuildRoad(site);
            case 7:
                int which = random.Next(4);
                // Around Hemön and out to Nyön, and now and then off the map.
                int q = random.Next(-14, 15) * (random.Next(20) == 0 ? 3 : 1);
                int r = random.Next(-14, 15);
                return _ => which switch
                {
                    0 => new Survey(),
                    1 => new SendExpedition(),
                    2 => new LiftFog(q, r),
                    _ => new Rush(),
                };
            case 8:
                return _ => new Workday(site);
            default:
                var change = new RestSchedule(random.Next(96) * 15, (5 * 60) + (random.Next(13) * 30));
                return _ => new ChangeRestSchedule(change);
        }
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

    internal sealed class Command(string key, Func<int, GameCommand> make) : Input
    {
        public string Key { get; } = key;

        public GameCommand For(World world) => make(world.GameDay);

        public override void ApplyTo(World world) => world.Apply(Key, For(world));

        public override void ApplyTo(Journal journal) => journal.Apply(Key, For(journal.World));
    }
}
