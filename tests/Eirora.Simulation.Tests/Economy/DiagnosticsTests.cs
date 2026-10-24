using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using FsCheck.Xunit;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// The bottleneck diagnosis (TASK-043, TASK-051; CIV-05, LOG-03): why a building stands still, why its goods do not reach
/// the store and what a road, a storehouse or more carriers would give, the sea route's use and each island's largest
/// bottleneck, read without changing the world (INV-D2).
/// </summary>
public sealed class DiagnosticsTests
{
    private const long Unit = Quantity.PerUnit;
    private static int s_key;

    [Fact]
    public void ASawmillWithoutTimberAboveTheReserveWaitsForTimberAndSaysHowMuchIsMissing()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0");

        var sawmill = world.Diagnose().At(L1)!;

        Assert.Equal(Production.Stopped, sawmill.Production);
        Assert.True(sawmill.StandsStill);
        var waiting = Assert.IsType<WaitingForInput>(Assert.Single(sawmill.Hindrances));
        Assert.Equal(Resource.Timber, waiting.Resource);
        Assert.Equal((18L, 20), (waiting.Stock, waiting.Reserve));

        // A full hour of planks takes two timber above the reserve of 20; the woodcutter's timber waits at the woodcutter.
        Assert.Equal(4, waiting.Missing);
        Assert.Equal(Unit, waiting.LostPerHour);
    }

    [Fact]
    public void ASawmillWithSomeTimberBroughtProcessesWhatThereIs()
    {
        // The carriers have brought it 0.3 timber, and the store has none above the reserve to bring more.
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 20), buildings: "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0\nheld Timber 300000");

        var sawmill = world.Diagnose().At(L1)!;

        Assert.Equal(Production.Reduced, sawmill.Production);
        Assert.IsType<WaitingForInput>(Assert.Single(sawmill.Hindrances));
        Assert.InRange(sawmill.PerHour, 1, Unit - 1);
    }

    [Fact]
    public void AFishingHutFourStepsAwayFishesFullyWhileTheCarriersKeepUp()
    {
        var far = FishingWater(4);
        var world = Edited(Morning(), buildings: $"building FishingHut {far.Q} {far.R} 1 1 0 0 0 0 0 0 0 0 0 0");

        var diagnosis = world.Diagnose();
        var hut = diagnosis.At(far)!;

        // Distance costs the carriers' time, not production (D-110): 1.25 fish an hour times the water's richness.
        Assert.Equal(Production.Full, hut.Production);
        Assert.Empty(hut.Hindrances);
        Assert.Equal(4 * (312_500L * world.Map.RichnessAt(far) / 100), hut.PerHour);
        var storehouse = diagnosis.DepotAt(S)!;
        Assert.Contains(world.BuildingAt(far)!, storehouse.Serves);
        Assert.Equal((1, 100), (storehouse.Depot.Carriers, storehouse.SharePercent));
        Assert.InRange(storehouse.LoadPercent, 1, 100);
    }

    [Fact]
    public void FarBuildingsWhoseCarriersCannotKeepUpSayWhatARoadACarrierAndAStorehouseWouldGive()
    {
        var (world, far) = BehindTheCarriers();

        var diagnosis = world.Diagnose();
        var depot = diagnosis.DepotAt(S)!;
        var behind = Assert.IsType<CarriersBehind>(Assert.Single(diagnosis.At(far[0])!.Hindrances));

        // The carriers bring their share of every building's goods: one carrier-hour of what the goods need (section 7.1).
        Assert.True(depot.NeedPerHour > Quantity.PerUnit, $"The storehouse needs only {depot.NeedPerHour}.");
        Assert.Equal((int)(Quantity.PerUnit * Quantity.PerUnit / depot.NeedPerHour * 100 / Quantity.PerUnit), depot.SharePercent);
        Assert.True(depot.LoadPercent > 100);
        Assert.Equal((4, S, depot.SharePercent), (behind.Trip.Steps, behind.Trip.Depot, behind.SharePercent));
        Assert.Equal(Production.Reduced, diagnosis.At(far[0])!.Production);

        // Each would bring more of the hut's fish to the store, and nothing would bring more than it makes.
        long full = behind.PerHour + behind.LostPerHour;
        Assert.InRange(behind.WithRoadPerHour, behind.PerHour + 1, full);
        Assert.InRange(behind.WithCarrierPerHour!.Value, behind.PerHour + 1, full);
        Assert.InRange(behind.BesideDepotPerHour, behind.PerHour + 1, full);
        Assert.NotNull(behind.BesideDepotSite);
        Assert.Equal(world.RoadToDepot(far[0]).Count, behind.RoadTiles);
        Assert.True(behind.VillagerFree);

        // The storehouse's sheet: one more carrier, the road that gives most and the storehouse that gives most.
        Assert.Contains(depot.WithCarrier!, g => g.Resource == Resource.Fish && g.PerHour > 0);
        Assert.Contains(depot.Road!.Gains, g => g.Resource == Resource.Fish && g.PerHour > 0);
        Assert.Contains(depot.Storehouse!.Gains, g => g.Resource == Resource.Fish && g.PerHour > 0);
        Assert.Equal(world.RoadToDepot(depot.Road.From.Tile), depot.Road.Tiles);
        Assert.Equal(Rejection.None, world.CheckBuild(BuildingKind.Storehouse, depot.Storehouse.Site));

        // A workday on the storehouse eases the island's bottleneck.
        var bottleneck = diagnosis.BottleneckOn(PrototypeHome.Home)!;
        Assert.IsType<CarriersBehind>(bottleneck.Hindrance);
        Assert.Equal(S, bottleneck.Workday!.Tile);
    }

    [Fact]
    public void WhatTheDiagnosisSaysARoadWouldGiveIsWhatTheRoadGives()
    {
        var (world, _) = BehindTheCarriers();
        var diagnosis = world.Diagnose();
        var road = diagnosis.DepotAt(S)!.Road!;
        var promised = Assert.IsType<CarriersBehind>(Assert.Single(diagnosis.At(road.From.Tile)!.Hindrances)).WithRoadPerHour;

        var after = Edited(world, roads: road.Tiles).Diagnose();

        Assert.Equal(promised, Reaches(after.At(road.From.Tile)!));
        Assert.Equal(road.Gains.Single(g => g.Resource == Resource.Fish).PerHour, Fish(after) - Fish(diagnosis));

        // What reaches the store an active hour: the carriers' share when they cannot keep up, else all it makes.
        static long Reaches(BuildingDiagnosis b) => b.Hindrances.OfType<CarriersBehind>().SingleOrDefault()?.PerHour ?? b.PerHour;
        static long Fish(Diagnostics d) => d.Buildings.Where(b => b.Product == Resource.Fish).Sum(Reaches);
    }

    [Fact]
    public void ABuildingThatNoStaffedDepotReachesSaysSo()
    {
        // Nyön's two villagers work the quarry, so its jetty has nobody to carry for it (D-106).
        var world = Edited(Morning(), stock: (PrototypeHome.Nyon, Resource.Fish, 0), settlement: (PrototypeHome.Nyon, 2), buildings: $"building Jetty {N0.Q} {N0.R} 1 0 0 0 0 0 4 0 0 0 0 0\nbuilding Quarry {N2.Q} {N2.R} 1 1 0 0 0 0 0 0 0 0 0 0");

        var quarry = world.Diagnose().At(N2)!;

        var none = Assert.IsType<NoDepot>(Assert.Single(quarry.Hindrances));
        Assert.True(none.DepotStands);
        Assert.Equal(Production.Reduced, quarry.Production);
        Assert.False(world.TripFrom(N2).Reaches);
    }

    [Fact]
    public void ABuildingWithoutStaffStandsStillAndSaysHowManyVillagersAreMissing()
    {
        // Eight villagers: seven run the start buildings, so one is free for a sawmill that takes two.
        var world = Edited(Morning(), villagers: 8, buildings: "building Sawmill -1 0 1 0 0 0 0 0 0 0 0 0 0 0");

        var sawmill = world.Diagnose().At(L1)!;

        Assert.Equal(Production.Stopped, sawmill.Production);
        Assert.True(sawmill.StandsStill);
        Assert.Equal(1, Assert.IsType<MissingStaff>(Assert.Single(sawmill.Hindrances)).Villagers);
    }

    [Fact]
    public void AStorehouseWithoutStaffStillStoresGoodsAndNeverStandsStill()
    {
        // Seven villagers run the start buildings, so none is left for a second storehouse; it stores all the same.
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Fish, 150), villagers: 7, buildings: "building Storehouse -1 0 1 0 0 0 0 0 4 0 0 0 0 0");

        var diagnosis = world.Diagnose();

        var storehouse = diagnosis.At(L1)!;
        Assert.Equal(Production.None, storehouse.Production);
        Assert.False(storehouse.StandsStill);
        Assert.Empty(storehouse.Hindrances);
        Assert.Equal(Production.Full, diagnosis.At(K1)!.Production);
        world.AdvanceTo(At("2026-10-15 09:15", 2));
        Assert.True(world.Settlement.Stock(Resource.Fish) > Quantity.Units(150));
    }

    [Fact]
    public void APausedBuildingStandsStillAndSaysWhatThePauseCosts()
    {
        var world = Morning();
        Assert.True(Apply(world, new PauseBuilding(K1)).Accepted);

        var hut = world.Diagnose().At(K1)!;

        Assert.Equal(Production.Stopped, hut.Production);
        Assert.True(hut.StandsStill);
        Assert.Equal(1_250_000, Assert.IsType<Paused>(Assert.Single(hut.Hindrances)).LostPerHour);

        // The player's own choice is never the island's bottleneck.
        Assert.Null(world.Diagnose().BottleneckOn(PrototypeHome.Home));
    }

    [Fact]
    public void AFarSawmillWithoutTimberWaitsForTimberAndARoadWouldGiveNothing()
    {
        var far = FarLand(4);
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: $"building Sawmill {far.Q} {far.R} 1 1 0 0 0 0 0 0 0 0 0 0");

        var sawmill = world.Diagnose().At(far)!;

        // Its carriers could not keep up either, but without timber there is nothing to carry.
        Assert.Equal(Production.Stopped, sawmill.Production);
        Assert.Equal([typeof(WaitingForInput), typeof(CarriersBehind)], sawmill.Hindrances.Select(h => h.GetType()));
        Assert.Equal(1_000_000, sawmill.Hindrances[0].LostPerHour);
    }

    [Fact]
    public void AFarSawmillShortOfTimberIsHeldBackMostByTheTimber()
    {
        // The carriers have brought half the sawmill's pace in timber and the store has none above the reserve; its
        // carriers cannot keep up either, but the timber costs it more.
        var far = FarLand(4);
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 20), buildings: $"building Sawmill {far.Q} {far.R} 1 1 0 0 0 0 0 0 0 0 0 0\nheld Timber 250000");

        var diagnosis = world.Diagnose();
        var sawmill = diagnosis.At(far)!;

        Assert.Equal(Production.Reduced, sawmill.Production);
        Assert.Equal([typeof(WaitingForInput), typeof(CarriersBehind)], sawmill.Hindrances.Select(h => h.GetType()));
        Assert.True(sawmill.Hindrances[0].LostPerHour > sawmill.Hindrances[1].LostPerHour);
    }

    [Fact]
    public void AFarSawmillWithTimberEnoughSaysWhatARoadAndAStorehouseWouldGive()
    {
        // A plank and two timber an hour, there and back four steps: one and a half carrier-hours for the sawmill alone.
        var far = FarLand(4);
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 30), buildings: $"building Sawmill {far.Q} {far.R} 1 1 0 0 0 0 0 0 0 0 0 0\nheld Timber 4000000");

        var sawmill = world.Diagnose().At(far)!;

        Assert.Equal(Production.Reduced, sawmill.Production);
        Assert.Equal(1_000_000, sawmill.PerHour);
        var behind = Assert.IsType<CarriersBehind>(Assert.Single(sawmill.Hindrances));
        Assert.True(behind.PerHour < behind.WithRoadPerHour && behind.WithRoadPerHour < 1_000_000, $"{behind.PerHour} planks an hour, {behind.WithRoadPerHour} with the road");
        Assert.True(behind.PerHour < behind.BesideDepotPerHour, $"{behind.PerHour} planks an hour, {behind.BesideDepotPerHour} beside a storehouse");
        Assert.Equal(1_000_000, behind.PerHour + behind.LostPerHour);
    }

    [Fact]
    public void ARoadOrAStorehouseGivesNoMoreThanTheStoreHasRoomFor()
    {
        var far = FishingWater(4);
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Fish, 150), buildings: $"building FishingHut {far.Q} {far.R} 1 1 0 0 0 0 0 0 0 0 0 0");

        var hut = world.Diagnose().At(far)!;

        // It fishes on into its outbox, but nothing reaches the full store.
        Assert.Equal(Production.Reduced, hut.Production);
        Assert.IsType<StoreFull>(Assert.Single(hut.Hindrances));
    }

    [Fact]
    public void AFullStoreHoldsTheFishingHutBackAndAFullOutboxStopsIt()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Fish, 150));

        var hut = world.Diagnose().At(K1)!;

        Assert.Equal(Production.Reduced, hut.Production);
        var full = Assert.IsType<StoreFull>(Assert.Single(hut.Hindrances));
        Assert.Equal((Resource.Fish, 150), (full.Resource, full.Capacity));
        Assert.Equal(1_250_000, full.LostPerHour);

        // Eight fish wait in its outbox once the store has stayed full for more than six hours.
        world.AdvanceTo(At("2026-10-15 16:00", 2));
        Assert.Equal(Logistics.OutboxUnits, world.BuildingAt(K1)!.WaitingUnits);
        Assert.Equal(Production.Stopped, world.Diagnose().At(K1)!.Production);
        Assert.True(world.Diagnose().At(K1)!.StandsStill);
    }

    [Fact]
    public void AFelledForestHoldsTheWoodcutterBack()
    {
        var woods = Woods(Morning(), F1);
        var world = Edited(Morning(), forest: woods.Select(tile => $"forest {tile.Q} {tile.R} 0"));

        var woodcutter = world.Diagnose().At(F1)!;

        // The forest grows back a quarter of an hour's regrowth before the woodcutter fells it again.
        Assert.Equal(Production.Reduced, woodcutter.Production);
        var felled = Assert.IsType<ForestFelled>(Assert.Single(woodcutter.Hindrances));
        Assert.Equal((woods.Count, woods.Count), (felled.Felled, felled.InReach));
        Assert.Equal(woods.Count * 250_000L, woodcutter.PerHour);
    }

    [Fact]
    public void AWoodcutterWithNoForestInReachStandsStill()
    {
        var start = Morning();
        var bare = Hex.Origin.Within(6).First(h => (start.UsesOf(h) & TileUse.Land) != 0 && start.BuildingAt(h) is null && Woods(start, h).Count == 0);
        var world = Edited(Morning(), buildings: $"building Woodcutter {bare.Q} {bare.R} 1 1 0 0 0 0 0 0 0 0 0 0");

        var woodcutter = world.Diagnose().At(bare)!;

        Assert.Equal(Production.Stopped, woodcutter.Production);
        var felled = Assert.IsType<ForestFelled>(woodcutter.Hindrances[0]);
        Assert.Equal((0, 0), (felled.Felled, felled.InReach));
        Assert.True(felled.LostPerHour > 0);
    }

    [Fact]
    public void TheStartVillageProducesFully()
    {
        var diagnosis = Morning().Diagnose();

        Assert.All(diagnosis.Buildings.Where(b => b.Product is not null), b => Assert.Equal(Production.Full, b.Production));
        Assert.All(diagnosis.Buildings, b => Assert.Empty(b.Hindrances));
        Assert.Null(diagnosis.BottleneckOn(PrototypeHome.Home));
    }

    [Fact]
    public void AFieldAtDawnHarvestsWhatRipenedInTheNightFully()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 07:00", 2));

        var field = world.Diagnose().At(O1)!;

        // It harvests at its pace, two grain an hour, faster than it grows, until the night's grain is in.
        Assert.Equal(Production.Full, field.Production);
        Assert.Equal(Resource.Grain, field.Product);
        Assert.Equal(2 * Unit, field.PerHour);
    }

    [Fact]
    public void TheIslandsBottleneckIsTheSawmillWaitingForTimberAndAWorkdayOnTheWoodcutterEasesIt()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0");

        var bottleneck = world.Diagnose().BottleneckOn(PrototypeHome.Home)!;

        Assert.Equal(L1, bottleneck.Building.Building.Tile);
        Assert.IsType<WaitingForInput>(bottleneck.Hindrance);
        Assert.Equal(F1, bottleneck.Workday!.Tile);
        Assert.Null(world.Diagnose().BottleneckOn(PrototypeHome.Nyon));
    }

    [Fact]
    public void AWorkdayAlreadyGivenIsNotSuggestedAgain()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0");
        Assert.True(Apply(world, new Workday(F1)).Accepted);

        Assert.Null(world.Diagnose().BottleneckOn(PrototypeHome.Home)!.Workday);
    }

    [Fact]
    public void TheSeaRouteSaysHowMuchOfItsCapacityIsUsedAndWhatWaits()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Nyon, Resource.Wool, 30), buildings: $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0 1 4 0 0 0 0 0\nbuilding Jetty {N0.Q} {N0.R} 1 0 0 0 0 0 4 0 0 0 0 0");

        var route = Assert.Single(world.Diagnose().SeaRoutes);

        Assert.Equal((PrototypeHome.Nyon, PrototypeHome.Home), (route.Route.From, route.Route.To));
        Assert.Equal((2_500_000L, 2_500_000L, 100), (route.CapacityPerHour, route.ShippedPerHour, route.UsedPercent));
        Assert.Equal([(Resource.Wool, 30L)], route.Waiting);
        Assert.Empty(route.Hindrances);
    }

    [Fact]
    public void AFullStoreAtHomeHoldsTheShipsBack()
    {
        var world = Edited(
            Edited(Morning(), stock: (PrototypeHome.Nyon, Resource.Wool, 30), buildings: $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0 1 4 0 0 0 0 0\nbuilding Jetty {N0.Q} {N0.R} 1 0 0 0 0 0 4 0 0 0 0 0"),
            stock: (PrototypeHome.Home, Resource.Wool, 150 + 50)); // A storehouse and a jetty.

        var route = world.Diagnose().SeaRouteFrom(PrototypeHome.Nyon)!;

        Assert.Equal((0L, 0), (route.ShippedPerHour, route.UsedPercent));
        Assert.Equal(Resource.Wool, Assert.IsType<StoreFull>(Assert.Single(route.Hindrances)).Resource);
    }

    [Fact]
    public void NoSeaRouteIsDiagnosedBeforeShipsSail()
    {
        var diagnosis = Morning().Diagnose();

        Assert.Empty(diagnosis.SeaRoutes);
        Assert.Null(diagnosis.SeaRouteFrom(PrototypeHome.Nyon));
    }

    [Fact]
    public void EachSeaRouteSaysItsOwnUseAndTheRoutesShareTheRoomAtHomeInMapOrder()
    {
        // Room for half a unit of wool at home, with its storehouse and jetty: the first island's ships take it, and the
        // second island's wait.
        var world = OnTheArchipelago(home: 199.5m, first: 30, second: 30);

        var routes = world.Diagnose().SeaRoutes;

        Assert.Equal([Archipelago.First, Archipelago.Second], routes.Select(r => r.Route.From));
        Assert.Equal((2_000_000L, 80), (routes[0].ShippedPerHour, routes[0].UsedPercent));
        Assert.Equal(500_000L, Assert.IsType<StoreFull>(Assert.Single(routes[0].Hindrances)).LostPerHour);
        Assert.Equal((0L, 0), (routes[1].ShippedPerHour, routes[1].UsedPercent));
        Assert.Equal(2_500_000L, Assert.IsType<StoreFull>(Assert.Single(routes[1].Hindrances)).LostPerHour);
        Assert.All(routes, r => Assert.Equal([(Resource.Wool, 30L)], r.Waiting));
        AssertForetellsTheNextQuarterHour(world);
    }

    [Fact]
    public void ReadingTheDiagnosisChangesNothing()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0");
        string before = world.Fingerprint();

        world.Diagnose();
        world.Diagnose();

        Assert.Equal(before, world.Fingerprint());
    }

    // INV-D2: reading the diagnosis after every input changes neither the world nor where it goes.
    [Property(MaxTest = 50)]
    public bool TheDiagnosisNeverChangesTheWorld(int seed)
    {
        var scenario = Scenario.Generate(seed);
        var world = scenario.Start();
        foreach (var input in scenario.Inputs)
        {
            input.ApplyTo(world);
            string before = world.Fingerprint();
            var diagnosis = world.Diagnose();
            if (world.Fingerprint() != before)
            {
                return false;
            }

            // The staffing gives a building all the villagers it needs none, so one 0 0 0 0 that lacks them lacks at least one.
            if (diagnosis.Buildings.SelectMany(b => b.Hindrances).OfType<MissingStaff>().Any(m => m.Villagers < 1))
            {
                return false;
            }
        }

        return world.Fingerprint() == scenario.Run().Fingerprint();
    }

    // The diagnosis follows the rules: what it foretells for the next quarter hour is what the stores then hold.
    [Property(MaxTest = 50)]
    public void TheDiagnosisForetellsTheNextQuarterHour(int seed)
    {
        var scenario = Scenario.Generate(seed);
        var world = scenario.Start();
        foreach (var input in scenario.Inputs)
        {
            input.ApplyTo(world);
            AssertForetellsTheNextQuarterHour(world);
        }
    }

    // The random scenarios seldom open the sea route, so the ships are followed through a day of their own, until the
    // store at home is full.
    [Fact]
    public void TheDiagnosisForetellsWhatTheShipsCarry()
    {
        var world = Edited(
            Edited(Morning(), stock: (PrototypeHome.Nyon, Resource.Wool, 30), buildings: $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0 1 4 0 0 0 0 0\nbuilding Jetty {N0.Q} {N0.R} 1 0 0 0 0 0 4 0 0 0 0 0"),
            stock: (PrototypeHome.Home, Resource.Wool, 190));
        for (int hour = 0; hour < 12; hour++)
        {
            for (int quarter = 0; quarter < 4; quarter++)
            {
                AssertForetellsTheNextQuarterHour(world);
                world.AdvanceTo(new TimeReading(world.Now.UtcSeconds + Settlement.TickSeconds, world.Now.ZoneId, world.Now.OffsetSeconds));
            }
        }

        Assert.Equal(150 + 50, world.Stock(PrototypeHome.Home, Resource.Wool));
        Assert.Contains(world.Diagnose().SeaRouteFrom(PrototypeHome.Nyon)!.Hindrances, h => h is StoreFull { Resource: Resource.Wool });
    }

    /// <summary>
    /// Plays the next quarter hour on a copy of the world and checks the stores against the diagnosis, unless more than
    /// work happens in it: a dawn or dusk, or a project finished that changes the buildings.
    /// </summary>
    private static void AssertForetellsTheNextQuarterHour(World world)
    {
        var foretold = world.Settlement.Diagnose(world.ActiveTicks).NextStores;
        var next = SimulationSnapshot.Take(world).Restore(world.Map);
        long tick = world.Now.UtcSeconds - (world.Now.UtcSeconds % Settlement.TickSeconds) + Settlement.TickSeconds;
        var events = next.AdvanceTo(new TimeReading(tick, world.Now.ZoneId, world.Now.OffsetSeconds));
        if (next.Phase != Phase.Active || events.Any(e => e is DawnBroke or DuskFell or ProjectCompleted))
        {
            return;
        }

        Assert.Equal(world.Islands, next.Islands);
        foreach (var island in world.Islands)
        {
            Assert.Equal(foretold[island], Enum.GetValues<Resource>().Select(r => next.Settlement.Stock(island, r)).ToArray());
        }
    }

    /// <summary>Fishing water at <paramref name="steps"/> steps over land from the start village's storehouse.</summary>
    private static Hex FishingWater(int steps) => FishingWaters(steps).First();

    private static IEnumerable<Hex> FishingWaters(int steps)
    {
        var world = Morning();
        return Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
            .Where(h => (world.Map.Features.UsesOf(h) & TileUse.FishingWater) != 0 && world.BuildingAt(h) is null && world.TripFrom(h).Steps == steps);
    }

    /// <summary>Land a sawmill takes at <paramref name="steps"/> steps over land from the start village's storehouse.</summary>
    private static Hex FarLand(int steps)
    {
        var world = Morning();
        return Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
            .First(h => world.CheckBuild(BuildingKind.Sawmill, h) == Rejection.None && world.TripFrom(h).Steps == steps);
    }

    /// <summary>
    /// The start village with three fishing huts four steps out, more than its one carrier can keep up with, and sixteen
    /// villagers, three of them free.
    /// </summary>
    private static (World World, Hex[] Far) BehindTheCarriers()
    {
        var far = FishingWaters(4).Take(3).ToArray();
        Assert.Equal(3, far.Length);
        var world = Edited(Morning(), villagers: 16, buildings: string.Join("\n", far.Select(h => $"building FishingHut {h.Q} {h.R} 1 1 0 0 0 0 0 0 0 0 0 0")));
        Assert.Equal(3, world.FreeVillagers);
        return (world, far);
    }

    /// <summary>The forest tiles a woodcutter on <paramref name="tile"/> reaches: known forest there and around it, unbuilt.</summary>
    private static List<Hex> Woods(World world, Hex tile) =>
        [.. new[] { tile }.Concat(Enumerable.Range(0, Hex.Directions).Select(tile.Neighbour))
            .Where(h => world.Map.Contains(h) && world.Map.TerrainAt(h) == Terrain.Forest && world.IsKnown(h) && (h == tile || world.BuildingAt(h) is null))
            .OrderBy(h => h)];

    /// <summary>
    /// The archipelago's three islands at nine in the morning, a jetty on each and whole units of wool in each store, as
    /// <paramref name="home"/>, <paramref name="first"/> and <paramref name="second"/> say.
    /// </summary>
    private static World OnTheArchipelago(decimal home, int first, int second)
    {
        var world = Archipelago.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 09:00", 2));
        // The other islands' settlements have no villagers, so their jetties are not staffed.
        var jetties = new[] { Archipelago.Home, Archipelago.First, Archipelago.Second }
            .Select(island => (Tile: Archipelago.Map.Features.LandOf(island).First(h => Archipelago.Map.Features.IsHarborSpot(h) && world.BuildingAt(h) is null), Home: island == Archipelago.Home))
            .Select(jetty => jetty.Home ? $"building Jetty {jetty.Tile.Q} {jetty.Tile.R} 1 1 0 0 0 1 4 0 0 0 0 0" : $"building Jetty {jetty.Tile.Q} {jetty.Tile.R} 1 0 0 0 0 0 4 0 0 0 0 0");
        world = Edited(world, stock: (Archipelago.First, Resource.Wool, first));
        world = Edited(world, stock: (Archipelago.Second, Resource.Wool, second));
        return Edited(world, stock: (Archipelago.Home, Resource.Wool, home), buildings: string.Join('\n', jetties));
    }

    private static World Morning()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 09:00", 2));
        return world;
    }

    /// <summary>
    /// The world with its state edited as a test needs it: a stock set, in units that may have a fraction (an island
    /// without a store gets one), villagers, buildings added at the end of the line, and felled forest, read back as a
    /// save would be.
    /// </summary>
    private static World Edited(
        World world,
        (string Island, Resource Resource, decimal Units)? stock = null,
        int? villagers = null,
        string? buildings = null,
        IEnumerable<string>? forest = null,
        IEnumerable<Hex>? roads = null,
        (string Island, int Villagers)? settlement = null)
    {
        var lines = world.Fingerprint().TrimEnd('\n').Split('\n').ToList();
        if (stock is var (island, resource, units))
        {
            // An island without a store gets an empty one after the others, as building there first would give it.
            if (!lines.Any(l => l.StartsWith($"stock {island} ", StringComparison.Ordinal)))
            {
                lines.Insert(lines.FindLastIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal)) + 1, $"settlement {island} 0");
                lines.InsertRange(
                    lines.FindLastIndex(l => l.StartsWith("stock ", StringComparison.Ordinal)) + 1,
                    Enum.GetValues<Resource>().Select(r => $"stock {island} {r} 0"));
            }

            int at = lines.FindIndex(l => l.StartsWith($"stock {island} {resource} ", StringComparison.Ordinal));
            lines[at] = $"stock {island} {resource} {(long)(units * Unit)}";
        }

        if (settlement is var (settled, people))
        {
            int at = lines.FindIndex(l => l.StartsWith($"settlement {settled} ", StringComparison.Ordinal));
            lines[at] = $"settlement {settled} {people.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        if (villagers is int count)
        {
            int at = lines.FindIndex(l => l.StartsWith($"settlement {world.HomeIsland} ", StringComparison.Ordinal));
            lines[at] = $"settlement {world.HomeIsland} {count.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        if (buildings is not null)
        {
            lines.Insert(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, buildings);
        }

        if (forest is not null)
        {
            lines.RemoveAll(l => l.StartsWith("forest ", StringComparison.Ordinal));
            lines.InsertRange(lines.FindIndex(l => l.StartsWith("fog ", StringComparison.Ordinal)), forest);
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
        world.Apply($"diagnosis-{Interlocked.Increment(ref s_key)}", command);
}
