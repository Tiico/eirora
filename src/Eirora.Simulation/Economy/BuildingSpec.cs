using System;
using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>What a building costs, needs and does (economy-v1.md, section 3), as the content catalog gives it.</summary>
    /// <remarks>
    /// Content is entered here, so a spec refuses what could never work: a negative amount, which a queued build would
    /// add to the store instead of paying, no work, no level, or a recipe without villagers to run it, since only a staffed
    /// building produces.
    /// </remarks>
    public sealed class BuildingSpec
    {
        internal BuildingSpec(
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

            // By resource rather than over the cost, whose order is the dictionary's (docs/development.md#determinism).
            foreach (Resource resource in Enum.GetValues(typeof(Resource)))
            {
                if (cost.TryGetValue(resource, out int amount) && amount < 1)
                {
                    throw new ArgumentException("Every resource a building costs is at least one unit.", nameof(cost));
                }
            }

            if (workHours < 1 || buildPower < 0 || staff < 0 || housing < 0 || maxLevel < 1)
            {
                throw new ArgumentException("A building takes work, at least level 1 and nothing negative.");
            }

            if (recipe is not null && staff < 1)
            {
                throw new ArgumentException("A building with a recipe needs staff, since only a staffed building produces.", nameof(staff));
            }
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
