using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Every land tile is a possible building site (D-79, TASK-037): what a tile is good for follows from the map, and each
/// building requires one use of its site (economy-v1.md, section 3.1).
/// </summary>
public sealed class BuildingSiteTests
{
    private static readonly WorldMap s_map = PrototypeHome.Map;
    private static int s_key;

    // economy-v1.md, section 3.1: land tiles, then building sites by requirement, on each island with nothing built.
    [Theory]
    [InlineData(PrototypeHome.Home, 46, 43, 37, 2, 1, 2, 1, 35, 28)]
    [InlineData(PrototypeHome.Nyon, 6, 5, 5, 0, 1, 1, 0, 12, 5)]
    public void TheMapGivesTheBuildingSitesTheDesignCounts(
        string island, int land, int general, int forest, int farmland, int pasture, int mountain, int crest, int fishing, int harbour)
    {
        var tiles = Enumerable.Range(0, s_map.Disc.Count).Select(i => s_map.Disc[i]).ToList();
        var onIsland = tiles.Where(h => s_map.IslandAt(h) == island).ToList();
        var offShore = tiles.Where(h => s_map.IslandAt(h) is null && Neighbours(h).Any(n => s_map.IslandAt(n) == island));
        int Count(IEnumerable<Hex> among, TileUse use) => among.Count(h => (s_map.Features.UsesOf(h) & use) != 0);

        Assert.Equal(
            (land, general, forest, farmland, pasture, mountain, crest, fishing, harbour),
            (onIsland.Count, Count(onIsland, TileUse.Land), Count(onIsland, TileUse.Forest), Count(onIsland, TileUse.Farmland),
                Count(onIsland, TileUse.Pasture), Count(onIsland, TileUse.Mountain), Count(onIsland, TileUse.Crest),
                Count(offShore, TileUse.FishingWater), Count(onIsland, TileUse.HarborSpot)));
    }

    [Fact]
    public void TheCrestBearsNothingButTheVardkase()
    {
        Assert.Equal(TileUse.Crest, s_map.Features.UsesOf(M1));
        Assert.Equal(Rejection.SiteUnavailable, Morning().CheckBuild(BuildingKind.Quarry, M1));
        Assert.Equal(Rejection.None, Morning().CheckBuild(BuildingKind.Quarry, M3));
    }

