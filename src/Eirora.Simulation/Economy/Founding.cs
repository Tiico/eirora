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
    /// a storehouse, a fishing hut and a field as near the map's start as their sites allow, on plain land where they can,
    /// and a woodcutter where the woods it reaches are richest near the storehouse (TASK-064); a first survey that
    /// reveals a quarry and two forest tiles, and a second that reveals farmland and fishing water, the farthest of their
    /// kind from the start; and the nearest island worth an expedition within
    /// <see cref="FirstIslandMinimum"/>..<see cref="FirstIslandMaximum"/> steps by sea, which is the island the map
    /// validator counts as the first (<see cref="FirstIslandOf"/>).
    /// <para>
    /// Because the founding is never stored, a save on a generated map reads its surveys and its expedition's island
    /// from this rule. A change to them changes worlds already played, so it needs a new version of the rule beside the
    /// old one, chosen by the map's generator and version (ADR-0010); the golden foundings fail on any change. The start
    /// buildings are read only when a world is created, and from then on stand in its state, so a change to where they
    /// stand changes only new worlds.
    /// </para>
    /// </remarks>
    public sealed class Founding
    {
        /// <summary>Land tiles an expedition's island has at least, besides some rich ground; a skerry never counts (Nyön has 6).</summary>
        public const int ExpeditionIslandLand = 6;

        /// <summary>Steps by sea from the start island to the first expedition's island (Nyön lies 10 away in epoch 1's model).</summary>
        public const int FirstIslandMinimum = 8;

        /// <summary>The most steps by sea to the first expedition's island.</summary>
        public const int FirstIslandMaximum = 12;

        /// <summary>Steps over land from the storehouse within which the start village's woodcutter looks for the richest woods.</summary>
        public const int WoodcutterReach = 3;

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

        /// <summary>
        /// The island the first expedition finds on a map that is not the prototype's: the nearest worth an expedition
        /// within <see cref="FirstIslandMinimum"/>..<see cref="FirstIslandMaximum"/> steps by sea from the start
        /// island's harbour spots, the first in name order when two are as near; null when there is none. The map
        /// validator's first island is this one (map-validation.md).
        /// </summary>
        public static string? FirstIslandOf(WorldMap map)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            string home = map.IslandAt(map.Start) ?? throw new ArgumentException("A map starts on land.", nameof(map));
            return ExpeditionIslandOf(map, home, map.Features.LandOf(home));
        }

        private static Founding Prototype(WorldMap map) =>
            new(PrototypeIsland.StartBuildings, PrototypeIsland.Surveys, map.IslandAt(PrototypeIsland.ExpeditionFinds), PrototypeIsland.ExpeditionFinds);

        private static Founding Derive(WorldMap map)
        {
            var features = map.Features;
            string home = map.IslandAt(map.Start) ?? throw new ArgumentException("A map starts on land.", nameof(map));
            var land = features.LandOf(home);
            var coast = Coast(map, land);

            // The surveys keep the tiles they have always had, chosen around the village on the nearest sites, so a world
            // already played on a generated map reads the surveys it was founded with (ADR-0010).
            var taken = new HashSet<Hex>();
            foreach (var (_, tile) in NearestVillage(map, land, coast))
            {
                taken.Add(tile);
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

            var hidden = new HashSet<Hex>(quarryAndWoods);
            hidden.UnionWith(coastAndFields);
            var buildings = Village(map, land, coast, hidden);

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
        /// The start village as it stood before TASK-064: every building on the site nearest the start where it can work,
        /// on plain land where it can. Only the surveys are chosen around it now.
        /// </summary>
        private static List<(BuildingKind Kind, Hex Tile)> NearestVillage(WorldMap map, IReadOnlyList<Hex> land, IReadOnlyList<Hex> coast)
        {
            var taken = new HashSet<Hex>();
            var buildings = new List<(BuildingKind, Hex)>();
            foreach (var (kind, site) in s_village)
            {
                if (NearestSite(map, site == TileUse.FishingWater ? coast : land, site, taken) is Hex tile)
                {
                    buildings.Add((kind, tile));
                    taken.Add(tile);
                }
            }

            return buildings;
        }

        /// <summary>
        /// The start village (TASK-064): the storehouse, the fishing hut and the field on the sites nearest the start where
        /// they can work, as before, and the woodcutter where the woods it reaches are richest within
        /// <see cref="WoodcutterReach"/> steps of the storehouse (<see cref="Woods"/>); nearer the storehouse among equals,
        /// then on plain land, then in map order. No building stands on a tile a survey hides. A woodcutter with no site
        /// in reach takes the nearest it has.
        /// </summary>
        /// <remarks>
        /// The fishing hut and the field stay on the nearest sites: generator v1 lays the rich ground out on the islands
        /// (MAP-03), so every fishing water and farmland tile of a generated start island is as rich as the next, and the
        /// richest of them is the nearest. The woods differ in how many of them a site reaches.
        /// </remarks>
        private static List<(BuildingKind Kind, Hex Tile)> Village(WorldMap map, IReadOnlyList<Hex> land, IReadOnlyList<Hex> coast, HashSet<Hex> hidden)
        {
            var taken = new HashSet<Hex>(hidden);
            var buildings = new List<(BuildingKind, Hex)>();
            foreach (var (kind, site) in s_village)
            {
                var candidates = site == TileUse.FishingWater ? coast : land;
                var tile = kind == BuildingKind.Woodcutter && buildings.Count > 0 ? RichestWoods(map, candidates, buildings, taken) : null;
                if ((tile ?? NearestSite(map, candidates, site, taken)) is Hex at)
                {
                    buildings.Add((kind, at));
                    taken.Add(at);
                }
            }

            return buildings;
        }

        /// <summary>The site of <see cref="Village"/>'s woodcutter, or null when none is in reach of the storehouse.</summary>
        private static Hex? RichestWoods(WorldMap map, IReadOnlyList<Hex> land, List<(BuildingKind Kind, Hex Tile)> village, HashSet<Hex> taken)
        {
            var steps = LandSteps(map, village[0].Tile);
            int fells = ContentCatalog.Current.For(BuildingKind.Woodcutter).Recipe!.ForestTiles;
            Hex? chosen = null;
            (long AtOnce, long Lasting, int Steps, int Rank) top = default;
            foreach (var tile in land)
            {
                if ((map.Features.UsesOf(tile) & TileUse.Forest) == 0 || taken.Contains(tile) || !steps.TryGetValue(tile, out int n) || n > WoodcutterReach)
                {
                    continue;
                }

                var (atOnce, lasting) = Woods(map, tile, fells, taken);
                var score = (atOnce, lasting, -n, -Rank(map, tile));
                if (chosen is not Hex best || score.CompareTo(top) > 0 || (score.CompareTo(top) == 0 && tile < best))
                {
                    (chosen, top) = (tile, score);
                }
            }

            return chosen;
        }

        /// <summary>
        /// The richness of the woods a woodcutter on <paramref name="tile"/> fells: forest on its own tile and its
        /// neighbours that no other building stands on and no survey hides, as the simulation fells them (D-93). It fells
        /// the richest <paramref name="fells"/> at once, and the others as those are felled and grow back (economy-v1.md,
        /// section 3.1), so <c>AtOnce</c> is the richness of those it fells first and <c>Lasting</c> that of all of them.
        /// </summary>
        private static (long AtOnce, long Lasting) Woods(WorldMap map, Hex tile, int fells, HashSet<Hex> taken)
        {
            var woods = new List<int>();
            for (int direction = -1; direction < Hex.Directions; direction++)
            {
                var near = direction < 0 ? tile : tile.Neighbour(direction);
                if (map.Contains(near) && map.TerrainAt(near) == Terrain.Forest && (near == tile || !taken.Contains(near)))
                {
                    woods.Add(map.RichnessAt(near));
                }
            }

            woods.Sort((a, b) => b.CompareTo(a));
            long atOnce = 0, lasting = 0;
            for (int i = 0; i < woods.Count; i++)
            {
                atOnce += i < fells ? woods[i] : 0;
                lasting += woods[i];
            }

            return (atOnce, lasting);
        }

        /// <summary>The site nearest the start that <paramref name="site"/> allows and is not taken, on plain land where it can.</summary>
        private static Hex? NearestSite(WorldMap map, IReadOnlyList<Hex> candidates, TileUse site, HashSet<Hex> taken)
        {
            var features = map.Features;
            Func<Hex, bool> fits = h => (features.UsesOf(h) & site) != 0 && !taken.Contains(h);
            return Nearest(map, candidates, h => fits(h) && IsPlain(map.TerrainAt(h))) ?? Nearest(map, candidates, fits);
        }

        /// <summary>Plain land first, so the village keeps woods, fields and pasture where it gives up nothing for it.</summary>
        private static int Rank(WorldMap map, Hex tile) => IsPlain(map.TerrainAt(tile)) ? 0 : 1;

        /// <summary>Steps over the island's land from <paramref name="from"/>, as the carriers walk them.</summary>
        private static Dictionary<Hex, int> LandSteps(WorldMap map, Hex from)
        {
            string? island = map.IslandAt(from);
            var steps = new Dictionary<Hex, int> { [from] = 0 };
            var open = new Queue<Hex>();
            open.Enqueue(from);
            while (open.Count > 0)
            {
                var at = open.Dequeue();
                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    var near = at.Neighbour(direction);
                    if (map.Contains(near) && map.IslandAt(near) == island && !steps.ContainsKey(near))
                    {
                        steps[near] = steps[at] + 1;
                        open.Enqueue(near);
                    }
                }
            }

            return steps;
        }

        /// <summary>See <see cref="FirstIslandOf"/>.</summary>
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

                if (distance < FirstIslandMinimum || distance > FirstIslandMaximum)
                {
                    continue;
                }

                if (distance < fewest || (distance == fewest && string.CompareOrdinal(island.Id, nearest) < 0))
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
