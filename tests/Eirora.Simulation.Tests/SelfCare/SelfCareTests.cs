using System.Globalization;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.SelfCare;
using FsCheck.Xunit;
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
    public void ACorrectionDownwardsNeverTakesBackMoreThanTheDayGave()
    {
        var world = Afternoon();
        for (int day = 0; day <= 2; day++)
        {
            world.AdvanceTo(At($"2026-10-{14 + day} 14:00", 2));
            Record(world, day, Habit.Steps, ContributionSource.Verified, 20_000);
        }

        Assert.Equal(400 + (30 / 2), world.Available(Force.Exploration)); // The third day crossed the full-credit cap.
        world.AdvanceTo(At("2026-10-17 14:00", 2));
        Record(world, 3, Habit.Steps, ContributionSource.Verified, 20_000);
        Assert.Equal(415 + (110 / 2), world.Available(Force.Exploration)); // A whole day at half credit gave 55.

        var result = Record(world, 3, Habit.Steps, ContributionSource.Verified, 0);

        Assert.Equal(415, world.Available(Force.Exploration));
        Assert.Equal(55, Assert.IsType<ForceWithdrawn>(Assert.Single(result.Events)).Amount);
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

    [Fact]
    public void ALateWorkoutMovesTheWeeksBuildPowerToEarlierDays()
    {
        var world = Afternoon();
        world.AdvanceTo(At("2026-10-16 12:00", 2));
        Record(world, 1, Habit.Workouts, ContributionSource.Verified, 1); // 100.
        Record(world, 2, Habit.Workouts, ContributionSource.Verified, 1); // 90.

        var result = Record(world, 0, Habit.Workouts, ContributionSource.Verified, 1); // Now 100, 90 and 80.

        Assert.Equal(100 + 270, world.Available(Force.BuildPower)); // Week total G(3) = 270.
        Assert.Equal(2, result.Events.OfType<ForceWithdrawn>().Count(e => e.Amount == 10));
    }

    [Property(MaxTest = 200)]
    public bool WorkoutsGiveTheSameBuildPowerInAnyOrder(byte[] sessions, int seed)
    {
        var days = Enumerable.Range(0, RecordSelfCare.DaysBack).ToArray();
        var inOrder = AfterAWeek();
        var shuffled = AfterAWeek();

        foreach (int day in days)
        {
            Record(inOrder, day, Habit.Workouts, ContributionSource.Verified, SessionsOn(day));
        }

        new Random(seed).Shuffle(days);
        foreach (int day in days)
        {
            Record(shuffled, day, Habit.Workouts, ContributionSource.Verified, SessionsOn(day));
        }

        return shuffled.Available(Force.BuildPower) == inOrder.Available(Force.BuildPower);

        // One session a day at most keeps every step under the soft cap, which no order can then cross.
        int SessionsOn(int day) => day < sessions.Length ? sessions[day] % 2 : 0;

        static World AfterAWeek()
        {
            var world = Afternoon();
            world.AdvanceTo(At("2026-10-20 12:00", 2));
            return world;
        }
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 190)]
    [InlineData(3, 270)]
    [InlineData(4, 320)]
    [InlineData(5, 345)]
    [InlineData(6, 345)]
    [InlineData(7, 345)]
    public void WeekAfterWeekOfWorkoutsGivesTheCurveForTheWeek(int sessionsPerWeek, int expected)
    {
        var world = Afternoon();
        int creditedInLastWeek = 0;
        for (int day = 0; day < 10 * 7; day++)
        {
            world.AdvanceTo(At(Day(day).ToString("yyyy-MM-dd 14:00", CultureInfo.InvariantCulture), 2));
            if (day % 7 < sessionsPerWeek)
            {
                var result = Record(world, day, Habit.Workouts, ContributionSource.Verified, 1);
                if (day >= 9 * 7)
                {
                    creditedInLastWeek += result.Events.OfType<ForceCredited>().Sum(e => e.Amount);
                }
            }

            // Spending everything keeps the store under the soft cap, so each day's Build Power is credited in full.
            world.Apply(NextKey(), new SpendAll(Force.BuildPower));
        }

        Assert.Equal(expected, creditedInLastWeek); // D-63: more training never gives less.
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

    private static DateTime Day(int gameDay) => new DateTime(2026, 10, 14).AddDays(gameDay);

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
