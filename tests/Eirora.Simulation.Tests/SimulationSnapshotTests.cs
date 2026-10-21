using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests;

/// <summary>A snapshot holds the whole world and restores it exactly (ADR-0006, INV-D5, INV-D6).</summary>
public sealed class SimulationSnapshotTests
{
    [Fact]
    public void TheHeaderNamesTheVersionsAndTheSimulationTime()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);

        var snapshot = SimulationSnapshot.FromBytes(SimulationSnapshot.Take(world).ToBytes());

        Assert.Equal(SimulationSnapshot.FormatVersion, snapshot.Format);
        Assert.Equal(RulesVersion.Current, snapshot.Rules);
        Assert.Equal(ContentVersion.Current, snapshot.Content);
        Assert.Equal(world.Now.UtcSeconds, snapshot.UtcSeconds);
    }

    [Fact]
    public void ARestedWorldWithChangesWaitingForDawnContinuesAsItWould()
    {
        World Night1()
        {
            var world = World.Create(At("2026-10-14 14:00", 2), Night);
            world.AdvanceTo(At("2026-10-15 01:00", 2));
            world.Apply("sleep", new RecordSelfCare(0, Habit.Sleep, ContributionSource.Verified, 420));
            world.Apply("steps", new RecordSelfCare(0, Habit.Steps, ContributionSource.SelfReported, 9_000));
            world.Apply("schedule", new ChangeRestSchedule(new RestSchedule(22 * 60, 9 * 60)));
            return world;
        }

        var uninterrupted = Night1();
        var restored = RoundTrip(Night1());
        Assert.Equal(Phase.Resting, restored.Phase);
        Assert.NotNull(restored.PendingRestSchedule);

        uninterrupted.AdvanceTo(At("2026-10-17 12:00", 2));
        restored.AdvanceTo(At("2026-10-17 12:00", 2));

        Assert.Equal(uninterrupted.Fingerprint(), restored.Fingerprint());
    }

    [Theory]
    [InlineData("with space")]
    [InlineData("100%")]
    [InlineData("line\nbreak")]
    [InlineData("tab\tand\rreturn")]
    [InlineData("åäö-日本")]
    public void AnyKeyIsKeptAndStaysUsedAfterLoading(string key)
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        var first = world.Apply(key, new Survey());

        var restored = RoundTrip(world);
        var again = restored.Apply(key, new Survey());

        Assert.True(first.Accepted);
        Assert.Equal(Describe.Result(first), Describe.Result(again));
        Assert.Equal(world.Fingerprint(), restored.Fingerprint());
        Assert.Equal(world.Available(Force.Exploration), restored.Available(Force.Exploration));
    }

    [Fact]
    public void EveryKindOfEventSurvivesASnapshot()
    {
        var samples = new GameEvent[]
        {
            new DuskFell(1),
            new DawnBroke(2, 3),
            new ForceCredited(4, Force.BuildPower, 5),
            new ForceWithdrawn(6, Force.DawnPower, 7),
            new VillagersMovedIn(8, 2),
            new ProjectCompleted(9, ProjectKind.Road, BuildingKind.Quarry, "M1"),
            new SitesRevealed(10, ["M2", "F3"]),
            new ExpeditionReturned(11, ["N0", "N1", "N2"]),
            new FogLifted(12, -5, 3),
        };
        var writer = new StateWriter();
        foreach (var sample in samples)
        {
            GameEventCodec.Write(writer, sample);
        }

        var reader = new StateReader(writer.ToString());
        var read = samples.Select(_ => GameEventCodec.Read(reader)).ToList();
        reader.End();

        var kinds = typeof(GameEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameEvent)) && !t.IsAbstract);
        Assert.Equal(kinds.OrderBy(t => t.Name), samples.Select(s => s.GetType()).OrderBy(t => t.Name));
        Assert.Equal(samples.Select(Describe.Event), read.Select(Describe.Event));
    }

    [Fact]
    public void AChangedByteIsCaught()
    {
        var bytes = SimulationSnapshot.Take(World.Create(At("2026-10-14 14:00", 2), Night)).ToBytes();
        int villagers = Encoding.UTF8.GetString(bytes).IndexOf("settlement 12", StringComparison.Ordinal);
        bytes[villagers + "settlement 1".Length] = (byte)'3';

        Assert.Throws<FormatException>(() => SimulationSnapshot.FromBytes(bytes));
    }

    [Theory]
    [InlineData("settlement 12 ", "settlement 012 ")] // Not the canonical form of the number.
    [InlineData("forest F1 ", "forest X9 ")] // A site that does not exist.
    public void AStateThatDoesNotRestoreToItselfIsRejected(string original, string replacement)
    {
        var text = Encoding.UTF8.GetString(SimulationSnapshot.Take(World.Create(At("2026-10-14 14:00", 2), Night)).ToBytes());
        var (header, state) = Split(text);
        Assert.Contains(original, state, StringComparison.Ordinal);

        var changed = Reseal(header, state.Replace(original, replacement, StringComparison.Ordinal));

        Assert.Throws<FormatException>(() => SimulationSnapshot.FromBytes(changed).Restore(PrototypeHome.Map));
    }

    [Fact]
    public void ANewerFormatIsNotRead()
    {
        var text = Encoding.UTF8.GetString(SimulationSnapshot.Take(World.Create(At("2026-10-14 14:00", 2), Night)).ToBytes());

        var newer = text.Replace($"eirora-snapshot {SimulationSnapshot.FormatVersion}\n", $"eirora-snapshot {SimulationSnapshot.FormatVersion + 1}\n", StringComparison.Ordinal);

        Assert.Throws<SavedByNewerVersionException>(() => SimulationSnapshot.FromBytes(Encoding.UTF8.GetBytes(newer)));
    }

    [Theory]
    [InlineData("rules", 1)]
    [InlineData("content", 1)]
    public void NewerRulesOrContentAreReadButNotRestored(string line, int change)
    {
        var snapshot = WithHeaderLine(line, change);

        Assert.True(snapshot.IsFromNewerVersion);
        Assert.Throws<SavedByNewerVersionException>(() => snapshot.Restore(PrototypeHome.Map));
    }

    [Fact]
    public void OlderRulesWithoutAMigrationAreNotRestored()
    {
        var snapshot = WithHeaderLine("rules", -1);

        Assert.Equal(RulesVersion.Current - 1, snapshot.Rules);
        Assert.Throws<FormatException>(() => snapshot.Restore(PrototypeHome.Map, Array.Empty<StateMigration>()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("eirora-journal 1 rules 1\ncreate 0 Europe/Stockholm 0 1380 480\n")]
    [InlineData("eirora-snapshot 1\nrules 1\ncontent 1\ntime 0\n")]
    [InlineData("eirora-snapshot 1\nrules 1\ncontent 1\ntime zero\nstate-sha256 00\n")]
    public void BytesThatAreNotASnapshotAreRejected(string text) =>
        Assert.Throws<FormatException>(() => SimulationSnapshot.FromBytes(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void BytesThatAreNotUtf8AreRejected() =>
        Assert.Throws<FormatException>(() => SimulationSnapshot.FromBytes([0x65, 0xFF, 0xFE, 0x0A]));

    private static SimulationSnapshot WithHeaderLine(string line, int change)
    {
        var text = Encoding.UTF8.GetString(SimulationSnapshot.Take(World.Create(At("2026-10-14 14:00", 2), Night)).ToBytes());
        int current = line == "rules" ? RulesVersion.Current : ContentVersion.Current;
        string changed = text.Replace($"\n{line} {current}\n", $"\n{line} {current + change}\n", StringComparison.Ordinal);
        return SimulationSnapshot.FromBytes(Encoding.UTF8.GetBytes(changed));
    }

    private static World RoundTrip(World world) => SimulationSnapshot.FromBytes(SimulationSnapshot.Take(world).ToBytes()).Restore(PrototypeHome.Map);

    private static (string Header, string State) Split(string text)
    {
        int end = 0;
        for (int line = 0; line < 5; line++)
        {
            end = text.IndexOf('\n', end) + 1;
        }

        return (text[..end], text[end..]);
    }

    private static byte[] Reseal(string header, string state)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
        var lines = header.Split('\n');
        lines[4] = $"state-sha256 {hash}";
        return Encoding.UTF8.GetBytes(string.Join('\n', lines) + state);
    }
}
