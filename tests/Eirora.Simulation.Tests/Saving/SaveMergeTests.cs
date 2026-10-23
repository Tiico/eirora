using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Two devices that played the same world apart merge their journals (TASK-023, D-99, ACC-03): nothing either did is
/// lost, nothing counts twice, and the result is the same whichever device merges.
/// </summary>
public sealed class SaveMergeTests
{
    private const int Hour = 3600;

    // Merging is commutative and idempotent, and the merged journal leads to the merged world: so a device that gets
    // back a merge of its own journal and merges once more ends exactly where the other device did.
    [Property(MaxTest = 40)]
    public bool MergingIsTheSameFromEitherSideAndMergingAgainChangesNothing(int seed)
    {
        var (a, b) = TwoDevices(seed);

        var ab = Save.Merge(a, b)!;
        var ba = Save.Merge(b, a)!;
        var again = Save.Merge(Save.Load(ab.ToBytes()), a)!;

        string merged = ab.World.Fingerprint();
        return merged == ba.World.Fingerprint()
            && merged == ab.Replay().Fingerprint()
            && merged == again.World.Fingerprint()
            && merged == Save.Merge(ab, b)!.World.Fingerprint()
            && merged == Save.Load(ab.ToBytes()).World.Fingerprint()
            && ab.ChangedOnLoad;
    }

    // Every command either device recorded is in the merged journal, and what they both hold only once. A command the
    // player sent again at another time stays twice, as in the journal it came from; the key makes it count once.
    [Property(MaxTest = 40)]
    public bool EveryCommandOfBothDevicesIsInTheMergedJournal(int seed)
    {
        var (a, b) = TwoDevices(seed);
        int shared = Commands(Save.Load(SharedSave(seed))).Count();

        var merged = Save.Merge(a, b)!;

        var commands = Commands(merged).ToList();
        return Commands(a).Union(Commands(b)).ToHashSet().SetEquals(commands)
            && commands.Count <= Commands(a).Count() + Commands(b).Count() - shared;
    }

    [Fact]
    public void WhatBothDevicesDidIsInTheMergedWorld()
    {
        var (a, b) = TwoDevices(seed: 3, play: false);
        int day = a.World.GameDay;
        var steps = a.Apply("a-steps", new RecordSelfCare(day, Habit.Steps, ContributionSource.Verified, 8_000));
        Advance(b, 2 * Hour);
        var schedule = new RestSchedule(21 * 60, 9 * 60);
        var change = b.Apply("b-schedule", new ChangeRestSchedule(schedule));

        var merged = Save.Merge(a, b)!;

        Assert.True(steps.Accepted && change.Accepted, $"{steps.Rejection} {change.Rejection}");
        Assert.Equal(8_000, merged.World.RecordedSelfCare(day, Habit.Steps, ContributionSource.Verified));
        Assert.Equal(schedule, merged.World.PendingRestSchedule);
        Assert.Equal(b.World.Now.UtcSeconds, merged.World.Now.UtcSeconds);
        Assert.Contains("a-steps", Keys(merged));
        Assert.Contains("b-schedule", Keys(merged));
    }

    // Both devices had the same save and one of them played on: the merge is that device's world.
    [Fact]
    public void ADeviceThatDidNothingMergesIntoTheOthersWorld()
    {
        var (a, b) = TwoDevices(seed: 5, play: false);
        Advance(a, 5 * Hour);
        a.Apply("a-rush", new Rush());

        Assert.Equal(a.World.Fingerprint(), Save.Merge(a, b)!.World.Fingerprint());
        Assert.Equal(a.World.Fingerprint(), Save.Merge(b, a)!.World.Fingerprint());
    }

