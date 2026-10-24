using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// Something that holds a building back (GLOSSARY: Flaskhals), with what it costs: how much of the building's product
    /// does not reach the store because of it.
    /// </summary>
    /// <remarks>
    /// Each rule that can hold a building back adds its own kind, so that later rules (carriers, needs) add theirs without
    /// changing how a diagnosis is read (TASK-043, D-107). A client that does not know a kind yet can still show the loss.
    /// </remarks>
    public abstract class Hindrance
    {
        protected Hindrance(long lostPerHour) => LostPerHour = lostPerHour;

        /// <summary>Millionths of a unit of the product an active hour that this alone keeps from the store.</summary>
        public long LostPerHour { get; }
    }

    /// <summary>
    /// Too few villagers are left for a producing building after those before it in the staffing order, so it stands
    /// still (economy-v1.md, section 4.1).
    /// </summary>
    public sealed class MissingStaff : Hindrance
    {
        internal MissingStaff(int villagers, long lostPerHour)
            : base(lostPerHour) => Villagers = villagers;

        /// <summary>How many more villagers it takes, at least one: the staffing gives a building all it needs or none.</summary>
        public int Villagers { get; }
    }

    /// <summary>The player has paused the building, so its villagers build instead (economy-v1.md, section 4.1).</summary>
    public sealed class Paused : Hindrance
    {
        internal Paused(long lostPerHour)
            : base(lostPerHour)
        {
        }
    }

    /// <summary>
    /// A raw material is not above what processing leaves in the store, the building's minimum stock or the island's
    /// reservation (LOG-06, TASK-046), so the building processes less or nothing.
    /// </summary>
    public sealed class WaitingForInput : Hindrance
    {
        internal WaitingForInput(Resource resource, long stock, int reserve, KeptBy keptBy, long missing, long lostPerHour)
            : base(lostPerHour)
        {
            Resource = resource;
            Stock = stock;
            Reserve = reserve;
            KeptBy = keptBy;
            Missing = missing;
        }

        public Resource Resource { get; }

        /// <summary>Whole units of it in the island's store.</summary>
        public long Stock { get; }

        /// <summary>Whole units that processing leaves in the store: the larger of its minimum stock and the reservation.</summary>
        public int Reserve { get; }

        /// <summary>Which of the two keeps it, the minimum stock when they are the same.</summary>
        public KeptBy KeptBy { get; }

        /// <summary>Whole units more it takes, above the reserve, to work a full hour.</summary>
        public long Missing { get; }
    }

    /// <summary>
    /// The player has reserved a good on the island a sea route sails from, so the ships leave it there although they
    /// have room for it (TASK-046).
    /// </summary>
    public sealed class Reserved : Hindrance
    {
        internal Reserved(Resource resource, int units, long lostPerHour)
            : base(lostPerHour)
        {
            Resource = resource;
            Units = units;
        }

        public Resource Resource { get; }

        /// <summary>Whole units of it reserved on the island.</summary>
        public int Units { get; }
    }

    /// <summary>The island's store has no room for more of the product; nothing is lost, it is simply not made (D-17).</summary>
    public sealed class StoreFull : Hindrance
    {
        internal StoreFull(Resource resource, int capacity, long lostPerHour)
            : base(lostPerHour)
        {
            Resource = resource;
            Capacity = capacity;
        }

        public Resource Resource { get; }

        /// <summary>Whole units of each resource the island's storehouses hold; 0 when the island has none.</summary>
        public int Capacity { get; }
    }

    /// <summary>The forest a woodcutter reaches is felled, or there is none left that it can fell.</summary>
    public sealed class ForestFelled : Hindrance
    {
        internal ForestFelled(int felled, int inReach, long lostPerHour)
            : base(lostPerHour)
        {
            Felled = felled;
            InReach = inReach;
        }

        /// <summary>Forest tiles in reach with too little timber left for a full quarter hour's felling.</summary>
        public int Felled { get; }

        /// <summary>
        /// Forest tiles the woodcutter can fell: known forest on its tile and around it that no other building stands on;
        /// 0 when none is left.
        /// </summary>
        public int InReach { get; }
    }

    /// <summary>
    /// The building's outbox is full and waits for the carriers, who keep up over time: what was made while they were
    /// busy elsewhere waits there, and nothing is lost (INV-R2).
    /// </summary>
    public sealed class OutboxFull : Hindrance
    {
        internal OutboxFull(int units, long lostPerHour)
            : base(lostPerHour) => Units = units;

        /// <summary>Whole units the outbox holds.</summary>
        public int Units { get; }
    }

    /// <summary>
    /// No staffed storehouse or jetty reaches the building over land, so what it makes waits in its outbox until one does
    /// (economy-v2.md, section 7): a depot without carriers takes over no buildings (D-39).
    /// </summary>
    public sealed class NoDepot : Hindrance
    {
        internal NoDepot(bool depotStands, long lostPerHour)
            : base(lostPerHour) => DepotStands = depotStands;

        /// <summary>Whether a storehouse or a jetty stands on the island but has no villager to carry for it.</summary>
        public bool DepotStands { get; }
    }

    /// <summary>
    /// The carriers of the building's depot cannot keep up, so only part of what it makes reaches the store and the rest
    /// waits in its outbox (economy-v2.md, sections 7 and 7.1); with what a road, one more carrier or a storehouse beside it
    /// would give, by the same rules as the carriers' own. Rates are an active hour at the building's level without a
    /// workday, as the depot plans its carriers.
    /// </summary>
    public sealed class CarriersBehind : Hindrance
    {
        internal CarriersBehind(Trip trip, int sharePercent, long perHour, long fullPerHour, int roadTiles, long withRoadPerHour, long? withCarrierPerHour, Hex? beside, long besideDepotPerHour, bool villagerFree)
            : base(fullPerHour - perHour)
        {
            Trip = trip;
            SharePercent = sharePercent;
            PerHour = perHour;
            RoadTiles = roadTiles;
            WithRoadPerHour = withRoadPerHour;
            WithCarrierPerHour = withCarrierPerHour;
            BesideDepotSite = beside;
            BesideDepotPerHour = besideDepotPerHour;
            VillagerFree = villagerFree;
        }

        /// <summary>The way to its depot, and which depot.</summary>
        public Trip Trip { get; }

        /// <summary>The share of what the depot's buildings make that its carriers bring to the store, in percent.</summary>
        public int SharePercent { get; }

        /// <summary>Millionths of a unit of the product that reach the store an active hour.</summary>
        public long PerHour { get; }

        /// <summary>The tiles a road to the depot would be laid on; 0 when the way has its road.</summary>
        public int RoadTiles { get; }

        /// <summary>What would reach the store with the road; the same as <see cref="PerHour"/> when the way has its road.</summary>
        public long WithRoadPerHour { get; }

        /// <summary>What would reach it with one more carrier at the depot, or null when it has the most it can take.</summary>
        public long? WithCarrierPerHour { get; }

        /// <summary>Where a storehouse beside it could stand, or null when none can be built there.</summary>
        public Hex? BesideDepotSite { get; }

        /// <summary>What would reach the store with that storehouse and its carrier; <see cref="PerHour"/> without a site.</summary>
        public long BesideDepotPerHour { get; }

        /// <summary>
        /// Whether a villager is free to carry; when none is, one more carrier or a new storehouse takes nobody from
        /// another building, and comes when a villager is free (D-39).
        /// </summary>
        public bool VillagerFree { get; }
    }
}
