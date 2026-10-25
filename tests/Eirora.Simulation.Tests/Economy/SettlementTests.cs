using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// A settlement on each island (TASK-050, CIV-01, D-106, D-113; economy-v2.md, section 6): its own villagers, housing
/// and store, villagers who staff only its buildings and eat from its store, at most one villager moving over a sea route
/// at dawn in the whole world, free villagers who build on the islands a sea route joins to theirs, materials from home
/// on the sea route, and food out to the island before its goods go home.
/// </summary>
/// <remarks>
/// Most worlds are written as state text on the prototype's map, with Nyön known, so that each settlement has exactly
/// the villagers, buildings and goods the test needs: Hemön's twelve villagers, seven of them in its four buildings.
/// </remarks>
public sealed class SettlementTests
{
    private const long WorkPerVillagerPerQuarter = 250; // Thousandths of a villager-hour.
    private static readonly string s_homeJetty = $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0 1 4 0 0 0 0 0";
    private static int s_key;

    // Acceptance: a new settlement has its own housing and store, and its villagers staff only buildings on its island; the
    // first jetty founds it, and only the first.
    [Fact]
    public void ANewSettlementHasItsOwnHousingAndStoreAndItsVillagersStaffOnlyItsIsland()
    {
        var world = Village(null, buildings: [s_homeJetty]);
        Record(world, Habit.Workouts, 3);
        Assert.True(Apply(world, new Build(BuildingKind.Jetty, N0)).Accepted);
        int free = world.FreeVillagers;

        // No settlement yet on Nyön, so the home island's free villagers build its jetty, and the ships owe its materials.
        var founding = world.AdvanceTo(At("2026-10-15 16:00", 2));
        Assert.Equal(PrototypeHome.Nyon, Assert.Single(founding.OfType<SettlementFounded>()).Island);

        var nyon = world.SettlementOn(PrototypeHome.Nyon)!;
        Assert.Equal([PrototypeHome.Home, PrototypeHome.Nyon], world.Islands);
        Assert.Equal((0, IslandSettlement.SettlerHousing, 0), (nyon.Villagers, nyon.Housing, nyon.FreeVillagers));
        Assert.All(Enum.GetValues<Resource>(), r => Assert.Equal(0, world.Stock(PrototypeHome.Nyon, r)));
        Assert.False(world.BuildingAt(N0)!.Staffed, "Hemön's villagers staff Nyön's jetty.");
        Assert.Equal(free, world.FreeVillagers);
        Assert.Equal(16 + IslandSettlement.SettlerHousing, world.Housing);

        // At dawn a free villager moves from Hemön and staffs the jetty; Hemön's buildings keep their villagers.
        var events = world.AdvanceTo(At("2026-10-16 08:00", 2));

        var moved = Assert.Single(events.OfType<VillagerMoved>());
        Assert.Equal((PrototypeHome.Home, PrototypeHome.Nyon), (moved.From, moved.To));
        Assert.Equal((1, 0), (nyon.Villagers, nyon.FreeVillagers));
        Assert.True(world.BuildingAt(N0)!.Staffed);
        Assert.All(world.Buildings.Where(b => b.Island == PrototypeHome.Home && world.Content.For(b.Kind).Staff > 0), b => Assert.True(b.Staffed, $"{b.Kind} lost its villagers."));

        // A second jetty on the island founds nothing: the settlement is there already.
        Record(world, Habit.Workouts, 3);
        Assert.True(Apply(world, new Build(BuildingKind.Jetty, N9)).Accepted);
        var second = world.AdvanceTo(At("2026-10-17 20:00", 2));
        Assert.Contains(second.OfType<ProjectCompleted>(), done => done.Tile == N9);
        Assert.Empty(second.OfType<SettlementFounded>());
    }

