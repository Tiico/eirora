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
/// The bottleneck diagnosis (TASK-043; CIV-05, LOG-03): why a building stands still, what distance costs, the sea
/// route's use and each island's largest bottleneck, read without changing the world (INV-D2).
/// </summary>
public sealed class DiagnosticsTests
{
    private const long Unit = Quantity.PerUnit;
    private static int s_key;

    [Fact]
    public void ASawmillWithoutTimberAboveTheReserveWaitsForTimberAndSaysHowMuchIsMissing()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0 0");

        var sawmill = world.Diagnose().At(L1)!;

        Assert.Equal(Production.Stopped, sawmill.Production);
        Assert.True(sawmill.StandsStill);
        var waiting = Assert.IsType<WaitingForInput>(Assert.Single(sawmill.Hindrances));
        Assert.Equal(Resource.Timber, waiting.Resource);
        Assert.Equal((18L, 20), (waiting.Stock, waiting.Reserve));

        // The woodcutter fells a little first; a full hour of planks takes two timber above the reserve of 20.
        Assert.Equal(4, waiting.Missing);
        Assert.Equal(Unit, waiting.LostPerHour);
    }

    [Fact]
    public void ASawmillWithSomeTimberAboveTheReserveProcessesWhatThereIs()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 20), buildings: "building Sawmill -1 0 1 1 0 0 0 0");

        var sawmill = world.Diagnose().At(L1)!;

        Assert.Equal(Production.Reduced, sawmill.Production);
        Assert.IsType<WaitingForInput>(Assert.Single(sawmill.Hindrances));
        Assert.InRange(sawmill.PerHour, 1, Unit - 1);
    }

    [Fact]
    public void AFishingHutFourStepsFromTheStorehouseDeliversHalfAndSaysWhatARoadWouldGive()
    {
        var far = FishingWater(4);
        var world = Edited(Morning(), buildings: $"building FishingHut {far.Q} {far.R} 1 1 0 0 0 0");

        var hut = world.Diagnose().At(far)!;

        Assert.Equal(Production.Reduced, hut.Production);
        var way = Assert.IsType<LongWay>(Assert.Single(hut.Hindrances));
        Assert.Equal((4, 50, 75), (way.Steps, way.Percent, way.RoadPercent));
        // Rates are scaled each quarter hour, as the settlement does: 1.25 fish an hour times the water's richness.
        long tick = 312_500L * world.Map.RichnessAt(far) / 100;
        Assert.Equal((4 * (tick / 2), 4 * (tick * 3 / 4), 4 * tick), (way.PerHour, way.WithRoadPerHour, way.BesideDepotPerHour));
        Assert.Equal(4 * (tick - (tick / 2)), way.LostPerHour);
        Assert.Equal(way.PerHour, hut.PerHour);
    }

    [Fact]
    public void AFarBuildingWithARoadSaysWhatAStorehouseWouldGiveButNoMoreRoad()
    {
        var far = FishingWater(4);
        var world = Edited(Morning(), buildings: $"building FishingHut {far.Q} {far.R} 1 1 1 0 0 0");

        var way = Assert.IsType<LongWay>(Assert.Single(world.Diagnose().At(far)!.Hindrances));

        Assert.Equal((75, (int?)null), (way.Percent, way.RoadPercent));
        Assert.Equal(way.PerHour, way.WithRoadPerHour);
        Assert.True(way.BesideDepotPerHour > way.PerHour);
    }

    [Fact]
    public void ABuildingWithoutStaffStandsStillAndSaysHowManyVillagersAreMissing()
    {
        // Eight villagers: seven run the start buildings, so one is free for a sawmill that takes two.
        var world = Edited(Morning(), villagers: 8, buildings: "building Sawmill -1 0 1 0 0 0 0 0");

        var sawmill = world.Diagnose().At(L1)!;

        Assert.Equal(Production.Stopped, sawmill.Production);
        Assert.True(sawmill.StandsStill);
        Assert.Equal(1, Assert.IsType<MissingStaff>(Assert.Single(sawmill.Hindrances)).Villagers);
    }

    [Fact]
    public void AStorehouseWithoutStaffStillStoresGoodsAndNeverStandsStill()
    {
        // Seven villagers run the start buildings, so none is left for a second storehouse; it stores all the same.
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Fish, 150), villagers: 7, buildings: "building Storehouse -1 0 1 0 0 0 0 0");

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
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: $"building Sawmill {far.Q} {far.R} 1 1 0 0 0 0");

        var sawmill = world.Diagnose().At(far)!;

        Assert.Equal(Production.Stopped, sawmill.Production);
        Assert.IsType<WaitingForInput>(Assert.Single(sawmill.Hindrances));
    }

    [Fact]
    public void AFarSawmillShortOfTimberIsHeldBackByTheTimberAndNotByTheWay()
    {
        // The woodcutter fells a quarter of timber first: enough for half the sawmill's pace, which is all that four
        // steps let reach the store anyway, so a road or a storehouse would give no more without more timber.
        var far = FarLand(4);
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 20), buildings: $"building Sawmill {far.Q} {far.R} 1 1 0 0 0 0");

        var diagnosis = world.Diagnose();
        var sawmill = diagnosis.At(far)!;

        Assert.Equal(Production.Reduced, sawmill.Production);
        Assert.IsType<WaitingForInput>(Assert.Single(sawmill.Hindrances));
        var bottleneck = diagnosis.BottleneckOn(PrototypeHome.Home)!;
        Assert.IsType<WaitingForInput>(bottleneck.Hindrance);
        Assert.Equal(F1, bottleneck.Workday!.Tile);
    }

    [Fact]
    public void AFarSawmillWithTimberEnoughSaysWhatARoadAndAStorehouseWouldGive()
    {
        var far = FarLand(4);
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 30), buildings: $"building Sawmill {far.Q} {far.R} 1 1 0 0 0 0");

        var sawmill = world.Diagnose().At(far)!;

        Assert.Equal(Production.Reduced, sawmill.Production);
        var way = Assert.IsType<LongWay>(Assert.Single(sawmill.Hindrances));
        Assert.Equal((500_000L, 750_000L, 1_000_000L), (way.PerHour, way.WithRoadPerHour, way.BesideDepotPerHour));
    }

    [Fact]
    public void ARoadOrAStorehouseGivesNoMoreThanTheStoreHasRoomFor()
    {
        var far = FishingWater(4);
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Fish, 150), buildings: $"building FishingHut {far.Q} {far.R} 1 1 0 0 0 0");

        var hut = world.Diagnose().At(far)!;

        Assert.Equal(Production.Stopped, hut.Production);
        Assert.IsType<StoreFull>(Assert.Single(hut.Hindrances));
    }

    [Fact]
    public void AFullStoreStopsTheFishingHut()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Fish, 150));

        var hut = world.Diagnose().At(K1)!;

        Assert.Equal(Production.Stopped, hut.Production);
        var full = Assert.IsType<StoreFull>(Assert.Single(hut.Hindrances));
        Assert.Equal((Resource.Fish, 150), (full.Resource, full.Capacity));
        Assert.Equal(1_250_000, full.LostPerHour);
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
        var world = Edited(Morning(), buildings: $"building Woodcutter {bare.Q} {bare.R} 1 1 0 0 0 0");

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
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0 0");

        var bottleneck = world.Diagnose().BottleneckOn(PrototypeHome.Home)!;

        Assert.Equal(L1, bottleneck.Building.Building.Tile);
        Assert.IsType<WaitingForInput>(bottleneck.Hindrance);
        Assert.Equal(F1, bottleneck.Workday!.Tile);
        Assert.Null(world.Diagnose().BottleneckOn(PrototypeHome.Nyon));
    }

    [Fact]
    public void AWorkdayAlreadyGivenIsNotSuggestedAgain()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0 0");
        Assert.True(Apply(world, new Workday(F1)).Accepted);

        Assert.Null(world.Diagnose().BottleneckOn(PrototypeHome.Home)!.Workday);
    }

    [Fact]
    public void TheSeaRouteSaysHowMuchOfItsCapacityIsUsedAndWhatWaits()
    {
        var world = Edited(Morning(), stock: (PrototypeHome.Nyon, Resource.Wool, 30), buildings: $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0 0\nbuilding Jetty {N0.Q} {N0.R} 1 0 0 0 0 0");

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
            Edited(Morning(), stock: (PrototypeHome.Nyon, Resource.Wool, 30), buildings: $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0 0\nbuilding Jetty {N0.Q} {N0.R} 1 0 0 0 0 0"),
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
        var world = Edited(Morning(), stock: (PrototypeHome.Home, Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0 0");
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

            // The staffing gives a building all the villagers it needs or none, so one that lacks them lacks at least one.
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
            Edited(Morning(), stock: (PrototypeHome.Nyon, Resource.Wool, 30), buildings: $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0 0\nbuilding Jetty {N0.Q} {N0.R} 1 0 0 0 0 0"),
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
        var foretold = world.Settlement.Diagnose().NextStores;
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
    private static Hex FishingWater(int steps)
    {
        var world = Morning();
        return Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
            .First(h => (world.Map.Features.UsesOf(h) & TileUse.FishingWater) != 0 && world.BuildingAt(h) is null && world.DepotDistance(h) == steps);
    }

    /// <summary>Land a sawmill takes at <paramref name="steps"/> steps over land from the start village's storehouse.</summary>
    private static Hex FarLand(int steps)
    {
        var world = Morning();
        return Enumerable.Range(0, world.Map.Disc.Count).Select(i => world.Map.Disc[i])
            .First(h => world.CheckBuild(BuildingKind.Sawmill, h) == Rejection.None && world.DepotDistance(h) == steps);
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
            .Select(jetty => $"building Jetty {jetty.Tile.Q} {jetty.Tile.R} 1 {(jetty.Home ? 1 : 0)} 0 0 0 0");
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
        IEnumerable<string>? forest = null)
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

        return World.Read(new StateReader(string.Join('\n', lines) + "\n"), world.Map);
    }

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"diagnosis-{Interlocked.Increment(ref s_key)}", command);
}
