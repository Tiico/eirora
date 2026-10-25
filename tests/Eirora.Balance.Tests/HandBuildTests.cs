using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;

namespace Eirora.Balance.Tests;

/// <summary>
/// The balance bot for building by hand without training (epoch-1.md, section 5; D-65, TASK-065): on the prototype's map
/// a player who has deselected training gets the sea route to Nyön in the second week, as the epoch's table says, and
/// wadmal from the weavery, which the bot without building by hand never gets.
/// </summary>
public sealed class HandBuildTests
{
    private const int Weeks = 4;

    private static readonly Lazy<RunResult> s_byHand = new(() => BalanceRun.Play(new RunSpec(MapChoice.Prototype, Strategy.NoTrainingByHand, SelfCareProfile.NoTraining, Weeks * 7)));
    private static readonly Lazy<RunResult> s_without = new(() => BalanceRun.Play(new RunSpec(MapChoice.Prototype, Strategy.NoTraining, SelfCareProfile.NoTraining, Weeks * 7)));

    [Fact]
    public void WithoutTrainingTheSeaRouteToNyonOpensInTheSecondWeekAsInEpochOne()
    {
        var run = s_byHand.Value;

        Assert.True(run.SeaRouteOn is int day && day <= 2 * 7, $"The sea route opened on game day {run.SeaRouteOn}: {string.Join(", ", run.GoalLog)}.");
        Assert.Equal(2, run.Islands);
        Assert.Contains(run.BuildingList, b => b.StartsWith(Names.Of(BuildingKind.Weavery), StringComparison.Ordinal));
        Assert.Empty(run.GoalsPending);
    }

    [Fact]
    public void TheBotBuildsByHandOnlyWhatItsBuildPowerDoesNotReach()
    {
        var run = s_byHand.Value;
        var byHand = run.Decisions.Select(d => d.Command).OfType<BuildByHand>().Select(b => b.Building).ToList();

        // The guided tour's 100 Build Power pays for the first jetty and the sawmill, and nothing grows it; the weavery is
        // queued while the expedition is at sea, before the jetty on the island it finds.
        Assert.Equal([BuildingKind.Weavery, BuildingKind.Jetty], byHand);
        Assert.Equal(90, run.Used[1]);
        Assert.Equal(10, run.Saved[1]);
    }

    [Fact]
    public void WithoutBuildingByHandThePlayerWithoutTrainingStaysOnHemon()
    {
        Assert.Null(s_without.Value.SeaRouteOn);
        Assert.Equal(1, s_without.Value.Islands);
    }
}
