using System;
using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>What a building costs, needs and does (economy-v1.md, section 3).</summary>
    public sealed class BuildingSpec
    {
        private static readonly Dictionary<BuildingKind, BuildingSpec> s_catalog = new()
        {
            [BuildingKind.Longhouse] = new(Materials(timber: 10, planks: 15, stone: 10), workHours: 40, buildPower: 60, staff: 0, TileUse.Land, housing: 8),
            [BuildingKind.Cottage] = new(Materials(timber: 15, stone: 5), workHours: 32, buildPower: 0, staff: 0, TileUse.Land, housing: 4),
            [BuildingKind.Storehouse] = new(Materials(timber: 10, stone: 5), workHours: 16, buildPower: 0, staff: 1, TileUse.Land),
            [BuildingKind.FishingHut] = new(Materials(timber: 8), workHours: 12, buildPower: 0, staff: 2, TileUse.FishingWater, maxLevel: 2),
            [BuildingKind.Field] = new(Materials(timber: 4), workHours: 8, buildPower: 0, staff: 2, TileUse.Farmland, maxLevel: 2),
            [BuildingKind.Woodcutter] = new(Materials(timber: 6), workHours: 8, buildPower: 0, staff: 2, TileUse.Forest, maxLevel: 2),
            [BuildingKind.SheepFarm] = new(Materials(timber: 10, planks: 5), workHours: 12, buildPower: 0, staff: 1, TileUse.Pasture, maxLevel: 2),
            [BuildingKind.Quarry] = new(Materials(timber: 10, planks: 4), workHours: 16, buildPower: 0, staff: 2, TileUse.Mountain, maxLevel: 2),
            [BuildingKind.Sawmill] = new(Materials(timber: 15, stone: 5), workHours: 24, buildPower: 30, staff: 2, TileUse.Land, maxLevel: 2),
            [BuildingKind.Weavery] = new(Materials(planks: 10, stone: 5), workHours: 24, buildPower: 60, staff: 2, TileUse.Land, maxLevel: 2),
            [BuildingKind.Jetty] = new(Materials(timber: 15, planks: 10), workHours: 24, buildPower: 60, staff: 1, TileUse.HarborSpot),
            [BuildingKind.Lookout] = new(Materials(stone: 10), workHours: 12, buildPower: 40, staff: 0, TileUse.Land),
        };

        private BuildingSpec(IReadOnlyDictionary<Resource, int> cost, int workHours, int buildPower, int staff, TileUse requirement, int housing = 0, int maxLevel = 1)
        {
            Cost = cost;
            WorkHours = workHours;
            BuildPower = buildPower;
            Staff = staff;
            Requirement = requirement;
            Housing = housing;
            MaxLevel = maxLevel;
        }

        /// <summary>Upgrading any producing building to level 2: +50 % production.</summary>
        public static BuildingSpec Upgrade { get; } = new(Materials(planks: 10, stone: 5), workHours: 16, buildPower: 50, staff: 0, TileUse.None);

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

        public static BuildingSpec For(BuildingKind kind) =>
            s_catalog.TryGetValue(kind, out var spec) ? spec : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building.");

        private static Dictionary<Resource, int> Materials(int timber = 0, int planks = 0, int stone = 0)
        {
            var cost = new Dictionary<Resource, int>();
            if (timber > 0)
            {
                cost[Resource.Timber] = timber;
            }

            if (planks > 0)
            {
                cost[Resource.Planks] = planks;
            }

            if (stone > 0)
            {
                cost[Resource.Stone] = stone;
            }

            return cost;
        }
    }
}
