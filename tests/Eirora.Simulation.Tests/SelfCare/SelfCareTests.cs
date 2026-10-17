using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.SelfCare;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.SelfCare;

/// <summary>Self-care becomes force (forces.md, INV-B1, INV-F7, INV-T9, D-36, D-53, D-54).</summary>
public sealed class SelfCareTests
{
    private static int s_key;

    [Fact]
    public void StepsBecomeExploration()
    {
        var world = Afternoon();

        var result = Record(world, 0, Habit.Steps, ContributionSource.Verified, 7_500);

        Assert.Equal(100 + 86, world.Available(Force.Exploration));
        Assert.Equal(86, Assert.IsType<ForceCredited>(Assert.Single(result.Events)).Amount);
    }

    [Fact]
    public void RecordingTheSameTotalAgainCreditsNothingMore()
    {
        var world = Afternoon();
        Record(world, 0, Habit.Steps, ContributionSource.Verified, 7_500);

        var again = Record(world, 0, Habit.Steps, ContributionSource.Verified, 7_500);

        Assert.Empty(again.Events);
        Assert.Equal(186, world.Available(Force.Exploration));
    }

    [Fact]
    public void AGrowingTotalIsCreditedOnlyForTheDifference()
    {
        var world = Afternoon();
        Record(world, 0, Habit.Steps, ContributionSource.Verified, 4_000);

        Record(world, 0, Habit.Steps, ContributionSource.Verified, 7_500);

        Assert.Equal(100 + 86, world.Available(Force.Exploration));
    }

    [Fact]
    public void TheHigherOfVerifiedAndSelfReportedCountsNotTheSum()
    {
        var world = Afternoon();
        Record(world, 0, Habit.Steps, ContributionSource.Verified, 2_000);

        Record(world, 0, Habit.Steps, ContributionSource.SelfReported, 7_500);

        Assert.Equal(100 + 86, world.Available(Force.Exploration));
    }

    [Fact]
    public void SelfReportedSelfCareGivesTheSameForceAsVerified()
    {
        var verified = Afternoon();
        var selfReported = Afternoon();

        Record(verified, 0, Habit.Workouts, ContributionSource.Verified, 1);
        Record(selfReported, 0, Habit.Workouts, ContributionSource.SelfReported, 1);

        Assert.Equal(verified.Available(Force.BuildPower), selfReported.Available(Force.BuildPower));
    }

    [Fact]
    public void ACorrectionDownwardsWithdrawsButNeverBelowZero()
    {
        var world = Afternoon();
        Record(world, 0, Habit.Steps, ContributionSource.Verified, 50_000);
        world.Apply(NextKey(), new SpendAll(Force.Exploration));

        var result = Record(world, 0, Habit.Steps, ContributionSource.Verified, 0);

        Assert.Equal(0, world.Available(Force.Exploration));
        Assert.Equal(0, Assert.IsType<ForceWithdrawn>(Assert.Single(result.Events)).Amount);
    }

    [Fact]
    public void DawnCreditsTheBaselineAndSleepTopsItUp()
    {
        var world = Afternoon();

        world.AdvanceTo(At("2026-10-15 07:30", 2));
        Assert.Equal(40 + 40, world.Available(Force.DawnPower));

        Record(world, 1, Habit.Sleep, ContributionSource.Verified, 7 * 60);
        Assert.Equal(40 + 96, world.Available(Force.DawnPower));
    }

    [Fact]
    public void SelfCareRecordedDuringRestIsCreditedAtDawn()
    {
        var world = Afternoon();
        world.AdvanceTo(At("2026-10-14 23:30", 2));

        var atNight = Record(world, 0, Habit.Steps, ContributionSource.SelfReported, 7_500);
        Assert.True(atNight.Accepted);
        Assert.Equal(100, world.Available(Force.Exploration));

        var dawn = world.AdvanceTo(At("2026-10-15 07:00", 2));
        Assert.Equal(186, world.Available(Force.Exploration));
        Assert.Contains(dawn, e => e is ForceCredited { Force: Force.Exploration, Amount: 86 });
    }

    [Fact]
    public void WorkoutsAreCountedPerRollingWeek()
    {
        var world = Afternoon();
        Record(world, 0, Habit.Workouts, ContributionSource.Verified, 1);
        world.AdvanceTo(At("2026-10-15 12:00", 2));

        Record(world, 1, Habit.Workouts, ContributionSource.Verified, 1);

        Assert.Equal(100 + 100 + 90, world.Available(Force.BuildPower));
    }

    [Fact]
    public void ALateWorkoutReordersTheWeekWithoutCreditingTwice()
    {
        var world = Afternoon();
        world.AdvanceTo(At("2026-10-15 12:00", 2));
        Record(world, 1, Habit.Workouts, ContributionSource.Verified, 1); // First of the week: 100.

        Record(world, 0, Habit.Workouts, ContributionSource.Verified, 1); // Arrives late for the day before.

        Assert.Equal(100 + 190, world.Available(Force.BuildPower)); // Week total G(2) = 190, whatever the order.
    }

    [Theory]
    [InlineData(1)] // Tomorrow.
    [InlineData(-7)] // Eight game days ago counting today.
    public void SelfCareOutsideTheLastSevenGameDaysIsRejected(int dayOffset)
    {
        var world = Afternoon();
        world.AdvanceTo(At("2026-10-21 12:00", 2));

        var result = Record(world, world.GameDay + dayOffset, Habit.Steps, ContributionSource.SelfReported, 5_000);

        Assert.Equal(Rejection.GameDayOutOfRange, result.Rejection);
    }

    [Theory]
    [InlineData(Habit.Steps, -1)]
    [InlineData(Habit.Steps, 60_001)]
    [InlineData(Habit.Workouts, 7)]
    [InlineData(Habit.Sleep, 16 * 60 + 1)]
    public void ImplausibleSelfReportedTotalsAreRejected(Habit habit, int total)
    {
        var world = Afternoon();

        var result = Record(world, 0, habit, ContributionSource.SelfReported, total);

        Assert.Equal(Rejection.AmountOutOfRange, result.Rejection);
    }

    private static World Afternoon() => World.Create(At("2026-10-14 14:00", 2), Night);

    private static CommandResult Record(World world, int day, Habit habit, ContributionSource source, int total) =>
        world.Apply(NextKey(), new RecordSelfCare(day, habit, source, total));

    private static string NextKey() => $"cmd-{Interlocked.Increment(ref s_key)}";

    private sealed class SpendAll(Force force) : GameCommand
    {
        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            return world.TrySpend(force, world.Available(force)) ? Rejection.None : Rejection.AmountOutOfRange;
        }
    }
}
