namespace Eirora.Balance;

/// <summary>The sets of runs the tool plays by name.</summary>
public static class Suites
{
    /// <summary>Twelve weeks: epoch 1 takes 6–8 weeks, and 11 with low self-care or without training (epoch-1.md, section 5).</summary>
    public const int EpochDays = 84;

    /// <summary>A year of game days.</summary>
    public const int YearDays = 365;

    /// <summary>The runs of economy v1, section 7: seven game days on the prototype's map.</summary>
    public static IReadOnlyList<RunSpec> EconomyV1() =>
    [
        new(MapChoice.Prototype, Strategy.Near, SelfCareProfile.Normal, 7),
        new(MapChoice.Prototype, Strategy.NearOrdered, SelfCareProfile.Normal, 7),
        new(MapChoice.Prototype, Strategy.Sea, SelfCareProfile.Normal, 7),
        new(MapChoice.Prototype, Strategy.Naive, SelfCareProfile.High, 7),
        new(MapChoice.Prototype, Strategy.NoTraining, SelfCareProfile.NoTraining, 7),
        new(MapChoice.Prototype, Strategy.Idle, SelfCareProfile.None, 7),
        new(MapChoice.Prototype, Strategy.Idle, SelfCareProfile.Normal, 7),
    ];

    /// <summary>
    /// The scenarios of economy v2, section 15: every thoughtful strategy and the naive one on each scenario's map with
    /// its profile, four weeks each.
    /// </summary>
    public static IReadOnlyList<RunSpec> Scenarios() => [.. Scenario.All.SelectMany(scenario => scenario.Runs())];

    /// <summary>
    /// A whole epoch (TASK-054): every compared strategy with every profile on each scenario's map for twelve weeks, so
    /// that the slowest profiles' milestone is within the run too (epoch-1.md, section 5).
    /// </summary>
    public static IReadOnlyList<RunSpec> Epoch() =>
    [
        .. from scenario in Scenario.All
           from profile in SelfCareProfile.All
           from run in scenario.Runs(profile)
           select run with { Days = EpochDays },
    ];

    /// <summary>
    /// A year (TASK-054): on each scenario's map, no player and no self-care, which must reach what the base rate gives
    /// and lose nothing (D-17), and Bygden with normal self-care, whose savings must stay under the soft cap (D-35).
    /// </summary>
    public static IReadOnlyList<RunSpec> Year() =>
    [
        .. from scenario in Scenario.All
           from run in new[] { (Strategy.Idle, SelfCareProfile.None), (Strategy.Village, SelfCareProfile.Normal) }
           select new RunSpec(scenario.Map, run.Item1, run.Item2, YearDays),
    ];

    /// <summary>Närområdet and Havet with normal self-care on generated maps, four weeks each.</summary>
    public static IReadOnlyList<RunSpec> Generated(ulong from = 0, int seeds = 20, int days = 28)
    {
        var runs = new List<RunSpec>();
        for (int i = 0; i < seeds; i++)
        {
            var map = MapChoice.Generated(from + (ulong)i);
            runs.Add(new(map, Strategy.Near, SelfCareProfile.Normal, days));
            runs.Add(new(map, Strategy.Sea, SelfCareProfile.Normal, days));
        }

        return runs;
    }
}
