using System.Text;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 14 → 15 (TASK-063): the stores have no tar and the islands no knarrs, and the unlocking goes on where the world
/// stands, so step 3 is unlocked when a settlement already has 45 villagers, of them 6 craftsmen, and a sea route joins
/// the home island to another settlement (ADR-0006).
/// </summary>
public sealed class SeafaringArrivesTests
{
    private const string Golden = "save-3-rules-14";

    // Acceptance: given a save from the previous rules version, when it is migrated, then the world is unchanged: the same
    // villagers of each tier, buildings and goods, no tar and no knarrs.
    [Fact]
    public void ASaveFromRulesFourteenKeepsItsWorld()
    {
        byte[] bytes = GoldenSaveTests.Bytes(Golden);
        string before = SaveSections.Text(Encoding.UTF8.GetString(bytes), "latest");

        var world = Save.Load(bytes).World;

        string after = RulesFourteen(world.Fingerprint());
        Assert.Equal(Lines(before, "settlement "), Lines(after, "settlement "));
        Assert.Equal(Lines(before, "stock "), Lines(after, "stock "));
        Assert.Equal(Lines(before, "building "), Lines(after, "building "));
        Assert.Equal(2, world.Step);
        Assert.All(world.Islands, island => Assert.Equal(0, world.Stock(island, Resource.Tar)));
        Assert.All(world.Islands, island => Assert.Equal(0, world.Knarrs(island)));
    }

    // Acceptance: the migrated world has step 3 when the condition already holds, and keeps step 2 when it does not: too
    // few villagers or craftsmen, step 2 not reached, or no sea route.
    [Theory]
    [InlineData(30, 9, 6, 2, true, "3")]
    [InlineData(30, 9, 5, 2, true, "2")]
    [InlineData(29, 9, 6, 2, true, "2")]
    [InlineData(30, 9, 6, 1, true, "1")]
    [InlineData(30, 9, 6, 2, false, "2")]
    public void StepThreeIsUnlockedWhenTheConditionAlreadyHolds(int crofters, int farmers, int craftsmen, int step, bool seaRoute, string migratedStep)
    {
        var world = Archipelago.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesFourteen(world.Fingerprint()).Split('\n').ToList();
        int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
        lines[economy] = $"{lines[economy][..lines[economy].LastIndexOf(' ')]} {step}";
        lines[lines.FindIndex(l => l.StartsWith($"settlement {Archipelago.Home} ", StringComparison.Ordinal))] = $"settlement {Archipelago.Home} {crofters} {farmers} {craftsmen}";
        if (seaRoute)
        {
            // A settlement on the first island and a jetty on each, as the archipelago's world has none yet.
            int stock = lines.FindIndex(l => l.StartsWith("stock ", StringComparison.Ordinal));
            lines.Insert(stock, $"settlement {Archipelago.First} 0 0 0");
            int building = lines.FindIndex(l => l.StartsWith("building ", StringComparison.Ordinal));
            lines.Insert(building, Jetty(world.Map, Archipelago.Home));
            lines.Insert(building, Jetty(world.Map, Archipelago.First));
        }

        var migrated = new SeafaringArrives().Migrate(string.Join('\n', lines), world.Map).Split('\n');

        Assert.Equal(migratedStep, migrated.Single(l => l.StartsWith("economy ", StringComparison.Ordinal)).Split(' ')[3]);
        Assert.Contains($"settlement {Archipelago.Home} {crofters} {farmers} {craftsmen}", migrated);
    }

    // A world of the current rules without tar or knarrs, written as rules 14 wrote it, migrates back to itself.
    [Theory]
    [InlineData("2026-10-14 14:00", 0)]
    [InlineData("2026-10-14 14:00", 5)]
    public void ARulesFourteenStateMigratesBackToItself(string start, int days)
    {
        var world = World.Create(TestClock.At(start, 2), TestClock.Night);
        var now = world.Now;
        world.AdvanceTo(new TimeReading(now.UtcSeconds + (days * 24 * 3600), now.ZoneId, now.OffsetSeconds));
        string state = world.Fingerprint();

        Assert.Equal(state, new SeafaringArrives().Migrate(RulesFourteen(state), PrototypeHome.Map));
        var reader = new StateReader(state);
        World.Read(reader, PrototypeHome.Map);
        reader.End();
    }

    // Tar comes last in an import order, after rules 14's goods.
    [Fact]
    public void AnImportOrderTakesTarLast()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesFourteen(world.Fingerprint()).Split('\n').ToList();
        var rulesFourteen = Enum.GetValues<Resource>().Where(r => r != Resource.Tar).Reverse();
        lines.Insert(lines.FindIndex(l => l.StartsWith("building ", StringComparison.Ordinal)), $"imports home {string.Join(' ', rulesFourteen)}");

        var migrated = new SeafaringArrives().Migrate(string.Join('\n', lines), PrototypeHome.Map).Split('\n');

        Assert.Contains($"imports home {string.Join(' ', rulesFourteen)} Tar", migrated);
    }

    [Theory]
    [InlineData("economy 0 0")]
    [InlineData("settlement home 12 0")]
    [InlineData("settlement home 12 0 0 0")]
    public void ALineThatIsNotOfRulesFourteenIsRefused(string line)
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesFourteen(world.Fingerprint()).Split('\n').ToList();
        int at = lines.FindIndex(l => l.StartsWith(line.Split(' ')[0] + " ", StringComparison.Ordinal));
        lines[at] = line;

        Assert.Throws<FormatException>(() => new SeafaringArrives().Migrate(string.Join('\n', lines), PrototypeHome.Map));
    }

    /// <summary>
    /// A state of rules 15 or later without knarrs as rules 14 wrote it: no step beyond 2, and no tar in the stores or in
    /// an import order; a state of rules 14 or earlier stays as it is.
    /// </summary>
    internal static string RulesFourteen(string state)
    {
        var lines = state.Split('\n').ToList();
        if (!lines.Any(l => l.StartsWith("stock ", StringComparison.Ordinal) && l.Split(' ')[2] == nameof(Resource.Tar)))
        {
            return state;
        }

        if (lines.Any(l => l.StartsWith("knarr", StringComparison.Ordinal)))
        {
            throw new ArgumentException("Rules 14 had no knarrs.", nameof(state));
        }

        int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
        var fields = lines[economy].Split(' ');
        fields[3] = fields[3] == "3" ? "2" : fields[3];
        lines[economy] = string.Join(' ', fields);
        lines.RemoveAll(l => l.StartsWith("stock ", StringComparison.Ordinal) && l.Split(' ')[2] == nameof(Resource.Tar));
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("imports ", StringComparison.Ordinal))
            {
                lines[i] = string.Join(' ', lines[i].Split(' ').Where(f => f != nameof(Resource.Tar)));
            }
        }

        return string.Join('\n', lines);
    }

    /// <summary>A finished, unstaffed jetty on the first harbour spot of an island, as a building line of rules 14.</summary>
    private static string Jetty(WorldMap map, string island)
    {
        var tile = map.Features.LandOf(island).First(h => (map.Features.UsesOf(h) & TileUse.HarborSpot) != 0);
        return $"building Jetty {tile.Q} {tile.R} 1 False False 0 False 0 4 False 0 False 0 0 0";
    }

    private static string[] Lines(string state, string prefix) =>
        [.. state.Split('\n').Where(l => l.StartsWith(prefix, StringComparison.Ordinal))];
}
