using System;
using System.Collections.Generic;

namespace Eirora.Simulation.Map
{
    /// <summary>
    /// What follows from a map's tiles and is therefore never stored (ADR-0010): fishing water, harbour spots, crests,
    /// how the sea hangs together and how far it is by sea. Worked out once per map, in map order, so the same map always
    /// gives the same answers.
    /// </summary>
    public sealed class MapFeatures
    {
        private readonly WorldMap _map;
        private readonly int[] _sea; // The sea area each sea tile belongs to, from 1; 0 for land.
        private readonly HashSet<int> _openSea = new(); // Sea areas that hold deep sea.
        private readonly Dictionary<string, List<Hex>> _land = new(StringComparer.Ordinal);

        public MapFeatures(WorldMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _sea = new int[map.Disc.Count];
            int areas = 0;
            for (int index = 0; index < map.Disc.Count; index++)
            {
                var hex = map.Disc[index];
                if (map.IslandAt(hex) is string island)
                {
                    if (!_land.TryGetValue(island, out var tiles))
                    {
                        _land[island] = tiles = new List<Hex>();
                    }

                    tiles.Add(hex);
                }
                else if (_sea[index] == 0)
                {
                    FillSea(index, ++areas);
                }
            }
        }

        public WorldMap Map => _map;

        /// <summary>The land tiles of an island, in map order.</summary>
        public IReadOnlyList<Hex> LandOf(string island) => _land.TryGetValue(island, out var tiles) ? tiles : Array.Empty<Hex>();

        /// <summary>Shallow sea next to land, where a fishing hut can fish.</summary>
        public bool IsFishingWater(Hex hex) => _map.TerrainAt(hex) == Terrain.ShallowSea && HasNeighbour(hex, n => _map.TerrainAt(n).IsLand());

        /// <summary>A beach next to shallow water that reaches the deep sea, where a jetty can stand.</summary>
        public bool IsHarborSpot(Hex hex) =>
            _map.TerrainAt(hex) == Terrain.Beach
            && HasNeighbour(hex, n => _map.TerrainAt(n) == Terrain.ShallowSea && _openSea.Contains(_sea[_map.Disc.IndexOf(n)]));

        /// <summary>
        /// An island's crest, its single highest tile, where the Vårdkase is built; none when two tiles share the top.
        /// </summary>
        public Hex? CrestOf(string island)
        {
            Hex? crest = null;
            int top = -1;
            bool shared = false;
            foreach (var hex in LandOf(island))
            {
                int height = _map.HeightAt(hex);
                if (height > top)
                {
                    (crest, top, shared) = (hex, height, false);
                }
                else if (height == top)
                {
                    shared = true;
                }
            }

            return shared ? null : crest;
        }

        /// <summary>
        /// The fewest steps by sea from any of <paramref name="from"/> to each tile: a ship leaves from a coast tile, sails
        /// only over sea and lands on a coast tile. Land a ship cannot reach, and tiles off the map, are absent.
        /// </summary>
        public IReadOnlyDictionary<Hex, int> SeaStepsFrom(IEnumerable<Hex> from)
        {
            var steps = new Dictionary<Hex, int>();
            var queue = new Queue<Hex>();
            foreach (var hex in from)
            {
                if (_map.Contains(hex) && !steps.ContainsKey(hex))
                {
                    steps[hex] = 0;
                    queue.Enqueue(hex);
                }
            }

            while (queue.Count > 0)
            {
                var hex = queue.Dequeue();
                bool atSea = !_map.TerrainAt(hex).IsLand();
                if (!atSea && steps[hex] > 0)
                {
                    continue; // Landed: a ship never sails on over land.
                }

                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    var next = hex.Neighbour(direction);
                    if (_map.Contains(next) && !steps.ContainsKey(next) && (atSea || !_map.TerrainAt(next).IsLand()))
                    {
                        steps[next] = steps[hex] + 1;
                        queue.Enqueue(next);
                    }
                }
            }

            return steps;
        }

        /// <summary>
        /// One of the shortest ways by sea from any of <paramref name="from"/> to any of <paramref name="to"/>, both ends
        /// included, sailing as <see cref="SeaStepsFrom"/> counts; empty when there is none. Equally short ways are told
        /// apart by map order, so the same map always gives the same way.
        /// </summary>
        public IReadOnlyList<Hex> SeaRoute(IEnumerable<Hex> from, IEnumerable<Hex> to)
        {
            var goals = new HashSet<Hex>(to);
            var parent = new Dictionary<Hex, Hex?>();
            var queue = new Queue<Hex>();
            foreach (var hex in new SortedSet<Hex>(from))
            {
                if (_map.Contains(hex))
                {
                    parent[hex] = null;
                    queue.Enqueue(hex);
                }
            }

            while (queue.Count > 0)
            {
                var hex = queue.Dequeue();
                if (goals.Contains(hex))
                {
                    var route = new List<Hex>();
                    for (Hex? at = hex; at is Hex step; at = parent[step])
                    {
                        route.Add(step);
                    }

                    route.Reverse();
                    return route;
                }

                bool atSea = !_map.TerrainAt(hex).IsLand();
                if (!atSea && parent[hex] is not null)
                {
                    continue;
                }

                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    var next = hex.Neighbour(direction);
                    if (_map.Contains(next) && !parent.ContainsKey(next) && (atSea || !_map.TerrainAt(next).IsLand()))
                    {
                        parent[next] = hex;
                        queue.Enqueue(next);
                    }
                }
            }

            return Array.Empty<Hex>();
        }

        private bool HasNeighbour(Hex hex, Func<Hex, bool> test)
        {
            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var next = hex.Neighbour(direction);
                if (_map.Contains(next) && test(next))
                {
                    return true;
                }
            }

            return false;
        }

        private void FillSea(int from, int area)
        {
            var open = new Stack<int>();
            _sea[from] = area;
            open.Push(from);
            while (open.Count > 0)
            {
                int index = open.Pop();
                var hex = _map.Disc[index];
                if (_map.TerrainAt(hex) == Terrain.DeepSea)
                {
                    _openSea.Add(area);
                }

                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    int next = _map.Disc.IndexOf(hex.Neighbour(direction));
                    if (next >= 0 && _sea[next] == 0 && !_map.TerrainAt(_map.Disc[next]).IsLand())
                    {
                        _sea[next] = area;
                        open.Push(next);
                    }
                }
            }
        }
    }
}
