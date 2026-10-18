using System;

namespace Eirora.Simulation.Economy
{
    /// <summary>What a site on the map allows (economy-v1.md, section 3).</summary>
    [Flags]
    public enum SiteKind
    {
        None = 0,
        Land = 1,
        FishingWater = 2,
        HarborSpot = 4,
        Farmland = 8,
        Forest = 16,
        Mountain = 32,
        Pasture = 64,
    }
}
