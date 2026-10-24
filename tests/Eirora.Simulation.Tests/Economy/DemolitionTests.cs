using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Demolition (economy-v1.md, section 3.2; TASK-047): the only way anything in the world becomes less (D-38). The tile is
/// free, its villagers build instead, half the materials come back to the island's store as far as there is room and no
/// force comes back (D-108, INV-F5); the goods waiting at it go to the store and must fit there (INV-R1, INV-R2).
/// </summary>
/// <remarks>
/// The worlds are written as state text, as in <see cref="StaffingTests"/>: the start village of twelve villagers, seven
/// of them in its four buildings, plus the buildings and stock the test needs.
/// </remarks>
public sealed class DemolitionTests
{
    private static readonly Hex s_secondForest = new(1, -2);
    private static int s_key;

    // Acceptance: a woodcutter demolished on a forest tile leaves the tile free, and another woodcutter fells its forest.
    [Fact]
    public void ADemolishedWoodcutterLeavesItsForestToAnother()
    {
        var world = Morning(["building Woodcutter 1 -2 1 1 0 0 0 0 0 0 0 0 0 0"]);
        Assert.Equal(Terrain.Forest, world.Map.TerrainAt(F1));
        Assert.Equal(Terrain.Forest, world.Map.TerrainAt(s_secondForest));
        int free = world.FreeVillagers;

        var result = Apply(world, new Demolish(F1));

        Assert.True(result.Accepted);
        Assert.Null(world.BuildingAt(F1));
        Assert.Equal(free + world.Content.For(BuildingKind.Woodcutter).Staff, world.FreeVillagers);
        Assert.Equal(Rejection.None, world.CheckBuild(BuildingKind.Cottage, F1));
        var demolished = Assert.IsType<BuildingDemolished>(Assert.Single(result.Events));
        Assert.Equal((BuildingKind.Woodcutter, F1), (demolished.Building, demolished.Tile));

        long left = world.Settlement.ForestLeft(F1);
        world.AdvanceTo(At("2026-10-15 11:00", 2));

        Assert.True(world.Settlement.ForestLeft(F1) < left, "The other woodcutter did not fell the freed forest.");
    }

