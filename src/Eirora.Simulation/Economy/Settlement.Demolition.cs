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
                long goods = (recipe is not null && (int)recipe.Product == r ? building.Waiting : 0) + (building.Held?[r] ?? 0) + InKnarr(building, r);
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
            var demolition = new Demolition(building, rejection, refund, returned, emptied, tooMuch, unharvested, upgradeQueued);

            // What becomes of the staffing and the sea routes is read from a copy that the building is taken from, by the
            // same code that the demolition runs, so that the confirmation cannot tell otherwise.
            var without = CopyForWork();
            without.Remove(without.BuildingAt(tile)!);
            var staffs = new List<Building>();
            var unstaffs = new List<Building>();
            foreach (var other in _buildings)
            {
                if (other != building && without.BuildingAt(other.Tile) is { } twin && twin.Staffed != other.Staffed)
                {
                    (twin.Staffed ? staffs : unstaffs).Add(other);
                }
            }

            var closed = new List<string>();
            foreach (var route in SeaRoutes)
            {
                if (!without.HasSeaRoute(route.From))
                {
                    closed.Add(route.From);
                }
            }

            demolition.Staffs = staffs;
            demolition.Unstaffs = unstaffs;
            demolition.MoreBuilders = without.FreeVillagersOf(without._settlements[building.Island]) - FreeVillagersOf(_settlements[building.Island]);
            demolition.ClosesSeaRoutes = closed;
            demolition.EndsExpeditions = building.Kind == BuildingKind.Jetty && building.Island == _home && !without.HasBuilt(BuildingKind.Jetty, _home);
            return demolition;
        }

        /// <summary>
        /// Demolishes a building that <see cref="Demolition(Hex)"/> found can be: the goods waiting at it, the materials of
        /// the knarr a shipyard was building and then what comes back go to the island's store, an upgrade in the queue goes with it, the tile is free and its villagers
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
                store[r] += (building.Held?[r] ?? 0) + InKnarr(building, r) + Quantity.Units(demolition.Returned((Resource)r));
            }

            if (Queued(building.Tile, ProjectKind.Upgrade) is { } upgrade)
            {
                _projects.Remove(upgrade);
            }

            Remove(building);
        }

        /// <summary>Takes a building away and gives its villagers and carriers out again, its buildings to other depots.</summary>
        private void Remove(Building building)
        {
            _buildings.Remove(building);
            _buildingAt.Remove(building.Tile);
            _capacity.Clear();
            _housing.Clear();
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

        /// <summary>Millionths of a resource paid for the knarr a shipyard is building, which goes back with the rest when it is demolished.</summary>
        private long InKnarr(Building building, int resource) =>
            building.KnarrWorkLeft > 0 && _content.For(building.Kind).Knarrs is KnarrSpec knarr ? Quantity.Units(CostOf(knarr.Cost, resource)) : 0;

        private static long CostOf(IReadOnlyDictionary<Resource, int> cost, int resource) =>
            cost.TryGetValue((Resource)resource, out int amount) ? amount : 0;
    }
}
