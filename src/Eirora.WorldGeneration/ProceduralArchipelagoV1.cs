using System;
using System.Collections.Generic;
using System.Linq;
using Eirora.Simulation.Map;
using Eirora.WorldGeneration.Validation;

namespace Eirora.WorldGeneration
{
    /// <summary>
    /// Generator v1 (TASK-031): a Nordic archipelago from a seed. A start island with a crest, two islands out where
    /// epoch 1's expeditions find them, a few skerries, and an inner sea between them (MAP-01, MAP-02, D-61). Richer
    /// ground lies out on the islands, so the player weighs richness against distance (MAP-03).
    /// </summary>
    /// <remarks>
    /// The generator draws again, deterministically, until the validator passes the map (TASK-030): attempt n draws from
    /// streams of its own, so the same seed always ends in the same map after the same number of attempts. Once locked,
    /// this code never changes (ADR-0010); a better generator is version 2.
    /// </remarks>
    public sealed class ProceduralArchipelagoV1 : IWorldGenerator, ICountsAttempts
    {
        public const string GeneratorId = "procedural-archipelago";

        /// <summary>No seed has needed more in the bulk runs; reaching it is a bug in the generator.</summary>
        public const int MaximumAttempts = 200;

        private const int Radius = 24;
        private const int LandLimit = 20; // Land never reaches the outer rings, which stay deep sea (ADR-0010).
        private const int Gap = 2; // Sea tiles at least between two islands, so they never grow into one.

        public string Id => GeneratorId;

        public int Version => 1;

        public MapSource Source => MapSource.Procedural;

        public WorldMap Generate(ulong seed) => Generate(seed, out _);

        public WorldMap Generate(ulong seed, out int attempts)
        {
            for (attempts = 1; attempts <= MaximumAttempts; attempts++)
            {
                var map = new Attempt(seed, attempts).Build();
                if (map is not null && MapValidator.Validate(map).Passed)
                {
                    return map;
                }
            }

            throw new InvalidOperationException($"Seed {seed} gave no valid map in {MaximumAttempts} attempts.");
        }

        /// <summary>One attempt at a map: land first, then what grows on it, then the sea around it.</summary>
        private sealed class Attempt
        {
            private readonly ulong _seed;
            private readonly int _number;
            // Land tile → island number, from 0; sorted, so the land is gone through in the same order on every runtime.
            private readonly SortedDictionary<Hex, int> _island = new();
            private readonly List<(string Id, Hex Anchor)> _islands = new();
            private readonly Dictionary<Hex, (Terrain Terrain, int Height, int Richness)> _ground = new();
            private readonly List<Hex> _islandCentres = new();
            private readonly HashSet<Hex> _shoals = new();

            public Attempt(ulong seed, int number)
            {
                _seed = seed;
                _number = number;
            }

            public WorldMap? Build()
            {
                var shapes = Random("islands");
                var start = Grow(shapes, Hex.Origin, shapes.Between(50, 70), 6);
                var first = Place(shapes, 14, 16, shapes.Between(7, 13), 2, awayFrom: null);
                var second = Place(shapes, 17, 18, shapes.Between(9, 16), 2, awayFrom: first);
                if (first is null || second is null)
                {
                    return null;
                }

                // A Nordic archipelago is full of skerries, many of them strung out from the larger islands.
                int skerries = shapes.Between(7, 13);
                for (int i = 0; i < skerries; i++)
                {
                    if (shapes.Chance(60) && Near(shapes) is Hex near)
                    {
                        Grow(shapes, near, shapes.Between(1, 4), 1);
                    }
                    else
                    {
                        Place(shapes, 7, 20, shapes.Between(1, 5), 1, awayFrom: null);
                    }
                }

                FillLakes();
                AddIsland("home", start);
                AddIsland("first", first.Value);
                AddIsland("second", second.Value);
                for (int i = 3; i < _islandCentres.Count; i++)
                {
                    AddIsland("skerry-" + (i - 2).ToString(System.Globalization.CultureInfo.InvariantCulture), _islandCentres[i]);
                }

                Shoals(Random("shoals"));
                var depth = Depth();
                Home(Random("home"), depth);
                Nyon(Random("first"), depth, Tiles(1));
                Outer(Random("second"), depth, Tiles(2));
                var rest = Random("skerries");
                for (int i = 3; i < _islandCentres.Count; i++)
                {
                    Skerry(rest, Tiles(i));
                }

                return Map();
            }

            private Pcg32 Random(string step) => Pcg32.ForStep(_seed, $"attempt-{_number}/{step}");