    // Acceptance: with housing to spare and a sea route to a full settlement, at most one villager moves at dawn; it goes
    // where the buildings lack the most villagers.
    [Fact]
    public void AtMostOneVillagerMovesAtDawnInTheWholeWorldToTheSettlementLackingMost()
    {
        var map = Archipelago.Map;
        var world = Archipelago.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 09:00", 2));
        Hex Landing(string island) => map.Features.LandOf(island).First(h => map.Features.IsHarborSpot(h) && world.BuildingAt(h) is null);
        var wood = map.Features.LandOf(Archipelago.Second).First(h => (map.Features.UsesOf(h) & TileUse.Forest) != 0 && h != Landing(Archipelago.Second));
        world = Edited(
            world,
            settlements: [(Archipelago.First, 0), (Archipelago.Second, 0)],
            buildings:
            [
                Line(BuildingKind.Jetty, Landing(Archipelago.Home), staffed: true),
                Line(BuildingKind.Jetty, Landing(Archipelago.First)),
                Line(BuildingKind.Jetty, Landing(Archipelago.Second)),
                Line(BuildingKind.Woodcutter, wood),
            ]);

        // The second island lacks three villagers and the first one, so the one villager of the first dawn goes to the
        // second. At the next its villager has eaten and one more has moved in, and it still lacks two for its
        // woodcutter, so it gets the villager again; then only the first lacks any.
        var first = world.AdvanceTo(At("2026-10-16 09:00", 2)).OfType<VillagerMoved>().ToList();
        var second = world.AdvanceTo(At("2026-10-17 09:00", 2)).OfType<VillagerMoved>().ToList();
        var third = world.AdvanceTo(At("2026-10-18 09:00", 2)).OfType<VillagerMoved>().ToList();

        Assert.Equal((Archipelago.Home, Archipelago.Second), (Assert.Single(first).From, first[0].To));
        Assert.Equal(Archipelago.Second, Assert.Single(second).To);
        Assert.Equal(Archipelago.First, Assert.Single(third).To);
        Assert.Equal(0, world.Diagnose().SettlementOn(Archipelago.Second)!.MissingStaff);
    }

    // A villager moves the other way when the home island's buildings lack villagers and an island has free ones; Nyön is
    // full, so it does not grow.
    [Fact]
    public void AFreeVillagerMovesHomeWhenTheHomeIslandLacksVillagers()
    {
        var world = Village(
            IslandSettlement.SettlerHousing,
            buildings:
            [
                s_homeJetty, $"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0", "building Quarry -3 -1 1 0 0 0 0 0 0 0 0 0 0 0", "building Sawmill -1 0 1 0 0 0 0 0 0 0 0 0 0 0",
                "building Weavery -1 1 1 0 0 0 0 0 0 0 0 0 0 0", "building Field -1 2 1 0 0 0 0 0 0 0 0 0 0 0",
            ],
            stock: [(PrototypeHome.Nyon, Resource.Fish, 10)]);

        var moved = Assert.Single(world.AdvanceTo(At("2026-10-16 08:00", 2)).OfType<VillagerMoved>());

        Assert.Equal((PrototypeHome.Nyon, PrototypeHome.Home), (moved.From, moved.To));
        Assert.Equal(IslandSettlement.SettlerHousing - 1, world.SettlementOn(PrototypeHome.Nyon)!.Villagers);
        Assert.True(world.BuildingAt(N0)!.Staffed, "The villager who moved left Nyön's jetty unstaffed.");
    }

    // Acceptance: missing food in one settlement stops only that settlement's growth, and no villager disappears (D-39).
    [Fact]
    public void MissingFoodInOneSettlementStopsOnlyItsGrowth()
    {
        // Nyön has no food and no sea route, since Hemön has no jetty; Hemön has its fish.
        var world = Village(3, buildings: [$"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0"]);
        Assert.Empty(world.SeaRoutes);
        int home = world.SettlementOn(PrototypeHome.Home)!.Villagers;

        var events = world.AdvanceTo(At("2026-10-16 08:00", 2));

        var grew = Assert.Single(events.OfType<VillagersMovedIn>());
        Assert.Equal(PrototypeHome.Home, grew.Island);
        Assert.True(world.SettlementOn(PrototypeHome.Home)!.Villagers > home);
        Assert.Equal(3, world.SettlementOn(PrototypeHome.Nyon)!.Villagers);
        Assert.True(world.BuildingAt(N0)!.Staffed);
    }

