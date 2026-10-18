using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Saving;
using FsCheck.Xunit;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>The save holds a world safely across writes, crashes and rules versions (ADR-0006, NFR-01).</summary>
public sealed class SaveTests
{
    // INV-D5 through the save: writing and loading at any point continues as the world would.
    [Property(MaxTest = 60)]
    public bool ASaveWrittenAndLoadedAtAnyPointContinuesAsTheWorldWould(int seed, int split)
    {
        var scenario = Scenario.Generate(seed);
        int at = Math.Abs(split % (scenario.Inputs.Count + 1));

        var save = Save.Begin(scenario.Install, scenario.Schedule);
        for (int i = 0; i < scenario.Inputs.Count; i++)
        {
            if (i == at)
            {
                save = Save.Load(save.ToBytes());
            }

            Apply(scenario.Inputs[i], save);
        }

        return save.World.Fingerprint() == scenario.Run().Fingerprint();
    }

    // The segment's start and journal always lead to the latest snapshot, so a migration can be redone from them.
    [Property(MaxTest = 60)]
    public bool TheJournalLeadsFromTheStartToTheLatestSnapshot(int seed)
    {
        var scenario = Scenario.Generate(seed);
        var save = Save.Begin(scenario.Install, scenario.Schedule);
        foreach (var input in scenario.Inputs)
        {
            Apply(input, save);
        }

        var loaded = Save.Load(save.ToBytes());

        return save.Replay().Fingerprint() == save.World.Fingerprint()
            && loaded.Replay().Fingerprint() == save.World.Fingerprint();
    }

    [Fact]
    public void WritingTwiceGivesTheSameBytes()
    {
        var save = PlayedSave();

        Assert.Equal(save.ToBytes(), save.ToBytes());
        Assert.Equal(save.ToBytes(), Save.Load(save.ToBytes()).ToBytes());
    }

    // A crash or an empty battery mid-write leaves an incomplete file, which must never pass for a world (NFR-01).
    [Fact]
    public void ASaveCutOffAnywhereIsNotLoaded()
    {
        var bytes = PlayedSave().ToBytes();

        for (int length = 0; length < bytes.Length; length += 1 + (bytes.Length / 200))
        {
            var cut = bytes[..length];
            var problem = Assert.ThrowsAny<FormatException>(() => Save.Load(cut));
            Assert.IsNotType<SavedByNewerVersionException>(problem);
        }
    }

    [Fact]
    public void AChangedByteIsCaught()
    {
        var bytes = PlayedSave().ToBytes();
        bytes[bytes.Length / 2] ^= 0x01;

        var problem = Assert.ThrowsAny<FormatException>(() => Save.Load(bytes));
        Assert.IsNotType<SavedByNewerVersionException>(problem);
    }

    [Fact]
    public void ASaveFromANewerFormatIsReportedAsNewer()
    {
        string text = Encoding.UTF8.GetString(PlayedSave().ToBytes());

        string newer = Reseal(text, Save.FormatVersion + 1);

        Assert.Throws<SavedByNewerVersionException>(() => Save.Load(Encoding.UTF8.GetBytes(newer)));
    }

    // The hash covers the header too, so a damaged format version is not mistaken for a newer app's save.
    [Fact]
    public void AChangedFormatVersionIsCaught()
    {
        string text = Encoding.UTF8.GetString(PlayedSave().ToBytes());

        string changed = text.Replace($"eirora-save {Save.FormatVersion}\n", $"eirora-save {Save.FormatVersion + 8}\n", StringComparison.Ordinal);

        var problem = Assert.ThrowsAny<FormatException>(() => Save.Load(Encoding.UTF8.GetBytes(changed)));
        Assert.IsNotType<SavedByNewerVersionException>(problem);
    }

    [Theory]
    [InlineData("rules")]
    [InlineData("content")]
    public void ASaveWithNewerRulesOrContentIsReportedAsNewer(string line)
    {
        int current = line == "rules" ? RulesVersion.Current : ContentVersion.Current;
        string text = Encoding.UTF8.GetString(PlayedSave().ToBytes());

        string newer = Reseal(text.Replace($"\n{line} {current}\n", $"\n{line} {current + 1}\n", StringComparison.Ordinal));

        Assert.Throws<SavedByNewerVersionException>(() => Save.Load(Encoding.UTF8.GetBytes(newer)));
    }

    [Fact]
    public void AnOlderRulesVersionIsMigratedIntoANewSegment()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        var old = OldRules(world);

        var migrated = Save.Load(old, [new AddCreatedLine()]);

        Assert.Equal(world.Fingerprint(), migrated.World.Fingerprint());
        Assert.True(migrated.ChangedOnLoad);
        Assert.True(migrated.HasPreviousSegment);

