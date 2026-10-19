using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.WorldGeneration.Validation
{
    /// <summary>
    /// What the validator worked out about a map, beside its report: the start island and its harbours, the crest, the
    /// islands chosen for epoch 1's expeditions, the distances by sea and the inner sea (TASK-030, TASK-034).
    /// </summary>
    public sealed class MapAnalysis
    {
        internal MapAnalysis(
            WorldMap map,
            MapFeatures features,
            string startIsland,
            IReadOnlyList<Hex> startHarbours,
            Hex? crest,
            string? firstIsland,
            string? secondIsland,
            IReadOnlyDictionary<string, int> distances,
            IReadOnlyList<Hex> innerSea,
            MapReport report)
        {
            Map = map;
            Features = features;
            StartIsland = startIsland;
            StartHarbours = startHarbours;
            Crest = crest;
            FirstIsland = firstIsland;
            SecondIsland = secondIsland;
            Distances = distances;
            InnerSea = innerSea;
            Report = report;
        }

        public WorldMap Map { get; }

        public MapFeatures Features { get; }

        public string StartIsland { get; }

        public IReadOnlyList<Hex> StartHarbours { get; }

        /// <summary>The start island's single highest tile, or none when the top is shared.</summary>
        public Hex? Crest { get; }

        /// <summary>The island for the first expedition, like Nyön, if one lies within the limits.</summary>
        public string? FirstIsland { get; }

        /// <summary>The island for the second expedition, like Ytterön, if one lies within the limits.</summary>
        public string? SecondIsland { get; }

        /// <summary>Steps by sea from the start island's harbours to each other island's nearest harbour, -1 if none.</summary>
        public IReadOnlyDictionary<string, int> Distances { get; }

        /// <summary>The sea tiles of the inner sea, whose fog the Vårdkase's fire needs lifted.</summary>
        public IReadOnlyList<Hex> InnerSea { get; }

        public MapReport Report { get; }
    }
}
