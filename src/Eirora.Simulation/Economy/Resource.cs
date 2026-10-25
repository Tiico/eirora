namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The goods of economy v1 (docs/design/economy-v1.md, section 2) and of the unlocked steps of economy v2
    /// (economy-v2.md, section 3), each added after the others so that a save's names keep their meaning.
    /// </summary>
    public enum Resource
    {
        /// <summary>Food from fishing huts (fisk).</summary>
        Fish,

        /// <summary>Food from fields, a slow process (säd).</summary>
        Grain,

        /// <summary>Building material from woodcutters (timmer).</summary>
        Timber,

        /// <summary>Building material from sawmills (plankor).</summary>
        Planks,

        /// <summary>Building material from quarries (sten).</summary>
        Stone,

        /// <summary>Raw material from sheep farms, a slow process (ull).</summary>
        Wool,

        /// <summary>Cloth from weaveries: clothes for farmers, and comfort that speeds up growth (tyg).</summary>
        Wadmal,

        /// <summary>Food and the raw material of hide, from hunting lodges (vilt; step 1, Farmers).</summary>
        Game,

        /// <summary>Clothes from tanneries, as wadmal is (skinn; step 1, Farmers).</summary>
        Hide,
    }
}
