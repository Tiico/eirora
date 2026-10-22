using System.Globalization;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests;

/// <summary>Saving by journal and loading by replay give the identical world (INV-D1, INV-D5).</summary>
public sealed class JournalTests
{
    private const long Hour = 3600;

    [Fact]
    public void AReplayedWeekOfPlayIsTheSameWorld()
    {
        var journal = PlayedWeek();

        var loaded = Journal.Load(journal.ToText());
        loaded.AdvanceTo(journal.World.Now); // The client always catches up to the present after loading.

        Assert.Equal(journal.World.Fingerprint(), loaded.World.Fingerprint());
    }

    [Fact]
    public void LoadingAndSavingGivesTheSameText()
    {
        var text = PlayedWeek().ToText();

        Assert.Equal(text, Journal.Load(text).ToText());
    }

    [Fact]
    public void TheJournalRecordsOnlyTimeThatMatters()
    {
        var journal = Journal.Start(At("2026-10-14 14:00", 2), Night);
        for (int hour = 1; hour <= 48; hour++)
        {
            journal.AdvanceTo(new TimeReading(journal.World.Now.UtcSeconds + Hour, Stockholm, 7200));
        }

        Assert.Equal(2, journal.ToText().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void AnOffsetChangeIsReplayedAtTheRightMoment()
    {
        // Daylight saving time ends at 01:00 UTC on 2026-10-25; the client splits the span there.
        var journal = Journal.Start(At("2026-10-24 14:00", 2), Night);
        journal.AdvanceTo(At("2026-10-25 03:00", 2));
        journal.AdvanceTo(At("2026-10-25 12:00", 1));
        journal.Apply("steps", new RecordSelfCare(journal.World.GameDay, Habit.Steps, ContributionSource.Verified, 5_000));

        var loaded = Journal.Load(journal.ToText());

        Assert.Equal(journal.World.Fingerprint(), loaded.World.Fingerprint());
    }

    // {rules} is the current rules version, so the cases stay malformed journals of today's rules when the rules move on.
    [Theory]
    [InlineData("")]
    [InlineData("something else\ncreate 0 Europe/Stockholm 0 1380 480\n")]
    [InlineData("eirora-journal 1 rules 999\ncreate 0 Europe/Stockholm 0 1380 480\n")]
    [InlineData("eirora-journal 1 rules {rules}\ncreate 0 Europe/Stockholm 0 1380 480\ncmd k1 teleport\n")]
    [InlineData("eirora-journal 1 rules {rules}\ncreate 0 Europe/Stockholm 0 1380 480\ncmd k1 build Castle 0 0\n")]
    [InlineData("eirora-journal 1 rules {rules}\ncreate 0 Europe/Stockholm 0 1380 480\ncmd k1 build Cottage L1\n")]
    [InlineData("eirora-journal 1 rules {rules}\ncreate zero Europe/Stockholm 0 1380 480\n")]
    public void TextThatIsNotAJournalIsRejected(string text) =>
        Assert.Throws<FormatException>(() => Journal.Load(text.Replace("{rules}", RulesVersion.Current.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)));

    // Every rules version before RulesVersion.ReplaysJournalsFrom would replay to another world (ADR-0006, D-104), so the
    // cases move with it when the rules move on.
    public static TheoryData<int> RulesNoLongerReplayed => new(Enumerable.Range(1, RulesVersion.ReplaysJournalsFrom - 1));

    [Theory]
    [MemberData(nameof(RulesNoLongerReplayed))]
    public void AJournalUnderRulesThatAreNoLongerReplayedIsRefused(int rules) =>
        Assert.Throws<JournalFromOlderRulesException>(() => Journal.Load(JournalUnderRules(rules)));

    // No rules version 0 ever existed, and a version after today's is from a newer build, so neither is kept as older rules.
    [Fact]
    public void AJournalUnderRulesThatNeverExistedIsNotAJournal()
    {
        Assert.Throws<FormatException>(() => Journal.Load(JournalUnderRules(0)));
        Assert.Throws<FormatException>(() => Journal.Load(JournalUnderRules(RulesVersion.Current + 1)));
    }

    private static string JournalUnderRules(int rules) =>
        $"eirora-journal 1 rules {rules.ToString(CultureInfo.InvariantCulture)}\ncreate 0 Europe/Stockholm 0 1380 480\n";

    private static Journal PlayedWeek()
    {
        var journal = Journal.Start(At("2026-10-14 14:00", 2), Night);
        var morning = At("2026-10-15 08:00", 2);
        for (int day = 0; day < 7; day++)
        {
            journal.AdvanceTo(new TimeReading(morning.UtcSeconds + (day * 24 * Hour), Stockholm, 7200));
            int today = journal.World.GameDay;
            journal.Apply($"steps-{day}", new RecordSelfCare(today, Habit.Steps, ContributionSource.Verified, 7_500));
            journal.Apply($"sleep-{day}", new RecordSelfCare(today, Habit.Sleep, ContributionSource.SelfReported, 400));
            journal.Apply($"train-{day}", new RecordSelfCare(today, Habit.Workouts, ContributionSource.Verified, day % 2));
            journal.Apply($"survey-{day}", new Survey());
            journal.Apply($"f3-{day}", new Build(BuildingKind.Woodcutter, F3));
            journal.Apply($"e2-{day}", new Build(BuildingKind.Sawmill, E2));
            journal.Apply($"road-{day}", new BuildRoad(F2));
            journal.Apply($"upgrade-{day}", new Upgrade(K1));
            journal.Apply($"workday-{day}", new Workday(O1));
            journal.Apply($"rush-{day}", new Rush());
            journal.Apply($"schedule-{day}", new ChangeRestSchedule(new RestSchedule((22 * 60) + (day * 15), 8 * 60)));
        }

        journal.AdvanceTo(new TimeReading(morning.UtcSeconds + (8 * 24 * Hour), Stockholm, 7200));
        return journal;
    }
}
