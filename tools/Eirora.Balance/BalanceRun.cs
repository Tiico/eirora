using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using Eirora.WorldGeneration;

namespace Eirora.Balance;

/// <summary>A map to play on: the prototype's handmade map or a generated one, made once however many runs use it.</summary>
public sealed class MapChoice
{
    private readonly Lazy<WorldMap> _map;

    private MapChoice(string name, ulong? seed, Func<WorldMap> make)
    {
        Name = name;
        Seed = seed;
        _map = new Lazy<WorldMap>(make);
    }

    public static MapChoice Prototype { get; } = new(PrototypeHome.GeneratorId, null, () => PrototypeHome.Map);

    /// <summary>The generator's name, such as procedural-archipelago-1, or the handmade map's.</summary>
    public string Name { get; }

    public ulong? Seed { get; }

    public WorldMap Map => _map.Value;

    /// <summary>A map from the procedural archipelago generator, version 1.</summary>
    public static MapChoice Generated(ulong seed)
    {
        var generator = WorldGenerators.Get(ProceduralArchipelagoV1.GeneratorId, 1);
        return new MapChoice($"{generator.Id}-{generator.Version}", seed, () => generator.Generate(seed));
    }
}

/// <summary>One bot playing one map with one strategy and one self-care profile.</summary>
public sealed record RunSpec(MapChoice Map, Strategy Strategy, SelfCareProfile Profile, int Days);

/// <summary>
/// Plays a world the way a player would, through the same commands and time readings as the client (INV-D2): the world
/// is made a quarter of an hour before its first dawn, so game day 1 is the first whole day; at each dawn the profile's
/// self-care is recorded and the strategy plays, and then time passes an hour at a time with the invariants checked.
/// The run ends just before the dawn after the last day, as the economy model's seven days do.
/// </summary>
public static class BalanceRun
{
    public const string Zone = "Etc/UTC";
    private const long Hour = 3600;

    /// <summary>Rest from 23:00 to 07:00, as the economy model's 16 waking hours.</summary>
    public static readonly RestSchedule Rest = new(23 * 60, 8 * 60);

    /// <summary>When every bot installs the game: 06:45 on Monday 5 January 2026, a quarter of an hour before dawn.</summary>
    public static readonly long InstallUtc = new DateTimeOffset(2026, 1, 5, 6, 45, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    public static RunResult Play(RunSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var world = World.Create(spec.Map.Map, new TimeReading(InstallUtc, Zone, 0), Rest);
        long startValue = Measures.Value(world);
        var plan = new Plan(spec.Strategy);
        var tally = new Tally();
        var invariants = new Invariants(world);

        long dawn = InstallUtc + (15 * 60);
        world.AdvanceTo(new TimeReading(dawn, Zone, 0));
        for (int day = 1; day <= spec.Days; day++, dawn += 24 * Hour)
        {
            if (world.GameDay != day || world.Phase != Phase.Active)
            {
                throw new InvalidOperationException($"Expected the dawn of game day {day}, but it is game day {world.GameDay}, {world.Phase}.");
            }

            spec.Profile.Record(world, day);
            plan.Play(new Turn(world, day, spec.Days, plan, tally));
            invariants.AfterCommands(world);
            plan.Note(world, day);
            for (int hour = 1; hour <= 24; hour++)
            {
                // The last hour stops a second short of the next dawn, so that the run ends before it.
                long until = dawn + (hour * Hour) - (day == spec.Days && hour == 24 ? 1 : 0);
                world.AdvanceTo(new TimeReading(until, Zone, 0));
                invariants.Check(world, day);
                plan.Note(world, day);
            }
        }

        return new RunResult(spec, world, startValue, plan, tally, invariants.Hours);
    }
}

/// <summary>What a run came to; amounts of goods and measures are whole millionths (<see cref="Quantity.PerUnit"/>).</summary>
public sealed class RunResult
{
    internal RunResult(RunSpec spec, World world, long startValue, Plan plan, Tally tally, int hoursChecked)
    {
        Spec = spec;
        Villagers = world.Villagers;
        Housing = world.Housing;
        Buildings = world.Buildings.Count;
        LevelTwo = world.Buildings.Count(b => b.Level == 2);
        Islands = world.Buildings.Select(b => b.Island).Distinct().Count();
        Stock = [.. Enum.GetValues<Resource>().Select(world.Stock)];
        DailyOutput = Measures.DailyOutput(world);
        Value = Measures.Value(world);
        StartValue = startValue;
        Used = [.. tally.Spent];
        Saved = [.. new[] { Force.Exploration, Force.BuildPower, Force.DawnPower }.Select(world.Available)];
        PlanFinishedOn = plan.FinishedOn;
        GoalsDone = plan.Count(GoalStatus.Done);
        GoalsSkipped = [.. plan.Skipped()];
        GoalsPending = [.. plan.Pending()];
        GoalLog = [.. plan.Log()];
        Commands = tally.Commands;
        Accepted = tally.Accepted;
        HoursChecked = hoursChecked;
        BuildingList = [.. world.Buildings.Select(b => $"{Names.Of(b.Kind)} {b.Tile}{(b.Level == 2 ? " nivå 2" : string.Empty)}")];
    }

    public RunSpec Spec { get; }

    public int Villagers { get; }

    public int Housing { get; }

    public int Buildings { get; }

    public int LevelTwo { get; }

    /// <summary>Islands with a building on them.</summary>
    public int Islands { get; }

    /// <summary>Whole units in store at home, by <see cref="Resource"/>.</summary>
    public IReadOnlyList<long> Stock { get; }

    public long DailyOutput { get; }

    public long Value { get; }

    public long StartValue { get; }

    /// <summary>Exploration, Build Power and Dawn Power spent.</summary>
    public IReadOnlyList<int> Used { get; }

    /// <summary>Exploration, Build Power and Dawn Power left at the end.</summary>
    public IReadOnlyList<int> Saved { get; }

    /// <summary>The game day the plan's last goal was done or skipped, or null if it never was.</summary>
    public int? PlanFinishedOn { get; }

    public int GoalsDone { get; }

    public IReadOnlyList<string> GoalsSkipped { get; }

    /// <summary>Goals still waiting at the end; on a long enough run, a plan that is stuck.</summary>
    public IReadOnlyList<string> GoalsPending { get; }

    /// <summary>Each goal of the plan and the game day it was done or skipped on.</summary>
    public IReadOnlyList<string> GoalLog { get; }

    public int Commands { get; }

    public int Accepted { get; }

    public int HoursChecked { get; }

    /// <summary>The buildings at the end, where they stand, in the order they were finished.</summary>
    public IReadOnlyList<string> BuildingList { get; }

    /// <summary>
    /// Development value gained per force used, in hundredths, rounded to nearest; null when nothing was used
    /// (economy-v1.md, section 7).
    /// </summary>
    public long? ValuePerForceHundredths
    {
        get
        {
            long used = Used.Sum();
            if (used == 0)
            {
                return null;
            }

            long gain = (Value - StartValue) * 100;
            long per = Quantity.Units(used);
            return gain >= 0 ? (gain + (per / 2)) / per : -((-gain + (per / 2)) / per);
        }
    }
}
