namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// Something that holds a building back (GLOSSARY: Flaskhals), with what it costs: how much of the building's product
    /// does not reach the store because of it.
    /// </summary>
    /// <remarks>
    /// Each rule that can hold a building back adds its own kind, so that later rules (bearers, needs) add theirs without
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
    /// A raw material is not above the reserve that processing always leaves for building (LOG-06), so the building
    /// processes less or nothing.
    /// </summary>
    public sealed class WaitingForInput : Hindrance
    {
        internal WaitingForInput(Resource resource, long stock, int reserve, long missing, long lostPerHour)
            : base(lostPerHour)
        {
            Resource = resource;
            Stock = stock;
            Reserve = reserve;
            Missing = missing;
        }

        public Resource Resource { get; }

        /// <summary>Whole units of it in the island's store.</summary>
        public long Stock { get; }

        /// <summary>Whole units that processing leaves in the store (the minimum stock).</summary>
        public int Reserve { get; }

        /// <summary>Whole units more it takes, above the reserve, to work a full hour.</summary>
        public long Missing { get; }
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
    /// The way to the nearest storehouse or jetty is long, so only part of the production reaches the store
    /// (economy-v1.md, section 5); with what a road or a storehouse beside it would give, as far as the raw materials, the
    /// forest and the room in the store let it. Only there when a storehouse beside it would give more.
    /// </summary>
    public sealed class LongWay : Hindrance
    {
        internal LongWay(int steps, int percent, int? roadPercent, long perHour, long withRoadPerHour, long besideDepotPerHour)
            : base(besideDepotPerHour - perHour)
        {
            Steps = steps;
            Percent = percent;
            RoadPercent = roadPercent;
            PerHour = perHour;
            WithRoadPerHour = withRoadPerHour;
            BesideDepotPerHour = besideDepotPerHour;
        }

        /// <summary>Steps over land to the nearest storehouse or jetty, or <see cref="int.MaxValue"/> when none can be reached.</summary>
        public int Steps { get; }

        /// <summary>The share of the production that reaches the store, in percent.</summary>
        public int Percent { get; }

        /// <summary>The share with a road to the storehouse, or null when the building has one or none can be built.</summary>
        public int? RoadPercent { get; }

        /// <summary>Millionths of a unit an active hour that reach the store now.</summary>
        public long PerHour { get; }

        /// <summary>What would reach the store with a road; the same as <see cref="PerHour"/> when no road can be built.</summary>
        public long WithRoadPerHour { get; }

        /// <summary>What would reach it with a storehouse beside the building, where everything reaches the store.</summary>
        public long BesideDepotPerHour { get; }
    }
}
