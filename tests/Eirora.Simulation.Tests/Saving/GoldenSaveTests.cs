using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Golden saves (ADR-0006): one per rules version and save format, made by a headless scenario with synthetic
/// self-care and checked in with the fingerprint that loading it with current code must give. An expected fingerprint
/// changes only in the change that bumps the rules or alters a migration, so the review sees it.
/// </summary>
public sealed class GoldenSaveTests
{
    private const string Prototype = "prototype-journal-rules-1";

    private static readonly string s_folder = Repository.PathTo("tests", "Eirora.Simulation.Tests", "Saving", "Golden");

    public static TheoryData<string> Saves =>
        Directory.Exists(s_folder)
            ? [.. Directory.GetFiles(s_folder, "*.save").Select(f => Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal)]
            : [];

    // The current version's golden save is written when it is missing, to be reviewed and committed; older ones cannot
    // be made any more, so a missing one fails.
    [Fact]
    public void EveryRulesVersionAndFormatHasAGoldenSave()
    {
        string current = Name(Save.FormatVersion, RulesVersion.Current);
        if (!File.Exists(Path.Combine(s_folder, current + ".save")))
        {
            Write(current);
            Assert.Fail($"Wrote the golden save {current}; review it and commit it.");
        }

        var names = Directory.GetFiles(s_folder, "*.save").Select(Path.GetFileNameWithoutExtension).ToList();
        for (int rules = 1; rules <= RulesVersion.Current; rules++)
        {
            Assert.Contains(names, n => n!.EndsWith($"-rules-{rules}", StringComparison.Ordinal));
        }

        for (int format = 1; format <= Save.FormatVersion; format++)
        {
            Assert.Contains(names, n => n!.StartsWith($"save-{format}-rules-", StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(Saves))]
    public void AGoldenSaveLoadsToItsExpectedWorldAndPlaysOn(string name)
    {
        var save = Save.Load(File.ReadAllBytes(Path.Combine(s_folder, name + ".save")));

        Assert.Equal(Expected(name), save.World.Fingerprint());
        PlayOn(save);
    }

    // Rules 4 made every land tile a building site, so the prototype's journal from M1 would replay to another world
    // than the one its player built; it is refused instead of shown (ADR-0006), and the client says so (TASK-014).
    [Fact]
    public void TheGoldenPrototypeJournalIsRefusedOnceItsRulesNoLongerReplay()
    {
        string path = Path.Combine(s_folder, Prototype + ".journal");
        Assert.True(File.Exists(path), $"The golden prototype journal {Prototype} is missing.");
        Assert.True(RulesVersion.ReplaysJournalsFrom > 1);

        var refused = Assert.Throws<JournalFromOlderRulesException>(() => Save.FromPrototypeJournal(File.ReadAllText(path, Encoding.UTF8)));

        Assert.Contains("rules version 1", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>Continues a loaded world for a few days under the current rules and writes and loads it again.</summary>
    private static void PlayOn(Save save)
    {
        var now = save.World.Now;
        for (int day = 1; day <= 3; day++)
        {
            save.AdvanceTo(new TimeReading(now.UtcSeconds + (day * 24 * 3600), now.ZoneId, now.OffsetSeconds));
            save.Apply($"golden-steps-{day}", new RecordSelfCare(save.World.GameDay, Habit.Steps, ContributionSource.Verified, 8_000));
            save.Apply($"golden-sleep-{day}", new RecordSelfCare(save.World.GameDay, Habit.Sleep, ContributionSource.SelfReported, 420));
            save.Apply($"golden-cottage-{day}", new Build(BuildingKind.Cottage, L1));
        }

        var again = Save.Load(save.ToBytes());
        Assert.Equal(save.World.Fingerprint(), again.World.Fingerprint());
        Assert.Equal(save.World.Fingerprint(), again.Replay().Fingerprint());
    }

    /// <summary>
    /// Makes the golden save of the current version from a fixed scenario: from rules 8 on, a world built out on the
    /// archipelago with a settlement on each of three islands, villagers in each, two sea routes with goods waiting and
    /// materials owed, and paused buildings (TASK-044, TASK-045, TASK-050).
    /// </summary>
    private static void Write(string name)
    {
        var save = BuiltOutWorld.GenerateSave(47);
        Assert.Equal(2, save.World.SeaRoutes.Count);
        Assert.Contains(save.World.Buildings, b => b.Paused);
        Assert.All(save.World.Settlements, s => Assert.True(s.Villagers > 0, $"No villagers on {s.Island}."));
        Assert.Contains(save.World.Islands, i => save.World.Settlement.Owed(i) > 0);

        Directory.CreateDirectory(s_folder);
        File.WriteAllBytes(Path.Combine(s_folder, name + ".save"), save.ToBytes());
        File.WriteAllText(Path.Combine(s_folder, name + ".expected"), Save.Load(save.ToBytes()).World.Fingerprint(), new UTF8Encoding(false));
    }

    /// <summary>The bytes of a checked-in golden save.</summary>
    internal static byte[] Bytes(string name) => File.ReadAllBytes(Path.Combine(s_folder, name + ".save"));

    private static string Expected(string name) => File.ReadAllText(Path.Combine(s_folder, name + ".expected"), Encoding.UTF8);

    private static string Name(int format, int rules) => $"save-{format}-rules-{rules}";
}

