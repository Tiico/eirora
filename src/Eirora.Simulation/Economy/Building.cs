namespace Eirora.Simulation.Economy
{
    /// <summary>A finished building in the settlement.</summary>
    public sealed class Building
    {
        internal Building(BuildingKind kind, Site site)
        {
            Kind = kind;
            Site = site;
        }

        public BuildingKind Kind { get; }

        public Site Site { get; }

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
