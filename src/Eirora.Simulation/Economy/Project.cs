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
        internal Project(ProjectKind kind, BuildingKind building, Site site, long workLeft)
        {
            Kind = kind;
            Building = building;
            Site = site;
            WorkLeft = workLeft;
        }

        public ProjectKind Kind { get; }

        public BuildingKind Building { get; }

        public Site Site { get; }

        /// <summary>Remaining work in thousandths of a villager-hour.</summary>
        public long WorkLeft { get; internal set; }
    }
}
