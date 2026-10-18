using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Golden saves (ADR-0006): one per rules version and save format, made by a headless scenario with synthetic
/// self-care and checked in with the fingerprint that loading it with current code must give. An expected fingerprint
/// changes only in the change that bumps the rules or alters a migration, so the review sees it.
/// </summary>
public sealed class GoldenSaveTests
{
    private const string Prototype = "prototype-journal-rules-1";

    private static readonly string s_folder = Path.Combine(RepositoryRoot(), "tests", "Eirora.Simulation.Tests", "Saving", "Golden");

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

    [Fact]
    public void TheGoldenPrototypeJournalBecomesItsExpectedWorld()
    {
        string path = Path.Combine(s_folder, Prototype + ".journal");
        if (!File.Exists(path) && RulesVersion.Current == 1)
        {
            // The last rules version of the prototype journals; it can only be made while it is current.
            WritePrototype(path);
            Assert.Fail($"Wrote the golden prototype journal {Prototype}; review it and commit it.");
        }

        Assert.True(File.Exists(path), $"The golden prototype journal {Prototype} is missing.");

        var save = Save.FromPrototypeJournal(File.ReadAllText(path, Encoding.UTF8));

        Assert.Equal(Expected(Prototype), save.World.Fingerprint());
        PlayOn(save);
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
            save.Apply($"golden-cottage-{day}", new Build(BuildingKind.Cottage, "L1"));
        }

        var again = Save.Load(save.ToBytes());
        Assert.Equal(save.World.Fingerprint(), again.World.Fingerprint());
        Assert.Equal(save.World.Fingerprint(), again.Replay().Fingerprint());
    }

    /// <summary>Makes the golden save of the current version from a fixed scenario.</summary>
    private static void Write(string name)
    {
        var scenario = Scenario.Generate(20261018, length: 150);
        var save = Save.Begin(scenario.Install, scenario.Schedule);
        foreach (var input in scenario.Inputs)
        {
            SaveTests.Apply(input, save);
        }

        Directory.CreateDirectory(s_folder);
        File.WriteAllBytes(Path.Combine(s_folder, name + ".save"), save.ToBytes());
        File.WriteAllText(Path.Combine(s_folder, name + ".expected"), Save.Load(save.ToBytes()).World.Fingerprint(), new UTF8Encoding(false));
    }

    private static void WritePrototype(string path)
    {
        var scenario = Scenario.Generate(20261017, length: 150);
        var journal = Journal.Start(scenario.Install, scenario.Schedule);
        foreach (var input in scenario.Inputs)
        {
            input.ApplyTo(journal);
        }

        Directory.CreateDirectory(s_folder);
        File.WriteAllText(path, journal.ToText(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(s_folder, Prototype + ".expected"), journal.World.Fingerprint(), new UTF8Encoding(false));
    }

    private static string Expected(string name) => File.ReadAllText(Path.Combine(s_folder, name + ".expected"), Encoding.UTF8);

    private static string Name(int format, int rules) => $"save-{format}-rules-{rules}";

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