    // Acceptance: when a settlement stands still, the diagnosis says why.
    [Fact]
    public void TheDiagnosisSaysWhyASettlementStandsStill()
    {
        var hungry = Village(3, buildings: [$"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0"]).Diagnose().SettlementOn(PrototypeHome.Nyon)!;
        Assert.Equal((Growth.Hungry, 0L, true), (hungry.Growth, hungry.Food, hungry.StandsStill));

        var full = Village(4, buildings: [$"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0"], stock: [(PrototypeHome.Nyon, Resource.Fish, 10)]).Diagnose().SettlementOn(PrototypeHome.Nyon)!;
        Assert.Equal((Growth.Full, 10L, true), (full.Growth, full.Food, full.StandsStill));

        // An empty settlement whose jetty lacks a villager: without a sea route no one can move there, with one Hemön's
        // free villagers can, one a dawn.
        var alone = Village(0, buildings: [$"building Jetty {N0.Q} {N0.R} 1 0 0 0 0 0 4 0 0 0 0 0"]).Diagnose().SettlementOn(PrototypeHome.Nyon)!;
        Assert.Equal((Growth.Empty, 1, 0, true), (alone.Growth, alone.MissingStaff, alone.FreeNearby, alone.StandsStill));
        var joined = Village(0, buildings: [s_homeJetty, $"building Jetty {N0.Q} {N0.R} 1 0 0 0 0 0 4 0 0 0 0 0"]);
        var waiting = joined.Diagnose().SettlementOn(PrototypeHome.Nyon)!;
        Assert.Equal((Growth.Empty, 1, joined.SettlementOn(PrototypeHome.Home)!.FreeVillagers, false), (waiting.Growth, waiting.MissingStaff, waiting.FreeNearby, waiting.StandsStill));

        var home = joined.Diagnose().SettlementOn(PrototypeHome.Home)!;
        Assert.Equal((Growth.Grows, false), (home.Growth, home.StandsStill));

        // Hungry and full at once, with a quarry that lacks two villagers and free ones at home: no one can move in, so
        // it stands still, and at dawn no one comes (review of #164).
        var crowded = Village(
            IslandSettlement.SettlerHousing,
            buildings: [s_homeJetty, $"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0", $"building Quarry {N2.Q} {N2.R} 1 1 0 0 0 0 0 0 0 0 0 0", $"building SheepFarm {N1.Q} {N1.R} 1 0 0 0 0 0 0 0 0 0 0 0"]);
        var still = crowded.Diagnose().SettlementOn(PrototypeHome.Nyon)!;
        Assert.Equal((Growth.Hungry, false, 1, true), (still.Growth, still.HasRoom, still.MissingStaff, still.StandsStill));
        Assert.True(still.FreeNearby > 0);
        Assert.DoesNotContain(crowded.AdvanceTo(At("2026-10-16 08:00", 2)).OfType<VillagerMoved>(), m => m.To == PrototypeHome.Nyon);
    }

    // Free villagers build on their own island and on the islands a sea route joins to theirs, but live and work at home
    // (D-113): Hemön's on Nyön, and Nyön's on Hemön.
    [Fact]
    public void FreeVillagersBuildOnTheIslandsASeaRouteJoinsToTheirs()
    {
        var world = Village(
            3,
            buildings: [s_homeJetty, $"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0"],
            stock: [(PrototypeHome.Nyon, Resource.Fish, 10), (PrototypeHome.Nyon, Resource.Stone, 10)]);
        int homeFree = world.SettlementOn(PrototypeHome.Home)!.FreeVillagers;
        Record(world, Habit.Workouts, 3);
        Assert.True(Apply(world, new Build(BuildingKind.Lookout, N1)).Accepted);
        long work = world.Projects[0].WorkLeft;

        world.AdvanceTo(At("2026-10-15 08:00", 2));

        Assert.Equal(work - (4 * (homeFree + 2) * WorkPerVillagerPerQuarter), world.Projects[0].WorkLeft);
        Assert.Equal((3, 2), (world.SettlementOn(PrototypeHome.Nyon)!.Villagers, world.SettlementOn(PrototypeHome.Nyon)!.FreeVillagers));
    }

