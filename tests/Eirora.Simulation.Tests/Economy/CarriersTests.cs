using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// The carriers and the roads on land (economy-v2.md, section 7; TASK-051, D-107, D-109–D-112): what a storehouse's carriers
/// bring to the store in an active hour, how a road speeds them, how the storehouse hires them at dawn, and how roads are
/// laid tile by tile. Nothing carried is lost or made (INV-R2), and the carriers rest with the villagers (INV-T4).
/// </summary>
/// <remarks>
/// The worlds are written as state text, so that the outboxes and the staffing are what each test needs. The three far
/// fishing huts stand four steps over land from the start village's storehouse, whose one carrier fetches two fish an hour
/// from each of them; paused, they make nothing, so all that moves is what the carrier carries.
/// </remarks>
public sealed class CarriersTests
{
    private const long Unit = Quantity.PerUnit;
    private static int s_key;

    // Acceptance: carriers that cannot keep up bring at most what they can carry, and the rest waits at the buildings.
    [Fact]
    public void AnActiveHourBringsTheStoreAtMostWhatItsCarriersCarryAndTheRestWaits()
    {
        var (world, far) = PausedFarHuts(waiting: [8, 8, 8]);
        long fish = world.Settlement.Stock(Resource.Fish);

        world.AdvanceTo(At("2026-10-15 10:00", 2));

        // An hour of one carrier on a trip of four steps there and back is two fish, and nothing more reaches the store.
        Assert.Equal(1, world.BuildingAt(S)!.Carriers);
        Assert.Equal(2 * Unit, world.Settlement.Stock(Resource.Fish) - fish);
        Assert.Equal(22 * Unit, far.Sum(h => world.BuildingAt(h)!.Waiting));

        // The fullest outbox goes first, so the carrier's quarter hours go round the huts (D-111).
        Assert.Equal([7 * Unit, 7_500_000, 7_500_000], far.Select(h => world.BuildingAt(h)!.Waiting));
    }

    // Acceptance: a road between the buildings and the storehouse lets the carriers carry more an hour (economy-v2.md, section 7).
    [Theory]
    [InlineData(1, 8_000_000, 8_000_000)]
    [InlineData(2, 4_000_000, 5_333_333)]
    [InlineData(3, 2_666_666, 4_000_000)]
    [InlineData(4, 2_000_000, 3_200_000)]
    [InlineData(6, 1_333_333, 2_285_714)]
    public void ARoadOnEveryTileBetweenLetsACarrierCarryWhatEconomyV2Says(int steps, long without, long with)
    {
        var walked = new Trip(steps * Logistics.StepTime, S, steps);
        var road = new Trip(((steps - 1) * Logistics.RoadStepTime) + Logistics.StepTime, S, steps);

        Assert.Equal((without, with), (walked.PerCarrierHour, road.PerCarrierHour));
    }

    [Fact]
    public void ARoadToAFarHutBringsMoreOfItsFishToTheStoreInAnHour()
    {
        var (walked, far) = PausedFarHuts(waiting: [8, 0, 0]);
        var road = walked.RoadToDepot(far[0]);
        Assert.Equal(3, road.Count);
        var (paved, _) = PausedFarHuts(waiting: [8, 0, 0], roads: road);
        Assert.Equal((2 * Logistics.RoadStepTime * 3 / 2) + Logistics.StepTime, paved.TripFrom(far[0]).Time);

        walked.AdvanceTo(At("2026-10-15 10:00", 2));
        paved.AdvanceTo(At("2026-10-15 10:00", 2));

        Assert.Equal(6 * Unit, walked.BuildingAt(far[0])!.Waiting);
        Assert.Equal(8 * Unit - 3_200_000, paved.BuildingAt(far[0])!.Waiting);
    }

