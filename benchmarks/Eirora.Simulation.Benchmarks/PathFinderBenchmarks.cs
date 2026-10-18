using BenchmarkDotNet.Attributes;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Benchmarks;

/// <summary>
/// Paths across a whole map of the size ADR-0010 sets (radius 24, 1 801 hexes), the worst cases the game can ask for:
/// corner to corner on an open sea, through rough terrain, and to a goal that cannot be reached, which searches every
/// hex the start can reach (TASK-028).
/// </summary>
[MemoryDiagnoser]
public class PathFinderBenchmarks
{
    private static readonly Hex s_from = new(-24, 12);
    private static readonly Hex s_to = new(24, -12);

    private readonly HexDisc _map = new(24);
    private readonly List<Hex> _path = new(1801);
    private PathFinder _finder = null!;

    [GlobalSetup]
    public void Setup() => _finder = new PathFinder(_map);

    [Benchmark(Baseline = true)]
    public int OpenSea() => Find(new Terrain(blockedPercent: 0, walledGoal: false));

    [Benchmark]
    public int RoughTerrain() => Find(new Terrain(blockedPercent: 15, walledGoal: false));

    [Benchmark]
    public int UnreachableGoal() => Find(new Terrain(blockedPercent: 15, walledGoal: true));

    private int Find(Terrain terrain)
    {
        _finder.TryFind(s_from, s_to, terrain, _path, out int cost);
        return cost;
    }

    /// <summary>Some hexes blocked, the rest costing 1 to 4 to enter; optionally a wall around the goal.</summary>
    private readonly struct Terrain : IStepCost
    {
        private readonly int _blockedPercent;
        private readonly bool _walledGoal;

        public Terrain(int blockedPercent, bool walledGoal)
        {
            _blockedPercent = blockedPercent;
            _walledGoal = walledGoal;
        }

        public int MinimumCost => 1;

        public int Cost(Hex origin, Hex neighbour)
        {
            if (_walledGoal && neighbour.DistanceTo(s_to) == 1)
            {
                return PathFinder.Impassable;
            }

            if (_blockedPercent == 0)
            {
                return 1;
            }

            uint hash = unchecked(((uint)neighbour.Q * 73856093u) ^ ((uint)neighbour.R * 19349663u) ^ 83492791u);
            hash ^= hash >> 13;
            hash = unchecked(hash * 0x5bd1e995u);
            hash ^= hash >> 15;
            return hash % 100 < (uint)_blockedPercent ? PathFinder.Impassable : 1 + (int)(hash % 4);
        }
    }
}
