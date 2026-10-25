using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using FsCheck.Xunit;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Building sites judged from the map alone (TASK-037, economy-v1.md, section 3.1), independently of how the rules work
/// them out, and forest shared between woodcutters.
/// </summary>
public sealed class BuildingSitePropertyTests
{
    private static readonly WorldMap s_map = PrototypeHome.Map;
    private static readonly Hex[] s_drawn = [.. Enumerable.Range(0, s_map.Disc.Count).Select(i => s_map.Disc[i]).Where(PrototypeHome.WasDrawn)];
    private static readonly HashSet<Hex> s_openShallows = OpenShallows();
    private static int s_key;

    // Acceptance: a tile that meets a building's requirement takes it. Every building is affordable at the start, so a
    // build is accepted exactly where the design allows it, judged here from terrain, neighbours and the fog.
    [Property(MaxTest = 300)]
    public void ABuildIsAcceptedExactlyWhereTheDesignAllowsIt(int seed, bool surveyed)
    {
        var random = new Random(seed);
        var world = Morning();
        if (surveyed)
        {
            Assert.True(Apply(world, new Survey()).Accepted);
        }

        var kinds = Enum.GetValues<BuildingKind>();
        var kind = kinds[random.Next(kinds.Length)];
        var tile = s_drawn[random.Next(s_drawn.Length)];

        bool allowed = Allowed(world, kind, tile);
        var result = Apply(world, new Build(kind, tile));

        Assert.True(result.Accepted == allowed, $"{kind} on {tile}: {result.Rejection}, but the design {(allowed ? "allows" : "forbids")} it.");
    }

    // Timber is counted per forest tile, so two woodcutters that reach the same forest share it.
    [Fact]
    public void TwoWoodcuttersThatReachTheSameForestShareIt()
    {
        var shared = new Hex(1, -2); // The start woodcutter's second tile, which a woodcutter on L2 reaches too.
        var alone = Morning();
        var together = Morning();
        Assert.True(Apply(together, new Build(BuildingKind.Woodcutter, L2)).Accepted);

        alone.AdvanceTo(At("2026-10-19 07:00", 2));
        together.AdvanceTo(At("2026-10-19 07:00", 2));

        Assert.True(alone.TimberLeft(shared) >= 30, $"alone {alone.TimberLeft(shared)}");
        Assert.True(together.TimberLeft(shared) <= 20, $"together {together.TimberLeft(shared)}");
    }

    /// <summary>Whether the design lets <paramref name="kind"/> stand on <paramref name="tile"/> in a world on Hemön alone.</summary>
    private static bool Allowed(World world, BuildingKind kind, Hex tile)
    {
        if (!world.IsKnown(tile) || world.BuildingAt(tile) is not null || world.Projects.Any(p => p.Tile == tile && p.Kind == ProjectKind.Build))
        {
            return false;
        }

        var terrain = s_map.TerrainAt(tile);
        bool water = terrain == Terrain.ShallowSea && Neighbours(tile).Any(n => s_map.IslandAt(n) == PrototypeHome.Home);
        if (s_map.IslandAt(tile) != PrototypeHome.Home && !water)
        {
            return false;
        }

        // A new world has reached no unlocked step, so only the buildings of the start can be built (TASK-061).
        if (world.Content.For(kind).Step > world.Step)
        {
            return false;
        }

        bool crest = tile == PrototypeHome.Crest;
        bool plain = terrain.IsLand() && terrain != Terrain.Mountain && !crest;
        return kind switch
        {
            BuildingKind.FishingHut => water,
            BuildingKind.Field => terrain == Terrain.Farmland,
            BuildingKind.SheepFarm => terrain == Terrain.Pasture,
            BuildingKind.Quarry => terrain == Terrain.Mountain && !crest,
            BuildingKind.Woodcutter or BuildingKind.HuntingLodge => plain && Neighbours(tile).Prepend(tile).Any(n => s_map.TerrainAt(n) == Terrain.Forest && world.IsKnown(n) && (n == tile || world.BuildingAt(n) is null)),
            BuildingKind.Jetty => plain && Neighbours(tile).Any(s_openShallows.Contains),
            _ => plain,
        };
    }

    /// <summary>Shallow water joined to the deep sea, where ships come in (D-89).</summary>
    private static HashSet<Hex> OpenShallows()
    {
        var sea = new HashSet<Hex>(Enumerable.Range(0, s_map.Disc.Count).Select(i => s_map.Disc[i]).Where(h => s_map.TerrainAt(h) == Terrain.DeepSea));
        var open = new Queue<Hex>(sea);
        while (open.Count > 0)
        {
            foreach (var next in Neighbours(open.Dequeue()).Where(n => !s_map.TerrainAt(n).IsLand() && sea.Add(n)))
            {
                open.Enqueue(next);
            }
        }

        return [.. sea.Where(h => s_map.TerrainAt(h) == Terrain.ShallowSea)];
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
