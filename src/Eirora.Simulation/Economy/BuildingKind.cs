namespace Eirora.Simulation.Economy
{
    /// <summary>The buildings of economy v1 (economy-v1.md, section 3).</summary>
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

        /// <summary>Timber from the forest, which grows back (huggarkoja).</summary>
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

        /// <summary>Surveys and expeditions cost a quarter less Exploration (utkiksvarde).</summary>
        Lookout,
    }
}
