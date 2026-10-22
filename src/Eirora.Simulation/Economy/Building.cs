using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>A finished building in the settlement, on its building site.</summary>
    public sealed class Building
    {
        internal Building(BuildingKind kind, Hex tile, IslandId island)
        {
            Kind = kind;
            Tile = tile;
            Island = island;
        }

        public BuildingKind Kind { get; }

        /// <summary>The tile it stands on; a fishing hut stands on stilts in the shallow water (D-88).</summary>
        public Hex Tile { get; }

        /// <summary>The island it belongs to; goods travel between islands only by sea route.</summary>
        public IslandId Island { get; }

        /// <summary>1, or 2 after an upgrade (+50 % production).</summary>
        public int Level { get; internal set; } = 1;

        /// <summary>Whether enough villagers work here for it to run.</summary>
        public bool Staffed { get; internal set; }

        /// <summary>A road to the nearest storehouse raises the distance class one step.</summary>
        public bool HasRoad { get; internal set; }

        /// <summary>Whether Dawn Power gives it a workday today (+50 %).</summary>
        public bool WorkdayToday { get; internal set; }

        /// <summary>Grain or wool that has grown and waits for the villagers to harvest it.</summary>
        internal long Ripe { get; set; }
    }
}
