using System.Globalization;
using System.Text;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 11 → 12 (TASK-061): every villager is a crofter, the stores have no game or hide, and the unlocking starts where
/// the world stands, so step 1 is unlocked when a settlement already has 20 villagers (ADR-0006).
/// </summary>
public sealed class PopulationTiersArriveTests
{
    private const string Golden = "save-3-rules-11";

    /// <summary>The resources of rules 11 and earlier, in their order; game and hide came with rules 12.</summary>
    internal static readonly Resource[] RulesElevenResources =
        [Resource.Fish, Resource.Grain, Resource.Timber, Resource.Planks, Resource.Stone, Resource.Wool, Resource.Wadmal];

    // Acceptance: given a save from the previous rules version, when it is migrated, then every villager is a crofter and
    // step 1 is unlocked, since the world already has 20 villagers.
    [Fact]
    public void EveryVillagerIsACrofterAndAWorldOfTwentyHasStepOne()
    {
        byte[] bytes = GoldenSaveTests.Bytes(Golden);
        Assert.Contains("settlement home 20", SaveSections.Text(Encoding.UTF8.GetString(bytes), "latest").Split('\n'));

        var world = Save.Load(bytes).World;

        Assert.Equal(1, world.Step);
        Assert.All(world.Settlements, s =>
        {
            Assert.Equal(s.Villagers, s.VillagersOf(PopulationTier.Crofters));
            Assert.Equal(0, s.VillagersOf(PopulationTier.Farmers));
        });
        Assert.All(world.Islands, island =>
        {
            Assert.Equal(0, world.Stock(island, Resource.Game));
            Assert.Equal(0, world.Stock(island, Resource.Hide));
        });
    }

    // A world below 20 villagers in every settlement starts at step 0, and reaches step 1 only at a dawn that has 20.
    [Fact]
    public void AWorldOfFewerStartsAtStepZero()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        string migrated = new PopulationTiersArrive().Migrate(RulesEleven(world.Fingerprint()), PrototypeHome.Map);

        Assert.Contains("economy 0 0 0", migrated.Split('\n'));
        Assert.Contains("settlement home 12 0", migrated.Split('\n'));
    }

    // A world of the current rules with only crofters and no game or hide, written as rules 11 wrote it, migrates back to itself.
    [Theory]
    [InlineData("2026-10-14 14:00", 0)]
    [InlineData("2026-10-14 14:00", 5)]
    public void ARulesElevenStateMigratesBackToItself(string start, int days)
    {
        var world = World.Create(TestClock.At(start, 2), TestClock.Night);
        var now = world.Now;
        world.AdvanceTo(new TimeReading(now.UtcSeconds + (days * 24 * 3600), now.ZoneId, now.OffsetSeconds));
        string state = world.Fingerprint();
        Assert.Equal(0, world.Settlements.Sum(s => s.VillagersOf(PopulationTier.Farmers)));

        Assert.Equal(HandBuildArrivesTests.RulesTwelve(state), new PopulationTiersArrive().Migrate(RulesEleven(state), PrototypeHome.Map));
        var reader = new StateReader(state);
        World.Read(reader, PrototypeHome.Map);
        reader.End();
    }

    [Theory]
    [InlineData("economy 0 0 0")]
    [InlineData("settlement home 12 0")]
    public void ALineThatIsNotOfRulesElevenIsRefused(string line)
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesEleven(world.Fingerprint()).Split('\n').ToList();
        int at = lines.FindIndex(l => l.StartsWith(line.Split(' ')[0] + " ", StringComparison.Ordinal));
        lines[at] = line;

        Assert.Throws<FormatException>(() => new PopulationTiersArrive().Migrate(string.Join('\n', lines), PrototypeHome.Map));
    }

    /// <summary>
    /// A state of rules 12 or later as rules 11 wrote it, after <see cref="HandBuildArrivesTests.RulesTwelve"/>: no step on the economy's line, the villagers of every tier
    /// together on each settlement's, no game or hide in the stores and none in an import order; a state of rules 11 or
    /// earlier stays as it is.
    /// </summary>
    internal static string RulesEleven(string state)
    {
        var lines = HandBuildArrivesTests.RulesTwelve(state).Split('\n').ToList();
        int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
        if (economy < 0 || lines[economy].Split(' ').Length != 4)
        {
            return state;
        }

        lines[economy] = lines[economy][..lines[economy].LastIndexOf(' ')];
        lines.RemoveAll(l => l.StartsWith("stock ", StringComparison.Ordinal) && l.Split(' ')[2] is nameof(Resource.Game) or nameof(Resource.Hide));
        for (int i = 0; i < lines.Count; i++)
        {
            var fields = lines[i].Split(' ');
            if (fields[0] == "settlement")
            {
                long villagers = fields.Skip(2).Sum(f => long.Parse(f, CultureInfo.InvariantCulture));
                lines[i] = $"settlement {fields[1]} {villagers.ToString(CultureInfo.InvariantCulture)}";
            }
            else if (fields[0] == "imports")
            {
                lines[i] = string.Join(' ', fields.Where(f => f is not nameof(Resource.Game) and not nameof(Resource.Hide)));
            }
        }

        return string.Join('\n', lines);
    }
}
