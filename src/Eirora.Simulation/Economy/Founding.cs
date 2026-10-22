using System;
using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// What a new world on a map starts with (economy-v1.md, sections 4 and 6): the start village's buildings, the
    /// surveys that wait under the fog, and the island the first expedition finds. It follows from the map alone, so
    /// it is never stored: the same map always gives the same founding.
    /// </summary>
    /// <remarks>
    /// The prototype's handmade map keeps the founding it has always had (<see cref="PrototypeIsland"/>), so every world
    /// made on it replays as before. Every other map gets the same kind of start by a rule that mirrors it (TASK-033):
    /// a storehouse, a fishing hut, a field and a woodcutter as near the map's start as their sites allow, on plain land
    /// where they can; a
    /// first survey that reveals a quarry and two forest tiles, and a second that reveals farmland and fishing water,
    /// the farthest of their kind from the start; and the nearest island worth an expedition.
    /// </remarks>
    public sealed class Founding
    {
        /// <summary>Land tiles an expedition's island has at least, besides some rich ground; a skerry never counts (Nyön has 6).</summary>
        public const int ExpeditionIslandLand = 6;

        private static readonly (BuildingKind Kind, TileUse Site)[] s_village =
        {
            (BuildingKind.Storehouse, TileUse.Land),
            (BuildingKind.FishingHut, TileUse.FishingWater),
            (BuildingKind.Field, TileUse.Farmland),
            (BuildingKind.Woodcutter, TileUse.Forest),
        };

        private Founding(
            IReadOnlyList<(BuildingKind Kind, Hex Tile)> startBuildings,
            IReadOnlyList<(int Cost, IReadOnlyList<Hex> Reveals)> surveys,
            string? expeditionIsland,
            Hex? expeditionFinds)
        {
            StartBuildings = startBuildings;
            Surveys = surveys;
            ExpeditionIsland = expeditionIsland;
            ExpeditionFinds = expeditionFinds;
        }

        /// <summary>The buildings a new world starts with, the storehouse first: on the start, unless the start is a mountain.</summary>
        public IReadOnlyList<(BuildingKind Kind, Hex Tile)> StartBuildings { get; }

        /// <summary>What each survey lifts the fog from, in order, and what it costs in Exploration.</summary>
        public IReadOnlyList<(int Cost, IReadOnlyList<Hex> Reveals)> Surveys { get; }

        /// <summary>The island the first expedition finds, or null when the map has none worth the journey.</summary>
        public string? ExpeditionIsland { get; }

        /// <summary>A tile of the island the first expedition finds: its anchor, or null when there is none.</summary>
        public Hex? ExpeditionFinds { get; }

        /// <summary>The founding for a map.</summary>
        public static Founding For(WorldMap map)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return map.Hash == PrototypeHome.Map.Hash ? Prototype(map) : Derive(map);
        }

        /// <summary>
        /// Whether an island is big enough and rich enough for an expedition to find: at least
        /// <see cref="ExpeditionIslandLand"/> land tiles and some ground richer than normal (map-validation.md).
        /// </summary>
        public static bool WorthAnExpedition(WorldMap map, string island)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var land = map.Features.LandOf(island);
            if (land.Count < ExpeditionIslandLand)
            {
                return false;
            }

            foreach (var hex in land)
            {
                if (map.RichnessAt(hex) > 100)
                {
                    return true;
                }
            }

            return false;
        }

        private static Founding Prototype(WorldMap map) =>
            new(PrototypeIsland.StartBuildings, PrototypeIsland.Surveys, map.IslandAt(PrototypeIsland.ExpeditionFinds), PrototypeIsland.ExpeditionFinds);

        private static Founding Derive(WorldMap map)
        {
            var features = map.Features;
            string home = map.IslandAt(map.Start) ?? throw new ArgumentException("A map starts on land.", nameof(map));
            var land = features.LandOf(home);
            var coast = Coast(map, land);
            var taken = new HashSet<Hex>();
            var buildings = new List<(BuildingKind, Hex)>();
            foreach (var (kind, site) in s_village)
            {
                // On plain land where it can, so the village keeps the woods, fields and pasture the surveys reveal.
                var candidates = site == TileUse.FishingWater ? coast : land;
                Func<Hex, bool> fits = h => (features.UsesOf(h) & site) != 0 && !taken.Contains(h);
                if ((Nearest(map, candidates, h => fits(h) && IsPlain(map.TerrainAt(h))) ?? Nearest(map, candidates, fits)) is Hex tile)
                {
                    buildings.Add((kind, tile));
                    taken.Add(tile);
                }
            }

            var crest = features.CrestOf(home);
            var quarryAndWoods = new List<Hex>();
            Reveal(quarryAndWoods, map, land, 1, h => map.TerrainAt(h) == Terrain.Mountain && h != crest, taken);
            Reveal(quarryAndWoods, map, land, 2, h => map.TerrainAt(h) == Terrain.Forest, taken);
            var coastAndFields = new List<Hex>();
            Reveal(coastAndFields, map, land, 1, h => map.TerrainAt(h) == Terrain.Farmland, taken);
            Reveal(coastAndFields, map, coast, 1, features.IsFishingWater, taken);
            var surveys = new (int, IReadOnlyList<Hex>)[]
            {
                (PrototypeIsland.Surveys[0].Cost, quarryAndWoods),
                (PrototypeIsland.Surveys[1].Cost, coastAndFields),
            };

            string? island = ExpeditionIslandOf(map, home, land);
            Hex? finds = null;
            foreach (var candidate in map.Islands)
            {
                if (candidate.Id == island)
                {
                    finds = candidate.Anchor;
                }
            }

            return new Founding(buildings, surveys, island, finds);
        }

        /// <summary>
        /// The nearest island worth an expedition by sea from the start island's harbour spots, the first in name order
        /// when two are as near, as the map validator finds the first expedition's island (map-validation.md).
        /// </summary>
        private static string? ExpeditionIslandOf(WorldMap map, string home, IReadOnlyList<Hex> land)
        {
            var features = map.Features;
            var harbours = new List<Hex>();
            foreach (var hex in land)
            {
                if (features.IsHarborSpot(hex))
                {
                    harbours.Add(hex);
                }
            }

            var steps = features.SeaStepsFrom(harbours);
            string? nearest = null;
            int fewest = int.MaxValue;
            foreach (var island in map.Islands)
            {
                if (island.Id == home || !WorthAnExpedition(map, island.Id))
                {
                    continue;
                }

                int distance = int.MaxValue;
                foreach (var hex in features.LandOf(island.Id))
                {
                    if (features.IsHarborSpot(hex) && steps.TryGetValue(hex, out int n) && n < distance)
                    {
                        distance = n;
                    }
                }

                if (distance < fewest || (distance == fewest && distance != int.MaxValue && string.CompareOrdinal(island.Id, nearest) < 0))
                {
                    (nearest, fewest) = (island.Id, distance);
                }
            }

            return nearest;
        }

        /// <summary>Land that is neither woods, fields, pasture nor mountain, and so is not scarce at the start.</summary>
        private static bool IsPlain(Terrain terrain) => terrain == Terrain.Grassland || terrain == Terrain.Beach;

        /// <summary>The sea tiles next to the land, in map order.</summary>
        private static List<Hex> Coast(WorldMap map, IReadOnlyList<Hex> land)
        {
            var coast = new SortedSet<Hex>();
            foreach (var hex in land)
            {
                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    var next = hex.Neighbour(direction);
                    if (map.Contains(next) && !map.TerrainAt(next).IsLand())
                    {
                        coast.Add(next);
                    }
                }
            }

            return new List<Hex>(coast);
        }

        /// <summary>The tile nearest the start that <paramref name="fits"/>, the first in map order when two are as near.</summary>
        private static Hex? Nearest(WorldMap map, IReadOnlyList<Hex> tiles, Func<Hex, bool> fits)
        {
            Hex? nearest = null;
            foreach (var hex in tiles)
            {
                if (fits(hex) && (nearest is not Hex best || Closer(map, hex, best)))
                {
                    nearest = hex;
                }
            }

            return nearest;
        }

        /// <summary>Hides up to <paramref name="count"/> tiles that <paramref name="fit"/>, the farthest from the start first.</summary>
        private static void Reveal(List<Hex> reveals, WorldMap map, IReadOnlyList<Hex> tiles, int count, Func<Hex, bool> fit, HashSet<Hex> taken)
        {
            var fitting = new List<Hex>();
            foreach (var hex in tiles)
            {
                if (fit(hex) && !taken.Contains(hex))
                {
                    fitting.Add(hex);
                }
            }

            // Farthest first; equally far tiles in map order, so the sort has a full order (docs/development.md#determinism).
            fitting.Sort((a, b) =>
            {
                int farther = b.DistanceTo(map.Start).CompareTo(a.DistanceTo(map.Start));
                return farther != 0 ? farther : a.CompareTo(b);
            });
            for (int i = 0; i < count && i < fitting.Count; i++)
            {
                reveals.Add(fitting[i]);
                taken.Add(fitting[i]);
            }
        }

        /// <summary>Whether <paramref name="a"/> is nearer the start than <paramref name="b"/>, or as near and first in map order.</summary>
        private static bool Closer(WorldMap map, Hex a, Hex b)
        {
            int da = a.DistanceTo(map.Start);
            int db = b.DistanceTo(map.Start);
            return da != db ? da < db : a < b;
        }
    }
}
