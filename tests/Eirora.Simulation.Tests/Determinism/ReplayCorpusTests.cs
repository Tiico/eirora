using System.Globalization;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Determinism;

/// <summary>
/// The replay corpus that proves the simulation gives the same state on the server and in the client (TASK-021,
/// NFR-02): hand-made scenarios from the UX journeys and economy v1, and random sequences. This test writes each
/// replay's state hash to artifacts/determinism/dotnet.txt, which scripts/check-determinism.sh compares with the hashes
/// the IL2CPP build reports on Android.
/// </summary>
public sealed class ReplayCorpusTests
{
    private const long Hour = 3600;
    private const int RandomReplays = 20;

    private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
    private static readonly string s_root = RepositoryRoot();
    private static readonly string s_folder = Path.Combine(s_root, "tests", "Replays");

    private static readonly (string Name, Func<Journal> Make)[] s_handMade =
    [
        ("ux-first-session", FirstSession),
        ("ux-seven-days-away", SevenDaysAway),
        ("ux-evening-with-rest-lock", EveningWithRestLock),
        ("economy-near", () => Week(Near)),
        ("economy-sea", () => Week(Sea)),
        ("time-daylight-saving-and-travel", DaylightSavingAndTravel),
    ];

    // Replays are inputs, not results, so they never change with the rules; a missing one is written to be committed.
    [Fact]
    public void TheCorpusHasEveryScenario()
    {
        var expected = s_handMade.Select(s => (s.Name, s.Make))
            .Concat(Enumerable.Range(1, RandomReplays).Select(i => ($"random-{i:00}", (Func<Journal>)(() => RandomJournal(i)))))
            .ToList();

        var written = new List<string>();
        Directory.CreateDirectory(s_folder);
        foreach (var (name, make) in expected)
        {
            string path = Path.Combine(s_folder, name + ".replay");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, Replay.Of(make()), new UTF8Encoding(false));
                written.Add(name);
            }
        }

        Assert.True(written.Count == 0, $"Wrote the replays {string.Join(", ", written)}; review and commit them.");
    }

    [Fact]
    public void EveryReplayGivesTheSameStateTwiceAndIsReportedForComparison()
    {
        var replays = Corpus();
        Assert.NotEmpty(replays);

        var lines = new StringBuilder();
        foreach (var (name, replay) in replays)
        {
            string hash = Replay.StateHash(replay);
            Assert.Equal(hash, Replay.StateHash(replay));
            lines.Append(name).Append(' ').Append(hash).Append('\n');
        }

        string report = Path.Combine(s_root, "artifacts", "determinism");
        Directory.CreateDirectory(report);
        File.WriteAllText(Path.Combine(report, "dotnet.txt"), lines.ToString(), new UTF8Encoding(false));
    }

    // The server and the players' phones run in different cultures, while the comparison on Android runs in the
    // emulator's en-US. sv-SE writes a minus sign as U+2212 and tr-TR lower-cases 'I' to a dotless 'ı'.
    [Theory]
    [InlineData("sv-SE")]
    [InlineData("tr-TR")]
    public void EveryReplayGivesTheSameStateInAnyCulture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        Assert.True(
            culture.NumberFormat.NegativeSign != "-" || culture.TextInfo.ToLower('I') != 'i',
            $"{name} formats like the invariant culture here (no ICU data?), so this test would prove nothing.");
        var replays = Corpus();

        var invariant = HashesIn(CultureInfo.InvariantCulture, replays);
        var local = HashesIn(culture, replays);

        Assert.Equal(invariant, local);
    }

    // UX journey 1: the first evening, from choosing the rest period to the first dawn.
    private static Journal FirstSession()
    {
        var journal = Journal.Start(At("2026-10-14 17:30", 2), s_night);
        journal.Apply("survey", new Survey());
        journal.Apply("cottage", new Build(BuildingKind.Cottage, "L1"));
        journal.Apply("steps", new RecordSelfCare(0, Habit.Steps, ContributionSource.SelfReported, 6_400));
        journal.AdvanceTo(At("2026-10-14 22:45", 2));
        journal.Apply("workout", new RecordSelfCare(0, Habit.Workouts, ContributionSource.SelfReported, 1));
        journal.AdvanceTo(At("2026-10-15 07:10", 2));
        journal.Apply("sleep", new RecordSelfCare(1, Habit.Sleep, ContributionSource.SelfReported, 450));
        journal.Apply("workday", new Workday("O1"));
        journal.AdvanceTo(At("2026-10-15 12:00", 2));
        return journal;
    }

    // UX journey 2: seven days away, then late self-care for the days that are still in the window (D-53).
    private static Journal SevenDaysAway()
    {
        var journal = Journal.Start(At("2026-10-14 12:00", 2), s_night);
        journal.Apply("woodcutter", new Build(BuildingKind.Woodcutter, "F2"));
        journal.AdvanceTo(At("2026-10-21 18:00", 2));
        for (int back = 0; back <= RecordSelfCare.DaysBack; back++)
        {
            int day = journal.World.GameDay - back;
            journal.Apply($"steps-{back}", new RecordSelfCare(day, Habit.Steps, ContributionSource.Verified, 5_000 + (back * 900)));
            journal.Apply($"sleep-{back}", new RecordSelfCare(day, Habit.Sleep, ContributionSource.Verified, 380 + (back * 10)));
        }

        journal.Apply("upgrade", new Upgrade("K1"));
        journal.AdvanceTo(At("2026-10-22 09:00", 2));
        return journal;
    }

    // UX journey 3: an evening when the rest lock holds, with self-care recorded at night and credited at dawn.
    private static Journal EveningWithRestLock()
    {
        var journal = Journal.Start(At("2026-10-14 20:00", 2), s_night);
        journal.AdvanceTo(At("2026-10-14 23:30", 2));
        journal.Apply("build-at-night", new Build(BuildingKind.Cottage, "L2"));
        journal.Apply("steps-at-night", new RecordSelfCare(0, Habit.Steps, ContributionSource.Verified, 11_000));
        journal.Apply("earlier-rest", new ChangeRestSchedule(new RestSchedule((22 * 60) + 30, 9 * 60)));
        journal.AdvanceTo(At("2026-10-15 07:05", 2));
        journal.Apply("sleep", new RecordSelfCare(1, Habit.Sleep, ContributionSource.Verified, 465));
        journal.AdvanceTo(At("2026-10-16 23:15", 2));
        journal.Apply("too-late", new Build(BuildingKind.Cottage, "L2"));
        journal.AdvanceTo(At("2026-10-17 08:00", 2));
        return journal;
    }

    // Economy v1, section 7: a week near home, building out the cluster with moderate self-care.
    private static void Near(Journal journal, int day)
    {
        (string Key, GameCommand Command)[] plan = day switch
        {
            0 => [("survey-1", new Survey()), ("woodcutter-f3", new Build(BuildingKind.Woodcutter, "F3"))],
            1 => [("survey-2", new Survey()), ("quarry-m2", new Build(BuildingKind.Quarry, "M2"))],
            2 => [("storehouse-e1", new Build(BuildingKind.Storehouse, "E1")), ("workday-m2", new Workday("M2"))],
            3 => [("sawmill-e2", new Build(BuildingKind.Sawmill, "E2")), ("rush", new Rush())],
            4 => [("longhouse-l1", new Build(BuildingKind.Longhouse, "L1")), ("workday-e2", new Workday("E2"))],
            5 => [("fishing-k2", new Build(BuildingKind.FishingHut, "K2"))],
            _ => [("field-o2", new Build(BuildingKind.Field, "O2")), ("road-m2", new BuildRoad("M2"))],
        };

        foreach (var (key, command) in plan)
        {
            journal.Apply($"{key}-{day}", command);
        }
    }

    // Economy v1, section 7: a week towards the sea, with an expedition to Nyön.
    private static void Sea(Journal journal, int day)
    {
        (string Key, GameCommand Command)[] plan = day switch
        {
            0 => [("jetty-h1", new Build(BuildingKind.Jetty, "H1")), ("woodcutter-f2", new Build(BuildingKind.Woodcutter, "F2"))],
            1 => [("sawmill-e2", new Build(BuildingKind.Sawmill, "E2")), ("workday-e2", new Workday("E2"))],
            2 => [("expedition", new SendExpedition()), ("rush", new Rush())],
            3 => [("expedition-again", new SendExpedition()), ("workday-f2", new Workday("F2"))],
            4 => [("jetty-n0", new Build(BuildingKind.Jetty, "N0")), ("sheep-n1", new Build(BuildingKind.SheepFarm, "N1"))],
            5 => [("quarry-n2", new Build(BuildingKind.Quarry, "N2")), ("weavery-l3", new Build(BuildingKind.Weavery, "L3"))],
            _ => [("longhouse-l1", new Build(BuildingKind.Longhouse, "L1")), ("workday-n1", new Workday("N1"))],
        };

        foreach (var (key, command) in plan)
        {
            journal.Apply($"{key}-{day}", command);
        }
    }

    /// <summary>Seven days with the forces.md moderate self-care: 7 500 steps, 6.5 h of sleep and three workouts.</summary>
    private static Journal Week(Action<Journal, int> plan)
    {
        var journal = Journal.Start(At("2026-10-14 10:00", 2), s_night);
        for (int day = 0; day < 7; day++)
        {
            journal.AdvanceTo(At($"2026-10-{14 + day} 08:30", 2));
            int today = journal.World.GameDay;
            journal.Apply($"steps-{day}", new RecordSelfCare(today, Habit.Steps, ContributionSource.Verified, 7_500));
            journal.Apply($"sleep-{day}", new RecordSelfCare(today, Habit.Sleep, ContributionSource.Verified, 390));
            journal.Apply($"workouts-{day}", new RecordSelfCare(today, Habit.Workouts, ContributionSource.Verified, day % 2 == 0 && day < 6 ? 1 : 0));
            plan(journal, day);
        }

        journal.AdvanceTo(At("2026-10-21 12:00", 2));
        return journal;
    }

    // Daylight saving time ends at 01:00 UTC on 2026-10-25 during rest; then a trip to New York and back.
    private static Journal DaylightSavingAndTravel()
    {
        var journal = Journal.Start(At("2026-10-24 15:00", 2), s_night);
        journal.Apply("steps", new RecordSelfCare(0, Habit.Steps, ContributionSource.Verified, 9_000));
        journal.AdvanceTo(At("2026-10-25 02:59", 2));
        journal.AdvanceTo(At("2026-10-25 09:00", 1));
        journal.Apply("sleep", new RecordSelfCare(journal.World.GameDay, Habit.Sleep, ContributionSource.Verified, 540));
        journal.AdvanceTo(At("2026-10-27 18:00", 1));
        journal.AdvanceTo(At("2026-10-27 23:30", -4, NewYork));
        journal.Apply("in-new-york", new RecordSelfCare(journal.World.GameDay, Habit.Steps, ContributionSource.SelfReported, 14_000));
        journal.AdvanceTo(At("2026-10-30 07:00", -4, NewYork));
        journal.AdvanceTo(At("2026-10-31 09:00", 1));
        journal.Apply("home-again", new Survey());
        journal.AdvanceTo(At("2026-11-01 12:00", 1));
        return journal;
    }

    private static Journal RandomJournal(int seed)
    {
        var scenario = Scenario.Generate(seed, length: 200);
        var journal = Journal.Start(scenario.Install, scenario.Schedule);
        foreach (var input in scenario.Inputs)
        {
            input.ApplyTo(journal);
        }

        return journal;
    }

    private static List<(string Name, string Replay)> Corpus() =>
        Directory.GetFiles(s_folder, "*.replay")
            .Order(StringComparer.Ordinal)
            .Select(path => (Path.GetFileNameWithoutExtension(path), File.ReadAllText(path, Encoding.UTF8)))
            .ToList();

    private static List<string> HashesIn(CultureInfo culture, List<(string Name, string Replay)> replays)
    {
        var (current, currentUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, culture);
            return replays.Select(r => $"{r.Name} {Replay.StateHash(r.Replay)}").ToList();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (current, currentUi);
        }
    }

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Eirora.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("The tests run outside the repository.");
    }
}