    // Acceptance: a sawmill that cost 15 timber and 5 stone gives back 7 timber and 2 stone, and no force.
    [Fact]
    public void ASawmillGivesBackHalfItsMaterialsRoundedDownAndNoForce()
    {
        var world = Morning(["building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0"]);
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Timber] = 15, [Resource.Stone] = 5 }, world.Content.For(BuildingKind.Sawmill).Cost);
        var stock = Stocks(world);
        var forces = Enum.GetValues<Force>().Select(world.Available).ToList();

        var demolition = world.Demolition(L1)!;
        Assert.Equal((Rejection.None, true), (demolition.Rejection, demolition.ReturnsAll));
        Assert.Equal((7, 2), (demolition.Refund(Resource.Timber), demolition.Refund(Resource.Stone)));
        Assert.True(Apply(world, new Demolish(L1)).Accepted);

        Assert.Equal(stock[Resource.Timber] + 7, world.Stock(Resource.Timber));
        Assert.Equal(stock[Resource.Stone] + 2, world.Stock(Resource.Stone));
        Assert.All(Enum.GetValues<Resource>().Except([Resource.Timber, Resource.Stone]), r => Assert.Equal(stock[r], world.Stock(r)));
        Assert.Equal(forces, Enum.GetValues<Force>().Select(world.Available));
    }

    // Acceptance: when the refund does not all fit, the demolition says beforehand what comes back and what does not, and
    // the store ends full rather than over its capacity (INV-R1).
    [Fact]
    public void ARefundThatDoesNotFitIsShownBeforehandAndTheStoreEndsFull()
    {
        var world = Morning(["building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0"], ("Timber", 147));

        var demolition = world.Demolition(L1)!;

        Assert.Equal(Rejection.None, demolition.Rejection);
        Assert.False(demolition.ReturnsAll);
        Assert.Equal((7, 3, 4), (demolition.Refund(Resource.Timber), demolition.Returned(Resource.Timber), demolition.Lost(Resource.Timber)));
        Assert.Equal((2, 0), (demolition.Returned(Resource.Stone), demolition.Lost(Resource.Stone)));

        Assert.True(Apply(world, new Demolish(L1)).Accepted);
        Assert.Equal(150, world.Stock(Resource.Timber));
        Assert.Equal(150 * Quantity.PerUnit, world.Settlement.Stock(Resource.Timber));
    }

    // The upgrade counts to what it cost, also one still in the queue, which goes with the building.
    [Fact]
    public void AnUpgradeCountsToWhatItCostAndAQueuedOneGoesWithTheBuilding()
    {
        var world = Morning(["building Sawmill -1 0 2 1 0 0 0 0 0 0 0 0 0 0", "building Quarry -3 -1 1 1 0 0 0 0 0 0 0 0 0 0", "project Upgrade Quarry -3 -1 16000"]);
        var upgrade = world.Content.Upgrade.Cost;

        var upgraded = world.Demolition(L1)!;
        var queued = world.Demolition(M3)!;

        Assert.Equal(((15 + 0) / 2, (5 + upgrade[Resource.Stone]) / 2, upgrade[Resource.Planks] / 2), (upgraded.Refund(Resource.Timber), upgraded.Refund(Resource.Stone), upgraded.Refund(Resource.Planks)));
        Assert.False(upgraded.CancelsUpgrade);
        Assert.True(queued.CancelsUpgrade);
        Assert.Equal(((4 + upgrade[Resource.Planks]) / 2, upgrade[Resource.Stone] / 2), (queued.Refund(Resource.Planks), queued.Refund(Resource.Stone)));

        Assert.True(Apply(world, new Demolish(M3)).Accepted);
        Assert.DoesNotContain(world.Projects, p => p.Tile == M3);
    }

    // Acceptance: a storehouse whose island's goods would not fit in the store that is left cannot be demolished; nothing
    // changes, and the demolition says how much must be used or shipped first (INV-D7, INV-R1).
    [Fact]
    public void AStorehouseIsNotDemolishedWhileTheGoodsWouldNotFit()
    {
        var world = Morning(["building Storehouse 2 -1 1 1 0 0 0 1 4 0 0 0 0 0"], ("Fish", 200), ("Stone", 151));
        string before = world.Fingerprint();

        var demolition = world.Demolition(E1)!;
        var result = Apply(world, new Demolish(E1));

        Assert.Equal(Rejection.NoRoomInStore, demolition.Rejection);
        Assert.Equal((50, 1, 0), (demolition.TooMuch(Resource.Fish), demolition.TooMuch(Resource.Stone), demolition.TooMuch(Resource.Timber)));
        Assert.Equal(Rejection.NoRoomInStore, result.Rejection);
        Assert.Empty(result.Events);
        Assert.Equal(before, world.Fingerprint());
    }

    // Once the goods fit it can be demolished; its carriers build, and the buildings it served belong to the depot their
    // carriers now reach fastest.
    [Fact]
    public void ADemolishedStorehouseLeavesItsBuildingsToAnotherDepot()
    {
        var world = Morning(["building Storehouse 2 -1 1 1 0 0 0 1 4 0 0 0 0 0", "building Quarry 3 -1 1 1 0 0 0 0 0 0 0 0 0 0"], ("Fish", 150));
        Assert.Equal(E1, world.BuildingAt(M2)!.Depot);
        int free = world.FreeVillagers;

        Assert.True(Apply(world, new Demolish(E1)).Accepted);

        Assert.Equal(S, world.BuildingAt(M2)!.Depot);
        Assert.Equal(free + 1, world.FreeVillagers);
        Assert.Equal(150, world.Stock(Resource.Fish));
        Assert.Equal(Rejection.None, world.CheckRoad(E1));
    }

    // Everything is paid from the home island's store, so its last storehouse stands, even when empty.
    [Fact]
    public void TheHomeIslandsLastStorehouseCannotBeDemolished()
    {
        var world = Morning([]);
        string before = world.Fingerprint();

        Assert.Equal(Rejection.LastStorehouse, world.Demolition(S)!.Rejection);
        Assert.Equal(Rejection.LastStorehouse, Apply(world, new Demolish(S)).Rejection);
        Assert.Equal(before, world.Fingerprint());
    }

    // The goods waiting at a building go to the store with the refund, so the demolition destroys none (INV-R2); what has
    // grown on a field and not been harvested goes with it, and the demolition says so.
    [Fact]
    public void TheGoodsAtABuildingGoToTheStoreAndWhatHasGrownGoesWithIt()
    {
        var world = Morning(
            ["building Sawmill -1 0 1 1 0 0 0 0 0 0 2500000 0 0 0", "held Timber 3000000", "building Field -1 2 1 1 0 4200000 0 0 0 0 1000000 0 0 0"]);
        var stock = Stocks(world);

        var sawmill = world.Demolition(L1)!;
        var field = world.Demolition(O2)!;

        Assert.Equal((2, 3), (sawmill.Emptied(Resource.Planks), sawmill.Emptied(Resource.Timber)));
        Assert.Equal((1, 4), (field.Emptied(Resource.Grain), field.Unharvested));
        Assert.True(Apply(world, new Demolish(L1)).Accepted);
        Assert.True(Apply(world, new Demolish(O2)).Accepted);

        Assert.Equal((stock[Resource.Planks] * Quantity.PerUnit) + 2_500_000, world.Settlement.Stock(Resource.Planks));
        Assert.Equal(stock[Resource.Timber] + 3 + 7 + 2, world.Stock(Resource.Timber));
        Assert.Equal(stock[Resource.Grain] + 1, world.Stock(Resource.Grain));
    }

    // Housing can be demolished below the villagers: no one leaves, but the village grows only once there is room (D-39).
    [Fact]
    public void DemolishedHousingLeavesEveryVillagerButStopsTheGrowth()
    {
        var world = Village(At("2026-10-15 07:00", 2), 20, ["building Cottage -1 0 1 0 0 0 0 0 0 0 0 0 0 0"], ("Fish", 150));
        Assert.Equal((20, 20), (world.Villagers, world.Housing));

        Assert.True(Apply(world, new Demolish(L1)).Accepted);
        world.AdvanceTo(At("2026-10-17 08:00", 2));

        Assert.Equal((20, 16), (world.Villagers, world.Housing));
    }

    // Acceptance: while the villagers rest, demolition is refused and changes nothing (INV-T3, INV-D7).
    [Fact]
    public void DemolitionIsRefusedDuringTheRest()
    {
        var world = Village(At("2026-10-15 23:30", 2), 12, ["building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0"]);
        Assert.Equal(Phase.Resting, world.Phase);
        string before = world.Fingerprint();

        var result = Apply(world, new Demolish(L1));

        Assert.Equal(Rejection.Resting, result.Rejection);
        Assert.Empty(result.Events);
        Assert.Equal(before, world.Fingerprint());
    }

    [Fact]
    public void OnlyABuildingCanBeDemolished()
    {
        var world = Morning([]);
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, L1)).Accepted);
        string before = world.Fingerprint();

        Assert.Null(world.Demolition(L1));
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new Demolish(L1)).Rejection);
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new Demolish(L2)).Rejection);
        Assert.Equal(before, world.Fingerprint());
    }

    // The extra carriers a storehouse took at dawn build again once it is gone, and the confirmation counts them.
    [Fact]
    public void ADemolishedStorehouseLetsAllItsCarriersBuild()
    {
        var world = Morning(["building Storehouse 2 -1 1 1 0 0 0 3 4 0 0 0 0 0"]);
        int free = world.FreeVillagers;

        var demolition = world.Demolition(E1)!;
        Assert.True(Apply(world, new Demolish(E1)).Accepted);

        Assert.Equal(3, demolition.MoreBuilders);
        Assert.Equal(free + 3, world.FreeVillagers);
    }

    // The goods waiting at a building whose storehouse goes stay in its outbox for the carriers of the one it now belongs to.
    [Fact]
    public void TheGoodsWaitingForADemolishedStorehouseWaitForTheNextOne()
    {
        var world = Morning(["building Storehouse 2 -1 1 1 0 0 0 1 4 0 0 0 0 0", "building Quarry 3 -1 1 1 0 0 0 0 0 0 5000000 1 2 -1"]);
        long stone = world.Settlement.Stock(Resource.Stone);

        Assert.True(Apply(world, new Demolish(E1)).Accepted);

        var quarry = world.BuildingAt(M2)!;
        Assert.Equal((5_000_000L, (Hex?)S), (quarry.Waiting, quarry.Depot));
        Assert.Equal(stone + (2 * Quantity.PerUnit), world.Settlement.Stock(Resource.Stone));
    }

    // Freed villagers are given out again in the staffing order (economy-v1.md, section 4.1): a building passed over for
    // want of them may take them and more, so that one after it stands still; the confirmation says so beforehand, as the
    // demolition then does.
    [Fact]
    public void TheConfirmationSaysWhichBuildingsTheStaffingGivesAndTakes()
    {
        var world = Village(
            At("2026-10-15 07:00", 2),
            11,
            ["building Quarry -3 -1 1 1 0 0 0 0 0 0 0 0 0 0", "building Storehouse 2 -1 1 1 0 0 0 1 4 0 0 0 0 0",
                "building Sawmill -1 0 1 0 0 0 0 0 0 0 0 0 0 0", "building Jetty 1 1 1 1 0 0 0 1 4 0 0 0 0 0"]);
        Assert.Equal((false, true, 0), (world.BuildingAt(L1)!.Staffed, world.BuildingAt(H1)!.Staffed, world.FreeVillagers));

        var demolition = world.Demolition(E1)!;
        Assert.Equal([L1], demolition.Staffs.Select(b => b.Tile));
        Assert.Equal([H1], demolition.Unstaffs.Select(b => b.Tile));
        Assert.Equal(0, demolition.MoreBuilders);

        Assert.True(Apply(world, new Demolish(E1)).Accepted);
        Assert.Equal((true, false, 0), (world.BuildingAt(L1)!.Staffed, world.BuildingAt(H1)!.Staffed, world.FreeVillagers));
    }

    // The home island's last jetty closes every sea route and ends the expeditions, and the confirmation says so; the goods
    // on the other island stay there (INV-R2).
    [Fact]
    public void DemolishingTheHomeJettyClosesTheSeaRoutesAndSaysSo()
    {
        var world = OnNyon(fish: 40);
        Assert.Equal([PrototypeHome.Nyon], world.SeaRoutes.Select(r => r.From));

        var demolition = world.Demolition(H1)!;
        Assert.Equal([PrototypeHome.Nyon], demolition.ClosesSeaRoutes);
        Assert.True(demolition.EndsExpeditions);

        Assert.True(Apply(world, new Demolish(H1)).Accepted);
        Assert.Empty(world.SeaRoutes);
        Assert.Equal(40, world.Stock(PrototypeHome.Nyon, Resource.Fish));
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new SendExpedition()).Rejection);
    }

    // An island's last jetty leaves a store that holds nothing (INV-R1): with goods there it stands until they are shipped,
    // and once the store is empty it goes, and none of its materials fit to come back (D-128).
    [Fact]
    public void AnIslandsLastJettyGoesOnlyWithAnEmptyStoreAndGivesNothingBack()
    {
        var full = OnNyon(fish: 40);
        var empty = OnNyon(fish: 0);

        Assert.Equal((Rejection.NoRoomInStore, 40L), (full.Demolition(N0)!.Rejection, full.Demolition(N0)!.TooMuch(Resource.Fish)));
        var demolition = empty.Demolition(N0)!;
        Assert.Equal((Rejection.None, 7L, 0L), (demolition.Rejection, demolition.Lost(Resource.Timber), demolition.Returned(Resource.Timber)));
        Assert.Equal([PrototypeHome.Nyon], demolition.ClosesSeaRoutes);
        Assert.False(demolition.EndsExpeditions);
    }

    /// <summary>The start village with a jetty at home and one on Nyön, whose store holds <paramref name="fish"/>.</summary>
    private static World OnNyon(int fish) => Village(
        At("2026-10-15 07:00", 2),
        12,
        ["building Jetty 1 1 1 1 0 0 0 1 4 0 0 0 0 0", "building Jetty 10 0 1 1 0 0 0 1 4 0 0 0 0 0"],
        lines => lines.InsertRange(
            lines.FindLastIndex(l => l.StartsWith("stock ", StringComparison.Ordinal)) + 1,
            Enum.GetValues<Resource>().Select(r => $"stock {PrototypeHome.Nyon} {r} {(r == Resource.Fish ? fish * Quantity.PerUnit : 0)}")));

    private static Dictionary<Resource, long> Stocks(World world) => Enum.GetValues<Resource>().ToDictionary(r => r, world.Stock);

    private static World Morning(string[] buildings, params (string Resource, int Units)[] stock) => Village(At("2026-10-15 07:00", 2), 12, buildings, stock);

    /// <summary>
    /// The start village at <paramref name="at"/> with its villagers, <paramref name="buildings"/> (and projects) after its
    /// own and some stock set.
    /// </summary>
    private static World Village(TimeReading at, int villagers, string[] buildings, params (string Resource, int Units)[] stock) =>
        Village(at, villagers, buildings, lines =>
        {
            foreach (var (resource, units) in stock)
            {
                lines[lines.FindIndex(l => l.StartsWith($"stock home {resource} ", StringComparison.Ordinal))] = $"stock home {resource} {units * Quantity.PerUnit}";
            }
        });

    /// <summary>The start village with its villagers, <paramref name="buildings"/> after its own and the state edited.</summary>
    private static World Village(TimeReading at, int villagers, string[] buildings, Action<List<string>> edit)
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(at);
        var lines = world.Fingerprint().Split('\n').ToList();
        lines[lines.FindIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal))] = $"settlement {villagers} 0 0";
        edit(lines);
        lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, buildings);
        return World.Read(new StateReader(string.Join("\n", lines)), PrototypeHome.Map);
    }

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"demolition-{Interlocked.Increment(ref s_key)}", command);
}