    // Building on an island is paid from its own store when that holds it all, otherwise from the home island's, and
    // those materials take room on the sea route before anything else (D-106).
    [Fact]
    public void BuildingIsPaidFromTheIslandsStoreOrFromHomeWhoseMaterialsSailFirst()
    {
        var stocked = Village(1, buildings: [s_homeJetty, $"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0"], stock: [(PrototypeHome.Nyon, Resource.Stone, 10), (PrototypeHome.Nyon, Resource.Fish, 2)]);
        long homeStone = stocked.Settlement.Stock(Resource.Stone);
        Record(stocked, Habit.Workouts, 3);
        Assert.True(Apply(stocked, new Build(BuildingKind.Lookout, N1)).Accepted);
        Assert.Equal((0L, homeStone, 0L), (stocked.Stock(PrototypeHome.Nyon, Resource.Stone), stocked.Settlement.Stock(Resource.Stone), stocked.Settlement.Owed(PrototypeHome.Nyon)));

        // Nyön has 9 stone, so the home island pays the 10 and the ships carry them before Nyön's own stone.
        var bare = Village(1, buildings: [s_homeJetty, $"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0"], stock: [(PrototypeHome.Nyon, Resource.Stone, 9), (PrototypeHome.Nyon, Resource.Fish, 2)]);
        Record(bare, Habit.Workouts, 3);
        Assert.True(Apply(bare, new Build(BuildingKind.Lookout, N1)).Accepted);
        Assert.Equal((9L, homeStone - Quantity.Units(10), Quantity.Units(10)), (bare.Stock(PrototypeHome.Nyon, Resource.Stone), bare.Settlement.Stock(Resource.Stone), bare.Settlement.Owed(PrototypeHome.Nyon)));
        Assert.Equal(10, bare.Diagnose().SeaRouteFrom(PrototypeHome.Nyon)!.MaterialsOwed);

        // 2.5 units an hour: four hours pay the 10, and then the stone sails.
        bare.AdvanceTo(At("2026-10-15 11:00", 2));
        Assert.Equal((0L, 9L), (bare.Settlement.Owed(PrototypeHome.Nyon), bare.Stock(PrototypeHome.Nyon, Resource.Stone)));
        bare.AdvanceTo(At("2026-10-15 12:00", 2));
        Assert.Equal(Quantity.Units(9) - (Quantity.Units(5) / 2), bare.Settlement.Stock(PrototypeHome.Nyon, Resource.Stone));
    }

    // The ships carry out food for two days for the island's villagers before they carry its goods home, and the island
    // keeps as much (economy-v2.md, section 6).
    [Fact]
    public void TheShipsKeepTwoDaysOfFoodOnTheIslandBeforeTheyCarryItsGoodsHome()
    {
        var world = Village(3, buildings: [s_homeJetty, $"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0"], stock: [(PrototypeHome.Nyon, Resource.Grain, 10)]);
        Assert.Equal((Resource.Grain, 4L), Assert.Single(world.Diagnose().SeaRouteFrom(PrototypeHome.Nyon)!.Waiting));

        world.AdvanceTo(At("2026-10-15 12:00", 2));

        // The grain above two days' food for the three villagers goes home, and no fish comes out.
        Assert.Equal((Quantity.Units(6), 0L), (world.Settlement.Stock(PrototypeHome.Nyon, Resource.Grain), world.Settlement.Stock(PrototypeHome.Nyon, Resource.Fish)));

        // Two days' food in all, fish first as the villagers eat it, not two days of each (review of #164).
        var both = Village(3, buildings: [s_homeJetty, $"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0"], stock: [(PrototypeHome.Nyon, Resource.Fish, 6), (PrototypeHome.Nyon, Resource.Grain, 6)]);
        Assert.Equal(6, both.ForTheShips(PrototypeHome.Nyon, Resource.Grain));
        both.AdvanceTo(At("2026-10-15 12:00", 2));
        Assert.Equal((Quantity.Units(6), 0L), (both.Settlement.Stock(PrototypeHome.Nyon, Resource.Fish), both.Settlement.Stock(PrototypeHome.Nyon, Resource.Grain)));

        var hungry = Village(3, buildings: [s_homeJetty, $"building Jetty {N0.Q} {N0.R} 1 1 0 0 0 1 4 0 0 0 0 0"]);
        Assert.Equal(2_500_000L, hungry.Diagnose().SeaRouteFrom(PrototypeHome.Nyon)!.SuppliesPerHour);
        hungry.AdvanceTo(At("2026-10-15 12:00", 2));
        Assert.Equal(Quantity.Units(6), hungry.Settlement.Stock(PrototypeHome.Nyon, Resource.Fish));
    }

