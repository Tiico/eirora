using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// The save's world part and contribution part (ADR-0008, D-57, D-100): the cloud save keeps the world part readable and
/// the contribution part encrypted, so the world part holds nothing per game day, and it carries on the world alone
/// when the contribution key is lost (D-101).
/// </summary>
public sealed class SavePartsTests
{
    private static readonly string[] s_perGameDay = ["selfcare ", "credited ", "gained ", "spends-at-credit ", "unreconciled ", "command ", "cmd ", "closed-through "];

    [Fact]
    public void TheWorldPartHoldsNothingPerGameDay()
    {
        var save = PlayedSave(11);

        string world = Encoding.UTF8.GetString(save.ToParts().World);

        Assert.Contains("\nsettlement ", world, StringComparison.Ordinal);
        Assert.All(world.Split('\n'), line => Assert.DoesNotContain(s_perGameDay, start => line.StartsWith(start, StringComparison.Ordinal)));
    }

    // The two parts together are the whole save: the same world, and a journal that still leads to it.
    [Property(MaxTest = 40)]
    public bool BothPartsLoadToTheWorldTheWholeSaveHolds(int seed)
    {
        var save = PlayedSave(seed);
        var (world, contributions) = save.ToParts();

        var loaded = Save.FromParts(world, contributions);

        return loaded.World.Fingerprint() == save.World.Fingerprint()
            && loaded.Replay().Fingerprint() == save.World.Fingerprint()
            && !loaded.ChangedOnLoad
            && Save.Load(loaded.ToBytes()).World.Fingerprint() == save.World.Fingerprint();
    }

    [Fact]
    public void TheWholeSaveSplitsIntoTheSameParts()
    {
        var save = PlayedSave(5);

        var loaded = Save.Load(save.ToBytes());

        Assert.Equal(save.ToParts().World, loaded.ToParts().World);
        Assert.Equal(save.ToParts().Contributions, loaded.ToParts().Contributions);
    }

    [Fact]
    public void WithoutItsContributionsTheWorldGoesOnWithItsGameDaysClosed()
    {
        var save = PlayedSave(11);
        var played = save.World;

        var alone = Save.FromParts(save.ToParts().World, contributions: null);
        var world = alone.World;

        Assert.True(alone.ChangedOnLoad);
        Assert.Equal(played.WorldState(), world.WorldState());
        Assert.All(Enum.GetValues<Force>(), force => Assert.Equal(played.Available(force), world.Available(force)));
        Assert.Equal(played.GameDay, world.LastClosedGameDay);
        Assert.Null(world.RecordedSelfCare(played.GameDay, Habit.Steps, ContributionSource.Verified));

        // A closed game day takes no more self-care, so nothing can be credited twice; the next one does.
        var today = alone.Apply("steps-today", new RecordSelfCare(world.GameDay, Habit.Steps, ContributionSource.Verified, 9_000));
        Assert.Equal(Rejection.GameDayOutOfRange, today.Rejection);
        alone.AdvanceTo(new TimeReading(world.GameDayStartUtc(world.GameDay)!.Value + (36 * 3600), world.Now.ZoneId, world.Now.OffsetSeconds));
        var tomorrow = alone.Apply("steps-tomorrow", new RecordSelfCare(world.GameDay, Habit.Steps, ContributionSource.Verified, 9_000));
        Assert.True(tomorrow.Accepted);

        // The closed days stay closed in the new segment, and its journal leads to the world.
        var reloaded = Save.Load(alone.ToBytes());
        Assert.Equal(alone.World.Fingerprint(), reloaded.World.Fingerprint());
        Assert.Equal(alone.World.Fingerprint(), reloaded.Replay().Fingerprint());
        Assert.Equal(played.GameDay, reloaded.World.LastClosedGameDay);
    }

    [Fact]
    public void ContributionsOfAnotherMomentAreNotLoaded()
    {
        var save = PlayedSave(11);
        var earlier = save.ToParts();
        save.AdvanceTo(new TimeReading(save.World.Now.UtcSeconds + 3600, save.World.Now.ZoneId, save.World.Now.OffsetSeconds));
        var later = save.ToParts();

        Assert.ThrowsAny<FormatException>(() => Save.FromParts(later.World, earlier.Contributions));
    }

    [Fact]
    public void PartsInTheWrongPlaceAreNotLoaded()
    {
        var (world, contributions) = PlayedSave(11).ToParts();

        Assert.ThrowsAny<FormatException>(() => Save.FromParts(contributions, world));
        Assert.ThrowsAny<FormatException>(() => Save.FromParts(world, world));
        Assert.ThrowsAny<FormatException>(() => Save.FromParts(contributions, contributions));
        Assert.ThrowsAny<FormatException>(() => Save.Load(world));
    }

    // A phone that lost its key on an older app still gets its world: the migrations run on the world part alone.
    [Fact]
    public void AWorldPartFromEarlierRulesIsMigratedWithoutItsContributions()
    {
        const string Golden = "save-3-rules-3";
        string whole = Encoding.UTF8.GetString(GoldenSaveTests.Bytes(Golden));
        byte[] worldPart = Sealed(SaveSections.Written(whole, "map") + SaveSections.Written(whole, "latest"));

        var alone = Save.FromParts(worldPart, contributions: null);
        var full = Save.Load(GoldenSaveTests.Bytes(Golden));

        Assert.Equal(full.World.WorldState(), alone.World.WorldState());
        Assert.Equal(full.World.GameDay, alone.World.LastClosedGameDay);
    }

    private static byte[] Sealed(string body)
    {
        string header = $"eirora-save {Save.FormatVersion}\n";
        return Encoding.UTF8.GetBytes($"{header}sha256 {Serialization.Sha256.Of(header + body)}\n{body}");
    }

    private static Save PlayedSave(int seed)
    {
        var scenario = Scenario.Generate(seed);
        var save = Save.Begin(scenario.Install, scenario.Schedule);
        foreach (var input in scenario.Inputs)
        {
            SaveTests.Apply(input, save);
        }

        return save;
    }
}