            /// <summary>A free spot a few tiles off the coast of some island, where the next skerry of a chain lies.</summary>
            private Hex? Near(Pcg32 random)
            {
                var coasts = _island.Keys.ToList();
                for (int tries = 0; tries < 12; tries++)
                {
                    var from = coasts[random.Below(coasts.Count)];
                    var spot = from.Ring(random.Between(3, 4)).ElementAt(random.Below(6 * 3));
                    if (!Blocked(spot, _islandCentres.Count))
                    {
                        return spot;
                    }
                }

                return null;
            }

            /// <summary>Places an island's centre on a ring around the start and grows it, or gives up.</summary>
            private Hex? Place(Pcg32 random, int nearest, int farthest, int size, int radius, Hex? awayFrom)
            {
                for (int tries = 0; tries < 24; tries++)
                {
                    int ring = random.Between(nearest, farthest);
                    var centre = Hex.Origin.Ring(ring).ElementAt(random.Below(6 * ring));
                    if (Blocked(centre, _islandCentres.Count) || (awayFrom is Hex other && centre.DistanceTo(other) < 10))
                    {
                        continue;
                    }

                    return Grow(random, centre, size, radius);
                }

                return null;
            }

            /// <summary>
            /// Grows an island from its centre, one tile at a time, favouring tiles near the centre and next to much
            /// land, so islands come out round but ragged.
            /// </summary>
            private Hex Grow(Pcg32 random, Hex centre, int size, int radius)
            {
                int number = _islandCentres.Count;
                _islandCentres.Add(centre);
                _island[centre] = number;
                var tiles = new List<Hex> { centre };
                while (tiles.Count < size)
                {
                    var candidates = new SortedSet<Hex>();
                    foreach (var tile in tiles)
                    {
                        for (int direction = 0; direction < Hex.Directions; direction++)
                        {
                            var next = tile.Neighbour(direction);
                            if (!_island.ContainsKey(next) && next.DistanceTo(centre) <= radius && !Blocked(next, number))
                            {
                                candidates.Add(next);
                            }
                        }
                    }

                    if (candidates.Count == 0)
                    {
                        break;
                    }

                    var weights = candidates.Select(c => (radius + 1 - c.DistanceTo(centre)) * (1 + LandAround(c, number))).ToList();
                    int pick = random.Below(weights.Sum());
                    int index = 0;
                    while (pick >= weights[index])
                    {
                        pick -= weights[index++];
                    }

                    var chosen = candidates.ElementAt(index);
                    _island[chosen] = number;
                    tiles.Add(chosen);
                }

                return centre;
            }

            /// <summary>Whether land here would leave the map's land limit or come too near another island.</summary>
            private bool Blocked(Hex hex, int island)
            {
                if (hex.DistanceTo(Hex.Origin) > LandLimit)
                {
                    return true;
                }

                foreach (var near in hex.Within(Gap))
                {
                    if (_island.TryGetValue(near, out int other) && other != island)
                    {
                        return true;
                    }
                }

                return false;
            }

            private int LandAround(Hex hex, int island)
            {
                int count = 0;
                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    if (_island.TryGetValue(hex.Neighbour(direction), out int other) && other == island)
                    {
                        count++;
                    }
                }

                return count;
            }

