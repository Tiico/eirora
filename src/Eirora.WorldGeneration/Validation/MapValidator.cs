using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eirora.Simulation.Map;
using static Eirora.WorldGeneration.Validation.ValidationLimits;

namespace Eirora.WorldGeneration.Validation
{
    /// <summary>
    /// Decides whether a map gives a good start and good ways to grow (TASK-030, ROADMAP M3): a measure and a limit per
    /// rule, from economy v1, epoch 1 and the archipelago's needs in ADR-0010. The validator comes before the generator,
    /// so the generator is built against a measurable goal; docs/design/map-validation.md explains each rule.
    /// </summary>
    public static class MapValidator
    {
        public static MapReport Validate(WorldMap map)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var features = new MapFeatures(map);
            string start = map.IslandAt(map.Start) ?? throw new InvalidOperationException("Every map starts on land.");
            var harbours = features.LandOf(start).Where(features.IsHarborSpot).ToList();
            var steps = features.SeaStepsFrom(harbours);
            var rules = new List<RuleResult>();

            // The archipelago (ADR-0010).
            rules.Add(Exactly("radius", map.Radius, Radius, string.Empty));
            int shore = Enumerable.Range(0, map.Disc.Count).Select(i => map.Disc[i])
                .Count(hex => hex.DistanceTo(Hex.Origin) > map.Radius - DeepSeaRings && map.TerrainAt(hex) != Terrain.DeepSea);
            rules.Add(Exactly("deep-sea-edge", shore, 0, "tiles in the outer rings that are not deep sea"));
            var withoutHarbour = map.Islands.Where(i => !features.LandOf(i.Id).Any(features.IsHarborSpot)).Select(i => i.Id).ToList();
            rules.Add(Exactly("islands-have-harbours", withoutHarbour.Count, 0, string.Join(",", withoutHarbour)));
            var edge = Enumerable.Range(0, map.Disc.Count).Select(i => map.Disc[i])
                .Where(h => h.DistanceTo(Hex.Origin) == map.Radius && steps.ContainsKey(h)).Select(h => steps[h]).DefaultIfEmpty(-1).Min();
            rules.Add(new RuleResult("fairway-to-edge", edge, ">=0", edge >= 0, "steps by sea from the start island's harbours to the edge"));

            // In name order, so they read the same on every runtime (docs/development.md#determinism).
            var distances = map.Islands.Where(i => i.Id != start)
                .Select(i => (i.Id, Steps: IslandDistance(features, steps, i.Id)))
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .ToList();
            var unreachable = distances.Where(d => d.Steps < 0).Select(d => d.Id).ToList();
            rules.Add(Exactly("islands-reachable", unreachable.Count, 0, string.Join(",", unreachable)));

            // The start (economy v1).
            var land = features.LandOf(start);
            rules.Add(Within("start-land", land.Count, StartLandMinimum, StartLandMaximum, string.Empty));
            rules.Add(AtLeast("start-grassland", land.Count(h => map.TerrainAt(h) == Terrain.Grassland), StartGrassland, "houses, storehouses, sawmill, weavery, lookout"));
            rules.Add(AtLeast("start-forest", land.Count(h => map.TerrainAt(h) == Terrain.Forest), StartForest, "woodcutter"));
            rules.Add(AtLeast("start-farmland", land.Count(h => map.TerrainAt(h) == Terrain.Farmland), StartFarmland, "field"));
            rules.Add(AtLeast("start-pasture", land.Count(h => map.TerrainAt(h) == Terrain.Pasture), StartPasture, "sheep farm"));
            rules.Add(AtLeast("start-mountain", land.Count(h => map.TerrainAt(h) == Terrain.Mountain), StartMountain, "quarry"));
            int fishing = Coast(map, land).Count(features.IsFishingWater);
            rules.Add(AtLeast("start-fishing-water", fishing, StartFishingWater, "fishing hut"));
            rules.Add(AtLeast("start-harbours", harbours.Count, StartHarbours, "jetty"));
            var crest = features.CrestOf(start);
            long crestHeight = crest is Hex top ? map.HeightAt(top) : -1;
            rules.Add(new RuleResult("crest", crestHeight, $">={CrestHeight}", crestHeight >= CrestHeight, crest is Hex at ? $"at {at.Q},{at.R}" : "the top is shared"));

            // Ways to grow (epoch 1).
            var first = Nearest(distances, FirstIslandMinimum, FirstIslandMaximum, except: null);
            var second = Nearest(distances, SecondIslandMinimum, SecondIslandMaximum, except: first?.Id);
            string others = string.Join(",", distances.Select(d => $"{d.Id}={d.Steps.ToString(CultureInfo.InvariantCulture)}"));
            rules.Add(Found("first-island", first, FirstIslandMinimum, FirstIslandMaximum, others));
            rules.Add(Found("second-island", second, SecondIslandMinimum, SecondIslandMaximum, others));
            int innerRadius = new[] { first, second }.Where(i => i is not null).Select(i => Reach(map, i!.Value.Id)).DefaultIfEmpty(SecondIslandMaximum).Max();
            int innerSea = CountInnerSea(map, land, innerRadius);
            rules.Add(AtLeast("inner-sea", innerSea, InnerSea, $"sea tiles within {innerRadius.ToString(CultureInfo.InvariantCulture)} of the start, off its coast"));
            var tradeOffs = FindTradeOffs(map, features);
            rules.Add(AtLeast("trade-offs", tradeOffs.Count, TradeOffs, string.Join(",", tradeOffs)));