    [Fact]
    public void ACottageCanStandOnEveryFreeKnownLandTileOfHemon()
    {
        var world = Morning();
        var land = Enumerable.Range(0, s_map.Disc.Count).Select(i => s_map.Disc[i])
            .Where(h => s_map.IslandAt(h) == PrototypeHome.Home && world.IsKnown(h) && world.BuildingAt(h) is null)
            .ToList();

        var refused = land.Where(h => world.CheckBuild(BuildingKind.Cottage, h) != Rejection.None).ToList();

        // Only the mountains and the crest do not take a cottage (D-91, D-92).
        Assert.True(land.Count > 30, $"{land.Count} free land tiles");
        Assert.Equal([M3, M1], refused);
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, land[0])).Accepted);
    }

    [Fact]
    public void NoBuildingStandsInTheDeepSeaUnderTheFogOrOnAnother()
    {
        var world = Morning();

        Assert.All(Enum.GetValues<BuildingKind>(), kind => Assert.Equal(Rejection.SiteUnavailable, world.CheckBuild(kind, new Hex(6, 0))));
        Assert.Equal(Rejection.SiteUnavailable, world.CheckBuild(BuildingKind.Quarry, M2)); // The vein waits for the survey.
        Assert.Equal(Rejection.SiteUnavailable, world.CheckBuild(BuildingKind.Cottage, S));
        Assert.Equal(TileUse.None, world.UsesOf(M2));
    }

    [Fact]
    public void AJettyStandsOnAnyShoreLandWhereShipsComeIn()
    {
        // D-89: not only a beach; a meadow by open water is a harbour spot too.
        var meadow = new Hex(-1, -3);
        Assert.NotEqual(Terrain.Beach, s_map.TerrainAt(meadow));

        var world = Morning();

        Assert.Equal(TileUse.Land | TileUse.HarborSpot, world.UsesOf(meadow) & (TileUse.Land | TileUse.HarborSpot));
        Assert.True(Apply(world, new Build(BuildingKind.Jetty, meadow)).Accepted);
        Assert.Equal(Rejection.SiteUnavailable, world.CheckBuild(BuildingKind.Jetty, L1)); // Inland.
    }

    [Fact]
    public void AFishingHutStandsOnStiltsInTheShallowsAndAFieldOnFarmland()
    {
        var world = Morning();

        Assert.Equal(TileUse.FishingWater, world.UsesOf(new Hex(3, 0)));
        Assert.Equal(Rejection.None, world.CheckBuild(BuildingKind.FishingHut, new Hex(3, 0)));
        Assert.Equal(Rejection.SiteUnavailable, world.CheckBuild(BuildingKind.FishingHut, L1));
        Assert.Equal(Rejection.SiteUnavailable, world.CheckBuild(BuildingKind.Field, L1));
        Assert.Equal(Rejection.None, world.CheckBuild(BuildingKind.SheepFarm, P1));
    }

    [Fact]
    public void AWoodcutterNeedsForestItCanFellAndNeverRevealsTheFog()
    {
        // (-3,0) is next to the old-growth forest, whose two tiles wait under the fog for the first survey (D-93).
        var edge = new Hex(-3, 0);
        var world = Morning();
        Assert.Equal(Rejection.SiteUnavailable, world.CheckBuild(BuildingKind.Woodcutter, edge));
        Assert.Equal(TileUse.None, world.UsesOf(edge) & TileUse.Forest);

        Assert.True(Apply(world, new Survey()).Accepted);

        Assert.Equal(Rejection.None, world.CheckBuild(BuildingKind.Woodcutter, edge));
        Assert.Equal(TileUse.Forest, world.UsesOf(edge) & TileUse.Forest);
        Assert.Equal(Rejection.None, world.CheckBuild(BuildingKind.Woodcutter, F3)); // On the forest itself too (D-90).
    }

    [Fact]
    public void TheStartWoodcutterFellsBothForestTilesItReaches()
    {
        var world = Morning();
        long before = world.Stock(Resource.Timber);

        world.AdvanceTo(At("2026-10-15 15:00", 2));

        // Two forest tiles at 0.5 timber an hour each for eight hours (economy-v1.md, section 3.1).
        Assert.Equal(8, world.Stock(Resource.Timber) - before);
    }

    [Fact]
    public void ABuildingOnAForestTileStopsTheFelling()
    {
        var world = Morning();
        var secondTile = new Hex(1, -2);
        Assert.Equal(Terrain.Forest, s_map.TerrainAt(secondTile));
        Assert.True(Apply(world, new Build(BuildingKind.Cottage, secondTile)).Accepted);
        Assert.True(Apply(world, new Rush()).Accepted);
        Assert.True(Apply(world, new Rush()).Accepted);
        world.AdvanceTo(At("2026-10-15 15:00", 2));
        Assert.NotNull(world.BuildingAt(secondTile));
        long before = world.Stock(Resource.Timber);

        world.AdvanceTo(At("2026-10-15 19:00", 2));

        // Only the woodcutter's own tile is left to fell (D-91).
        Assert.Equal(2, world.Stock(Resource.Timber) - before);
    }

    [Fact]
    public void FelledForestGrowsBackAndIsKeptInTheState()
    {
        var world = Morning();
        world.AdvanceTo(At("2026-10-15 15:00", 2));
        Assert.Contains("forest 1 -2 ", world.Fingerprint(), StringComparison.Ordinal);

        // While the village sleeps nothing is felled, and every forest tile grows back 0.25 timber an hour.
        var settlement = world.Settlement;
        world.AdvanceTo(At("2026-10-15 23:00", 2));
        long night = settlement.ForestLeft(F1);
        Assert.True(night < 40 * Quantity.PerUnit);
        world.AdvanceTo(At("2026-10-16 06:00", 2));
        Assert.Equal(night + (7 * 250_000), settlement.ForestLeft(F1));
    }

    [Fact]
    public void TheWayIsCountedInStepsOverLandSoTheBayMakesItLonger()
    {
        // The fishing water east of the village lies three hexes from the storehouse, but the hut's carriers go ashore
        // and round the water: four steps of 15 minutes each (economy-v2.md, section 7). The hut still fishes at full
        // pace, and while the carriers keep up all of it reaches the store (D-110).
        var water = new Hex(3, 0);
        var world = Morning();
        Assert.Equal(3, water.DistanceTo(S));
        Assert.Equal((4, 4 * Logistics.StepTime, S), (world.TripFrom(water).Steps, world.TripFrom(water).Time, world.TripFrom(water).Depot));
        Assert.Equal(1, world.TripFrom(K1).Steps);
        Assert.True(Apply(world, new Build(BuildingKind.FishingHut, water)).Accepted);
        world.AdvanceTo(At("2026-10-15 14:00", 2));
        Assert.True(world.BuildingAt(water)!.Staffed);
        long Fish() => world.Settlement.Stock(Resource.Fish) + world.Buildings.Where(b => b.Kind == BuildingKind.FishingHut).Sum(b => b.Waiting);
        long before = Fish();

        world.AdvanceTo(At("2026-10-15 22:00", 2));

        // Eight hours of 1.25 fish an hour from each hut, nearly all of it in the store.
        Assert.Equal(20 * Quantity.PerUnit, Fish() - before);
        Assert.True(world.BuildingAt(water)!.Waiting <= Quantity.Units(1), $"{world.BuildingAt(water)!.Waiting} fish wait at the far hut.");
    }

    [Fact]
    public void ABuildingOnAnotherIslandCountsItsWayOnThatIsland()
    {
        var world = Morning();

        Assert.False(world.TripFrom(N1).Reaches); // No depot on Nyön yet.
        Assert.Equal(PrototypeHome.Nyon, world.Settlement.IslandOf(N1));
        Assert.Equal(PrototypeHome.Home, world.Settlement.IslandOf(K1));
    }

    private static IEnumerable<Hex> Neighbours(Hex hex) => Enumerable.Range(0, Hex.Directions).Select(hex.Neighbour).Where(s_map.Contains);

    private static World Morning()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 07:00", 2));
        return world;
    }

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"site-{Interlocked.Increment(ref s_key)}", command);
}
