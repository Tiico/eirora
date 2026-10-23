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
    private const BuildingKind Kiln = (BuildingKind)100;
    private static int s_key;

    [Fact]
    public void ASawmillWithoutTimberAboveTheReserveWaitsForTimberAndSaysHowMuchIsMissing()
    {
        var world = Edited(Morning(), stock: ("Home", Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0");

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
        var world = Edited(Morning(), stock: ("Home", Resource.Timber, 20), buildings: "building Sawmill -1 0 1 1 0 0 0");

        var sawmill = world.Diagnose().At(L1)!;

        Assert.Equal(Production.Reduced, sawmill.Production);
        Assert.IsType<WaitingForInput>(Assert.Single(sawmill.Hindrances));
        Assert.InRange(sawmill.PerHour, 1, Unit - 1);
    }

    [Fact]
    public void AFishingHutFourStepsFromTheStorehouseDeliversHalfAndSaysWhatARoadWouldGive()
    {
        var far = FishingWater(4);
        var world = Edited(Morning(), buildings: $"building FishingHut {far.Q} {far.R} 1 1 0 0 0");

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
        var world = Edited(Morning(), buildings: $"building FishingHut {far.Q} {far.R} 1 1 1 0 0");

        var way = Assert.IsType<LongWay>(Assert.Single(world.Diagnose().At(far)!.Hindrances));

        Assert.Equal((75, (int?)null), (way.Percent, way.RoadPercent));
        Assert.Equal(way.PerHour, way.WithRoadPerHour);
        Assert.True(way.BesideDepotPerHour > way.PerHour);
    }

    [Fact]
    public void ABuildingWithoutStaffStandsStillAndSaysHowManyVillagersAreMissing()
    {
        // Eight villagers: seven run the start buildings, so one is free for a sawmill that takes two.
        var world = Edited(Morning(), villagers: 8, buildings: "building Sawmill -1 0 1 0 0 0 0");

        var sawmill = world.Diagnose().At(L1)!;

        Assert.Equal(Production.Stopped, sawmill.Production);
        Assert.True(sawmill.StandsStill);
        Assert.Equal(1, Assert.IsType<MissingStaff>(Assert.Single(sawmill.Hindrances)).Villagers);
    }

    [Fact]
    public void ABuildingThatProducesNothingStandsStillOnlyWithoutStaff()
    {
        var world = Edited(Morning(), villagers: 7, buildings: "building Storehouse -1 0 1 0 0 0 0");

        var diagnosis = world.Diagnose();

        Assert.False(diagnosis.At(S)!.StandsStill);
        Assert.Equal(Production.None, diagnosis.At(S)!.Production);
        var storehouse = diagnosis.At(L1)!;
        Assert.Equal(Production.None, storehouse.Production);
        Assert.True(storehouse.StandsStill);
        Assert.Equal(1, Assert.IsType<MissingStaff>(Assert.Single(storehouse.Hindrances)).Villagers);
    }

    [Fact]
    public void AFullStoreStopsTheFishingHut()
    {
        var world = Edited(Morning(), stock: ("Home", Resource.Fish, 150));

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
        var world = Edited(Morning(), buildings: $"building Woodcutter {bare.Q} {bare.R} 1 1 0 0 0");

        var woodcutter = world.Diagnose().At(bare)!;

        Assert.Equal(Production.Stopped, woodcutter.Production);
        var felled = Assert.IsType<ForestFelled>(woodcutter.Hindrances[0]);
        Assert.Equal((0, 0), (felled.Felled, felled.InReach));
        Assert.True(felled.LostPerHour > 0);
    }

    [Fact]
    public void ASlowProcessWithNothingGrownWaitsForItToGrow()
    {
        // A kiln that grows a millionth of a unit a quarter hour grows nothing two steps from the storehouse.
        var spec = new BuildingSpec(
            new Dictionary<Resource, int> { [Resource.Timber] = 4 }, workHours: 2, buildPower: 0, staff: 1, TileUse.Land, maxLevel: 2,
            recipe: new Recipe(Resource.Wool, perHour: 4, growsPerHour: 4));
        var world = World.Create(PrototypeHome.Map, At("2026-10-15 07:00", 2), Night, ContentCatalog.Current.With(Kiln, spec));
        Assert.Equal(2, world.DepotDistance(E1));
        Assert.True(Apply(world, new Build(Kiln, E1)).Accepted);
        world.AdvanceTo(At("2026-10-15 10:00", 2));

        var kiln = world.Diagnose().At(E1)!;

        Assert.Equal(Production.Stopped, kiln.Production);
        Assert.IsType<NothingRipe>(kiln.Hindrances[0]);
    }

    [Fact]
    public void TheStartVillageProducesFully()
    {
        var diagnosis = Morning().Diagnose();

        Assert.All(diagnosis.Buildings.Where(b => b.Product is not null), b => Assert.Equal(Production.Full, b.Production));
        Assert.All(diagnosis.Buildings, b => Assert.Empty(b.Hindrances));
        Assert.Null(diagnosis.BottleneckOn(IslandId.Home));
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
        var world = Edited(Morning(), stock: ("Home", Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0");

        var bottleneck = world.Diagnose().BottleneckOn(IslandId.Home)!;

        Assert.Equal(L1, bottleneck.Building.Building.Tile);
        Assert.IsType<WaitingForInput>(bottleneck.Hindrance);
        Assert.Equal(F1, bottleneck.Workday!.Tile);
        Assert.Null(world.Diagnose().BottleneckOn(IslandId.Nyon));
    }

    [Fact]
    public void AWorkdayAlreadyGivenIsNotSuggestedAgain()
    {
        var world = Edited(Morning(), stock: ("Home", Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0");
        Assert.True(Apply(world, new Workday(F1)).Accepted);

        Assert.Null(world.Diagnose().BottleneckOn(IslandId.Home)!.Workday);
    }

    [Fact]
    public void TheSeaRouteSaysHowMuchOfItsCapacityIsUsedAndWhatWaits()
    {
        var world = Edited(Morning(), stock: ("Nyon", Resource.Wool, 30), buildings: $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0\nbuilding Jetty {N0.Q} {N0.R} 1 1 0 0 0");

        var route = world.Diagnose().SeaRoute;

        Assert.True(route.Open);
        Assert.Equal((2_500_000L, 2_500_000L, 100), (route.CapacityPerHour, route.ShippedPerHour, route.UsedPercent));
        Assert.Equal([(Resource.Wool, 30L)], route.Waiting);
        Assert.Empty(route.Hindrances);
    }

    [Fact]
    public void AFullStoreAtHomeHoldsTheShipsBack()
    {
        var world = Edited(
            Edited(Morning(), stock: ("Nyon", Resource.Wool, 30), buildings: $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0\nbuilding Jetty {N0.Q} {N0.R} 1 1 0 0 0"),
            stock: ("Home", Resource.Wool, 150));

        var route = world.Diagnose().SeaRoute;

        Assert.Equal((0L, 0), (route.ShippedPerHour, route.UsedPercent));
        Assert.Equal(Resource.Wool, Assert.IsType<StoreFull>(Assert.Single(route.Hindrances)).Resource);
    }

    [Fact]
    public void AClosedSeaRouteShipsNothing()
    {
        var route = Morning().Diagnose().SeaRoute;

        Assert.False(route.Open);
        Assert.Equal(0, route.UsedPercent);
        Assert.Empty(route.Waiting);
    }

    [Fact]
    public void ReadingTheDiagnosisChangesNothing()
    {
        var world = Edited(Morning(), stock: ("Home", Resource.Timber, 18), buildings: "building Sawmill -1 0 1 1 0 0 0");
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
            world.Diagnose();
            if (world.Fingerprint() != before)
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
            Edited(Morning(), stock: ("Nyon", Resource.Wool, 30), buildings: $"building Jetty {H1.Q} {H1.R} 1 1 0 0 0\nbuilding Jetty {N0.Q} {N0.R} 1 1 0 0 0"),
            stock: ("Home", Resource.Wool, 140));
        for (int hour = 0; hour < 12; hour++)
        {
            for (int quarter = 0; quarter < 4; quarter++)
            {
                AssertForetellsTheNextQuarterHour(world);
                world.AdvanceTo(new TimeReading(world.Now.UtcSeconds + Settlement.TickSeconds, world.Now.ZoneId, world.Now.OffsetSeconds));
            }
        }

        Assert.Equal(150, world.Stock(IslandId.Home, Resource.Wool));
        Assert.Contains(world.Diagnose().SeaRoute.Hindrances, h => h is StoreFull { Resource: Resource.Wool });
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

        foreach (var island in Enum.GetValues<IslandId>())
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

    /// <summary>The forest tiles a woodcutter on <paramref name="tile"/> reaches: known forest there and around it, unbuilt.</summary>
    private static List<Hex> Woods(World world, Hex tile) =>
        [.. new[] { tile }.Concat(Enumerable.Range(0, Hex.Directions).Select(tile.Neighbour))
            .Where(h => world.Map.Contains(h) && world.Map.TerrainAt(h) == Terrain.Forest && world.IsKnown(h) && (h == tile || world.BuildingAt(h) is null))
            .OrderBy(h => h)];

    private static World Morning()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 09:00", 2));
        return world;
    }

    /// <summary>
    /// The world with its state edited as a test needs it: a stock set, villagers, buildings added at the end of the
    /// line, and felled forest, read back as a save would be.
    /// </summary>
    private static World Edited(
        World world,
        (string Island, Resource Resource, long Units)? stock = null,
        int? villagers = null,
        string? buildings = null,
        IEnumerable<string>? forest = null)
    {
        var lines = world.Fingerprint().TrimEnd('\n').Split('\n').ToList();
        if (stock is var (island, resource, units))
        {
            int at = lines.FindIndex(l => l.StartsWith($"stock {island} {resource} ", StringComparison.Ordinal));
            lines[at] = $"stock {island} {resource} {units * Unit}";
        }

        if (villagers is int count)
        {
            int at = lines.FindIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal));
            var parts = lines[at].Split(' ');
            parts[1] = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            lines[at] = string.Join(' ', parts);
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
