using System.Globalization;
using Eirora.Simulation;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Time;

namespace Eirora.Balance.Tests;

/// <summary>The bots as players: deterministic, carrying out their plans on generated maps, and reading the map right.</summary>
public sealed class BalanceRunTests
{
    [Fact]
    public void TheSameRunsGiveTheSameReportInAnyCulture()
    {
        RunSpec[] specs =
        [
            new(MapChoice.Prototype, Strategy.Sea, SelfCareProfile.Normal, 7),
            new(MapChoice.Generated(3), Strategy.Near, SelfCareProfile.ManualOnly, 7),
        ];

        var first = specs.Select(BalanceRun.Play).ToList();
        var culture = CultureInfo.CurrentCulture;
        List<RunResult> second;
        try
        {
            // Swedish writes a minus sign and a decimal comma of its own; the report must not.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            second = [.. specs.Select(BalanceRun.Play)];
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        Assert.Equal(Report.Tsv(first), Report.Tsv(second));
        Assert.Equal(Report.Markdown(first), Report.Markdown(second));
    }

    [Fact]
    public void NearAndSeaCarryOutTheirPlansOnTwentyGeneratedMapsInFourWeeks()
    {
        var runs = Suites.Generated(from: 0, seeds: 20, days: 28).Select(BalanceRun.Play).ToList();

        Assert.Equal(40, runs.Count);
        Assert.All(runs, run =>
        {
            string name = $"seed {run.Spec.Map.Seed}, {run.Spec.Strategy.Name}";
            Assert.True(run.PlanFinishedOn is not null, $"{name} is stuck on: {string.Join(", ", run.GoalsPending)}.");

            // A skipped goal also leaves nothing pending, so the plan is carried out only if none was skipped either.
            Assert.True(run.GoalsSkipped.Count == 0, $"{name} skipped: {string.Join(", ", run.GoalsSkipped)}.");
            Assert.Equal(28 * 24, run.HoursChecked);
        });
        Assert.All(runs.Where(r => r.Spec.Strategy == Strategy.Sea), run => Assert.Equal(2, run.Islands));

        string report = Report.Markdown(runs);
        Assert.All(Enumerable.Range(0, 20), seed => Assert.Contains($"| seed {seed} | Havet |", report, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("high", 721, 345, 686)]
    [InlineData("normal", 602, 270, 644)]
    [InlineData("manual", 300, 270, 560)]
    [InlineData("low", 315, 100, 616)]
    [InlineData("no-training", 602, 0, 644)]
    [InlineData("none", 0, 0, 280)]
    public void EachProfileGivesTheWeekOfForcesEpochOneLists(string profile, int exploration, int buildPower, int dawnPower)
    {
        var run = BalanceRun.Play(new RunSpec(MapChoice.Prototype, Strategy.Idle, SelfCareProfile.Get(profile), 7));

        // The guided tour's forces (D-45), its Dawn Power standing in for game day 0, and the week credited under the soft cap.
        var expected = ForceBalances.GuidedTourStart();
        expected.Credit(Force.Exploration, exploration);
        expected.Credit(Force.BuildPower, buildPower);
        expected.Credit(Force.DawnPower, dawnPower);
        Assert.Equal(
            new[] { Force.Exploration, Force.BuildPower, Force.DawnPower }.Select(expected.Available),
            run.Saved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7UL)]
    [InlineData(12UL)]
    public void TheBotsReadDistanceToAStoreAsTheSimulationCountsIt(ulong? seed)
    {
        var map = seed is ulong s ? MapChoice.Generated(s) : MapChoice.Prototype;
        var world = World.Create(map.Map, new TimeReading(BalanceRun.InstallUtc, BalanceRun.Zone, 0), BalanceRun.Rest);
        var sites = new Sites(world);

        int[] steps = sites.StepsFrom(sites.PlannedDepots());

        for (int i = 0; i < world.Map.Disc.Count; i++)
        {
            var tile = world.Map.Disc[i];
            var trip = world.TripFrom(tile);
            int counted = trip.Reaches ? trip.Steps : sites.PlannedDepots().Contains(tile) ? 0 : int.MaxValue;
            Assert.True(counted == sites.StepsTo(tile, steps), $"{tile}: {sites.StepsTo(tile, steps)} steps, but the world's carriers walk {counted}.");
        }
    }

    [Fact]
    public void TheDevelopmentValueStartsWhereTheModelsDoes()
    {
        var run = BalanceRun.Play(new RunSpec(MapChoice.Prototype, Strategy.Idle, SelfCareProfile.None, 1));

        Assert.Equal(409_000_000, run.StartValue); // economy-v1.md, section 7.
    }
}
