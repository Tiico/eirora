using System.Globalization;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.SelfCare;
using FsCheck;
using FsCheck.Xunit;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.SelfCare;

/// <summary>Self-care becomes force (forces.md, INV-B1, INV-F7, INV-T9, D-36, D-53, D-54, D-62, D-69, D-74).</summary>
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
    public void ACorrectionAfterSpendingNeverTakesBackMoreThanTheDayGave()
    {
        var world = Afternoon();
        for (int day = 0; day <= 3; day++)
        {
            world.AdvanceTo(At($"2026-10-{14 + day} 14:00", 2));
            Record(world, day, Habit.Steps, ContributionSource.Verified, 20_000);
        }

        Assert.Equal(415 + (110 / 2), world.Available(Force.Exploration)); // The fourth day gave 55 at half credit.
        world.Apply(NextKey(), new Spend(Force.Exploration, 170)); // Under the full-credit cap again.

        var result = Record(world, 3, Habit.Steps, ContributionSource.Verified, 0);

        Assert.Equal(300 - 55, world.Available(Force.Exploration)); // Not 110 at full credit (D-62, D-74).
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
    public void LateWorkoutsAcrossTheSoftCapGiveTheSameTotalAsWorkoutsInOrder()
    {
        var inOrder = Afternoon();
        var late = Afternoon();
        inOrder.AdvanceTo(At("2026-10-15 12:00", 2));
        late.AdvanceTo(At("2026-10-15 12:00", 2));

        Record(inOrder, 0, Habit.Workouts, ContributionSource.Verified, 3);
        Record(inOrder, 1, Habit.Workouts, ContributionSource.Verified, 3);
        Record(late, 1, Habit.Workouts, ContributionSource.Verified, 3);
        Record(late, 0, Habit.Workouts, ContributionSource.Verified, 3); // Briefly lifts the balance over 600.

        Assert.Equal(100 + 345, inOrder.Available(Force.BuildPower));
        Assert.Equal(inOrder.Available(Force.BuildPower), late.Available(Force.BuildPower));
    }

    [Fact]
    public void LateWorkoutsAboveTheFullCreditCapGiveTheSameTotalAsWorkoutsInOrder()
    {
        var inOrder = Afternoon();
        var late = Afternoon();
        foreach (var world in new[] { inOrder, late })
        {
            for (int day = 0; day <= 14; day += 7)
            {
                world.AdvanceTo(At($"2026-10-{14 + day} 14:00", 2));
                Record(world, day, Habit.Workouts, ContributionSource.Verified, 3);
            }

            world.AdvanceTo(At("2026-11-05 14:00", 2));
        }

        Record(inOrder, 21, Habit.Workouts, ContributionSource.Verified, 3);
        Record(inOrder, 22, Habit.Workouts, ContributionSource.Verified, 3);
        Record(late, 22, Habit.Workouts, ContributionSource.Verified, 3);
        Record(late, 21, Habit.Workouts, ContributionSource.Verified, 3);

        Assert.Equal(inOrder.Available(Force.BuildPower), late.Available(Force.BuildPower));
    }

    [Fact]
    public void ACorrectionAfterTheSoftCapGivesWhatTheCorrectedDataWouldHaveGiven()
    {
        var corrected = Afternoon();
        var correctFromTheStart = Afternoon();
        Record(corrected, 0, Habit.Steps, ContributionSource.Verified, 20_000);
        for (int day = 1; day <= 2; day++)
        {
            corrected.AdvanceTo(At($"2026-10-{14 + day} 14:00", 2));
            correctFromTheStart.AdvanceTo(At($"2026-10-{14 + day} 14:00", 2));
            Record(corrected, day, Habit.Steps, ContributionSource.Verified, 20_000);
            Record(correctFromTheStart, day, Habit.Steps, ContributionSource.Verified, 20_000);
        }

        Assert.Equal(400 + (30 / 2), corrected.Available(Force.Exploration)); // Day 2 crossed the full-credit cap.

        Record(corrected, 0, Habit.Steps, ContributionSource.Verified, 0);

        Assert.Equal(100 + 110 + 110, correctFromTheStart.Available(Force.Exploration));
        Assert.Equal(correctFromTheStart.Available(Force.Exploration), corrected.Available(Force.Exploration));
    }

    // INV-B1 with the soft cap (D-35, D-69): a week of self-care arriving in any order, in growing parts and with
    // duplicates, gives the same forces as arriving in order, after a week saved that may reach any cap.
    [Property(MaxTest = 100)]
    public bool SelfCareGivesTheSameForcesInAnyOrderAcrossTheSoftCap(PositiveInt[] volumes, int seed)
    {
        var inOrder = Afternoon();
        var anyOrder = Afternoon();
        int Volume(int day, Habit habit) => volumes.Length == 0 ? 0 : volumes[((3 * day) + (int)habit) % volumes.Length].Get;
        int Total(int day, Habit habit) => habit switch
        {
            Habit.Steps => Volume(day, habit) % 30_001,
            Habit.Workouts => Volume(day, habit) % 7,
            _ => Volume(day, habit) % (12 * 60),
        };

        var habits = new[] { Habit.Steps, Habit.Workouts, Habit.Sleep };
        for (int day = 0; day <= 13; day++)
        {
            inOrder.AdvanceTo(At($"2026-10-{14 + day} 14:00", 2));
            anyOrder.AdvanceTo(At($"2026-10-{14 + day} 14:00", 2));
            if (day > 6)
            {
                continue;
            }

            foreach (var habit in habits)
            {
                Record(inOrder, day, habit, ContributionSource.Verified, Total(day, habit));
                Record(anyOrder, day, habit, ContributionSource.Verified, Total(day, habit));
            }
        }

        var parts = new List<(int Day, Habit Habit, int Total)>();
        for (int day = 7; day <= 13; day++)
        {
            foreach (var habit in habits)
            {
                Record(inOrder, day, habit, ContributionSource.Verified, Total(day, habit));
                parts.Add((day, habit, Total(day, habit) / 2));
                parts.Add((day, habit, Total(day, habit)));
                parts.Add((day, habit, Total(day, habit)));
            }
        }

        // Shuffle across days and habits, but keep each day's own totals growing as a health store reports them.
        var random = new Random(seed);
        var queues = parts.GroupBy(p => (p.Day, p.Habit)).Select(g => new Queue<(int Day, Habit Habit, int Total)>(g)).ToList();
        while (queues.Count > 0)
        {
            int next = random.Next(queues.Count);
            var (day, habit, total) = queues[next].Dequeue();
            Record(anyOrder, day, habit, ContributionSource.Verified, total);
            if (queues[next].Count == 0)
            {
                queues.RemoveAt(next);
            }
        }

        return Enumerable.Range(0, 3).All(f => inOrder.Available((Force)f) == anyOrder.Available((Force)f));
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

    [Fact]
    public void LateWorkoutsThatBrieflyCrossTheSoftCapGiveTheSameBuildPowerAsWorkoutsInOrder()
    {
        var inOrder = Afternoon();
        var late = Afternoon();
        inOrder.AdvanceTo(At("2026-10-20 12:00", 2));
        late.AdvanceTo(At("2026-10-20 12:00", 2));
        for (int day = 0; day <= 6; day++)
        {
            Record(inOrder, day, Habit.Workouts, ContributionSource.Verified, day % 2 == 0 ? 1 : 3);
        }

        // Day 3 is credited over 600 before day 5's correction brings the balance back under it, and is later
        // taken back at full credit (D-74).
        foreach (int day in new[] { 5, 3, 2, 6, 4, 0, 1 })
        {
            Record(late, day, Habit.Workouts, ContributionSource.Verified, day % 2 == 0 ? 1 : 3);
        }

        Assert.Equal(100 + 345, inOrder.Available(Force.BuildPower));
        Assert.Equal(inOrder.Available(Force.BuildPower), late.Available(Force.BuildPower));
    }

    // A week of workouts in any order gives the same Build Power as in order (D-63), after weeks of training saved
    // that put the balance under, between or over the soft cap's thresholds (D-69).
    [Property(MaxTest = 200)]
    public bool WorkoutsGiveTheSameBuildPowerInAnyOrder(byte[] sessions, byte weeksSaved, int seed)
    {
        int firstDay = 7 * (weeksSaved % 6);
        var days = Enumerable.Range(firstDay, RecordSelfCare.DaysBack).ToArray();
        var inOrder = Afternoon();
        var shuffled = Afternoon();
        for (int day = 0; day <= days[^1]; day++)
        {
            inOrder.AdvanceTo(At(Day(day).ToString("yyyy-MM-dd 14:00", CultureInfo.InvariantCulture), 2));
            shuffled.AdvanceTo(At(Day(day).ToString("yyyy-MM-dd 14:00", CultureInfo.InvariantCulture), 2));
            if (day < firstDay)
            {
                Record(inOrder, day, Habit.Workouts, ContributionSource.Verified, SessionsOn(day));
                Record(shuffled, day, Habit.Workouts, ContributionSource.Verified, SessionsOn(day));
            }
        }

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

        int SessionsOn(int day) => sessions.Length == 0 ? 0 : sessions[day % sessions.Length] % 7;
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

    private sealed class Spend(Force force, int cost) : GameCommand
    {
        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world)
        {
            return world.TrySpend(force, cost) ? Rejection.None : Rejection.AmountOutOfRange;
        }
    }
}