            return new MapReport(rules);
        }

        /// <summary>Steps by sea from the start island's harbours to the nearest harbour of an island, or -1.</summary>
        private static int IslandDistance(MapFeatures features, IReadOnlyDictionary<Hex, int> steps, string island) =>
            features.LandOf(island).Where(features.IsHarborSpot).Select(h => steps.TryGetValue(h, out int n) ? n : -1).Where(n => n >= 0).DefaultIfEmpty(-1).Min();

        /// <summary>The nearest island within the limits, the first in name order when two are as near.</summary>
        private static (string Id, int Steps)? Nearest(List<(string Id, int Steps)> distances, int minimum, int maximum, string? except)
        {
            var found = distances
                .Where(d => d.Id != except && d.Steps >= minimum && d.Steps <= maximum)
                .OrderBy(d => d.Steps).ThenBy(d => d.Id, StringComparer.Ordinal)
                .ToList();
            return found.Count > 0 ? found[0] : null;
        }

        /// <summary>How far from the start the far side of an island lies, which bounds the inner sea.</summary>
        private static int Reach(WorldMap map, string island) =>
            Enumerable.Range(0, map.Disc.Count).Select(i => map.Disc[i]).Where(h => map.IslandAt(h) == island).Select(h => h.DistanceTo(map.Start)).Max();

        /// <summary>Sea within <paramref name="radius"/> of the start that is not next to the start island, which is seen from home.</summary>
        private static int CountInnerSea(WorldMap map, IReadOnlyList<Hex> startLand, int radius)
        {
            var coast = new HashSet<Hex>(Coast(map, startLand));
            return map.Start.Within(radius).Count(h => map.Contains(h) && !map.TerrainAt(h).IsLand() && !coast.Contains(h));
        }

        /// <summary>The sea tiles next to some land, in map order.</summary>
        private static SortedSet<Hex> Coast(WorldMap map, IReadOnlyList<Hex> land)
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

            return coast;
        }

        /// <summary>
        /// The kinds of ground where a richer tile lies farther from the start than the nearest ordinary one, so the
        /// player weighs richness against distance (MAP-03).
        /// </summary>
        private static List<string> FindTradeOffs(WorldMap map, MapFeatures features)
        {
            var kinds = new (string Name, Func<Hex, bool> Is)[]
            {
                ("forest", h => map.TerrainAt(h) == Terrain.Forest),
                ("stone", h => map.TerrainAt(h) == Terrain.Mountain),
                ("fish", features.IsFishingWater),
                ("wool", h => map.TerrainAt(h) == Terrain.Pasture),
                ("grain", h => map.TerrainAt(h) == Terrain.Farmland),
            };

            var tiles = Enumerable.Range(0, map.Disc.Count).Select(i => map.Disc[i]).ToList();
            var found = new List<string>();
            foreach (var (name, isKind) in kinds)
            {
                var ofKind = tiles.Where(isKind).ToList();
                int nearestOrdinary = ofKind.Where(h => map.RichnessAt(h) <= 100).Select(h => h.DistanceTo(map.Start)).DefaultIfEmpty(int.MaxValue).Min();
                if (ofKind.Any(h => map.RichnessAt(h) > 100 && h.DistanceTo(map.Start) > nearestOrdinary))
                {
                    found.Add(name);
                }
            }

            return found;
        }

        private static RuleResult Exactly(string rule, long value, long expected, string detail) =>
            new(rule, value, "=" + expected.ToString(CultureInfo.InvariantCulture), value == expected, detail);

        private static RuleResult AtLeast(string rule, long value, long minimum, string detail) =>
            new(rule, value, ">=" + minimum.ToString(CultureInfo.InvariantCulture), value >= minimum, detail);

        private static RuleResult Within(string rule, long value, long minimum, long maximum, string detail) =>
            new(rule, value, $"{minimum.ToString(CultureInfo.InvariantCulture)}..{maximum.ToString(CultureInfo.InvariantCulture)}", value >= minimum && value <= maximum, detail);

        private static RuleResult Found(string rule, (string Id, int Steps)? island, int minimum, int maximum, string others) =>
            island is { } found
                ? Within(rule, found.Steps, minimum, maximum, found.Id)
                : new RuleResult(rule, -1, $"{minimum.ToString(CultureInfo.InvariantCulture)}..{maximum.ToString(CultureInfo.InvariantCulture)}", false, others.Length > 0 ? "none; " + others : "none");
    }
}