        // Written again, it loads without migrations and keeps the earlier segment until the next rules change.
        migrated.Apply("survey", new Survey());
        var reloaded = Save.Load(migrated.ToBytes(), []);
        Assert.False(reloaded.ChangedOnLoad);
        Assert.True(reloaded.HasPreviousSegment);
        Assert.Equal(migrated.World.Fingerprint(), reloaded.World.Fingerprint());
        Assert.Equal(reloaded.World.Fingerprint(), reloaded.Replay().Fingerprint());
    }

    [Fact]
    public void AnOlderRulesVersionWithoutAMigrationIsNotLoaded()
    {
        var old = OldRules(World.Create(At("2026-10-14 14:00", 2), Night));

        var problem = Assert.ThrowsAny<FormatException>(() => Save.Load(old, []));
        Assert.IsNotType<SavedByNewerVersionException>(problem);
    }

    [Fact]
    public void ThereIsAMigrationFromEveryEarlierRulesVersion()
    {
        for (int rules = 1; rules < RulesVersion.Current; rules++)
        {
            Assert.Single(Migrations.All, m => m.FromRules == rules);
        }

        Assert.DoesNotContain(Migrations.All, m => m.FromRules < 1 || m.FromRules >= RulesVersion.Current);
    }

    // ADR-0006: a prototype journal from M1 is replayed once and becomes the first segment.
    [Fact]
    public void APrototypeJournalBecomesTheFirstSegment()
    {
        var journal = Journal.Start(At("2026-10-14 14:00", 2), Night);
        foreach (var input in Scenario.Generate(7).Inputs)
        {
            input.ApplyTo(journal);
        }

        var save = Save.FromPrototypeJournal(journal.ToText());
        var loaded = Save.Load(save.ToBytes());

        Assert.True(save.ChangedOnLoad);
        Assert.Equal(journal.World.Fingerprint(), save.World.Fingerprint());
        Assert.Equal(journal.World.Fingerprint(), loaded.World.Fingerprint());
        Assert.Equal(journal.World.Fingerprint(), loaded.Replay().Fingerprint());
    }

    internal static void Apply(Scenario.Input input, Save save)
    {
        switch (input)
        {
            case Scenario.Advance advance:
                save.AdvanceTo(advance.To);
                break;
            case Scenario.Command command:
                save.Apply(command.Key, command.For(save.World));
                break;
        }
    }

    private static Save PlayedSave()
    {
        var scenario = Scenario.Generate(11);
        var save = Save.Begin(scenario.Install, scenario.Schedule);
        foreach (var input in scenario.Inputs)
        {
            Apply(input, save);
        }

        return save;
    }

    /// <summary>A save of <paramref name="world"/> as an imagined rules version before `created` was recorded.</summary>
    private static byte[] OldRules(World world)
    {
        string state = string.Join(string.Empty, world.Fingerprint().Split('\n').Where(l => !l.StartsWith("created ", StringComparison.Ordinal)).Select(l => l.Length == 0 ? l : l + "\n"));
        string snapshot = string.Concat(
            "eirora-snapshot 1\n",
            $"rules {RulesVersion.Current - 1}\n",
            $"content {ContentVersion.Current}\n",
            $"time {world.Now.UtcSeconds.ToString(CultureInfo.InvariantCulture)}\n",
            $"state-sha256 {Sha256(state)}\n",
            state);
        string body = Section("start", snapshot) + Section("inputs", string.Empty) + Section("latest", snapshot);
        return Encoding.UTF8.GetBytes(Sealed(1, body));

        static string Section(string name, string text) => $"{name} {text.Length}\n{text}";
    }

    /// <summary>Gives a hand-changed save a matching hash, and its snapshots too.</summary>
    private static string Reseal(string save, int format = Save.FormatVersion)
    {
        int bodyStart = save.IndexOf('\n', save.IndexOf('\n') + 1) + 1;
        string body = save[bodyStart..];
        var resealed = new StringBuilder();
        int at = 0;
        while (at < body.Length)
        {
            int lineEnd = body.IndexOf('\n', at);
            var fields = body[at..lineEnd].Split(' ');
            int length = int.Parse(fields[1], CultureInfo.InvariantCulture);
            string text = body.Substring(lineEnd + 1, length);
            if (text.StartsWith("eirora-snapshot", StringComparison.Ordinal))
            {
                var lines = text.Split('\n');
                string state = string.Join('\n', lines[5..]);
                lines[4] = $"state-sha256 {Sha256(state)}";
                text = string.Join('\n', lines);
            }

            resealed.Append(fields[0]).Append(' ').Append(text.Length).Append('\n').Append(text);
            at = lineEnd + 1 + length;
        }

        return Sealed(format, resealed.ToString());
    }

    /// <summary>A save's frame around <paramref name="body"/>: the header and a hash of the header and the body.</summary>
    private static string Sealed(int format, string body)
    {
        string header = $"eirora-save {format.ToString(CultureInfo.InvariantCulture)}\n";
        return $"{header}sha256 {Sha256(header + body)}\n{body}";
    }

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>The imagined migration that records when the world was created, which the cycle line already holds.</summary>
    private sealed class AddCreatedLine : StateMigration
    {
        public override int FromRules => RulesVersion.Current - 1;

        public override string Migrate(string state)
        {
            var lines = state.Split('\n').ToList();
            int cycle = lines.FindIndex(l => l.StartsWith("cycle ", StringComparison.Ordinal));
            lines.Insert(cycle + 1, $"created {lines[cycle].Split(' ')[1]}");
            return string.Join("\n", lines);
        }
    }
}
