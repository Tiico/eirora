using System.Globalization;

namespace Eirora.Balance.Tests;

/// <summary>
/// The scenarios of economy v2, section 15 (TASK-053, AC-06, D-34): on each a different strategy gives the greatest
/// development value after four weeks, no strategy is best on every scenario with every profile, and every thoughtful
/// strategy gives at least 1.3 times the naive one's value per force used. A rule change that makes one strategy best
/// everywhere fails the scenario where another one should be.
/// </summary>
public sealed class ScenarioTests
{
    private const decimal D34 = 1.3m;

    /// <summary>
    /// How much smaller, in percent, a world with more self-care may end with the strategy's own decisions, which differ
    /// with the forces; with the same decisions D-119 allows 0.1 %. Proposed in TASK-097 and awaiting the product owner.
    /// </summary>
    private const int OwnDecisionsPercent = 2;

    // Every compared strategy on every scenario with every profile, played once for the whole class.
    private static readonly Lazy<IReadOnlyList<RunResult>> s_runs = new(() =>
    [
        .. Scenario.All
            .SelectMany(scenario => SelfCareProfile.All.SelectMany(profile => scenario.Runs(profile)))
            .AsParallel().AsOrdered().Select(BalanceRun.Play),
    ]);

    public static TheoryData<string> Scenarios() => [.. Scenario.All.Select(s => s.Id)];

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void EachScenarioHasItsOwnBestStrategy(string id)
    {
        var scenario = Scenario.All.Single(s => s.Id == id);
        var runs = RunsOf(scenario, scenario.Profile);
        var best = runs.MaxBy(r => r.Value)!;

        Assert.True(
            best.Spec.Strategy == scenario.Best,
            $"{scenario.Name}: {best.Spec.Strategy.Name} is best, not {scenario.Best.Name}. If the change is intended, " +
            $"replace the scenario's row in economy-v2.md, section 15, with this:\n{Row(scenario, runs)}");
    }

    [Fact]
    public void AtLeastThreeStrategiesAreEachBestInAScenario()
    {
        var best = Scenario.All.Select(s => s.Best).ToList();

        Assert.Equal(best.Count, best.Distinct().Count());
        Assert.True(best.Count >= 3);
        Assert.Contains(Strategy.Near, best);
        Assert.Contains(Strategy.Sea, best);
    }

    [Fact]
    public void NoStrategyIsBestOnEveryScenarioWithEveryProfile()
    {
        var best = Scenario.All
            .SelectMany(scenario => SelfCareProfile.All.Select(profile => (scenario, profile, RunsOf(scenario, profile).MaxBy(r => r.Value)!.Spec.Strategy)))
            .ToList();

        Assert.True(
            best.Select(b => b.Strategy).Distinct().Count() > 1,
            $"{best[0].Strategy.Name} is best everywhere:\n{string.Join('\n', best.Select(b => $"{b.scenario.Name}, {b.profile.Name}: {b.Strategy.Name}"))}");
    }

    [Fact]
    public void EveryThoughtfulStrategyGivesAtLeastOnePointThreeTimesTheNaiveValuePerForce()
    {
        var failures = new List<string>();
        foreach (var scenario in Scenario.All)
        {
            foreach (var profile in SelfCareProfile.All)
            {
                var runs = RunsOf(scenario, profile);
                decimal naive = PerForce(runs.Single(r => r.Spec.Strategy == Strategy.Naive));
                foreach (var run in runs.Where(r => r.Spec.Strategy != Strategy.Naive))
                {
                    decimal ratio = PerForce(run) / naive;
                    if (ratio < D34)
                    {
                        failures.Add(string.Create(CultureInfo.InvariantCulture, $"{scenario.Name}, {profile.Name}: {run.Spec.Strategy.Name} {PerForce(run):0.00} against {naive:0.00}, {ratio:0.00} times"));
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures));
    }

    /// <summary>
    /// More self-care never lures a strategy into a smaller world with its own decisions (TASK-097, R-01, D-34, D-119):
    /// with more Exploration the planner once built where a carrier took the last builder, and counted another island's
    /// wool as clothes at home that the sea route never brought, and with more Build Power it kept the queue too busy to
    /// upgrade; low self-care then ended up to 17 % larger than normal. What is left is how choices made on other dawns
    /// play out, which goes both ways over the four weeks; <see cref="OwnDecisionsPercent"/> bounds it.
    /// </summary>
    [Fact]
    public void MoreSelfCareNeverEndsInASmallerWorldWithAnyStrategy()
    {
        SelfCareProfile[] rising = [SelfCareProfile.Low, SelfCareProfile.Normal, SelfCareProfile.High];
        var failures = new List<string>();
        foreach (var scenario in Scenario.All)
        {
            foreach (var strategy in Scenario.Compared)
            {
                var values = rising.Select(profile => RunsOf(scenario, profile).Single(r => r.Spec.Strategy == strategy).Value).ToList();
                for (int i = 1; i < values.Count; i++)
                {
                    if (values[i] * 100 < values[i - 1] * (100 - OwnDecisionsPercent))
                    {
                        failures.Add($"{scenario.Name}, {strategy.Name}: {Units(values[i])} with {rising[i].Name} against {Units(values[i - 1])} with {rising[i - 1].Name}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, $"More than {OwnDecisionsPercent} % smaller with more self-care:\n{string.Join('\n', failures)}");
    }

    [Fact]
    public void EveryScenarioRunKeepsTheInvariantsEveryHourAndIsTheSameEachTime()
    {
        Assert.All(s_runs.Value, run => Assert.Equal(Scenario.Days * 24, run.HoursChecked));
        Assert.Equal(Report.Tsv(Scenario.Prototype.Runs().Select(BalanceRun.Play)), Report.Tsv(RunsOf(Scenario.Prototype, Scenario.Prototype.Profile)));
    }

    private static List<RunResult> RunsOf(Scenario scenario, SelfCareProfile profile) =>
        [.. s_runs.Value.Where(r => r.Spec.Map == scenario.Map && r.Spec.Profile == profile)];

    private static decimal PerForce(RunResult run) => (run.ValuePerForceHundredths ?? 0) / 100m;

    /// <summary>The scenario's row in the format of the first table in economy-v2.md, section 15.</summary>
    private static string Row(Scenario scenario, IReadOnlyList<RunResult> runs)
    {
        var ranked = runs.OrderByDescending(r => r.Value).ToList();
        long margin = ((ranked[0].Value * 100) + (ranked[1].Value / 2)) / ranked[1].Value - 100;
        string map = scenario.Map.Seed is ulong seed ? $"seed {seed}" : "prototypens";
        var values = Scenario.Compared.Select(strategy => runs.Single(r => r.Spec.Strategy == strategy)).Select(r => r == ranked[0] ? $"**{Units(r.Value)}**" : Units(r.Value));
        return $"| {scenario.Name} | {map} | {scenario.Profile.Name} | {string.Join(" | ", values)} | {ranked[0].Spec.Strategy.Name}, {margin} % |";
    }

    /// <summary>Millionths as whole units with a space between thousands, as the design documents write them.</summary>
    private static string Units(long millionths) =>
        ((millionths + 500_000) / 1_000_000).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');
}
