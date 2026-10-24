using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// Why goods do not reach the store and what holds the world back (CIV-05, LOG-03): each building's production and
    /// what hinders it, each depot's carriers and what would help them, each sea route's use, and each island's largest
    /// bottleneck (GLOSSARY: Flaskhals).
    /// </summary>
    /// <remarks>
    /// A reading of the world as it is: it tells what the next quarter hour with the villagers awake would do and never
    /// changes the state or the fingerprint (INV-D2). Rates are millionths of a unit an active hour (INV-D3).
    /// </remarks>
    public sealed class Diagnostics
    {
        private readonly Dictionary<Hex, BuildingDiagnosis> _byTile = new();
        private readonly Dictionary<Hex, DepotDiagnosis> _depots = new();
        private readonly Dictionary<string, Bottleneck?> _bottlenecks;

        internal Diagnostics(
            IReadOnlyList<BuildingDiagnosis> buildings,
            IReadOnlyList<DepotDiagnosis> depots,
            IReadOnlyList<SeaRouteDiagnosis> seaRoutes,
            Dictionary<string, Bottleneck?> bottlenecks,
            IReadOnlyDictionary<string, long[]> nextStores,
            bool queueStandsStill)
        {
            Buildings = buildings;
            Depots = depots;
            NextStores = nextStores;
            SeaRoutes = seaRoutes;
            QueueStandsStill = queueStandsStill;
            _bottlenecks = bottlenecks;
            foreach (var building in buildings)
            {
                _byTile[building.Building.Tile] = building;
            }

            foreach (var depot in depots)
            {
                _depots[depot.Depot.Tile] = depot;
            }
        }

        /// <summary>Every building, in the order the settlement works them.</summary>
        public IReadOnlyList<BuildingDiagnosis> Buildings { get; }

        /// <summary>Every storehouse and jetty, in the order the settlement works them, with its carriers (economy-v2.md, section 7.1).</summary>
        public IReadOnlyList<DepotDiagnosis> Depots { get; }

        /// <summary>
        /// Whether the building queue stands still because every villager no building needs carries (D-109): the player
        /// can lower a depot's cap to let some build.
        /// </summary>
        public bool QueueStandsStill { get; }

        /// <summary>Every sea route that ships sail, in map order, as <see cref="World.SeaRoutes"/> lists them.</summary>
        public IReadOnlyList<SeaRouteDiagnosis> SeaRoutes { get; }

        /// <summary>What each island's store would hold after the quarter hour diagnosed, which keeps the diagnosis true to the rules.</summary>
        internal IReadOnlyDictionary<string, long[]> NextStores { get; }

        /// <summary>The building on a tile, or null when none stands there.</summary>
        public BuildingDiagnosis? At(Hex tile) => _byTile.TryGetValue(tile, out var building) ? building : null;

        /// <summary>The storehouse or jetty on a tile, or null when none stands there.</summary>
        public DepotDiagnosis? DepotAt(Hex tile) => _depots.TryGetValue(tile, out var depot) ? depot : null;

        /// <summary>
        /// What costs an island most right now, so the player sees where Dawn Power does most good (economy-v1.md,
        /// section 6, decision 3); null when nothing there is held back.
        /// </summary>
        /// <param name="island">The map's id of the island.</param>
        public Bottleneck? BottleneckOn(string island) => _bottlenecks.TryGetValue(island, out var bottleneck) ? bottleneck : null;

        /// <summary>The sea route from an island, by the map's id, or null when no ships sail from it.</summary>
        public SeaRouteDiagnosis? SeaRouteFrom(string island)
        {
            foreach (var route in SeaRoutes)
            {
                if (route.Route.From == island)
                {
                    return route;
                }
            }

            return null;
        }
    }

    /// <summary>How a building produces right now.</summary>
    public enum Production
    {
        /// <summary>It produces nothing by design, such as housing, a storehouse or a jetty.</summary>
        None,

        /// <summary>It makes all it can, and the carriers bring it all to the store.</summary>
        Full,

        /// <summary>It produces, but something holds part of it back, there or on the way to the store.</summary>
        Reduced,

        /// <summary>It stands still: it makes nothing.</summary>
        Stopped,
    }

    /// <summary>One building's production and what holds it back (TASK-043).</summary>
    public sealed class BuildingDiagnosis
    {
        internal BuildingDiagnosis(Building building, Production production, Resource? product, long perHour, IReadOnlyList<Hindrance> hindrances)
        {
            Building = building;
            Production = production;
            Product = product;
            PerHour = perHour;
            Hindrances = hindrances;
        }

        public Building Building { get; }

        public Production Production { get; }

        /// <summary>What it makes, or null when it makes nothing.</summary>
        public Resource? Product { get; }

        /// <summary>
        /// Millionths of a unit of the product it makes into its outbox in the next active hour at this pace; what reaches
        /// the store is <see cref="CarriersBehind.PerHour"/> when its carriers cannot keep up.
        /// </summary>
        public long PerHour { get; }

        /// <summary>What holds it back, in the order the rules apply them: staff, raw materials or nature, store, carriers.</summary>
        public IReadOnlyList<Hindrance> Hindrances { get; }

        /// <summary>Whether it stands still, which the map marks: it produces nothing, or it lacks the villagers to run it.</summary>
        public bool StandsStill
        {
            get
            {
                if (Production == Production.Stopped)
                {
                    return true;
                }

                foreach (var hindrance in Hindrances)
                {
                    if (hindrance is MissingStaff)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    /// <summary>A sea route to the home island: how much of its capacity is used and what waits on the other side.</summary>
    public sealed class SeaRouteDiagnosis
    {
        internal SeaRouteDiagnosis(SeaRoute route, long capacityPerHour, long shippedPerHour, IReadOnlyList<(Resource Resource, long Units)> waiting, IReadOnlyList<Hindrance> hindrances)
        {
            Route = route;
            CapacityPerHour = capacityPerHour;
            ShippedPerHour = shippedPerHour;
            Waiting = waiting;
            Hindrances = hindrances;
        }

        public SeaRoute Route { get; }

        /// <summary>Millionths of a unit the ships carry home at most an active hour, shared by every resource.</summary>
        public long CapacityPerHour { get; }

        /// <summary>Millionths of a unit they would carry home in the next active hour at this pace.</summary>
        public long ShippedPerHour { get; }

        /// <summary>The share of the capacity in use, in percent.</summary>
        public int UsedPercent => CapacityPerHour > 0 ? (int)(ShippedPerHour * 100 / CapacityPerHour) : 0;

        /// <summary>Whole units waiting on the island the route sails from, by resource; only resources with any.</summary>
        public IReadOnlyList<(Resource Resource, long Units)> Waiting { get; }

        /// <summary>What keeps goods from reaching home, such as a full store there.</summary>
        public IReadOnlyList<Hindrance> Hindrances { get; }
    }

    /// <summary>What costs an island most right now (GLOSSARY: Flaskhals).</summary>
    public sealed class Bottleneck
    {
        internal Bottleneck(BuildingDiagnosis building, Hindrance hindrance, Building? workday)
        {
            Building = building;
            Hindrance = hindrance;
            Workday = workday;
        }

        /// <summary>The building held back.</summary>
        public BuildingDiagnosis Building { get; }

        /// <summary>What holds it back, the hindrance on the island that costs most.</summary>
        public Hindrance Hindrance { get; }

        /// <summary>
        /// Where a workday would ease it: the building that makes what is missing and can take a workday today; null when
        /// a workday does not help, as when what is missing is villagers, room or a shorter way.
        /// </summary>
        public Building? Workday { get; }
    }

    /// <summary>
    /// A storehouse's or a jetty's carriers (economy-v2.md, section 7.1): how many carrier-hours its buildings need an active
    /// hour, how many carriers it has, how much reaches the store, and what one more carrier, a road or a new storehouse
    /// would give, by the same rules as the carriers' own.
    /// </summary>
    public sealed class DepotDiagnosis
    {
        internal DepotDiagnosis(Building depot, long needPerHour, int sharePercent, IReadOnlyList<Building> serves, IReadOnlyList<Gain>? withCarrier, RoadAdvice? road, StorehouseAdvice? storehouse, bool villagerFree)
        {
            Depot = depot;
            NeedPerHour = needPerHour;
            SharePercent = sharePercent;
            Serves = serves;
            WithCarrier = withCarrier;
            Road = road;
            Storehouse = storehouse;
            VillagerFree = villagerFree;
        }

        public Building Depot { get; }

        /// <summary>Millionths of carrier-hours an active hour that its buildings' goods need for everything to reach the store.</summary>
        public long NeedPerHour { get; }

        /// <summary>Its carriers' hours an active hour, a workday counted, in millionths.</summary>
        public long CarrierHours => Depot.Carriers * Quantity.PerUnit * (Depot.WorkdayToday ? 150 : 100) / 100;

        /// <summary>The carrier-hours each carrier needs an active hour, in percent: over 100 they cannot keep up.</summary>
        public int LoadPercent => CarrierHours > 0 ? (int)(NeedPerHour * 100 / CarrierHours) : 0;

        /// <summary>The share of what its buildings make that its carriers bring to the store, in percent.</summary>
        public int SharePercent { get; }

        /// <summary>The buildings it serves: those whose carriers reach it fastest, in the order the settlement works them.</summary>
        public IReadOnlyList<Building> Serves { get; }

        /// <summary>What more would reach the store with one more carrier; null when it has the most it can take.</summary>
        public IReadOnlyList<Gain>? WithCarrier { get; }

        /// <summary>The road among its buildings' ways that would give most, or null when none would give anything.</summary>
        public RoadAdvice? Road { get; }

        /// <summary>The new storehouse beside one of its buildings that would give most, or null when none would.</summary>
        public StorehouseAdvice? Storehouse { get; }

        /// <summary>
        /// Whether a villager is free to carry: one more carrier and a new storehouse need one and take nobody from
        /// another building, so without one they give what they give once a villager is free (D-39).
        /// </summary>
        public bool VillagerFree { get; }
    }

    /// <summary>How much more of a resource reaches the store an active hour, in millionths of a unit.</summary>
    public readonly struct Gain
    {
        internal Gain(Resource resource, long perHour)
        {
            Resource = resource;
            PerHour = perHour;
        }

        public Resource Resource { get; }

        public long PerHour { get; }
    }

    /// <summary>A road to a depot from one of its buildings (D-112), and what more it would bring to the store.</summary>
    public sealed class RoadAdvice
    {
        internal RoadAdvice(Building from, IReadOnlyList<Hex> tiles, IReadOnlyList<Gain> gains)
        {
            From = from;
            Tiles = tiles;
            Gains = gains;
        }

        /// <summary>The building whose way it would be laid along with <em>Road to the storehouse</em>.</summary>
        public Building From { get; }

        /// <summary>The tiles it would lie on, nearest the depot first.</summary>
        public IReadOnlyList<Hex> Tiles { get; }

        public IReadOnlyList<Gain> Gains { get; }
    }

    /// <summary>A new storehouse with its first carrier, and what more it would bring to the store.</summary>
    public sealed class StorehouseAdvice
    {
        internal StorehouseAdvice(Hex site, Building beside, IReadOnlyList<Gain> gains)
        {
            Site = site;
            Beside = beside;
            Gains = gains;
        }

        public Hex Site { get; }

        /// <summary>The building it would stand beside.</summary>
        public Building Beside { get; }

        public IReadOnlyList<Gain> Gains { get; }
    }
}
