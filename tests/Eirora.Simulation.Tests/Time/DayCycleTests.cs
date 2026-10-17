using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Time;
using FsCheck;
using FsCheck.Xunit;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Time;

/// <summary>Rest period, dawn and game days (INV-T1–T8, INV-D4, INV-D6–D8, D-37, D-50, D-51).</summary>
public sealed class DayCycleTests
{
    [Fact]
    public void AWorldCreatedInTheAfternoonIsActiveOnGameDayZero()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);

        Assert.Equal(Phase.Active, world.Phase);
        Assert.Equal(0, world.GameDay);
    }

    [Fact]
    public void AWorldCreatedInsideTheRestWindowStartsResting()
    {
        var world = World.Create(At("2026-10-15 01:30", 2), Night);

        Assert.Equal(Phase.Resting, world.Phase);
        world.AdvanceTo(At("2026-10-15 06:59", 2));
        Assert.Equal(Phase.Resting, world.Phase);
        world.AdvanceTo(At("2026-10-15 07:00", 2));
        Assert.Equal(Phase.Active, world.Phase);
    }

    [Fact]
    public void DuskAndDawnFollowTheScheduleAndDawnStartsTheNextGameDay()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);

        var events = world.AdvanceTo(At("2026-10-15 12:00", 2));

        Assert.Collection(
            events.Where(e => e is DuskFell or DawnBroke),
            dusk => Assert.Equal("2026-10-14 21:00", Utc(Assert.IsType<DuskFell>(dusk).UtcSeconds)),
            dawn =>
            {
                Assert.Equal("2026-10-15 05:00", Utc(dawn.UtcSeconds));
                Assert.Equal(1, Assert.IsType<DawnBroke>(dawn).GameDay);
            });
        Assert.Equal(1, world.GameDay);
    }

    [Fact]
    public void TheRestLockRejectsGameplayAndLeavesTheWorldUntouched()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-14 23:30", 2));
        var before = world.Fingerprint();

        var result = world.Apply("build-1", new TestGameplayCommand());

        Assert.Equal(Rejection.Resting, result.Rejection);
        Assert.Equal(before, world.Fingerprint());
    }

    [Fact]
    public void ACommandRejectedAtNightCanBeSentAgainAfterDawnWithTheSameKey()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-14 23:30", 2));
        world.Apply("build-1", new TestGameplayCommand());

        world.AdvanceTo(At("2026-10-15 07:30", 2));

        Assert.True(world.Apply("build-1", new TestGameplayCommand()).Accepted);
    }

    [Fact]
    public void AdministrativeCommandsWorkDuringRest()
    {
        var world = World.Create(At("2026-10-14 23:30", 2), Night);

        var result = world.Apply("schedule-1", new ChangeRestSchedule(new RestSchedule(22 * 60, 9 * 60)));

        Assert.True(result.Accepted);
    }

    [Theory]
    [InlineData(23 * 60, 5 * 60 + 59)]
    [InlineData(23 * 60, 10 * 60 + 1)]
    [InlineData(24 * 60, 8 * 60)]
    [InlineData(-1, 8 * 60)]
    public void ARestScheduleOutsideSixToTenHoursIsRejected(int start, int duration)
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);

        var result = world.Apply("schedule-1", new ChangeRestSchedule(new RestSchedule(start, duration)));

        Assert.Equal(Rejection.InvalidRestSchedule, result.Rejection);
    }

    [Fact]
    public void AScheduleChangeOnlyAppliesToRestStartingADayLater()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.Apply("schedule-1", new ChangeRestSchedule(new RestSchedule(21 * 60, 8 * 60)));

        var tonight = world.AdvanceTo(At("2026-10-15 12:00", 2));
        var tomorrow = world.AdvanceTo(At("2026-10-16 12:00", 2));

        Assert.Equal("2026-10-14 21:00", Utc(tonight.OfType<DuskFell>().Single().UtcSeconds)); // Still 23:00 local.
        Assert.Equal("2026-10-15 19:00", Utc(tomorrow.OfType<DuskFell>().Single().UtcSeconds)); // Now 21:00 local.
    }

    [Fact]
    public void AnOngoingRestIsNeverShortened()
    {
        var world = World.Create(At("2026-10-14 23:30", 2), Night);
        world.Apply("schedule-1", new ChangeRestSchedule(new RestSchedule(20 * 60, 6 * 60)));

        var events = world.AdvanceTo(At("2026-10-15 12:00", 2));

        Assert.Equal("2026-10-15 05:00", Utc(events.OfType<DawnBroke>().Single().UtcSeconds)); // 07:00 local as before.
    }

    [Fact]
    public void WhenDaylightSavingTimeEndsRestStillEndsAtSevenOnTheWallClock()
    {
        // 2026-10-25: clocks go back from 03:00 CEST to 02:00 CET (01:00 UTC); the client splits the span there.
        var world = World.Create(At("2026-10-24 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-25 03:00", 2));

        var events = world.AdvanceTo(At("2026-10-25 12:00", 1));

        Assert.Equal("2026-10-25 06:00", Utc(events.OfType<DawnBroke>().Single().UtcSeconds)); // 07:00 CET, nine real hours.
    }

    [Fact]
    public void TravellingDuringRestKeepsTheDawnWhereItWas()
    {
        var home = World.Create(At("2026-10-14 14:00", 2), Night);
        var travelling = World.Create(At("2026-10-14 14:00", 2), Night);
        home.AdvanceTo(At("2026-10-15 01:00", 2));
        travelling.AdvanceTo(At("2026-10-15 01:00", 2));

        var stayed = home.AdvanceTo(At("2026-10-15 12:00", 2));
        var flew = travelling.AdvanceTo(At("2026-10-15 06:00", -4, NewYork)); // Same UTC instant as 12:00 in Stockholm.

        Assert.Equal(stayed.OfType<DawnBroke>().Single().UtcSeconds, flew.OfType<DawnBroke>().Single().UtcSeconds);
    }

    [Fact]
    public void AReadingFromThePastChangesNothing()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 12:00", 2));
        var before = world.Fingerprint();

        var events = world.AdvanceTo(At("2026-10-15 09:00", 2));

        Assert.Empty(events);
        Assert.Equal(before, world.Fingerprint());
    }

    [Fact]
    public void ACommandIsAppliedOnlyOncePerKey()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        var first = world.Apply("schedule-1", new ChangeRestSchedule(new RestSchedule(22 * 60, 8 * 60)));
        var afterFirst = world.Fingerprint();

        var second = world.Apply("schedule-1", new ChangeRestSchedule(new RestSchedule(21 * 60, 8 * 60)));

        Assert.Same(first, second);
        Assert.Equal(afterFirst, world.Fingerprint());
    }

    // INV-D4: advancing in many steps gives the same world as advancing in one, and phases alternate (INV-T2).
    [Property(MaxTest = 200)]
    public bool AdvancingInPartsEqualsAdvancingAtOnce(NonNegativeInt startMinute, NonNegativeInt duration, PositiveInt[] steps)
    {
        var schedule = new RestSchedule(startMinute.Get % RestSchedule.MinutesPerDay, RestSchedule.MinimumMinutes + (duration.Get % 241));
        var install = At("2026-04-01 10:00", 2);
        var stepped = World.Create(install, schedule);
        var direct = World.Create(install, schedule);

        long now = install.UtcSeconds;
        var events = new List<GameEvent>();
        foreach (var step in steps)
        {
            now += (step.Get % (36 * 60)) * 60L;
            events.AddRange(stepped.AdvanceTo(new TimeReading(now, Stockholm, 7200)));
        }

        var directEvents = direct.AdvanceTo(new TimeReading(now, Stockholm, 7200));

        return stepped.Fingerprint() == direct.Fingerprint()
            && events.Select(Describe).SequenceEqual(directEvents.Select(Describe))
            && PhasesAlternate(events);
    }

    private static string Describe(GameEvent e) => $"{e.GetType().Name}@{e.UtcSeconds}";

    private static bool PhasesAlternate(IReadOnlyList<GameEvent> events)
    {
        var transitions = events.Where(e => e is DuskFell or DawnBroke).ToList();
        for (int i = 1; i < transitions.Count; i++)
        {
            if (transitions[i].GetType() == transitions[i - 1].GetType())
            {
                return false;
            }
        }

        return transitions.OfType<DawnBroke>().Select(d => d.GameDay).SequenceEqual(
            Enumerable.Range(1, transitions.OfType<DawnBroke>().Count()));
    }

    private sealed class TestGameplayCommand : GameCommand
    {
        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world) => Rejection.None;
    }
}
