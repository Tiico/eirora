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
            Recipe? recipe = null,
            int step = 0,
            PopulationTier tier = PopulationTier.Crofters,
            PopulationTier houses = PopulationTier.Crofters,
            int handBuildDays = 0,
            KnarrSpec? knarrs = null)
        {
            Cost = cost;
            WorkHours = workHours;
            BuildPower = buildPower;
            Staff = staff;
            Requirement = requirement;
            Housing = housing;
            MaxLevel = maxLevel;
            Recipe = recipe;
            Step = step;
            Tier = tier;
            Houses = houses;
            HandBuildDays = handBuildDays;
            Knarrs = knarrs;

            // By resource rather than over the cost, whose order is the dictionary's (docs/development.md#determinism).
            foreach (Resource resource in Enum.GetValues(typeof(Resource)))
            {
                if (cost.TryGetValue(resource, out int amount) && amount < 1)
                {
                    throw new ArgumentException("Every resource a building costs is at least one unit.", nameof(cost));
                }
            }

            if (workHours < 1 || buildPower < 0 || staff < 0 || housing < 0 || maxLevel < 1 || step < 0 || handBuildDays < 0)
            {
                throw new ArgumentException("A building takes work, at least level 1 and nothing negative.");
            }

            if ((recipe is not null || knarrs is not null) && staff < 1)
            {
                throw new ArgumentException("A building with a recipe or knarrs needs staff, since only a staffed building produces.", nameof(staff));
            }

            if (recipe is not null && knarrs is not null)
            {
                throw new ArgumentException("A building makes goods or builds knarrs, never both.", nameof(knarrs));
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

        /// <summary>The unlocked step from which it can be built (economy-v2.md, section 5); 0 from the start.</summary>
        public int Step { get; }

        /// <summary>The lowest population tier whose villagers can staff it; every higher tier can too (economy-v2.md, section 2).</summary>
        public PopulationTier Tier { get; }

        /// <summary>The population tier its <see cref="Housing"/> is for.</summary>
        public PopulationTier Houses { get; }

        /// <summary>
        /// The game days it takes to build by hand, without its Build Power and the free villagers' work (GLOSSARY:
        /// Handkraft; D-65); 0 when it cannot be built by hand.
        /// </summary>
        public int HandBuildDays { get; }

        /// <summary>
        /// Whether it can be built by hand in a fixed time (<see cref="HandBuildDays"/>), which the content validator
        /// counts as made without forces (INV-R4).
        /// </summary>
        public bool HandBuild => HandBuildDays > 0;

        /// <summary>The knarrs it builds, one at a time, or null for every building but the shipyard (economy-v2.md, section 4).</summary>
        public KnarrSpec? Knarrs { get; }

        /// <summary>Whether it produces, so that it can be upgraded, given a road and worked.</summary>
        public bool Produces => Recipe is not null;

        /// <summary>Whether its staff work at it, producing or building knarrs, so that the player can pause it (D-39).</summary>
        public bool Pausable => Recipe is not null || Knarrs is not null;
    }

    /// <summary>
    /// The knarrs a shipyard builds (GLOSSARY: Knarr; economy-v2.md, section 4): one at a time, paid from its island's store
    /// when it is begun, each carrying <see cref="UnitsPerGameDay"/> more a game day on the sea routes to and from the
    /// island, at most <see cref="Most"/> on a route. A knarr is never lost (INV-V1).
    /// </summary>
    public sealed class KnarrSpec
    {
        internal KnarrSpec(IReadOnlyDictionary<Resource, int> cost, int workHours, int unitsPerGameDay, int most)
        {
            foreach (Resource resource in Enum.GetValues(typeof(Resource)))
            {
                if (cost.TryGetValue(resource, out int amount) && amount < 1)
                {
                    throw new ArgumentException("Every resource a knarr costs is at least one unit.", nameof(cost));
                }
            }

            if (workHours < 1 || unitsPerGameDay < 1 || most < 1)
            {
                throw new ArgumentException("A knarr takes work and carries something, and a route has room for one.");
            }

            Cost = cost;
            WorkHours = workHours;
            UnitsPerGameDay = unitsPerGameDay;
            Most = most;
        }

        /// <summary>Whole units of each resource, paid from the shipyard's island's store when the knarr is begun.</summary>
        public IReadOnlyDictionary<Resource, int> Cost { get; }

        /// <summary>Active hours of the staffed shipyard's work on each knarr.</summary>
        public int WorkHours { get; }

        /// <summary>What each knarr adds to a sea route a game day, both ways together.</summary>
        public int UnitsPerGameDay { get; }

        /// <summary>The most knarrs that carry on one sea route, and that one island's shipyards build.</summary>
        public int Most { get; }
    }
}
