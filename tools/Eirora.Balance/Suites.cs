namespace Eirora.Balance;

/// <summary>The sets of runs the tool plays by name.</summary>
public static class Suites
{
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
