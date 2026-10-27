using System.Text;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 10 → 11 (TASK-046): every processing building gets the minimum stock of 20 that all processing left, the sea
/// routes keep the fixed order and nothing is reserved, so a world goes on as it did under rules 10 (ADR-0006).
/// </summary>
public sealed class StockRulesArriveTests
{
    private const string Golden = "save-3-rules-10";

    private static readonly string s_folder = Repository.PathTo("tests", "Eirora.Simulation.Tests", "Saving", "Golden");

    // Acceptance: a save from the previous rules gives every processing building the minimum stock 20 and the sea routes
    // the default order, which is wool before stone since rules 16 (TASK-056).
    [Fact]
    public void EveryProcessingBuildingLeavesTwentyAndTheShipsKeepTheFixedOrder()
    {
        byte[] bytes = GoldenSaveTests.Bytes(Golden);
        Assert.Contains("rules 10", SaveSections.Text(Encoding.UTF8.GetString(bytes), "latest").Split('\n'));

        var world = Save.Load(bytes).World;

        Assert.Contains(world.Buildings, b => b.Kind == BuildingKind.Sawmill);
        Assert.Equal(2, world.SeaRoutes.Count);
        Assert.All(world.Buildings, b => Assert.Equal(world.Content.For(b.Kind).Recipe?.Inputs.Count > 0 ? StockRules.StandardReserve : 0, b.Reserve));
        Assert.All(world.Islands, island =>
        {
            Assert.Equal(StockRules.DefaultImportOrder, world.ImportOrder(island));
            Assert.All(Enum.GetValues<Resource>(), r => Assert.Equal(0, world.ReservationOf(island, r)));
        });
    }

    /// <summary>
    /// Acceptance: the world stays the same. The <c>.week</c> file of the golden save from rules 10 is its world a week
    /// later, as the rules 10 code played it until rules 12 changed the week (TASK-061): since then it is the current
    /// rules' week. A later rules version that changes the week writes the file again in the change that bumps the rules,
    /// so the review sees what changed (ADR-0006).
    /// </summary>
    [Fact]
    public void AWorldFromRulesTenGivesTheSameWeekAsUnderRulesTen()
    {
        var save = Save.Load(GoldenSaveTests.Bytes(Golden));
        Assert.True(save.ChangedOnLoad);
        var now = save.World.Now;
        for (int day = 1; day <= 7; day++)
        {
            save.AdvanceTo(new TimeReading(now.UtcSeconds + (day * 24 * 3600), now.ZoneId, now.OffsetSeconds));
        }

        string week = Path.Combine(s_folder, Golden + ".week");
        if (!File.Exists(week))
        {
            File.WriteAllText(week, save.World.Fingerprint(), new UTF8Encoding(false));
            Assert.Fail($"Wrote {week}; review it and commit it.");
        }

        Assert.Equal(File.ReadAllText(week, Encoding.UTF8), save.World.Fingerprint());
    }

    // A world built under the current rules without stock rules of the player's, written as rules 10 wrote it, migrates
    // back to itself as rules 11 wrote it.
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void ARulesTenStateMigratesBackToItself(int seed)
    {
        var world = BuiltOutWorld.BeforeTiers(seed).World;
        string state = WithoutPlayersRules(world.Fingerprint());
        var map = world.Map;

        Assert.Equal(PopulationTiersArriveTests.RulesEleven(state), new StockRulesArrive().Migrate(RulesTen(state), map));
        var reader = new StateReader(state);
        World.Read(reader, map);
        reader.End();
    }

    [Fact]
    public void ABuildingLineThatIsNotOfRulesTenIsRefused()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);

        Assert.Throws<FormatException>(() => new StockRulesArrive().Migrate(world.Fingerprint(), PrototypeHome.Map));
    }

    /// <summary>
    /// A state of rules 11 or later as rules 10 wrote it: no minimum stock on the buildings, no reservations and no import
    /// orders; a state of rules 10 or earlier stays as it is.
    /// </summary>
    internal static string RulesTen(string state) => string.Join(
        "\n",
        PopulationTiersArriveTests.RulesEleven(state).Split('\n')
            .Where(line => !line.StartsWith("reserved ", StringComparison.Ordinal) && !line.StartsWith("imports ", StringComparison.Ordinal))
            .Select(line => line.StartsWith("building ", StringComparison.Ordinal) && line.Split(' ').Length == BuildingFields ? line[..line.LastIndexOf(' ')] : line));

    private const int BuildingFields = 17; // A building line of rules 11, its kind and tile counted.

    /// <summary>The state with the standard minimum stock everywhere and nothing reserved or reordered, as rules 10 played.</summary>
    private static string WithoutPlayersRules(string state) => string.Join(
        "\n",
        state.Split('\n')
            .Where(line => !line.StartsWith("reserved ", StringComparison.Ordinal) && !line.StartsWith("imports ", StringComparison.Ordinal))
            .Select(line =>
            {
                if (!line.StartsWith("building ", StringComparison.Ordinal))
                {
                    return line;
                }

                bool processing = line.StartsWith("building Sawmill ", StringComparison.Ordinal) || line.StartsWith("building Weavery ", StringComparison.Ordinal);
                return $"{line[..line.LastIndexOf(' ')]} {(processing ? StockRules.StandardReserve : 0)}";
            }));
}
