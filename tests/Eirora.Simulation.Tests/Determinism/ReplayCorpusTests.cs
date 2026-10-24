using System.Globalization;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using Eirora.WorldGeneration;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Determinism;

/// <summary>
/// The replay corpus that proves the simulation gives the same state on the server and in the client (TASK-021,
/// NFR-02): hand-made scenarios from the UX journeys and economy v1, and random sequences. This test writes the state
/// hash of each replay, golden save and golden map to artifacts/determinism/dotnet.txt, under the names DeterminismRun
/// gives them, and scripts/check-determinism.sh compares it with the hashes the IL2CPP build reports on Android.
/// </summary>
public sealed class ReplayCorpusTests
{
    private const long Hour = 3600;
    private const int RandomReplays = 20;

    private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);
    private static readonly string s_folder = Repository.PathTo("tests", "Replays");
    private static readonly string s_goldenSaves = Repository.PathTo("tests", "Eirora.Simulation.Tests", "Saving", "Golden");
    private static readonly string s_goldenMaps = Repository.PathTo("tests", "Eirora.Simulation.Tests", "WorldGeneration", "Golden", "maps.txt");

    private static readonly (string Name, Func<Journal> Make)[] s_handMade =
    [
        ("ux-first-session", FirstSession),
        ("ux-seven-days-away", SevenDaysAway),
        ("ux-evening-with-rest-lock", EveningWithRestLock),
        ("economy-near", () => Week(Near)),
        ("economy-sea", () => Week(Sea)),
        ("economy-staffing", Staffing),
        ("economy-carriers", Carriers),
        ("time-daylight-saving-and-travel", DaylightSavingAndTravel),
        ("epoch-fog-towards-nyon", FogTowardsNyon),
    ];

    // A hand-made replay proves what its commands do only if they are carried out, so each is accepted unless the
    // scenario is about its rejection.
    private static readonly Dictionary<string, (string Key, Rejection Rejection)[]> s_rejectedOnPurpose = new(StringComparer.Ordinal)
    {
        ["ux-evening-with-rest-lock"] = [("build-at-night", Rejection.Resting), ("too-late", Rejection.Resting)],
        ["epoch-fog-towards-nyon"] = [("nyon", Rejection.FoundByExpedition)],
    };

    public static TheoryData<string> HandMade => [.. s_handMade.Select(s => s.Name)];

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

    [Theory]
    [MemberData(nameof(HandMade))]
    public void AHandMadeReplaysCommandsAreAcceptedUnlessItIsAboutTheirRejection(string name)
    {
        var replay = Corpus().Single(r => r.Name == name).Replay;

        Assert.Equal(s_rejectedOnPurpose.GetValueOrDefault(name, []), Replay.Rejections(replay));
    }

    // Scenario makes most commands suit the world, so the random replays change the world rather than bounce off it,
    // and makes the rest blind, so each still plays rejections.
    [Fact]
    public void TheRandomReplaysCommandsAreMostlyAccepted()
    {
        var random = Corpus().Where(r => r.Name.StartsWith("random-", StringComparison.Ordinal))
            .Select(r => (r.Name, Commands: r.Replay.Split('\n').Count(line => line.StartsWith("cmd ", StringComparison.Ordinal)), Rejected: Replay.Rejections(r.Replay).Count))
            .ToList();
        Assert.Equal(RandomReplays, random.Count);

        Assert.All(random, r => Assert.True(r.Rejected > 0, $"{r.Name} rejects no command."));
        int commands = random.Sum(r => r.Commands);
        int rejected = random.Sum(r => r.Rejected);
        Assert.True(rejected * 2 < commands, $"The random replays reject {rejected} of {commands} commands.");
    }

    [Fact]
    public void TheCorpusGivesTheSameStateTwiceAndIsReportedForComparison()
    {
        var report = Report();
        Assert.Contains(report, line => line.StartsWith("replay/", StringComparison.Ordinal));
        Assert.Contains(report, line => line.StartsWith("save/", StringComparison.Ordinal));
        Assert.Contains(report, line => line.StartsWith("map/procedural-archipelago-", StringComparison.Ordinal));

        Assert.Equal(report, Report());

        string folder = Repository.PathTo("artifacts", "determinism");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "dotnet.txt"), string.Concat(report.Select(line => line + "\n")), new UTF8Encoding(false));
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

        var invariant = ReportIn(CultureInfo.InvariantCulture);
        var local = ReportIn(culture);

        Assert.Equal(invariant, local);
    }

    // UX journey 1: the first evening, from choosing the rest period to the first dawn.
    private static Journal FirstSession()
    {
        var journal = Journal.Start(At("2026-10-14 17:30", 2), s_night);
        journal.Apply("survey", new Survey());
        journal.Apply("cottage", new Build(BuildingKind.Cottage, Tiles.L1));
        journal.Apply("steps", new RecordSelfCare(0, Habit.Steps, ContributionSource.SelfReported, 6_400));
        journal.AdvanceTo(At("2026-10-14 22:45", 2));
        journal.Apply("workout", new RecordSelfCare(0, Habit.Workouts, ContributionSource.SelfReported, 1));
        journal.AdvanceTo(At("2026-10-15 07:10", 2));
        journal.Apply("sleep", new RecordSelfCare(1, Habit.Sleep, ContributionSource.SelfReported, 450));
        journal.Apply("workday", new Workday(Tiles.O1));
        journal.AdvanceTo(At("2026-10-15 12:00", 2));
        return journal;
    }

    // UX journey 2: seven days away, then late self-care for the days that are still in the window (D-53).
    private static Journal SevenDaysAway()
    {
        var journal = Journal.Start(At("2026-10-14 12:00", 2), s_night);
        journal.Apply("woodcutter", new Build(BuildingKind.Woodcutter, Tiles.F2));
        journal.AdvanceTo(At("2026-10-21 18:00", 2));
        for (int back = 0; back < RecordSelfCare.DaysBack; back++)
        {
            int day = journal.World.GameDay - back;
            journal.Apply($"steps-{back}", new RecordSelfCare(day, Habit.Steps, ContributionSource.Verified, 5_000 + (back * 900)));
            journal.Apply($"sleep-{back}", new RecordSelfCare(day, Habit.Sleep, ContributionSource.Verified, 380 + (back * 10)));
        }

        journal.Apply("upgrade", new Upgrade(Tiles.K1));
        journal.AdvanceTo(At("2026-10-22 09:00", 2));
        return journal;
    }

    // UX journey 3: an evening when the rest lock holds, with self-care recorded at night and credited at dawn.
    private static Journal EveningWithRestLock()
    {
        var journal = Journal.Start(At("2026-10-14 20:00", 2), s_night);
        journal.AdvanceTo(At("2026-10-14 23:30", 2));
        journal.Apply("build-at-night", new Build(BuildingKind.Cottage, Tiles.L2));
        journal.Apply("steps-at-night", new RecordSelfCare(0, Habit.Steps, ContributionSource.Verified, 11_000));
        journal.Apply("earlier-rest", new ChangeRestSchedule(new RestSchedule((22 * 60) + 30, 9 * 60)));
        journal.AdvanceTo(At("2026-10-15 07:05", 2));
        journal.Apply("sleep", new RecordSelfCare(1, Habit.Sleep, ContributionSource.Verified, 465));
        journal.AdvanceTo(At("2026-10-16 23:15", 2));
        journal.Apply("too-late", new Build(BuildingKind.Cottage, Tiles.L2));
        journal.AdvanceTo(At("2026-10-17 08:00", 2));
        return journal;
    }

    // Economy v1, section 7: a week near home, building out the cluster with moderate self-care.
    private static void Near(Journal journal, int day)
    {
        (string Key, GameCommand Command)[] plan = day switch
        {
            0 => [("survey-1", new Survey()), ("woodcutter-f3", new Build(BuildingKind.Woodcutter, Tiles.F3))],
            1 => [("survey-2", new Survey()), ("quarry-m2", new Build(BuildingKind.Quarry, Tiles.M2))],
            2 => [("storehouse-e1", new Build(BuildingKind.Storehouse, Tiles.E1)), ("workday-m2", new Workday(Tiles.M2))],
            3 => [("sawmill-e2", new Build(BuildingKind.Sawmill, Tiles.E2)), ("rush", new Rush())],
            4 => [("longhouse-l1", new Build(BuildingKind.Longhouse, Tiles.L1)), ("workday-e2", new Workday(Tiles.E2))],
            5 => [("fishing-k2", new Build(BuildingKind.FishingHut, Tiles.K2))],
            _ => [("field-o2", new Build(BuildingKind.Field, Tiles.O2)), ("road-k2", new BuildRoad(Tiles.K2))],
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
            0 => [("jetty-h1", new Build(BuildingKind.Jetty, Tiles.H1)), ("woodcutter-f2", new Build(BuildingKind.Woodcutter, Tiles.F2))],
            1 => [("sawmill-e2", new Build(BuildingKind.Sawmill, Tiles.E2)), ("rush", new Rush()), ("workday-f2", new Workday(Tiles.F2))],
            2 => [("expedition", new SendExpedition()), ("workday-e2", new Workday(Tiles.E2))],
            3 => [("workday-f2", new Workday(Tiles.F2))],
            4 => [("jetty-n0", new Build(BuildingKind.Jetty, Tiles.N0)), ("workday-e2", new Workday(Tiles.E2))],
            5 => [("sheep-n1", new Build(BuildingKind.SheepFarm, Tiles.N1)), ("quarry-n2", new Build(BuildingKind.Quarry, Tiles.N2))],
            _ => [("weavery-l3", new Build(BuildingKind.Weavery, Tiles.L3)), ("longhouse-l1", new Build(BuildingKind.Longhouse, Tiles.L1)), ("workday-n1", new Workday(Tiles.N1))],
        };

        foreach (var (key, command) in plan)
        {
            journal.Apply($"{key}-{day}", command);
        }
    }

    // Economy v2, section 7 (TASK-051): a road laid on a tile and one to the storehouse, the storehouse's carriers capped,
    // given a workday, and the quarry put first for them.
    private static Journal Carriers()
    {
        var journal = Journal.Start(At("2026-10-14 10:00", 2), s_night);
        journal.Apply("lay-l2", new LayRoad(Tiles.L2));
        journal.Apply("quarry-m3", new Build(BuildingKind.Quarry, Tiles.M3));
        journal.AdvanceTo(At("2026-10-14 18:00", 2));
        journal.Apply("road-m3", new BuildRoad(Tiles.M3));
        journal.Apply("cap-s", new SetCarrierCap(Tiles.S, 2));
        journal.AdvanceTo(At("2026-10-15 08:00", 2));
        journal.Apply("workday-s", new Workday(Tiles.S));
        journal.Apply("m3-first", new Prioritize(Tiles.M3, 0));
        journal.AdvanceTo(At("2026-10-16 12:00", 2));
        return journal;
    }

    // Economy v1, section 4 (TASK-044): the quarry staffed first, the fishing hut paused overnight and a cottage moved
    // first in the queue.
    private static Journal Staffing()
    {
        var journal = Journal.Start(At("2026-10-14 10:00", 2), s_night);
        journal.Apply("quarry-m3", new Build(BuildingKind.Quarry, Tiles.M3));
        journal.Apply("cottage-l1", new Build(BuildingKind.Cottage, Tiles.L1));
        journal.Apply("cottage-l2", new Build(BuildingKind.Cottage, Tiles.L2));
        journal.AdvanceTo(At("2026-10-14 15:00", 2));
        journal.Apply("quarry-first", new Prioritize(Tiles.M3, 0));
        journal.Apply("pause-k1", new PauseBuilding(Tiles.K1));
        journal.Apply("l2-first", new QueueFirst(ProjectKind.Build, Tiles.L2));
        journal.AdvanceTo(At("2026-10-15 08:30", 2));
        journal.Apply("resume-k1", new ResumeBuilding(Tiles.K1));
        journal.Apply("k1-first", new Prioritize(Tiles.K1, 0));
        journal.AdvanceTo(At("2026-10-15 12:00", 2));
        return journal;
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

    // Epoch 1 (TASK-032): the fog lifted tile by tile east towards Nyön, which only an expedition can find.
    private static Journal FogTowardsNyon()
    {
        var journal = Journal.Start(At("2026-10-14 10:00", 2), s_night);
        journal.Apply("lift-6-2", new LiftFog(6, -2));
        journal.Apply("lift-7-2", new LiftFog(7, -2));
        journal.Apply("lift-8-3", new LiftFog(8, -3));
        journal.Apply("nyon", new LiftFog(PrototypeHome.NyonCentre.Q, PrototypeHome.NyonCentre.R));
        journal.Apply("steps", new RecordSelfCare(0, Habit.Steps, ContributionSource.Verified, 12_000));
        journal.AdvanceTo(At("2026-10-15 08:30", 2));
        journal.Apply("lift-5-1", new LiftFog(5, -1));
        journal.Apply("lift-4-0", new LiftFog(4, 0));
        journal.AdvanceTo(At("2026-10-15 12:00", 2));
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

    /// <summary>
    /// The state hash of everything the determinism build plays, under the names DeterminismRun gives them: each replay,
    /// each golden save after loading (and migrating) and saving again, and each golden map made again by its generator.
    /// The golden prototype journal is not played: rules 4 refuse it (GoldenSaveTests).
    /// </summary>
    private static List<string> Report()
    {
        IEnumerable<(string Name, string Hash)> entries = Corpus().Select(r => ($"replay/{r.Name}", Replay.StateHash(r.Replay)))
            .Concat(Golden("*.save").Select(path => ($"save/{Path.GetFileNameWithoutExtension(path)}", Replay.StateHashAfterSaving(Save.Load(File.ReadAllBytes(path))))))
            .Concat(GoldenMaps().Select(m => ($"map/{m.Id}-{m.Version}-{m.Seed}", WorldGenerators.Get(m.Id, int.Parse(m.Version, CultureInfo.InvariantCulture)).Generate(ulong.Parse(m.Seed, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)).Hash)));
        return entries.OrderBy(e => e.Name, StringComparer.Ordinal).Select(e => $"{e.Name} {e.Hash}").ToList();
    }

    private static IEnumerable<string> Golden(string pattern) => Directory.GetFiles(s_goldenSaves, pattern).Order(StringComparer.Ordinal);

    /// <summary>The generator, version and seed in hex of each golden map.</summary>
    private static IEnumerable<(string Id, string Version, string Seed)> GoldenMaps() =>
        File.ReadAllLines(s_goldenMaps, Encoding.UTF8).Where(line => line.Length > 0).Select(line => line.Split(' ')).Select(f => (f[0], f[1], f[2]));

    private static List<string> ReportIn(CultureInfo culture)
    {
        var (current, currentUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, culture);
            return Report();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (current, currentUi);
        }
    }
}
