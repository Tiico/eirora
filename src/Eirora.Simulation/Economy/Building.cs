using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>A finished building in the settlement, on its building site.</summary>
    public sealed class Building
    {
        internal Building(BuildingKind kind, Hex tile, string island)
        {
            Kind = kind;
            Tile = tile;
            Island = island;
        }

        public BuildingKind Kind { get; }

        /// <summary>The tile it stands on; a fishing hut stands on stilts in the shallow water (D-88).</summary>
        public Hex Tile { get; }

        /// <summary>The map's id of the island it belongs to; goods travel between islands only by sea route.</summary>
        public string Island { get; }

        /// <summary>1, or 2 after an upgrade (+50 % production).</summary>
        public int Level { get; internal set; } = 1;

        /// <summary>Whether enough villagers work here for it to run; a storehouse or a jetty is staffed by its first carrier.</summary>
        public bool Staffed { get; internal set; }

        /// <summary>
        /// Whether the player has paused it: it is not staffed and its villagers build instead, but it keeps its level
        /// and its place in the staffing order (D-39).
        /// </summary>
        public bool Paused { get; internal set; }

        /// <summary>
        /// Whether Dawn Power gives it a workday today: +50 % production, or for a storehouse or a jetty +50 % of what
        /// its carriers carry (economy-v2.md, section 7).
        /// </summary>
        public bool WorkdayToday { get; internal set; }

        /// <summary>
        /// The villagers who carry for a storehouse or a jetty, 0 to <see cref="Logistics.MostCarriers"/> (D-109): 0 while
        /// it is not staffed, and 0 for every other building.
        /// </summary>
        public int Carriers { get; internal set; }

        /// <summary>
        /// The most carriers the player lets a storehouse or a jetty take, 1 to <see cref="Logistics.MostCarriers"/>; free
        /// planning (D-23). 0 for every other building.
        /// </summary>
        public int CarrierCap { get; internal set; }

        /// <summary>
        /// Whether the player has moved it up in the staffing order, so that the carriers serve it before the buildings
        /// where most waits (D-111).
        /// </summary>
        public bool CarriedFirst { get; internal set; }

        /// <summary>
        /// Where the storehouse or jetty stands whose carriers serve it: the staffed one they reached fastest at the last
        /// dawn, or when it was built; null for a building no staffed depot reaches, and for one without a recipe.
        /// </summary>
        public Hex? Depot { get; internal set; }

        /// <summary>
        /// Whole units of each raw material that the carriers always leave in the island's store for building (GLOSSARY:
        /// Minimilager, LOG-06): 0 to what the store holds, <see cref="StockRules.StandardReserve"/> when it is built; free
        /// planning (D-23). 0 for a building without raw materials.
        /// </summary>
        public int Reserve { get; internal set; }

        /// <summary>Whole units of its product waiting for the carriers in its outbox (economy-v2.md, section 7).</summary>
        public long WaitingUnits => Quantity.Whole(Waiting);

        /// <summary>Grain or wool that has grown and waits for the villagers to harvest it.</summary>
        internal long Ripe { get; set; }

        /// <summary>Millionths of a unit of its product in its outbox, which holds <see cref="Logistics.OutboxUnits"/>.</summary>
        internal long Waiting { get; set; }

        /// <summary>
        /// The raw materials the carriers have brought to it, by resource, which it processes from; null for a building
        /// whose recipe has none.
        /// </summary>
        internal long[]? Held { get; set; }

        /// <summary>Whole units of a raw material the carriers have brought to it.</summary>
        public long HeldUnits(Resource resource) => Held is null ? 0 : Quantity.Whole(Held[(int)resource]);

        internal Building Copy() => new(Kind, Tile, Island)
        {
            Level = Level,
            Staffed = Staffed,
            Paused = Paused,
            WorkdayToday = WorkdayToday,
            Carriers = Carriers,
            CarrierCap = CarrierCap,
            CarriedFirst = CarriedFirst,
            Depot = Depot,
            Reserve = Reserve,
            Ripe = Ripe,
            Waiting = Waiting,
            Held = (long[]?)Held?.Clone(),
        };
    }
}
