namespace Eirora.WorldGeneration.Validation
{
    /// <summary>
    /// Every limit the validator holds a map to, in one place (TASK-030). They start from epoch 1's model and today's
    /// Hemön (docs/design/map-validation.md) and are tuned when the bulk runs show how generated maps spread.
    /// </summary>
    public static class ValidationLimits
    {
        /// <summary>Every map has the same size (D-80).</summary>
        public const int Radius = 24;

        /// <summary>The outer rings that are deep sea, so worlds side by side in an archipelago have sea between them (ADR-0010).</summary>
        public const int DeepSeaRings = 2;

        /// <summary>The start island's land tiles; Hemön has 46.</summary>
        public const int StartLandMinimum = 40;
        public const int StartLandMaximum = 90;

        /// <summary>Ground on the start island for every building in economy v1, with room to choose.</summary>
        public const int StartGrassland = 8;
        public const int StartForest = 3;
        public const int StartFarmland = 2;
        public const int StartPasture = 1;
        public const int StartFishingWater = 2;
        public const int StartHarbours = 1;

        /// <summary>Mountains to quarry besides the crest, which bears only the Vårdkase (D-92, D-95).</summary>
        public const int StartQuarries = 2;

        /// <summary>The crest the Vårdkase stands on is the start island's single highest tile, and a hill.</summary>
        public const int CrestHeight = 4;

        /// <summary>Steps by sea from Hemön to the first expedition's island (Nyön lies 10 away in epoch 1's model).</summary>
        public const int FirstIslandMinimum = Simulation.Economy.Founding.FirstIslandMinimum;
        public const int FirstIslandMaximum = Simulation.Economy.Founding.FirstIslandMaximum;

        /// <summary>
        /// Land tiles an expedition's island has at least, besides some rich ground; a skerry never counts (Nyön has 6).
        /// </summary>
        public const int ExpeditionIslandLand = Simulation.Economy.Founding.ExpeditionIslandLand;

        /// <summary>Steps by sea to the second expedition's island (Ytterön lies 12 away in epoch 1's model).</summary>
        public const int SecondIslandMinimum = 11;
        public const int SecondIslandMaximum = 16;

        /// <summary>Sea tiles of the inner sea, whose fog the fire of the Vårdkase needs lifted (epoch-1.md).</summary>
        public const int InnerSea = 150;

        /// <summary>Kinds of ground where richer ground lies farther away than ordinary ground (MAP-03).</summary>
        public const int TradeOffs = 3;
    }
}