    [Fact]
    public void AWorkdayOnTheStorehouseLetsItsCarriersCarryHalfAsMuchAgain()
    {
        var (world, far) = PausedFarHuts(waiting: [8, 8, 8]);
        long fish = world.Settlement.Stock(Resource.Fish);
        int dawnPower = world.Available(Force.DawnPower);

        Assert.True(Apply(world, new Workday(S)).Accepted);
        world.AdvanceTo(At("2026-10-15 10:00", 2));

        Assert.Equal(dawnPower - Workday.Cost, world.Available(Force.DawnPower));
        Assert.Equal(3 * Unit, world.Settlement.Stock(Resource.Fish) - fish);
        Assert.Equal(21 * Unit, far.Sum(h => world.BuildingAt(h)!.Waiting));
    }

    [Fact]
    public void TheCarriersDoNotCarryWhileTheVillagersRest()
    {
        var (world, far) = PausedFarHuts(waiting: [8, 8, 8]);
        world.AdvanceTo(At("2026-10-15 23:00", 2));
        string atDusk = Where(world);

        world.AdvanceTo(At("2026-10-16 06:59", 2));

        Assert.Equal(atDusk, Where(world));
        static string Where(World w) => string.Join(' ', w.Buildings.Select(b => b.Waiting).Append(w.Settlement.Stock(Resource.Fish)));
    }

    [Fact]
    public void ThePlayersPriorityGoesBeforeWhereMostWaits()
    {
        var (world, far) = PausedFarHuts(waiting: [8, 8, 2]);
        Assert.True(Apply(world, new Prioritize(far[2], 0)).Accepted);
        Assert.True(world.BuildingAt(far[2])!.CarriedFirst);

        world.AdvanceTo(At("2026-10-15 10:00", 2));

        Assert.Equal([8 * Unit, 8 * Unit, 0], far.Select(h => world.BuildingAt(h)!.Waiting));

        // Moved down again, it waits its turn where most waits.
        Assert.True(Apply(world, new Prioritize(far[2], world.StaffingOrder.Count - 1)).Accepted);
        Assert.False(world.BuildingAt(far[2])!.CarriedFirst);
    }

    // Review of #166: the priority is an explicit mode the player sets by moving a building up (D-111), not its place.
    [Fact]
    public void ABuildingMovedUpStaysFirstWhenOthersAreMovedAboveItOrItIsLeftInItsPlace()
    {
        var (world, far) = PausedFarHuts(waiting: [8, 8, 2]);
        Assert.True(Apply(world, new Prioritize(far[2], 1)).Accepted);
        Assert.True(world.BuildingAt(far[2])!.CarriedFirst);

        Assert.True(Apply(world, new Prioritize(far[0], 0)).Accepted);
        var again = Apply(world, new Prioritize(far[2], world.StaffingOrder.ToList().IndexOf(world.BuildingAt(far[2])!)));

        Assert.Equal(Rejection.AlreadyDone, again.Rejection);
        Assert.Equal([true, false, true], far.Select(h => world.BuildingAt(h)!.CarriedFirst));
    }

    [Fact]
    public void AStorehouseMovedUpIsNoBuildingTheCarriersServeFirst()
    {
        var world = Morning();
        Assert.True(Apply(world, new Prioritize(S, world.StaffingOrder.Count - 1)).Accepted);
        Assert.True(Apply(world, new Prioritize(S, 0)).Accepted);
        Assert.False(world.BuildingAt(S)!.CarriedFirst);
    }

    [Fact]
    public void TheCarriersBringRawMaterialsToTheProcessingAndKeepTheReserveInTheStore()
    {
        // 22 timber: two above the reserve of 20, which the carriers bring to the sawmill beside the storehouse first,
        // since it has least of its raw material left. The woodcutter rests, so no timber comes in.
        var world = Edited(Morning(), "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0 20", stock: (Resource.Timber, 22));
        Assert.True(Apply(world, new PauseBuilding(F1)).Accepted);

        world.AdvanceTo(At("2026-10-15 09:15", 2));
        var sawmill = world.BuildingAt(L1)!;
        Assert.Equal(2, sawmill.HeldUnits(Resource.Timber));
        Assert.Equal(20 * Unit, world.Settlement.Stock(Resource.Timber));

        // A quarter hour later it saws a quarter plank of the timber it holds, and the reserve stays in the store.
        long planks = world.Settlement.Stock(Resource.Planks);
        world.AdvanceTo(At("2026-10-15 09:30", 2));
        Assert.Equal(250_000, sawmill.Waiting + world.Settlement.Stock(Resource.Planks) - planks);
        Assert.Equal(1_500_000, sawmill.Held![(int)Resource.Timber]);
        Assert.Equal(20 * Unit, world.Settlement.Stock(Resource.Timber));
    }