            /// <summary>Sea that no ship can reach from the open sea becomes land of the island around it.</summary>
            private void FillLakes()
            {
                var open = new HashSet<Hex>();
                var queue = new Queue<Hex>(Hex.Origin.Ring(Radius));
                foreach (var hex in queue)
                {
                    open.Add(hex);
                }

                while (queue.Count > 0)
                {
                    var hex = queue.Dequeue();
                    for (int direction = 0; direction < Hex.Directions; direction++)
                    {
                        var next = hex.Neighbour(direction);
                        if (next.DistanceTo(Hex.Origin) <= Radius && !_island.ContainsKey(next) && open.Add(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }

                foreach (var hex in Hex.Origin.Within(Radius))
                {
                    if (!_island.ContainsKey(hex) && !open.Contains(hex))
                    {
                        for (int direction = 0; direction < Hex.Directions; direction++)
                        {
                            if (_island.TryGetValue(hex.Neighbour(direction), out int island))
                            {
                                _island[hex] = island;
                                break;
                            }
                        }
                    }
                }
            }

            private void AddIsland(string id, Hex anchor) => _islands.Add((id, anchor));

            /// <summary>Shallow banks out at sea, which narrow the fairways between the islands (MAP-03).</summary>
            private void Shoals(Pcg32 random)
            {
                int count = random.Between(4, 9);
                for (int i = 0; i < count; i++)
                {
                    int ring = random.Between(6, 19);
                    var bank = new List<Hex> { Hex.Origin.Ring(ring).ElementAt(random.Below(6 * ring)) };
                    int size = random.Between(2, 7);
                    for (int tries = 0; bank.Count < size && tries < 4 * size; tries++)
                    {
                        var next = bank[random.Below(bank.Count)].Neighbour(random.Below(Hex.Directions));
                        if (next.DistanceTo(Hex.Origin) <= LandLimit + 1 && !_island.ContainsKey(next) && !bank.Contains(next))
                        {
                            bank.Add(next);
                        }
                    }

                    foreach (var hex in bank.Where(h => !_island.ContainsKey(h)))
                    {
                        _shoals.Add(hex);
                    }
                }
            }

            private List<Hex> Tiles(int island)
            {
                var tiles = new List<Hex>();
                foreach (var tile in _island)
                {
                    if (tile.Value == island)
                    {
                        tiles.Add(tile.Key);
                    }
                }

                return tiles;
            }

            /// <summary>Steps from each land tile to the nearest sea: 1 on the coast.</summary>
            private Dictionary<Hex, int> Depth()
            {
                var depth = new Dictionary<Hex, int>();
                var queue = new Queue<Hex>();
                foreach (var hex in _island.Keys)
                {
                    if (Enumerable.Range(0, Hex.Directions).Any(d => !_island.ContainsKey(hex.Neighbour(d))))
                    {
                        depth[hex] = 1;
                        queue.Enqueue(hex);
                    }
                }

                while (queue.Count > 0)
                {
                    var hex = queue.Dequeue();
                    for (int direction = 0; direction < Hex.Directions; direction++)
                    {
                        var next = hex.Neighbour(direction);
                        if (_island.ContainsKey(next) && !depth.ContainsKey(next))
                        {
                            depth[next] = depth[hex] + 1;
                            queue.Enqueue(next);
                        }
                    }
                }

                return depth;
            }

            /// <summary>The start island: a crest inland, woods, fields and pasture, and beaches for the jetty.</summary>
            private void Home(Pcg32 random, Dictionary<Hex, int> depth)
            {
                var tiles = Tiles(0);
                int deepest = tiles.Max(h => depth[h]);
                var summits = tiles.Where(h => depth[h] == deepest).ToList();
                var crest = summits[random.Below(summits.Count)];
                Set(crest, Terrain.Mountain, 6);
                int ridges = random.Between(2, 3); // Mountains to quarry besides the crest (D-92, O-16).
                for (int i = 0; i < ridges; i++)
                {
                    var spots = tiles.Where(h => Free(h) && depth[h] >= 2 && h.DistanceTo(crest) >= 2).ToList();
                    if (spots.Count > 0)
                    {
                        Set(spots[random.Below(spots.Count)], Terrain.Mountain, 5);
                    }
                }

                Clumps(random, tiles, depth, Terrain.Forest, random.Between(2, 4), 2, 4, 100);
                Clumps(random, tiles, depth, Terrain.Farmland, 1, 2, 4, 100);
                Clumps(random, tiles, depth, Terrain.Pasture, 1, 1, 3, 100);
                Coast(random, tiles, depth, beachPercent: 40, rockPercent: 10);
            }

            /// <summary>The first expedition's island, like Nyön: a rich stone vein and rich pasture.</summary>
            private void Nyon(Pcg32 random, Dictionary<Hex, int> depth, List<Hex> tiles)
            {
                var inland = tiles.OrderByDescending(h => depth[h]).ThenBy(h => h).ToList();
                Set(inland[0], Terrain.Mountain, 5, 200);
                Clumps(random, tiles, depth, Terrain.Pasture, 1, 1, 3, 200);
                Clumps(random, tiles, depth, Terrain.Forest, random.Between(0, 1), 1, 3, 100);
                Coast(random, tiles, depth, beachPercent: 35, rockPercent: 20);
            }

            /// <summary>The second expedition's island, like Ytterön: old forest, rich farmland and rich fishing water.</summary>
            private void Outer(Pcg32 random, Dictionary<Hex, int> depth, List<Hex> tiles)
            {
                Clumps(random, tiles, depth, Terrain.Forest, 1, 2, 4, 150);
                Clumps(random, tiles, depth, Terrain.Farmland, 1, 1, 3, 150);
                Coast(random, tiles, depth, beachPercent: 35, rockPercent: 20);
                var shore = tiles.SelectMany(h => Enumerable.Range(0, Hex.Directions).Select(h.Neighbour))
                    .Where(h => !_island.ContainsKey(h)).Distinct().OrderBy(h => h).ToList();
                var bank = shore[random.Below(shore.Count)];
                _ground[bank] = (Terrain.ShallowSea, 1, 150);
            }

            /// <summary>A skerry: rock, a few trees or bare grass, and a beach to land on.</summary>
            private void Skerry(Pcg32 random, List<Hex> tiles)
            {
                foreach (var hex in tiles)
                {
                    // Mostly bare granite, some pine, a little grass: the skerries of the outer archipelago.
                    int roll = random.Below(10);
                    Set(hex, roll < 5 ? Terrain.Mountain : roll < 8 ? Terrain.Forest : Terrain.Grassland, roll < 5 ? 4 : 3);
                }

                var beach = tiles[random.Below(tiles.Count)];
                Set(beach, Terrain.Beach, 2, force: true);
            }

            /// <summary>Clumps of one kind of ground, each grown from an inland tile over free land.</summary>
            private void Clumps(Pcg32 random, List<Hex> tiles, Dictionary<Hex, int> depth, Terrain terrain, int count, int smallest, int largest, int richness)
            {
                for (int i = 0; i < count; i++)
                {
                    var seeds = tiles.Where(h => Free(h) && depth[h] >= 2).ToList();
                    if (seeds.Count == 0)
                    {
                        seeds = tiles.Where(Free).ToList();
                    }

                    if (seeds.Count == 0)
                    {
                        return;
                    }

                    var clump = new List<Hex> { seeds[random.Below(seeds.Count)] };
                    int size = random.Between(smallest, largest);
                    while (clump.Count < size)
                    {
                        var next = clump.SelectMany(h => Enumerable.Range(0, Hex.Directions).Select(h.Neighbour))
                            .Where(h => tiles.Contains(h) && Free(h) && !clump.Contains(h)).Distinct().OrderBy(h => h).ToList();
                        if (next.Count == 0)
                        {
                            break;
                        }

                        clump.Add(next[random.Below(next.Count)]);
                    }

                    foreach (var hex in clump)
                    {
                        Set(hex, terrain, depth[hex] >= 3 ? 4 : 3, richness);
                    }
                }
            }

            /// <summary>
            /// Beaches on part of the free coast, at least one, and bare rock on some of it; the rest of the free land is
            /// grass. Rock on the coast is low, so the crest stays the single highest tile.
            /// </summary>
            private void Coast(Pcg32 random, List<Hex> tiles, Dictionary<Hex, int> depth, int beachPercent, int rockPercent)
            {
                var coast = tiles.Where(h => depth[h] == 1 && Free(h)).ToList();
                foreach (var hex in coast)
                {
                    int roll = random.Below(100);
                    if (roll < beachPercent)
                    {
                        Set(hex, Terrain.Beach, 2);
                    }
                    else if (roll < beachPercent + rockPercent)
                    {
                        Set(hex, Terrain.Mountain, 4);
                    }
                }

                if (coast.Count > 0 && !tiles.Any(h => _ground.TryGetValue(h, out var g) && g.Terrain == Terrain.Beach))
                {
                    Set(coast[random.Below(coast.Count)], Terrain.Beach, 2);
                }

                foreach (var hex in tiles.Where(Free))
                {
                    Set(hex, Terrain.Grassland, depth[hex] >= 3 ? 4 : 3);
                }
            }

            private bool Free(Hex hex) => !_ground.ContainsKey(hex);

            private void Set(Hex hex, Terrain terrain, int height, int richness = 100, bool force = false)
            {
                if (force || Free(hex))
                {
                    _ground[hex] = (terrain, height, richness);
                }
            }

            private WorldMap? Map()
            {
                var map = new WorldMapBuilder(Radius, MapSource.Procedural, GeneratorId, 1) { Seed = _seed, Start = Hex.Origin };
                foreach (var hex in Hex.Origin.Within(Radius))
                {
                    if (_ground.TryGetValue(hex, out var ground))
                    {
                        map.Set(hex, ground.Terrain, ground.Height, ground.Richness);
                    }
                    else if (_island.ContainsKey(hex))
                    {
                        map.Set(hex, Terrain.Grassland, 3);
                    }
                    else if (Enumerable.Range(0, Hex.Directions).Any(d => _island.ContainsKey(hex.Neighbour(d))) || _shoals.Contains(hex))
                    {
                        map.Set(hex, Terrain.ShallowSea, 1);
                    }
                }

                foreach (var (id, anchor) in _islands)
                {
                    map.AddIsland(id, anchor);
                }

                try
                {
                    return map.Build();
                }
                catch (ArgumentException)
                {
                    return null; // A shape the map's rules refuse: draw again.
                }
            }
        }
    }
}
