using System.Text;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 13 → 14 (TASK-062): no villager is a craftsman, the stores have no honey, mead or ale, and the unlocking goes on
/// where the world stands, so step 2 is unlocked when a settlement already has 30 villagers, of them 6 farmers (ADR-0006).
/// </summary>
public sealed class CraftsmenArriveTests
{
    private const string Golden = "save-3-rules-13";

    private static readonly string[] s_newGoods = [nameof(Resource.Honey), nameof(Resource.Mead), nameof(Resource.Ale)];

    // Acceptance: given a save from the previous rules version, when it is migrated, then the world is unchanged: the same
    // villagers of each tier, buildings and goods, no craftsmen and none of the new goods.
    [Fact]
    public void ASaveFromRulesThirteenKeepsItsWorld()
    {
        byte[] bytes = GoldenSaveTests.Bytes(Golden);
        string before = SaveSections.Text(Encoding.UTF8.GetString(bytes), "latest");

        var world = Save.Load(bytes).World;

        string after = RulesThirteen(world.Fingerprint());
        Assert.Equal(Lines(before, "settlement "), Lines(after, "settlement "));
        Assert.Equal(Lines(before, "stock "), Lines(after, "stock "));
        Assert.Equal(Lines(before, "building "), Lines(after, "building "));
        Assert.All(world.Settlements, s => Assert.Equal(0, s.VillagersOf(PopulationTier.Craftsmen)));
        Assert.All(world.Islands, island => Assert.All(s_newGoods, good => Assert.Equal(0, world.Stock(island, Enum.Parse<Resource>(good)))));
    }

    // Acceptance: the migrated world has step 2 when the condition already holds, and keeps step 1 when it does not.
    [Theory]
    [InlineData(24, 6, 1, "2")]
    [InlineData(25, 5, 1, "1")]
    [InlineData(23, 6, 1, "1")]
    [InlineData(30, 0, 0, "0")]
    public void StepTwoIsUnlockedWhenTheConditionAlreadyHolds(int crofters, int farmers, int step, string migratedStep)
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesThirteen(world.Fingerprint()).Split('\n').ToList();
        int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
        lines[economy] = $"{lines[economy][..lines[economy].LastIndexOf(' ')]} {step}";
        lines[lines.FindIndex(l => l.StartsWith("settlement home ", StringComparison.Ordinal))] = $"settlement home {crofters} {farmers}";

        var migrated = new CraftsmenArrive().Migrate(string.Join('\n', lines), PrototypeHome.Map).Split('\n');

        Assert.Equal(migratedStep, migrated.Single(l => l.StartsWith("economy ", StringComparison.Ordinal)).Split(' ')[3]);
        Assert.Contains($"settlement home {crofters} {farmers} 0", migrated);
    }

    // A world of the current rules without craftsmen and the new goods, written as rules 13 wrote it, migrates back to itself.
    [Theory]
    [InlineData("2026-10-14 14:00", 0)]
    [InlineData("2026-10-14 14:00", 5)]
    public void ARulesThirteenStateMigratesBackToItself(string start, int days)
    {
        var world = World.Create(TestClock.At(start, 2), TestClock.Night);
        var now = world.Now;
        world.AdvanceTo(new TimeReading(now.UtcSeconds + (days * 24 * 3600), now.ZoneId, now.OffsetSeconds));
        string state = world.Fingerprint();

        Assert.Equal(state, new CraftsmenArrive().Migrate(RulesThirteen(state), PrototypeHome.Map));
        var reader = new StateReader(state);
        World.Read(reader, PrototypeHome.Map);
        reader.End();
    }

    // The new goods come last in an import order, after rules 13's.
    [Fact]
    public void AnImportOrderTakesTheNewGoodsLast()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesThirteen(world.Fingerprint()).Split('\n').ToList();
        var rulesThirteen = Enum.GetValues<Resource>().Where(r => !s_newGoods.Contains(r.ToString())).Reverse();
        lines.Insert(lines.FindIndex(l => l.StartsWith("building ", StringComparison.Ordinal)), $"imports home {string.Join(' ', rulesThirteen)}");

        var migrated = new CraftsmenArrive().Migrate(string.Join('\n', lines), PrototypeHome.Map).Split('\n');

        Assert.Contains($"imports home {string.Join(' ', rulesThirteen)} Honey Mead Ale", migrated);
    }

    [Theory]
    [InlineData("economy 0 0")]
    [InlineData("settlement home 12 0 0")]
    [InlineData("settlement home 12")]
    public void ALineThatIsNotOfRulesThirteenIsRefused(string line)
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesThirteen(world.Fingerprint()).Split('\n').ToList();
        int at = lines.FindIndex(l => l.StartsWith(line.Split(' ')[0] + " ", StringComparison.Ordinal));
        lines[at] = line;

        Assert.Throws<FormatException>(() => new CraftsmenArrive().Migrate(string.Join('\n', lines), PrototypeHome.Map));
    }

    /// <summary>
    /// A state of rules 14 or later as rules 13 wrote it: no craftsmen on each settlement's line, no step beyond 1, and no
    /// honey, mead or ale in the stores or in an import order; a state of rules 13 or earlier stays as it is.
    /// </summary>
    internal static string RulesThirteen(string state)
    {
        var lines = state.Split('\n').ToList();
        if (!lines.Any(l => l.StartsWith("settlement ", StringComparison.Ordinal) && l.Split(' ').Length == 5))
        {
            return state;
        }

        int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
        var fields = lines[economy].Split(' ');
        fields[3] = fields[3] == "2" ? "1" : fields[3];
        lines[economy] = string.Join(' ', fields);
        lines.RemoveAll(l => l.StartsWith("stock ", StringComparison.Ordinal) && s_newGoods.Contains(l.Split(' ')[2]));
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("settlement ", StringComparison.Ordinal))
            {
                lines[i] = lines[i][..lines[i].LastIndexOf(' ')];
            }
            else if (lines[i].StartsWith("imports ", StringComparison.Ordinal))
            {
                lines[i] = string.Join(' ', lines[i].Split(' ').Where(f => !s_newGoods.Contains(f)));
            }
        }

        return string.Join('\n', lines);
    }

    private static string[] Lines(string state, string prefix) =>
        [.. state.Split('\n').Where(l => l.StartsWith(prefix, StringComparison.Ordinal))];
}
