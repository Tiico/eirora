namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The buildings of economy v1 (economy-v1.md, section 3) and of the unlocked steps of economy v2 (economy-v2.md,
    /// section 4), each added after the others so that a save's names keep their meaning.
    /// </summary>
    public enum BuildingKind
    {
        /// <summary>Housing for eight, costs Build Power (långhus).</summary>
        Longhouse,

        /// <summary>Housing for four, only materials and work, so growth never needs training (stuga, D-43).</summary>
        Cottage,

        /// <summary>Storage and the hub that distance is measured to (förråd).</summary>
        Storehouse,

        /// <summary>Fish from fishing water (fiskebod).</summary>
        FishingHut,

        /// <summary>Grain that ripens day and night and is harvested by day (åker).</summary>
        Field,

        /// <summary>Timber from the forest, which grows back (skogshuggare).</summary>
        Woodcutter,

        /// <summary>Wool that grows day and night and is sheared by day (fårgård).</summary>
        SheepFarm,

        /// <summary>Stone from the mountain (stenbrott).</summary>
        Quarry,

        /// <summary>Planks from timber (sågverk).</summary>
        Sawmill,

        /// <summary>Wadmal from wool (väveri).</summary>
        Weavery,

        /// <summary>A jetty: sea routes and expeditions start here; counts as a storehouse for distance (brygga).</summary>
        Jetty,

        /// <summary>Surveys and expeditions cost a quarter less Exploration (utkikstorn).</summary>
        Lookout,

        /// <summary>Housing for six farmers (bondgård; step 1, Farmers).</summary>
        Farmstead,

        /// <summary>Game from the forest around it, which hunting does not fell (jaktstuga; step 1, Farmers).</summary>
        HuntingLodge,

        /// <summary>Hide from game (garveri; step 1, Farmers).</summary>
        Tannery,

        /// <summary>Housing for eight craftsmen (hantverkargård; step 2, Craftsmen).</summary>
        CraftsmensHouse,

        /// <summary>Honey that grows day and night on a meadow and is gathered by day (bigård; step 2, Craftsmen).</summary>
        Apiary,

        /// <summary>Ale from grain, staffed by farmers (bryggeri; step 2, Craftsmen).</summary>
        Brewery,

        /// <summary>Mead from honey, staffed by farmers (mjöderi; step 2, Craftsmen).</summary>
        Meadery,

        /// <summary>Tar from timber, staffed by farmers (tjärdal; step 3, Seafaring).</summary>
        TarKiln,

        /// <summary>Builds knarrs, which carry more on the sea routes to and from its island, staffed by craftsmen (varv; step 3, Seafaring).</summary>
        Shipyard,
    }
}
