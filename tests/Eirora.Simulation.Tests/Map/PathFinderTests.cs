using Eirora.Simulation.Map;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Map;

/// <summary>
/// A* over a cost the caller gives (TASK-028): always the cheapest path, always the same one, without allocating.
/// </summary>
public sealed class PathFinderTests
{
    private static readonly HexDisc s_map = new(24);

    [Property(MaxTest = 100)]
    public bool OnAnOpenMapThePathIsAsLongAsTheDistance(int a, int b, int c, int d)
    {
        var from = OnMap(a, b);
        var to = OnMap(c, d);
        var path = new List<Hex>();

        bool found = new PathFinder(s_map).TryFind(from, to, new Terrain(blockedPercent: 0, seed: 0), path, out int cost);

        return found && cost == from.DistanceTo(to) && path.Count == cost + 1 && IsWalk(path, from, to);
    }

    [Property(MaxTest = 200)]
    public bool ThePathIsAsCheapAsAnyAndBlockedHexesAreNeverEntered(int seed, int a, int b, int c, int d)
    {
        var terrain = new Terrain(blockedPercent: 30, seed);
        var from = OnMap(a, b);
        var to = OnMap(c, d);
        var path = new List<Hex>();

        bool found = new PathFinder(s_map).TryFind(from, to, terrain, path, out int cost);
        int? cheapest = Dijkstra(terrain, from, to);

        if (!found)
        {
            return cheapest is null && path.Count == 0;
        }

        int walked = path.Zip(path.Skip(1)).Sum(step => terrain.Cost(step.First, step.Second));
        return cost == cheapest && walked == cost && IsWalk(path, from, to)
            && path.Skip(1).All(hex => terrain.Cost(hex, hex) != PathFinder.Impassable);
    }

    [Property(MaxTest = 100)]
    public bool TheSameSearchAlwaysGivesTheSamePath(int seed, int a, int b, int c, int d)
    {
        var terrain = new Terrain(blockedPercent: 20, seed);
        var from = OnMap(a, b);
        var to = OnMap(c, d);
        var reused = new PathFinder(s_map);
        var first = new List<Hex>();
        var second = new List<Hex>();
        var fresh = new List<Hex>();

        reused.TryFind(from, to, terrain, first, out _);
        reused.TryFind(to, from, terrain, new List<Hex>(), out _);
        reused.TryFind(from, to, terrain, second, out _);
        new PathFinder(s_map).TryFind(from, to, terrain, fresh, out _);

        return first.SequenceEqual(second) && first.SequenceEqual(fresh);
    }

    /// <summary>
    /// Many paths across an open map cost the same; this one is the path the tie-breaking rules choose, on every
    /// platform. The Unity EditMode tests assert the same path (PathFinderTiesTests).
    /// </summary>
    [Fact]
    public void AmongEquallyCheapPathsTheChoiceIsFixed()
    {
        var path = new List<Hex>();

        new PathFinder(s_map).TryFind(new Hex(-3, 2), new Hex(2, -1), new Terrain(blockedPercent: 0, seed: 0), path, out int cost);

        Assert.Equal(5, cost);
        Assert.Equal("(-3, 2) (-2, 1) (-1, 0) (0, -1) (1, -1) (2, -1)", string.Join(" ", path));
    }

    [Fact]
    public void AWalledOffGoalHasNoPath()
    {
        var goal = new Hex(5, 5);
        var walls = new HashSet<Hex>(goal.Ring(1));
        var path = new List<Hex> { Hex.Origin };

        bool found = new PathFinder(s_map).TryFind(Hex.Origin, goal, new Walls(walls), path, out _);

        Assert.False(found);
        Assert.Empty(path);
    }

    [Fact]
    public void AHexOffTheMapHasNoPath()
    {
        Assert.False(new PathFinder(s_map).TryFind(Hex.Origin, new Hex(30, 0), new Terrain(0, 0), new List<Hex>(), out _));
    }

