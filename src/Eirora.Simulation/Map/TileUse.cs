using System;

namespace Eirora.Simulation.Map
{
    /// <summary>
    /// What a tile is good for, which follows from its terrain, height and neighbours (D-79, economy-v1.md, section 3.1).
    /// Each building requires one of them of its building site.
    /// </summary>
    [Flags]
    public enum TileUse
    {
        None = 0,

        /// <summary>Any land tile except a mountain and the crest.</summary>
        Land = 1,

        /// <summary>Land with forest on or next to it, for a woodcutter; it also needs forest it can fell (D-93).</summary>
        Forest = 2,

        Farmland = 4,

        Pasture = 8,

        /// <summary>A mountain other than the crest, for a quarry.</summary>
        Mountain = 16,

        /// <summary>The start island's single highest tile, which bears only the Vårdkase (D-92).</summary>
        Crest = 32,

        /// <summary>Shallow water next to land, where a fishing hut stands on stilts (D-88).</summary>
        FishingWater = 64,

        /// <summary>Land except a mountain next to shallow water that reaches the deep sea, for a jetty (D-89).</summary>
        HarborSpot = 128,
    }
}
