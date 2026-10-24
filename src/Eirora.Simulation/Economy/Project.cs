using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>What a queued project produces when its work is done.</summary>
    public enum ProjectKind
    {
        Build,
        Upgrade,
        Road,
    }

    /// <summary>Work in the building queue; materials and forces are paid when it is queued.</summary>
    public sealed class Project
    {
        internal Project(ProjectKind kind, BuildingKind? building, Hex tile, long workLeft)
        {
            Kind = kind;
            Building = building;
            Tile = tile;
            WorkLeft = workLeft;
        }

        public ProjectKind Kind { get; }

        /// <summary>What is built or upgraded; null for a road, which lies on a tile of its own (D-112).</summary>
        public BuildingKind? Building { get; }

        /// <summary>The building site, the building an upgrade is for, or the tile a road is laid on.</summary>
        public Hex Tile { get; }

        /// <summary>Remaining work in thousandths of a villager-hour.</summary>
        public long WorkLeft { get; internal set; }
    }
}
