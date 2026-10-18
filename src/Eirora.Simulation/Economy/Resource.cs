namespace Eirora.Simulation.Economy
{
    /// <summary>The goods of economy v1 (docs/design/economy-v1.md, section 2).</summary>
    public enum Resource
    {
        /// <summary>Food from fishing huts (fisk).</summary>
        Fish,

        /// <summary>Food from fields, a slow process (säd).</summary>
        Grain,

        /// <summary>Building material from woodcutters (timmer).</summary>
        Timber,

        /// <summary>Building material from sawmills (plank).</summary>
        Planks,

        /// <summary>Building material from quarries (sten).</summary>
        Stone,

        /// <summary>Raw material from sheep farms, a slow process (ull).</summary>
        Wool,

        /// <summary>Cloth from weaveries; comfort that speeds up growth (vadmal).</summary>
        Wadmal,
    }
}
