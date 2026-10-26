namespace Eirora.Balance.Tests;

/// <summary>
/// H4 (economy-v2.md, section 5): with the guided tour, the first week of economy v2 shows the player the systems it
/// shows today and no more; the population tiers and the steps come later.
/// </summary>
public sealed class FirstWeekTests
{
    // Acceptance of TASK-052: given a new world, when the first week is played with the guided tour, then the player meets
    // no more systems than economy v2 states for that week.
    [Theory]
    [InlineData("normal", true)]
    [InlineData("manual", false)]
    public void TheGuidedTourMeetsOnlyTheSystemsEconomyV2Names(string profile, bool seaRoute)
    {
        var run = BalanceRun.Play(new RunSpec(MapChoice.Prototype, Strategy.Tour, SelfCareProfile.Get(profile), 7));

        string[] expected = seaRoute ? [Systems.Fog, Systems.Expedition, Systems.Processing, Systems.SeaRoute] : [Systems.Fog, Systems.Expedition, Systems.Processing];
        Assert.Equal(expected.Order(StringComparer.Ordinal), run.SystemsMet.Keys.Order(StringComparer.Ordinal));
        Assert.All([Systems.Fog, Systems.Expedition, Systems.Processing], system => Assert.Equal(1, run.SystemsMet[system]));

        // The sea route and Nyön's settlement come on the week's last day with D-106's rules, a day later than today.
        Assert.Equal(seaRoute ? 7 : (int?)null, run.SystemsMet.TryGetValue(Systems.SeaRoute, out int day) ? day : null);
    }
}