    // The same self-care from both devices, as when a health store was read on both: the day is credited once.
    [Fact]
    public void TheSameSelfCareFromBothDevicesCountsOnce()
    {
        var (a, b) = TwoDevices(seed: 7, play: false);
        int day = a.World.GameDay;
        a.Apply("a-steps", new RecordSelfCare(day, Habit.Steps, ContributionSource.Verified, 9_000));
        Advance(b, Hour);
        b.Apply("b-steps", new RecordSelfCare(day, Habit.Steps, ContributionSource.Verified, 9_000));

        var merged = Save.Merge(a, b)!;

        Assert.Equal(9_000, merged.World.RecordedSelfCare(day, Habit.Steps, ContributionSource.Verified));
    }

    // Two commands in the same second on one device keep their order, whatever their keys: the later one wins, as it did there.
    [Fact]
    public void CommandsOfTheSameSecondKeepTheirOwnOrder()
    {
        var (a, b) = TwoDevices(seed: 9, play: false);
        int day = a.World.GameDay;
        a.Apply("z-first", new RecordSelfCare(day, Habit.Steps, ContributionSource.SelfReported, 6_000));
        a.Apply("a-second", new RecordSelfCare(day, Habit.Steps, ContributionSource.SelfReported, 9_000));
        Advance(b, Hour);
        b.Apply("b-later", new Rush());

        var merged = Save.Merge(a, b)!;
        var reversed = Save.Merge(b, a)!;

        Assert.Equal(9_000, merged.World.RecordedSelfCare(day, Habit.Steps, ContributionSource.SelfReported));
        Assert.Equal(merged.World.Fingerprint(), reversed.World.Fingerprint());
        Assert.Equal(["z-first", "a-second", "b-later"], Keys(merged).TakeLast(3));
    }

    [Fact]
    public void SavesOfDifferentWorldsAreNotMerged()
    {
        var one = Save.Begin(new TimeReading(1_780_304_400, "Europe/Stockholm", 7200), new RestSchedule(23 * 60, 8 * 60));
        var other = Save.Begin(new TimeReading(1_780_304_401, "Europe/Stockholm", 7200), new RestSchedule(23 * 60, 8 * 60));

        Assert.Null(Save.Merge(one, other));
        Assert.Null(Save.Merge(PlayedSave(1), PlayedSave(2)));
    }

    // A new segment without the contribution part (D-101) shares no start with the save it came from.
    [Fact]
    public void AWorldThatWentOnWithoutItsContributionsIsAnotherSegment()
    {
        var save = PlayedSave(4);
        var alone = Save.FromParts(save.ToParts().World, contributions: null);

        Assert.Null(Save.Merge(save, alone));
    }

    private static void Advance(Save save, long seconds)
    {
        var now = save.World.Now;
        save.AdvanceTo(new TimeReading(now.UtcSeconds + seconds, now.ZoneId, now.OffsetSeconds));
    }

    private static IEnumerable<string> Keys(Save save) => Commands(save).Select(l => l.Split(' ')[1]);

    /// <summary>The command lines of the save's journals, as written.</summary>
    private static IEnumerable<string> Commands(Save save) =>
        Encoding.UTF8.GetString(save.ToParts().Contributions).Split('\n').Where(l => l.StartsWith("cmd ", StringComparison.Ordinal));

    /// <summary>The same played save on two devices, each of which then plays on by itself.</summary>
    private static (Save A, Save B) TwoDevices(int seed, bool play = true)
    {
        byte[] shared = SharedSave(seed);
        var a = Save.Load(shared);
        var b = Save.Load(shared);
        if (play)
        {
            foreach (var input in Scenario.Continue(seed * 2 + 1, a.World, "a"))
            {
                SaveTests.Apply(input, a);
            }

            foreach (var input in Scenario.Continue(seed * 2 + 2, b.World, "b"))
            {
                SaveTests.Apply(input, b);
            }
        }

        return (a, b);
    }

    private static byte[] SharedSave(int seed) => PlayedSave(seed).ToBytes();

    private static Save PlayedSave(int seed)
    {
        var scenario = Scenario.Generate(seed, 30);
        var save = scenario.Begin();
        foreach (var input in scenario.Inputs)
        {
            SaveTests.Apply(input, save);
        }

        return save;
    }
}
