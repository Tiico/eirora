using Eirora.Simulation.Forces;
using FsCheck;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Forces;

/// <summary>Rules v1 from docs/design/forces.md and the force invariants in docs/design/invariants.md.</summary>
[Properties(MaxTest = 500, EndSize = 100_000)]
public sealed class ForceRulesTests
{
    private const int MaxSteps = 100_000;
    private const int MaxSleepMinutes = 24 * 60;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2_000, 30)]
    [InlineData(4_000, 60)]
    [InlineData(7_500, 86)]
    [InlineData(10_000, 95)]
    [InlineData(15_000, 103)]
    [InlineData(20_000, 110)]
    [InlineData(50_000, 110)]
    public void ExplorationMatchesTheWorkedExamples(int steps, int expected) =>
        Assert.Equal(expected, ForceRules.Exploration(steps));

    [Theory]
    [InlineData(0, 40)]
    [InlineData(4 * 60, 72)]
    [InlineData(6 * 60 + 30, 92)]
    [InlineData(7 * 60, 96)]
    [InlineData(7 * 60 + 10, 96)]
    [InlineData(8 * 60, 98)]
    [InlineData(9 * 60, 100)]
    [InlineData(12 * 60, 100)]
    public void DawnPowerMatchesTheWorkedExamples(int sleepMinutes, int expected) =>
        Assert.Equal(expected, ForceRules.DawnPower(sleepMinutes));

    [Fact]
    public void MissingSleepDataGivesTheBaseline() =>
        Assert.Equal(ForceRules.DawnPowerBaseline, ForceRules.DawnPower(null));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 100)]
    [InlineData(3, 270)]
    [InlineData(5, 345)]
    [InlineData(14, 345)]
    public void AWeekOfWorkoutsAccumulatesWithDiminishingReturns(int sessions, int expected) =>
        Assert.Equal(expected, ForceRules.BuildPower(0, sessions));

    [Fact]
    public void EachGameDayIsCreditedAgainstThePreviousSix() =>
        Assert.Equal(90, ForceRules.BuildPower(sessionsInPreviousSixDays: 1, sessionsToday: 1));

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(90, true)]
    public void AWorkoutCountsFromTenMinutes(int minutes, bool counts) =>
        Assert.Equal(counts, ForceRules.CountsAsWorkout(minutes));

    [Fact]
    public void ActiveMinutesCountAsOneHundredStepsEach() =>
        Assert.Equal(3_000, ForceRules.StepsFromActiveMinutes(30));

    [Fact]
    public void NegativeInputIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ForceRules.Exploration(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ForceRules.DawnPower(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ForceRules.BuildPower(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ForceRules.BuildPower(0, -1));
    }

    // INV-F3: more self-care always helps, but less and less, up to a cap.

    [Property]
    public bool ExplorationIsIncreasingAndBounded(NonNegativeInt a, NonNegativeInt b)
    {
        var (low, high) = Ordered(Scale(a.Get, MaxSteps), Scale(b.Get, MaxSteps));
        int fLow = ForceRules.Exploration(low), fHigh = ForceRules.Exploration(high);
        return fLow <= fHigh && fHigh <= ForceRules.ExplorationCap;
    }

    [Property]
    public bool ExplorationIsConcaveBeforeRounding(NonNegativeInt x, NonNegativeInt d)
    {
        int step = Scale(d.Get, 10_000), start = Scale(x.Get, MaxSteps);
        long f0 = ForceRules.ExplorationNumerator(start);
        long f1 = ForceRules.ExplorationNumerator(start + step);
        long f2 = ForceRules.ExplorationNumerator(start + (2 * step));
        return f2 - f1 <= f1 - f0;
    }

    [Property]
    public bool DawnPowerIsIncreasingAndBounded(NonNegativeInt a, NonNegativeInt b)
    {
        var (low, high) = Ordered(Scale(a.Get, MaxSleepMinutes), Scale(b.Get, MaxSleepMinutes));
        int fLow = ForceRules.DawnPower(low), fHigh = ForceRules.DawnPower(high);
        return fLow <= fHigh && fHigh <= ForceRules.DawnPowerCap;
    }

    [Property]
    public bool DawnPowerIsConcaveBeforeRounding(NonNegativeInt x, NonNegativeInt d)
    {
        int step = Scale(d.Get, 180), start = Scale(x.Get, MaxSleepMinutes);
        long f0 = ForceRules.DawnPowerNumerator(start);
        long f1 = ForceRules.DawnPowerNumerator(start + step);
        long f2 = ForceRules.DawnPowerNumerator(start + (2 * step));
        return f2 - f1 <= f1 - f0;
    }

    // INV-F4: no logged sleep gives less than missing data.

    [Property]
    public bool LoggedSleepNeverGivesLessThanMissingData(NonNegativeInt minutes) =>
        ForceRules.DawnPower(Scale(minutes.Get, MaxSleepMinutes)) >= ForceRules.DawnPower(null);

    // Workouts: rest days never cost anything, and a rolling week never exceeds the cap.

    [Property]
    public bool ARestDayGivesNothingAndTakesNothing(NonNegativeInt previous) =>
        ForceRules.BuildPower(previous.Get, 0) == 0;

    [Property]
    public bool BuildPowerWithinAWeekNeverExceedsTheCap(NonNegativeInt previous, NonNegativeInt today) =>
        ForceRules.BuildPower(0, previous.Get) + ForceRules.BuildPower(previous.Get, today.Get)
            <= ForceRules.BuildPowerWeeklyCap;

    private static int Scale(int value, int max) => value % (max + 1);

    private static (int Low, int High) Ordered(int a, int b) => a <= b ? (a, b) : (b, a);
}
