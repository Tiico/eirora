using System.Globalization;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
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
    public void TheTsvHasACellForEveryColumnAndAColumnForEveryResource()
    {
        var run = BalanceRun.Play(new RunSpec(MapChoice.Prototype, Strategy.Idle, SelfCareProfile.None, 1));
        string[] lines = Report.Tsv([run]).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string[] columns = lines[0].Split('\t');
        Assert.Equal(columns.Length, lines[1].Split('\t').Length);
        Assert.All(Enum.GetValues<Resource>(), resource => Assert.Contains(resource.ToString().ToLowerInvariant(), columns));
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

    // D-135: the bots' Havet ships the island's wool first, once the island has a store, and never moves it again.
    [Fact]
    public void HavetShipsTheIslandsWoolFirst()
    {
        var sea = BalanceRun.Play(new RunSpec(Scenario.RichHome.Map, Strategy.Sea, SelfCareProfile.Normal, Scenario.Days));

        var moves = sea.Decisions.Select(d => d.Command).OfType<MoveImport>().ToList();
        Assert.Equal(Resource.Wool, Assert.Single(moves).Resource);
        Assert.Equal(0, moves[0].Place);
    }

    [Theory]
    [InlineData("high", 721, 345)]
    [InlineData("normal", 602, 270)]
    [InlineData("manual", 300, 270)]
    [InlineData("low", 315, 100)]
    [InlineData("no-training", 602, 0)]
    [InlineData("none", 0, 0)]
    public void WhatEachProfileEarnsIsEpochOnesWeekEveryWeek(string profile, int exploration, int buildPower)
    {
        var earned = SelfCareProfile.Get(profile).Earned(3 * 7);

        Assert.All(earned.Chunk(7), week =>
        {
            Assert.Equal(exploration, week.Sum(day => day.Exploration));
            Assert.Equal(buildPower, week.Sum(day => day.BuildPower));
        });
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

    // Moving a building by demolishing it and building it again costs half its materials and all its force (D-108), so it
    // pays only what the better site gives (economy-v1.md, section 3.2; TASK-047). The founding puts the start woodcutter
    // by the richest woods in reach (TASK-064), so moving it gains next to nothing over Närområdet in four weeks: under
    // 2 % in median and 5 % at most on twelve generated maps, and under 5 % on the prototype's map. Both play their own
    // plans without the planner from week 2 (KeepGrowing), whose later decisions part for other reasons than the woodcutter.
    // The bot keeps the invariants with the demolition between two checks.
    [Fact]
    public void MovingTheStartWoodcutterGainsNextToNothingOverTheFoundedOne()
    {
        var gains = new List<double>();
        var maps = new[] { MapChoice.Prototype }.Concat(Enumerable.Range(0, 12).Select(seed => MapChoice.Generated((ulong)seed)));
        foreach (var map in maps)
        {
            var near = BalanceRun.Play(new RunSpec(map, OwnPlanOnly(Strategy.Near), SelfCareProfile.Normal, 28));
            var moved = BalanceRun.Play(new RunSpec(map, OwnPlanOnly(Strategy.NearMoved), SelfCareProfile.Normal, 28));

            string name = map.Seed is ulong seed ? $"seed {seed}" : "the prototype";
            Assert.True(moved.PlanFinishedOn is not null, $"{name}: moving is stuck on {string.Join(", ", moved.GoalsPending)}.");
            Assert.Contains(moved.Decisions, d => d.Command is Simulation.Commands.Demolish);
            Assert.Equal(28 * 24, moved.HoursChecked);
            double gain = (double)(moved.Values[^1] - near.Values[^1]) / near.Values[^1];
            Assert.True(gain < 0.05, $"{name}: moving ends at {moved.Values[^1]} against {near.Values[^1]}.");
            if (map.Seed is not null)
            {
                gains.Add(gain);
            }
        }

        gains.Sort();
        double median = (gains[5] + gains[6]) / 2;
        Assert.True(median < 0.02, $"Moving gains {median:P1} in median: {string.Join(", ", gains.Select(g => g.ToString("P1", CultureInfo.InvariantCulture)))}.");
    }

    private static Strategy OwnPlanOnly(Strategy strategy) =>
        new(strategy.Id, strategy.Name, strategy.Goals, [.. strategy.Daily.Where(move => move is not KeepGrowing)]);

    [Fact]
    public void TheDevelopmentValueStartsWhereTheModelsDoes()
    {
        var run = BalanceRun.Play(new RunSpec(MapChoice.Prototype, Strategy.Idle, SelfCareProfile.None, 1));

        Assert.Equal(409_000_000, run.StartValue); // economy-v1.md, section 7.
    }
}