    [Fact]
    public void TheStartIsAPathOfItsOwn()
    {
        var path = new List<Hex>();

        Assert.True(new PathFinder(s_map).TryFind(new Hex(1, 1), new Hex(1, 1), new Terrain(0, 0), path, out int cost));
        Assert.Equal(0, cost);
        Assert.Equal(new[] { new Hex(1, 1) }, path);
    }

    [Fact]
    public void AStepCheaperThanTheMinimumIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new PathFinder(s_map).TryFind(Hex.Origin, new Hex(3, 0), new TooCheap(), new List<Hex>(), out _));
    }

    [Fact]
    public void ASearchAllocatesNothing()
    {
        var finder = new PathFinder(s_map);
        var terrain = new Terrain(blockedPercent: 15, seed: 1);
        var path = new List<Hex>(s_map.Count);
        var from = new Hex(-24, 12);
        var to = new Hex(24, -12);
        finder.TryFind(from, to, terrain, path, out _);

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool found = finder.TryFind(from, to, terrain, path, out _);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(found);
        Assert.Equal(0, allocated);
    }

    private static Hex OnMap(int a, int b)
    {
        var hex = new Hex(Math.Abs(a % 49) - 24, Math.Abs(b % 49) - 24);
        return s_map.Contains(hex) ? hex : new Hex(hex.Q / 2, hex.R / 2);
    }

    private static bool IsWalk(List<Hex> path, Hex from, Hex to) =>
        path[0] == from && path[^1] == to && path.Zip(path.Skip(1)).All(step => step.First.DistanceTo(step.Second) == 1);

    /// <summary>Plain Dijkstra without a heuristic: the reference for the cheapest cost.</summary>
    private static int? Dijkstra(Terrain terrain, Hex from, Hex to)
    {
        var best = new Dictionary<Hex, int> { [from] = 0 };
        var queue = new PriorityQueue<Hex, int>();
        queue.Enqueue(from, 0);
        while (queue.TryDequeue(out var hex, out int cost))
        {
            if (hex == to)
            {
                return cost;
            }

            if (cost > best[hex])
            {
                continue;
            }

            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var next = hex.Neighbour(direction);
                int step = s_map.Contains(next) ? terrain.Cost(hex, next) : PathFinder.Impassable;
                if (step != PathFinder.Impassable && (!best.TryGetValue(next, out int known) || cost + step < known))
                {
                    best[next] = cost + step;
                    queue.Enqueue(next, cost + step);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A deterministic made-up terrain: some hexes are blocked, the rest cost 1 to 4 to enter. The start of a search
    /// may lie on a blocked hex; only entering one is refused.
    /// </summary>
    private readonly struct Terrain : IStepCost
    {
        private readonly int _blockedPercent;
        private readonly int _seed;

        public Terrain(int blockedPercent, int seed)
        {
            _blockedPercent = blockedPercent;
            _seed = seed;
        }

        public int MinimumCost => 1;

        public int Cost(Hex origin, Hex neighbour)
        {
            if (_blockedPercent == 0)
            {
                return 1;
            }

            uint hash = unchecked(((uint)neighbour.Q * 73856093u) ^ ((uint)neighbour.R * 19349663u) ^ ((uint)_seed * 83492791u));
            hash ^= hash >> 13;
            hash = unchecked(hash * 0x5bd1e995u);
            hash ^= hash >> 15;
            return hash % 100 < (uint)_blockedPercent ? PathFinder.Impassable : 1 + (int)(hash % 4);
        }
    }

    private sealed class Walls : IStepCost
    {
        private readonly HashSet<Hex> _walls;

        public Walls(HashSet<Hex> walls) => _walls = walls;

        public int MinimumCost => 1;

        public int Cost(Hex origin, Hex neighbour) => _walls.Contains(neighbour) ? PathFinder.Impassable : 1;
    }

    private sealed class TooCheap : IStepCost
    {
        public int MinimumCost => 2;

        public int Cost(Hex origin, Hex neighbour) => 1;
    }
}
