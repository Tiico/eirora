using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>What a building costs, needs and does (economy-v1.md, section 3), as the content catalog gives it.</summary>
    public sealed class BuildingSpec
    {
        public BuildingSpec(
            IReadOnlyDictionary<Resource, int> cost,
            int workHours,
            int buildPower,
            int staff,
            TileUse requirement,
            int housing = 0,
            int maxLevel = 1,
            Recipe? recipe = null)
        {
            Cost = cost;
            WorkHours = workHours;
            BuildPower = buildPower;
            Staff = staff;
            Requirement = requirement;
            Housing = housing;
            MaxLevel = maxLevel;
            Recipe = recipe;
        }

        /// <summary>Whole units of each resource, paid when the work is queued.</summary>
        public IReadOnlyDictionary<Resource, int> Cost { get; }

        /// <summary>Hours of work by free villagers.</summary>
        public int WorkHours { get; }

        public int BuildPower { get; }

        /// <summary>Villagers it takes to run the building.</summary>
        public int Staff { get; }

        /// <summary>What its building site must be good for (economy-v1.md, section 3.1); none for an upgrade.</summary>
        public TileUse Requirement { get; }

        /// <summary>Villagers it can house.</summary>
        public int Housing { get; }

        /// <summary>The highest level it can be upgraded to; 1 when it cannot be upgraded (D-39: levels never decrease).</summary>
        public int MaxLevel { get; }

        /// <summary>What it produces, or null when it produces nothing.</summary>
        public Recipe? Recipe { get; }

        /// <summary>Whether it produces, so that it can be upgraded, given a road and worked.</summary>
        public bool Produces => Recipe is not null;
    }
}
