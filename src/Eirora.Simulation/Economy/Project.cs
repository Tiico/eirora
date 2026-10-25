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
        internal Project(ProjectKind kind, BuildingKind? building, Hex tile, long workLeft, int daysLeft = 0)
        {
            Kind = kind;
            Building = building;
            Tile = tile;
            WorkLeft = workLeft;
            DaysLeft = daysLeft;
        }

        public ProjectKind Kind { get; }

        /// <summary>What is built or upgraded; null for a road, which lies on a tile of its own (D-112).</summary>
        public BuildingKind? Building { get; }

        /// <summary>The building site, the building an upgrade is for, or the tile a road is laid on.</summary>
        public Hex Tile { get; }

        /// <summary>Remaining work in thousandths of a villager-hour; 0 for a project built by hand.</summary>
        public long WorkLeft { get; internal set; }

        /// <summary>
        /// The dawns left until a building built by hand stands (GLOSSARY: Handkraft; D-65), at least 1 while it is
        /// queued; 0 for work by the free villagers.
        /// </summary>
        public int DaysLeft { get; internal set; }

        /// <summary>
        /// Whether it is built by hand in a fixed time (D-65): the free villagers' work, rushing and the queue's order do
        /// not touch it, and it is finished at a dawn.
        /// </summary>
        public bool ByHand => DaysLeft > 0;
    }
}