    [Fact]
    public void AtDawnTheStorehouseTakesTheCarriersItsGoodsNeedFromTheFreeVillagers()
    {
        var world = FarHuts(running: true);
        var storehouse = world.BuildingAt(S)!;
        long need = world.Settlement.NeedAt(storehouse, world.ActiveTicks);
        Assert.InRange(need, 2 * Unit + 1, 3 * Unit);
        int free = world.FreeVillagers;

        world.AdvanceTo(At("2026-10-16 07:15", 2));

        // The village is full, and the storehouse took two more carriers of the free villagers.
        Assert.Equal(3, storehouse.Carriers);
        Assert.Equal(free - 2, world.FreeVillagers);
        Assert.All(world.Buildings.Where(b => world.Content.For(b.Kind).Staff > 0 && !b.Paused), b => Assert.True(b.Staffed));
    }

    [Fact]
    public void ThePlayersCapLimitsTheCarriersAndLoweringItLetsThemBuildAtOnce()
    {
        var world = FarHuts(running: true);
        world.AdvanceTo(At("2026-10-16 07:15", 2));
        int free = world.FreeVillagers;

        Assert.True(Apply(world, new SetCarrierCap(S, 1)).Accepted);
        Assert.Equal((1, 1, free + 2), (world.BuildingAt(S)!.CarrierCap, world.BuildingAt(S)!.Carriers, world.FreeVillagers));

        // A higher cap is taken up at the next dawn.
        Assert.True(Apply(world, new SetCarrierCap(S, 4)).Accepted);
        Assert.Equal(1, world.BuildingAt(S)!.Carriers);
        world.AdvanceTo(At("2026-10-17 07:15", 2));
        Assert.Equal(3, world.BuildingAt(S)!.Carriers);

        Assert.Equal(Rejection.AmountOutOfRange, Apply(world, new SetCarrierCap(S, 5)).Rejection);
        Assert.Equal(Rejection.AmountOutOfRange, Apply(world, new SetCarrierCap(S, 0)).Rejection);
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new SetCarrierCap(S, 4)).Rejection);
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new SetCarrierCap(K1, 2)).Rejection);
    }

    [Fact]
    public void ADepotNeverTakesAVillagerFromABuildingAndANewBuildingTakesOnlyFreeVillagers()
    {
        var hired = FarHuts(running: true);
        hired.AdvanceTo(At("2026-10-16 07:15", 2));
        Assert.Equal(3, hired.BuildingAt(S)!.Carriers);

        // No villager is free, and a paused quarry is resumed: the carriers stay, and so does everyone at work.
        var world = Edited(hired, "building Quarry -3 -1 1 0 0 0 1 0 0 0 0 0 0 0 0", villagers: hired.Villagers - hired.FreeVillagers);
        Assert.Equal(0, world.FreeVillagers);
        var staffed = world.Buildings.Where(b => b.Staffed).Select(b => b.Tile).ToList();

        Assert.True(Apply(world, new ResumeBuilding(M3)).Accepted);

        Assert.False(world.BuildingAt(M3)!.Staffed);
        Assert.Equal(3, world.BuildingAt(S)!.Carriers);
        Assert.All(staffed, t => Assert.True(world.BuildingAt(t)!.Staffed));

        // Lowering the cap gives the quarry its villagers.
        Assert.True(Apply(world, new SetCarrierCap(S, 1)).Accepted);
        Assert.True(Apply(world, new PauseBuilding(M3)).Accepted);
        Assert.True(Apply(world, new ResumeBuilding(M3)).Accepted);
        Assert.True(world.BuildingAt(M3)!.Staffed);
    }

    // Review of #166: a storehouse staffed in the middle of the day takes over no building before dawn, so its one carrier
    // never lessens what reaches the store that day (D-39); at dawn it takes over the buildings it reaches fastest.
    [Theory]
    [InlineData(0, -1)]
    [InlineData(2, -2)]
    [InlineData(1, -2)]
    public void ANewStorehouseTakesOverBuildingsOnlyAtDawnSoItNeverLessensWhatReachesTheStoreThatDay(int q, int r)
    {
        var hired = FarHuts(running: true);
        hired.AdvanceTo(At("2026-10-16 07:15", 2));
        Assert.Equal(3, hired.BuildingAt(S)!.Carriers);
        var site = new Hex(q, r);
        var with = Edited(hired, $"building Storehouse {q} {r} 1 1 0 0 0 1 4 0 0 0 0 0 0", villagers: hired.Villagers + 1);
        var without = Edited(hired, null, villagers: hired.Villagers + 1);

        with.AdvanceTo(At("2026-10-16 13:15", 2));
        without.AdvanceTo(At("2026-10-16 13:15", 2));

        Assert.InRange(with.Settlement.Stock(Resource.Fish), without.Settlement.Stock(Resource.Fish), long.MaxValue);
        Assert.All(FarWaters(), h => Assert.Equal(S, with.TripFrom(h).Depot));

        // At dawn the huts beside it move over, and the old storehouse's carriers go to it: nothing less reaches the store.
        with.AdvanceTo(At("2026-10-17 07:15", 2));
        without.AdvanceTo(At("2026-10-17 07:15", 2));
        Assert.Contains(FarWaters(), h => with.TripFrom(h).Depot == site);
        long dawnWith = with.Settlement.Stock(Resource.Fish);
        long dawnWithout = without.Settlement.Stock(Resource.Fish);

        with.AdvanceTo(At("2026-10-17 19:00", 2));
        without.AdvanceTo(At("2026-10-17 19:00", 2));
        Assert.InRange(with.Settlement.Stock(Resource.Fish) - dawnWith, without.Settlement.Stock(Resource.Fish) - dawnWithout, long.MaxValue);
    }

    [Fact]
    public void ARoadFinishedInTheDayLeavesEveryBuildingWithItsDepotUntilDawn()
    {
        var hired = FarHuts(running: true);
        hired.AdvanceTo(At("2026-10-16 07:15", 2));
        var depots = Edited(hired, "building Storehouse 2 -2 1 1 0 0 0 1 4 0 0 0 0 0 0", villagers: hired.Villagers + 1);
        depots.AdvanceTo(At("2026-10-17 07:15", 2));
        var before = depots.Buildings.Where(b => b.Kind == BuildingKind.FishingHut).ToDictionary(b => b.Tile, b => depots.TripFrom(b.Tile).Depot);

        // Roads on every free tile of the island, finished at once: the ways get faster, but no hut changes depot.
        var paved = Edited(depots, null, roads: Hex.Origin.Within(6).Where(t => depots.CheckRoad(t) == Rejection.None));

        Assert.All(before, hut => Assert.Equal(hut.Value, paved.TripFrom(hut.Key).Depot));
    }

    // Review of #166: the carriers a depot lets go at dawn staff the buildings that wait for villagers that same dawn.
    [Fact]
    public void TheCarriersLetGoAtDawnStaffTheBuildingsThatWaitForVillagers()
    {
        var hired = FarHuts(running: true);
        hired.AdvanceTo(At("2026-10-16 07:15", 2));
        Assert.Equal(3, hired.BuildingAt(S)!.Carriers);
        foreach (var hut in FarWaters())
        {
            Assert.True(Apply(hired, new PauseBuilding(hut)).Accepted);
        }

        // The huts rest, so the storehouse needs one carrier tomorrow; a woodcutter waits for two villagers, and none is free.
        var world = Edited(hired, "building Woodcutter 3 -2 1 0 0 0 0 0 0 0 0 0 0 0 0", villagers: hired.Villagers - hired.FreeVillagers);
        Assert.Equal((0, 3, false), (world.FreeVillagers, world.BuildingAt(S)!.Carriers, world.BuildingAt(F2)!.Staffed));

        world.AdvanceTo(At("2026-10-17 07:15", 2));

        Assert.True(world.BuildingAt(F2)!.Staffed, $"{world.FreeVillagers} villagers are free and {world.BuildingAt(S)!.Carriers} carry.");
        Assert.Equal(1, world.BuildingAt(S)!.Carriers);
    }

    [Fact]
    public void ANewStorehouseWithoutCarriersTakesOverNoBuildings()
    {
        // Every villager is at work, so a storehouse beside the far huts has no one to carry for it (D-39).
        var (world, far) = PausedFarHuts(waiting: [8, 8, 8]);
        var site = Enumerable.Range(0, Hex.Directions).Select(far[0].Neighbour).First(h => world.CheckBuild(BuildingKind.Storehouse, h) == Rejection.None);
        var crowded = Edited(world, $"building Storehouse {site.Q} {site.R} 1 0 0 0 0 0 4 0 0 0 0 0 0", villagers: world.Villagers - world.FreeVillagers);

        Assert.Equal(S, crowded.TripFrom(far[0]).Depot);
        Assert.Equal(world.TripFrom(far[0]).Time, crowded.TripFrom(far[0]).Time);

        // With a villager to carry it takes over the huts beside it at dawn.
        var staffed = Edited(world, $"building Storehouse {site.Q} {site.R} 1 1 0 0 0 1 4 0 0 0 0 0 0");
        Assert.Equal(S, staffed.TripFrom(far[0]).Depot);
        staffed.AdvanceTo(At("2026-10-17 07:15", 2));
        Assert.Equal(site, staffed.TripFrom(far[0]).Depot);
    }

    [Fact]
    public void TheCarriersWayLeadsStepByStepToTheirDepotAndTakesTheTripsTime()
    {
        var (world, far) = PausedFarHuts(waiting: [0, 0, 0], roads: [L1]);

        foreach (var hut in far.Append(F1))
        {
            var way = world.WayToDepot(hut);
            var trip = world.TripFrom(hut);
            Assert.Equal((trip.Steps, S), (way.Count, way[^1]));
            Assert.All(way.Zip(way.Skip(1)), step => Assert.Equal(1, step.First.DistanceTo(step.Second)));
            Assert.Equal(1, hut.DistanceTo(way[0]));
            Assert.Equal(trip.Time, way.Sum(t => world.HasRoad(t) ? Logistics.RoadStepTime : Logistics.StepTime));
        }

        Assert.Empty(world.WayToDepot(N1));
    }

    [Fact]
    public void ARoadIsLaidTileByTileForTwoStoneAndFourHoursOfWork()
    {
        var world = Morning();
        var tile = L1;
        Assert.Equal(Rejection.None, world.CheckRoad(tile));
        long stone = world.Settlement.Stock(Resource.Stone);

        Assert.True(Apply(world, new LayRoad(tile)).Accepted);

        Assert.Equal(stone - (2 * Unit), world.Settlement.Stock(Resource.Stone));
        var project = Assert.Single(world.Projects);
        Assert.Equal((ProjectKind.Road, (BuildingKind?)null, tile, 4000L), (project.Kind, project.Building, project.Tile, project.WorkLeft));
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new LayRoad(tile)).Rejection);
        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new LayRoad(S)).Rejection); // A building stands there.
        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new LayRoad(K1)).Rejection); // Water.
        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new LayRoad(N1)).Rejection); // Nyön, under the fog.
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, L2)).Accepted);
        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new LayRoad(L2)).Rejection); // A building in the queue.

        world.AdvanceTo(At("2026-10-15 11:00", 2));
        Assert.Equal([tile], world.Roads);

        // A building on the road makes the road its yard, and the road stays.
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, tile)).Accepted);
        world.AdvanceTo(At("2026-10-16 12:00", 2));
        Assert.Equal(BuildingKind.Cottage, world.BuildingAt(tile)!.Kind);
        Assert.True(world.HasRoad(tile));
    }

    [Fact]
    public void RoadToTheStorehouseLaysEveryTileOfTheWayNearestTheStorehouseFirst()
    {
        var (world, far) = PausedFarHuts(waiting: [0, 0, 0]);
        var tiles = world.RoadToDepot(far[0]);
        long stone = world.Settlement.Stock(Resource.Stone);

        Assert.True(Apply(world, new BuildRoad(far[0])).Accepted);

        Assert.Equal(tiles, world.Projects.Select(p => p.Tile));
        Assert.All(world.Projects, p => Assert.Equal((ProjectKind.Road, 4000L), (p.Kind, p.WorkLeft)));
        Assert.Equal(stone - (2 * Unit * tiles.Count), world.Settlement.Stock(Resource.Stone));
        Assert.Equal(1, tiles[0].DistanceTo(S));
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new BuildRoad(far[0])).Rejection);
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new BuildRoad(S)).Rejection);
    }

    [Fact]
    public void RoadToTheStorehouseNeedsStoneForEveryTile()
    {
        var (world, far) = PausedFarHuts(waiting: [0, 0, 0]);
        int tiles = world.RoadToDepot(far[0]).Count;
        var poor = Edited(world, null, stock: (Resource.Stone, (2 * tiles) - 1));

        Assert.Equal(Rejection.InsufficientResources, Apply(poor, new BuildRoad(far[0])).Rejection);
        Assert.Empty(poor.Projects);
    }

    [Fact]
    public void RoadsCarriersAndWhatWaitsAreSavedAndReadBack()
    {
        var (world, far) = PausedFarHuts(waiting: [8, 3, 0], roads: [L1]);
        Assert.True(Apply(world, new Prioritize(far[1], 0)).Accepted);
        Assert.True(Apply(world, new SetCarrierCap(S, 2)).Accepted);
        Assert.True(Apply(world, new LayRoad(L2)).Accepted);
        var sawmilled = Edited(world, "building Sawmill -1 1 1 1 0 0 0 0 0 0 1500000 0 0 0 20\nheld Timber 2500000");

        var copy = SimulationSnapshot.FromBytes(SimulationSnapshot.Take(sawmilled).ToBytes()).Restore(sawmilled.Map);

        Assert.Equal(sawmilled.Fingerprint(), copy.Fingerprint());
        Assert.Equal([L1], copy.Roads);
        Assert.Equal((2, true), (copy.BuildingAt(S)!.CarrierCap, copy.BuildingAt(far[1])!.CarriedFirst));
        Assert.Equal((2, 1L), (copy.BuildingAt(L3)!.HeldUnits(Resource.Timber), copy.BuildingAt(L3)!.WaitingUnits));
        Assert.Equal((ProjectKind.Road, (BuildingKind?)null), (copy.Projects[^1].Kind, copy.Projects[^1].Building));
    }

    [Theory]
    [InlineData("building FishingHut 1 0 1 1 0 0 0 0 0 0 9000000 0 0 0 0")] // More than the outbox holds.
    [InlineData("building FishingHut 1 0 1 1 0 0 0 1 4 0 0 0 0 0 0")] // Carriers at a fishing hut.
    [InlineData("building Storehouse 0 0 1 1 0 0 0 5 4 0 0 0 0 0 0")] // More carriers than four.
    [InlineData("building Storehouse 0 0 1 0 0 0 0 1 4 0 0 0 0 0 0")] // Carriers at a storehouse nobody staffs.
    [InlineData("held Wool 1000000")] // Wool at the storehouse.
    public void AStateThatBreaksTheCarriersRulesIsRefused(string line)
    {
        var lines = Morning().Fingerprint().Split('\n').ToList();
        string kind = line.Split(' ')[0] == "held" ? "Storehouse" : line.Split(' ')[1];
        int at = lines.FindIndex(l => l.StartsWith($"building {kind} ", StringComparison.Ordinal));
        if (line.StartsWith("held ", StringComparison.Ordinal))
        {
            lines.Insert(at + 1, line);
        }
        else
        {
            lines[at] = line;
        }

        Assert.Throws<FormatException>(() => World.Read(new StateReader(string.Join('\n', lines)), Morning().Map));
    }

    /// <summary>
    /// The start village with three fishing huts four steps out, staffed, their outboxes empty, and sixteen villagers, as
    /// many as it houses: thirteen at work and three free.
    /// </summary>
    private static World FarHuts(bool running)
    {
        var far = FarWaters();
        string Hut(Hex h) => $"building FishingHut {h.Q} {h.R} 1 {(running ? 1 : 0)} 0 0 {(running ? 0 : 1)} 0 0 0 0 0 0 0 0";
        var world = Edited(Morning(), string.Join('\n', far.Select(Hut)), villagers: 16);
        Assert.Equal(3, world.FreeVillagers);
        return world;
    }

    /// <summary>
    /// The start village with its producing buildings and three far fishing huts paused, the huts' outboxes holding
    /// <paramref name="waiting"/> whole fish, and roads on <paramref name="roads"/>.
    /// </summary>
    private static (World World, Hex[] Far) PausedFarHuts(int[] waiting, IEnumerable<Hex>? roads = null)
    {
        var far = FarWaters();
        var world = Edited(Morning(), string.Join('\n', far.Select((h, i) => $"building FishingHut {h.Q} {h.R} 1 0 0 0 1 0 0 0 {waiting[i] * Unit} 0 0 0 0")), roads: roads);
        foreach (var start in world.Buildings.Where(b => world.Content.For(b.Kind).Produces && !b.Paused).Select(b => b.Tile).ToList())
        {
            Assert.True(Apply(world, new PauseBuilding(start)).Accepted);
        }

        return (world, far);
    }

    /// <summary>Three fishing waters four steps over land from the start village's storehouse.</summary>
    private static Hex[] FarWaters()
    {
        var world = Morning();
        var far = Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
            .Where(h => (world.Map.Features.UsesOf(h) & TileUse.FishingWater) != 0 && world.BuildingAt(h) is null && world.TripFrom(h).Steps == 4)
            .Take(3).ToArray();
        Assert.Equal(3, far.Length);
        return far;
    }

    internal static World Morning()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 09:00", 2));
        return world;
    }

    /// <summary>The world with buildings added at the end of the line, a stock on the home island, villagers and roads, read back as a save would be.</summary>
    internal static World Edited(World world, string? buildings, (Resource Resource, int Units)? stock = null, int? villagers = null, IEnumerable<Hex>? roads = null)
    {
        var lines = world.Fingerprint().TrimEnd('\n').Split('\n').ToList();
        if (buildings is not null)
        {
            int last = lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal));
            while (lines[last + 1].StartsWith("held ", StringComparison.Ordinal))
            {
                last++;
            }

            lines.Insert(last + 1, buildings);
        }

        if (stock is var (resource, units))
        {
            int at = lines.FindIndex(l => l.StartsWith($"stock {PrototypeHome.Home} {resource} ", StringComparison.Ordinal));
            lines[at] = $"stock {PrototypeHome.Home} {resource} {units * Unit}";
        }

        if (villagers is int count)
        {
            int at = lines.FindIndex(l => l.StartsWith($"settlement {world.HomeIsland} ", StringComparison.Ordinal));
            lines[at] = $"settlement {world.HomeIsland} {count.ToString(System.Globalization.CultureInfo.InvariantCulture)} 0";
        }

        if (roads is not null)
        {
            var laid = roads.Concat(world.Roads).Distinct().Order().Select(t => $"road {t.Q} {t.R}");
            lines.RemoveAll(l => l.StartsWith("road ", StringComparison.Ordinal));
            lines.InsertRange(lines.FindIndex(l => l.StartsWith("forest ", StringComparison.Ordinal) || l.StartsWith("fog ", StringComparison.Ordinal)), laid);
        }

        return World.Read(new StateReader(string.Join('\n', lines) + "\n"), world.Map);
    }

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"carriers-{Interlocked.Increment(ref s_key)}", command);
}
