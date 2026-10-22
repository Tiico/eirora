using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using Eirora.WorldGeneration;
using Eirora.WorldGeneration.Validation;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// What a new world starts with on its map (TASK-033, economy-v1.md sections 4 and 6): the prototype's map keeps its
/// handmade founding, and a generated map gets the same kind of start by rule, so the first session plays on it.
/// </summary>
public sealed class FoundingTests
{
    private static readonly ProceduralArchipelagoV1 s_generator = new();
    private static int s_key;

    [Fact]
    public void ThePrototypesMapKeepsItsHandmadeFounding()
    {
        var founding = Founding.For(PrototypeHome.Map);

        Assert.Equal(PrototypeIsland.StartBuildings, founding.StartBuildings);
        Assert.Equal(PrototypeIsland.Surveys.Select(s => (s.Cost, s.Reveals.ToList())), founding.Surveys.Select(s => (s.Cost, s.Reveals.ToList())));
        Assert.Equal((PrototypeHome.Nyon, PrototypeIsland.ExpeditionFinds), (founding.ExpeditionIsland, founding.ExpeditionFinds!.Value));
    }

    [Fact]
    public void EveryGeneratedMapOfTwoHundredSeedsFoundsAWholeVillageOnSitesItCanUse()
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            var map = s_generator.Generate(seed);
            var founding = Founding.For(map);
            var world = World.Create(map, At("2026-10-14 14:00", 2), Night);

            Assert.Equal(
                [BuildingKind.Storehouse, BuildingKind.FishingHut, BuildingKind.Field, BuildingKind.Woodcutter],
                founding.StartBuildings.Select(b => b.Kind));
            if (map.TerrainAt(map.Start) is Terrain.Grassland or Terrain.Beach)
            {
                Assert.Equal(map.Start, founding.StartBuildings[0].Tile);
            }

            Assert.Equal(founding.StartBuildings.Count, founding.StartBuildings.Select(b => b.Tile).Distinct().Count());
            foreach (var (kind, tile) in founding.StartBuildings)
            {
                var site = BuildingSpec.For(kind).Requirement;
                Assert.True((map.Features.UsesOf(tile) & site) == site, $"Seed {seed}: the {kind} at {tile} stands where it cannot work.");
                Assert.True(world.IsKnown(tile), $"Seed {seed}: the {kind} at {tile} is under the fog.");
            }

            Assert.Equal(founding.StartBuildings.Count, world.Buildings.Count);
            Assert.All(world.Buildings, b => Assert.True(b.Staffed, $"Seed {seed}: the {b.Kind} has no staff."));
        }
    }

    [Fact]
    public void EveryGeneratedMapOfTwoHundredSeedsHidesTwoSurveysOnTheStartIsland()
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            var map = s_generator.Generate(seed);
            var founding = Founding.For(map);
            var world = World.Create(map, At("2026-10-14 14:00", 2), Night);
            string home = map.IslandAt(map.Start)!;

            Assert.Equal([60, 120], founding.Surveys.Select(s => s.Cost));
            var nearLand = founding.Surveys[0].Reveals;
            Assert.Equal(1, nearLand.Count(h => map.TerrainAt(h) == Terrain.Mountain));
            Assert.Equal(2, nearLand.Count(h => map.TerrainAt(h) == Terrain.Forest));
            var coast = founding.Surveys[1].Reveals;
            Assert.Equal(1, coast.Count(h => map.TerrainAt(h) == Terrain.Farmland));
            Assert.Equal(1, coast.Count(map.Features.IsFishingWater));

            foreach (var hex in founding.Surveys.SelectMany(s => s.Reveals))
            {
                Assert.False(world.IsKnown(hex), $"Seed {seed}: {hex} is known before its survey.");
                Assert.True(map.IslandAt(hex) == home || map.Features.IsFishingWater(hex), $"Seed {seed}: {hex} is not on the start island.");
                Assert.NotEqual(TileUse.Crest, map.Features.UsesOf(hex));
            }
        }
    }

    [Fact]
    public void TheFirstExpeditionFindsTheIslandTheValidatorCountsAsTheFirst()
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            var map = s_generator.Generate(seed);
            var founding = Founding.For(map);

            Assert.Equal(MapValidator.Analyse(map).FirstIsland, founding.ExpeditionIsland);
            Assert.Equal(founding.ExpeditionIsland, map.IslandAt(founding.ExpeditionFinds!.Value));
        }
    }

    [Fact]
    public void AMapWithoutSitesFoundsWhatFits()
    {
        var map = Map.WorldMapTests.SmallMap();

        var founding = Founding.For(map);

        Assert.Equal(BuildingKind.Storehouse, founding.StartBuildings[0].Kind);
        Assert.Null(founding.ExpeditionIsland);
        Assert.Null(founding.ExpeditionFinds);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(20261018UL)]
    public void TheFirstSessionPlaysOnAGeneratedWorld(ulong seed)
    {
        var map = s_generator.Generate(seed);
        var founding = Founding.For(map);
        var world = World.Create(map, At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 07:00", 2));

        // A good day for the fishing hut, then the survey of the near land (ux-journeys.md, journey 1).
        var hut = founding.StartBuildings.Single(b => b.Kind == BuildingKind.FishingHut).Tile;
        Assert.True(Apply(world, new Workday(hut)).Accepted);
        Assert.Single(Apply(world, new Survey()).Events.OfType<TilesRevealed>());
        Assert.All(founding.Surveys[0].Reveals, h => Assert.True(world.IsKnown(h)));

        // A jetty on the coast, and a woodcutter in the woods the survey found.
        var harbour = map.Features.LandOf(map.IslandAt(map.Start)!).First(h => world.CheckBuild(BuildingKind.Jetty, h) == Rejection.None);
        Assert.True(Apply(world, new Build(BuildingKind.Jetty, harbour)).Accepted);
        var woods = founding.Surveys[0].Reveals.Where(h => map.TerrainAt(h) == Terrain.Forest);
        Assert.Contains(woods, h => world.CheckBuild(BuildingKind.Woodcutter, h) == Rejection.None);
        Assert.True(Apply(world, new Build(BuildingKind.Woodcutter, woods.First(h => world.CheckBuild(BuildingKind.Woodcutter, h) == Rejection.None))).Accepted);
        world.AdvanceTo(At("2026-10-15 13:00", 2));
        Assert.Contains(world.Buildings, b => b.Kind == BuildingKind.Jetty);

        // The expedition sails from the jetty and finds the first island.
        world.Apply("steps-0", new RecordSelfCare(world.GameDay, Habit.Steps, ContributionSource.Verified, 50_000));
        world.AdvanceTo(At("2026-10-16 08:00", 2));
        world.Apply("steps-1", new RecordSelfCare(world.GameDay, Habit.Steps, ContributionSource.Verified, 50_000));
        Assert.True(Apply(world, new SendExpedition()).Accepted);
        var events = world.AdvanceTo(At("2026-10-18 20:00", 2));

        Assert.Equal(founding.ExpeditionIsland, Assert.Single(events.OfType<ExpeditionReturned>()).Island);
        Assert.True(world.IsKnown(world.ExpeditionFinds!.Value));
    }

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"f-{Interlocked.Increment(ref s_key)}", command);
}
