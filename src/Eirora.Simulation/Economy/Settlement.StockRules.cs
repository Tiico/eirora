using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation.Economy
{
    /// <summary>The player's rules for the stores (LOG-06, TASK-046): minimum stock, import order and reservations.</summary>
    /// <remarks>
    /// Processing never takes a raw material below the larger of its minimum stock and the island's reservation, and
    /// the ships never take a good below its reservation; they take the goods home in the island's import order. Only
    /// commands change the rules, never a tick, and building may still pay from what is reserved (economy-v1.md, section 5).
    /// </remarks>
    internal sealed partial class Settlement
    {
        private static readonly Resource[] s_fixedOrder = (Resource[])Enum.GetValues(typeof(Resource));
        private static readonly ReadOnlyCollection<Resource> s_fixedOrderShown = Array.AsReadOnly(s_fixedOrder);

        /// <summary>Whether an island has a store of its own, which it has once something is built there.</summary>
        public bool HasStore(string? island) => island is not null && _stores.ContainsKey(island);

        /// <summary>Whole units of a resource that neither the ships nor processing take from an island's store.</summary>
        public int ReservationOf(string island, Resource resource) =>
            _settlements.TryGetValue(island, out var settlement) && settlement.Reserved is int[] reserved ? reserved[(int)resource] : 0;

        /// <summary>The order the ships take an island's goods home in: the player's, or the resources' fixed order.</summary>
        public IReadOnlyList<Resource> ImportOrder(string island) =>
            _settlements.TryGetValue(island, out var settlement) && settlement.Imports is Resource[] order ? Array.AsReadOnly(order) : s_fixedOrderShown;

        /// <summary>Whole units of each resource an island's store holds.</summary>
        public int StorageUnits(string island) => (int)Quantity.Whole(StorageCapacity(island));

        /// <summary>Sets how much of its materials a processing building or a shipyard leaves in the store.</summary>
        public static void SetReserve(Building processing, int units) => processing.Reserve = units;

        /// <summary>Reserves whole units of a resource on an island, which has a store.</summary>
        public void SetReservation(string island, Resource resource, int units)
        {
            var settlement = _settlements[island];
            var reserved = settlement.Reserved ?? new int[s_resourceCount];
            reserved[(int)resource] = units;
            settlement.Reserved = Array.TrueForAll(reserved, r => r == 0) ? null : reserved;
        }

        /// <summary>Moves a resource to <paramref name="place"/> in an island's import order, counted from 0.</summary>
        public void MoveImport(string island, Resource resource, int place)
        {
            var order = new List<Resource>(ImportOrder(island));
            order.Remove(resource);
            order.Insert(place, resource);
            _settlements[island].Imports = IsFixedOrder(order) ? null : order.ToArray();
        }

        /// <summary>
        /// Millionths of a unit of a raw material that the carriers leave in the store of a processing building's island:
        /// the larger of its minimum stock and the island's reservation, and which of them that is.
        /// </summary>
        private (long Kept, KeptBy By) Leaves(Building processing, Resource input)
        {
            return Leaves(processing, input, _settlements[processing.Island].Reserved);
        }

        /// <summary>
        /// As <see cref="Leaves(Building, Resource)"/>, with <paramref name="reserved"/> the reservations on the building's
        /// island, or null when it has none, looked up once for every building a depot's carriers serve.
        /// </summary>
        private static (long Kept, KeptBy By) Leaves(Building processing, Resource input, int[]? reserved)
        {
            int units = reserved is null ? 0 : reserved[(int)input];
            return units > processing.Reserve ? (Quantity.Units(units), KeptBy.Reservation) : (Quantity.Units(processing.Reserve), KeptBy.Reserve);
        }

        /// <summary>The order the ships take an island's goods home in, as the array they go through every quarter hour.</summary>
        private static Resource[] ShippingOrder(IslandSettlement settlement) => settlement.Imports ?? s_fixedOrder;

        /// <summary>
        /// Millionths of a unit of a resource in an island's store that the ships may take home: all but the larger of
        /// what is <paramref name="reserved"/> there (the island's reservations, or null when it has none) and what of the
        /// needs in <paramref name="keeps"/> the island keeps for its settlement (<see cref="Kept"/>), as D-125 has it for
        /// processing.
        /// </summary>
        private long Shippable(long[] store, int[]? reserved, Resource resource, long[] keeps)
        {
            long kept = Kept(store, resource, keeps);
            long reservation = reserved is null ? 0 : Quantity.Units(reserved[(int)resource]);
            long left = store[(int)resource] - (reservation > kept ? reservation : kept);
            return left > 0 ? left : 0;
        }

        /// <summary>Whether an order of every resource is the resources' own, which the ships keep until the player changes it.</summary>
        private static bool IsFixedOrder(IList<Resource> order)
        {
            for (int i = 0; i < s_fixedOrder.Length; i++)
            {
                if (order[i] != s_fixedOrder[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The reservations and import orders, the islands in map order, after the stores and what is owed.</summary>
        private void WriteStockRules(StateWriter writer)
        {
            foreach (var island in _islands)
            {
                for (int resource = 0; _settlements[island].Reserved is int[] reserved && resource < s_resourceCount; resource++)
                {
                    if (reserved[resource] > 0)
                    {
                        writer.Line("reserved").Text(island).Enum((Resource)resource).Long(reserved[resource]);
                    }
                }
            }

            foreach (var island in _islands)
            {
                if (_settlements[island].Imports is Resource[] order)
                {
                    var line = writer.Line("imports").Text(island);
                    foreach (var resource in order)
                    {
                        line.Enum(resource);
                    }
                }
            }
        }

        /// <summary>
        /// Reads what <see cref="WriteStockRules"/> writes: a reservation is more than 0 on an island with a store, each
        /// once and in order; an import order is every resource once, off the home island, and not the fixed order.
        /// </summary>
        private void ReadStockRules(StateReader reader, WorldMap map)
        {
            (int Island, int Resource) last = (-1, -1);
            while (reader.Next("reserved"))
            {
                string island = reader.Text();
                var resource = reader.Enum<Resource>();
                int units = reader.Int();
                var at = (IndexOf(map, island), (int)resource);
                if (!_stores.ContainsKey(island) || at.CompareTo(last) <= 0 || units <= 0)
                {
                    throw new FormatException($"{units} {resource} reserved on {island} is not a reservation after the previous one.");
                }

                SetReservation(island, resource, units);
                last = at;
            }

            int previous = -1;
            while (reader.Next("imports"))
            {
                string island = reader.Text();
                var order = new Resource[s_resourceCount];
                for (int i = 0; i < s_resourceCount; i++)
                {
                    order[i] = reader.Enum<Resource>();
                }

                int index = IndexOf(map, island);
                bool everyOnce = new HashSet<Resource>(order).Count == s_resourceCount;
                if (!_stores.ContainsKey(island) || island == _home || index <= previous || !everyOnce || IsFixedOrder(order))
                {
                    throw new FormatException($"The import order on {island} is not an order of every resource after the previous island's.");
                }

                _settlements[island].Imports = order;
                previous = index;
            }
        }
    }
}