    /// <summary>
    /// The prototype's world at seven in the morning with Nyön known, its settlement with <paramref name="nyon"/>
    /// villagers (none when null), the <paramref name="buildings"/> after the founding's, and the stock given in whole
    /// units; read back as a save would be.
    /// </summary>
    private static World Village(int? nyon, string[] buildings, (string Island, Resource Resource, int Units)[]? stock = null)
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 07:00", 2));
        world.Settlement.Fog.LiftIsland(PrototypeHome.Nyon);
        return Edited(world, nyon is int villagers ? [(PrototypeHome.Nyon, villagers)] : [], buildings, stock ?? []);
    }

    // Each island's depots hire their carriers from its own free villagers (D-106): with the same buildings to carry for,
    // the island without free villagers keeps one carrier at its jetty, and the island with free villagers hires more.
    [Fact]
    public void EachIslandsDepotsHireCarriersOnlyFromItsOwnFreeVillagers()
    {
        var map = Archipelago.Map;
        var world = Archipelago.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 09:00", 2));
        int fisherStaff = world.Content.For(BuildingKind.FishingHut).Staff;
        Hex Landing(string island) => map.Features.LandOf(island).First(h => map.Features.IsHarborSpot(h) && world.BuildingAt(h) is null);
        IEnumerable<string> Island(string island)
        {
            var jetty = Landing(island);
            var waters = Enumerable.Range(0, map.Disc.Count).Select(i => map.Disc[i])
                .Where(h => (map.Features.UsesOf(h) & TileUse.FishingWater) != 0 && world.IslandOf(h) == island && h.DistanceTo(jetty) >= 3)
                .OrderBy(h => h.DistanceTo(jetty)).ThenBy(h => h.Q).ThenBy(h => h.R).Take(3);
            return [Line(BuildingKind.Jetty, jetty, staffed: true), .. waters.Select(h => Line(BuildingKind.FishingHut, h, staffed: true))];
        }

        int staff = 1 + (3 * fisherStaff);
        world = Edited(
            world,
            settlements: [(Archipelago.First, staff), (Archipelago.Second, staff + 3)],
            buildings: [Line(BuildingKind.Jetty, Landing(Archipelago.Home), staffed: true), .. Island(Archipelago.First), .. Island(Archipelago.Second)],
            stock: [(Archipelago.First, Resource.Fish, 40), (Archipelago.Second, Resource.Fish, 40)]);

        world.AdvanceTo(At("2026-10-16 10:00", 2));

        int Carriers(string island) => world.Buildings.Single(b => b.Kind == BuildingKind.Jetty && b.Island == island).Carriers;
        Assert.Equal(0, world.SettlementOn(Archipelago.First)!.FreeVillagers);
        Assert.Equal(1, Carriers(Archipelago.First));
        Assert.True(Carriers(Archipelago.Second) > 1, $"The second island's jetty has {Carriers(Archipelago.Second)} carriers.");
        Assert.Equal(3 - (Carriers(Archipelago.Second) - 1), world.SettlementOn(Archipelago.Second)!.FreeVillagers);
        Assert.All(world.Settlements, s => Assert.Equal(
            s.Villagers - world.Buildings.Where(b => b.Island == s.Island).Sum(b => Logistics.IsDepot(b.Kind) ? b.Carriers : b.Staffed ? world.Content.For(b.Kind).Staff : 0),
            s.FreeVillagers));
    }

    /// <summary>
    /// The world with settlements founded on islands with the villagers given, each with an empty store, buildings added at
    /// the end of the staffing order and stock set in whole units, read back as a save would be.
    /// </summary>
    private static World Edited(World world, (string Island, int Villagers)[] settlements, string[] buildings, (string Island, Resource Resource, int Units)[]? stock = null)
    {
        var lines = world.Fingerprint().TrimEnd('\n').Split('\n').ToList();
        foreach (var (island, villagers) in settlements)
        {
            lines.Insert(lines.FindLastIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal)) + 1, $"settlement {island} {villagers}");
            lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("stock ", StringComparison.Ordinal)) + 1, Enum.GetValues<Resource>().Select(r => $"stock {island} {r} 0"));
        }

        foreach (var (island, resource, units) in stock ?? [])
        {
            lines[lines.FindIndex(l => l.StartsWith($"stock {island} {resource} ", StringComparison.Ordinal))] = $"stock {island} {resource} {Quantity.Units(units)}";
        }

        lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, buildings);
        var reader = new StateReader(string.Join('\n', lines) + "\n");
        var read = World.Read(reader, world.Map);
        reader.End();
        return read;
    }

    /// <summary>A building as the state writes it, a storehouse or a jetty with its one carrier when staffed, no other with a depot yet.</summary>
    private static string Line(BuildingKind kind, Hex tile, bool staffed = false) => Logistics.IsDepot(kind)
        ? $"building {kind} {tile.Q} {tile.R} 1 {(staffed ? 1 : 0)} 0 0 0 {(staffed ? 1 : 0)} 4 0 0 0 0 0"
        : $"building {kind} {tile.Q} {tile.R} 1 {(staffed ? 1 : 0)} 0 0 0 0 0 0 0 0 0 0";

    private static void Record(World world, Habit habit, int total) =>
        Assert.True(Apply(world, new RecordSelfCare(world.GameDay, habit, ContributionSource.Verified, total)).Accepted);

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"settlement-{Interlocked.Increment(ref s_key)}", command);
}
