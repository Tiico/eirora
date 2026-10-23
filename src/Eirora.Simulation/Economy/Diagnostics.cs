using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// Why goods do not reach the store and what holds the world back (CIV-05, LOG-03): each building's production and
    /// what hinders it, each sea route's use, and each island's largest bottleneck (GLOSSARY: Flaskhals).
    /// </summary>
    /// <remarks>
    /// A reading of the world as it is: it tells what the next quarter hour with the villagers awake would do and never
    /// changes the state or the fingerprint (INV-D2). Rates are millionths of a unit an active hour (INV-D3).
    /// </remarks>
    public sealed class Diagnostics
    {
        private readonly Dictionary<Hex, BuildingDiagnosis> _byTile = new();
        private readonly Dictionary<string, Bottleneck?> _bottlenecks;

        internal Diagnostics(
            IReadOnlyList<BuildingDiagnosis> buildings,
            IReadOnlyList<SeaRouteDiagnosis> seaRoutes,
            Dictionary<string, Bottleneck?> bottlenecks,
            IReadOnlyDictionary<string, long[]> nextStores)
        {
            Buildings = buildings;
            NextStores = nextStores;
            SeaRoutes = seaRoutes;
            _bottlenecks = bottlenecks;
            foreach (var building in buildings)
            {
                _byTile[building.Building.Tile] = building;
            }
        }

        /// <summary>Every building, in the order the settlement works them.</summary>
        public IReadOnlyList<BuildingDiagnosis> Buildings { get; }

        /// <summary>Every sea route that ships sail, in map order, as <see cref="World.SeaRoutes"/> lists them.</summary>
        public IReadOnlyList<SeaRouteDiagnosis> SeaRoutes { get; }

        /// <summary>What each island's store would hold after the quarter hour diagnosed, which keeps the diagnosis true to the rules.</summary>
        internal IReadOnlyDictionary<string, long[]> NextStores { get; }

        /// <summary>The building on a tile, or null when none stands there.</summary>
        public BuildingDiagnosis? At(Hex tile) => _byTile.TryGetValue(tile, out var building) ? building : null;

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

        /// <summary>Everything it can make reaches the store.</summary>
        Full,

        /// <summary>It produces, but something holds part of it back.</summary>
        Reduced,

        /// <summary>It stands still: nothing reaches the store.</summary>
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

        /// <summary>Millionths of a unit of the product that reach the store in the next active hour at this pace.</summary>
        public long PerHour { get; }

        /// <summary>What holds it back, in the order the rules apply them: staff, raw materials or nature, store, distance.</summary>
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
}
