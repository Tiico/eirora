using System.Collections.Generic;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>Demolition, the only way anything in the world becomes less (D-38, D-108; economy-v1.md, section 3.2).</summary>
    internal sealed partial class Settlement
    {
        /// <summary>
        /// What demolishing the building on <paramref name="tile"/> would give back and lose, and whether it can be
        /// demolished now; null when no building stands there.
        /// </summary>
        public Demolition? Demolition(Hex tile)
        {
            if (BuildingAt(tile) is not { } building)
            {
                return null;
            }

            var store = _stores[building.Island];
            long capacity = StorageCapacity(building.Island) - Quantity.Units(StorageUnits(building));
            var recipe = _content.For(building.Kind).Recipe;
            bool upgradeQueued = IsQueued(tile, ProjectKind.Upgrade);
            int upgrades = building.Level - 1 + (upgradeQueued ? 1 : 0);

            var refund = new long[s_resourceCount];
            var returned = new long[s_resourceCount];
            var emptied = new long[s_resourceCount];
            var tooMuch = new long[s_resourceCount];
            bool fits = true;
            for (int r = 0; r < s_resourceCount; r++)
            {
                // The goods waiting at the building go to the store with the rest of the island's goods, and must fit there.
                long goods = (recipe is not null && (int)recipe.Product == r ? building.Waiting : 0) + (building.Held?[r] ?? 0);
                long after = store[r] + goods;
                emptied[r] = Quantity.Whole(goods);
                if (after > capacity)
                {
                    tooMuch[r] = (after - capacity + Quantity.PerUnit - 1) / Quantity.PerUnit;
                    fits = false;
                }

                // Half of what it cost, its upgrades included, into what room is left (D-108).
                refund[r] = (CostOf(_content.For(building.Kind).Cost, r) + (upgrades * CostOf(_content.Upgrade.Cost, r))) / 2;
                long room = capacity > after ? Quantity.Whole(capacity - after) : 0;
                returned[r] = Min(refund[r], room);
            }

            // Everything is paid from the home island's store, so without a storehouse there nothing could ever be built.
            var rejection = building.Kind == BuildingKind.Storehouse && building.Island == _home && !HasOtherStorehouse(building)
                ? Rejection.LastStorehouse
                : fits ? Rejection.None : Rejection.NoRoomInStore;
            long unharvested = recipe is { IsSlowProcess: true } ? Quantity.Whole(building.Ripe) : 0;
            return new Demolition(building, rejection, refund, returned, emptied, tooMuch, unharvested, upgradeQueued);
        }

        /// <summary>
        /// Demolishes a building that <see cref="Demolition(Hex)"/> found can be: the goods waiting at it and then what
        /// comes back go to the island's store, an upgrade in the queue goes with it, the tile is free and its villagers
        /// and carriers are given out again. Buildings its carriers served belong to the depot they reach fastest now.
        /// </summary>
        public void Demolish(Demolition demolition)
        {
            var building = demolition.Building;
            var store = _stores[building.Island];
            if (_content.For(building.Kind).Recipe is Recipe recipe)
            {
                store[(int)recipe.Product] += building.Waiting;
            }

            for (int r = 0; r < s_resourceCount; r++)
            {
                store[r] += (building.Held?[r] ?? 0) + Quantity.Units(demolition.Returned((Resource)r));
            }

            _buildings.Remove(building);
            _buildingAt.Remove(building.Tile);
            if (Queued(building.Tile, ProjectKind.Upgrade) is { } upgrade)
            {
                _projects.Remove(upgrade);
            }

            _capacity.Clear();
            ForgetTrips();
            StaffBuildings();
        }

        private bool HasOtherStorehouse(Building storehouse)
        {
            foreach (var building in _buildings)
            {
                if (building != storehouse && building.Kind == BuildingKind.Storehouse && building.Island == storehouse.Island)
                {
                    return true;
                }
            }

            return false;
        }

        private static long CostOf(IReadOnlyDictionary<Resource, int> cost, int resource) =>
            cost.TryGetValue((Resource)resource, out int amount) ? amount : 0;
    }
}
