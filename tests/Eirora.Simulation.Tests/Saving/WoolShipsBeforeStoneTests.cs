using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Tests.Economy;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 15 → 16 (TASK-056, D-135): the default import order ships wool before stone. An island without an order of the
/// player's takes the new default, and an order the player chose stays as it is (ADR-0006).
/// </summary>
public sealed class WoolShipsBeforeStoneTests
{
    private const string Golden = "save-3-rules-15";

    private static readonly string s_newDefault = string.Join(' ', StockRules.DefaultImportOrder);

    [Fact]
    public void TheDefaultOrderShipsWoolJustBeforeStone()
    {
        var order = StockRules.DefaultImportOrder.ToList();

        Assert.Equal(order.IndexOf(Resource.Stone) - 1, order.IndexOf(Resource.Wool));
        Assert.Equal(Enum.GetValues<Resource>().Where(r => r != Resource.Wool), order.Where(r => r != Resource.Wool));
    }

    // Acceptance: given a save from the previous rules version, when it is migrated, then the balances, buildings and
    // villagers are unchanged, the player's own import order is kept, and the other islands ship wool before stone.
    [Fact]
    public void ASaveFromRulesFifteenKeepsItsWorldAndThePlayersOrder()
    {
        byte[] bytes = GoldenSaveTests.Bytes(Golden);
        string before = SaveSections.Text(Encoding.UTF8.GetString(bytes), "latest");
        Assert.Contains("rules 15", before.Split('\n'));

        var save = Save.Load(bytes);
        var world = save.World;
        string after = world.Fingerprint();

        Assert.True(save.ChangedOnLoad);
        foreach (string kind in new[] { "settlement ", "stock ", "building ", "reserved ", "imports ", "force " })
        {
            Assert.Equal(Lines(before, kind), Lines(after, kind));
        }

        var players = Lines(before, "imports ").Select(l => l.Split(' ')[1]).ToList();
        Assert.NotEmpty(players);
        Assert.All(players, island => Assert.Equal(Resource.Stone, world.ImportOrder(island)[0]));
        Assert.All(world.Islands.Except(players), island => Assert.Equal(StockRules.DefaultImportOrder, world.ImportOrder(island)));
    }

    // An order the player chose that is now the default is the default, which is never written.
    [Fact]
    public void AnOrderOfThePlayersThatIsNowTheDefaultIsLeftOut()
    {
        var world = SeaRouteTests.OnlyShips((_, _) => 0);
        Assert.True(world.Apply("stone", new MoveImport(Archipelago.Second, Resource.Stone, 0)).Accepted);
        var lines = world.Fingerprint().Split('\n').ToList();
        int at = lines.FindIndex(l => l.StartsWith("building ", StringComparison.Ordinal));
        lines.Insert(at, $"imports {Archipelago.First} {s_newDefault}");

        var migrated = new WoolShipsBeforeStone().Migrate(string.Join('\n', lines), world.Map);

        Assert.Equal(world.Fingerprint(), migrated);
        Assert.Contains(migrated.Split('\n'), l => l.StartsWith($"imports {Archipelago.Second} Stone ", StringComparison.Ordinal));
    }

    // Rules 16 never write the default order, so a state of the current rules is a state of rules 15 that migrates to itself.
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    public void ARulesSixteenStateMigratesToItself(int seed)
    {
        var world = BuiltOutWorld.Generate(seed);
        string state = world.Fingerprint();

        Assert.Equal(state, new WoolShipsBeforeStone().Migrate(state, world.Map));
        var reader = new StateReader(state);
        World.Read(reader, world.Map);
        reader.End();
    }

    // Acceptance: without an order of the player's, the ships take the wool home before the stone, where rules 15 took
    // the stone first. Half a game day of the route is 20 units, all of them wool.
    [Fact]
    public void TheShipsTakeTheWoolBeforeTheStoneByDefault()
    {
        var world = SeaRouteTests.OnlyShips((island, r) => island == Archipelago.First && r is Resource.Stone or Resource.Wool ? 30 : 0);
        var now = world.Now;

        world.AdvanceTo(new TimeReading(now.UtcSeconds + (8 * 3600), now.ZoneId, now.OffsetSeconds));

        Assert.Equal((Quantity.Units(10), Quantity.Units(30)), (world.Settlement.Stock(Archipelago.First, Resource.Wool), world.Settlement.Stock(Archipelago.First, Resource.Stone)));
        Assert.Equal(Quantity.Units(20), world.Settlement.Stock(Resource.Wool));
    }

    private static List<string> Lines(string state, string prefix) =>
        [.. state.Split('\n').Where(l => l.StartsWith(prefix, StringComparison.Ordinal))];
}
