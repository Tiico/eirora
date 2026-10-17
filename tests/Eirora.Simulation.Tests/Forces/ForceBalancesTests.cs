using Eirora.Simulation.Forces;
using FsCheck;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Forces;

/// <summary>Saving forces under the soft cap (D-35), corrections (D-36) and exact spending (INV-F5).</summary>
public sealed class ForceBalancesTests
{
    [Fact]
    public void TheGuidedTourStartsWithItsGift() =>
        Assert.Equal((100, 100, 40), Balances(ForceBalances.GuidedTourStart()));

    [Theory]
    [InlineData(300, 400)] // 100 + 300 reaches the full-credit threshold exactly.
    [InlineData(500, 500)] // 300 at full credit, then 200 at half credit adds 100.
    [InlineData(2_000, 800)] // Stops at the half-credit threshold.
    public void ExplorationIsCreditedFullyThenAtHalfThenNotAtAll(int credit, int expected)
    {
        var balances = ForceBalances.GuidedTourStart();

        balances.Credit(Force.Exploration, credit);

        Assert.Equal(expected, balances.Available(Force.Exploration));
    }

    [Fact]
    public void ACorrectionNeverTakesTheBalanceBelowZero()
    {
        var balances = ForceBalances.GuidedTourStart();

        int removed = balances.Withdraw(Force.DawnPower, 90);

        Assert.Equal(40, removed);
        Assert.Equal(0, balances.Available(Force.DawnPower));
    }

    [Fact]
    public void SpendingTakesExactlyTheCostOrNothing()
    {
        var balances = ForceBalances.GuidedTourStart();

        Assert.True(balances.TrySpend(Force.BuildPower, 60));
        Assert.False(balances.TrySpend(Force.BuildPower, 41));
        Assert.Equal(40, balances.Available(Force.BuildPower));
    }

    // INV-F8: the same credit gives the same balance however it is split, cap included.
    [Property(MaxTest = 300)]
    public bool CreditingInPartsEqualsCreditingAtOnce(PositiveInt[] parts, NonNegativeInt start)
    {
        var amounts = parts.Select(p => p.Get % 400).ToArray();
        var inParts = Started(start.Get % 900);
        var atOnce = Started(start.Get % 900);

        foreach (int amount in amounts)
        {
            inParts.Credit(Force.Exploration, amount);
        }

        atOnce.Credit(Force.Exploration, amounts.Sum());
        return inParts.Available(Force.Exploration) == atOnce.Available(Force.Exploration);
    }

    private static ForceBalances Started(int exploration)
    {
        var balances = ForceBalances.GuidedTourStart();
        balances.TrySpend(Force.Exploration, 100);
        balances.Credit(Force.Exploration, exploration);
        return balances;
    }

    private static (int, int, int) Balances(ForceBalances balances) =>
        (balances.Available(Force.Exploration), balances.Available(Force.BuildPower), balances.Available(Force.DawnPower));
}
