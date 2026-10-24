using System.Collections.Generic;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Map;
using static Eirora.Simulation.Economy.Logistics;

namespace Eirora.Simulation.Economy
{
    /// <summary>The carriers and the roads on land (economy-v2.md, section 7; D-107, D-109–D-112).</summary>
    /// <remarks>
    /// Every producing building puts what it makes in its outbox, and the carriers of the storehouse or jetty it belongs
    /// to bring it to the island's store and bring raw materials out to the processing; distance costs the carriers'
    /// time instead of production (D-110). Times are millionths of an hour and goods millionths of a unit (INV-D3).
    /// </remarks>
    internal sealed partial class Settlement
    {
        private const long CarrierTimePerTick = 250_000; // A carrier's quarter hour, in millionths of an hour.

        // Every road tile, in map order; roads are only ever added.
        private readonly SortedSet<Hex> _roads = new();

        // Each island's trips by tile to the nearest staffed depot, and each depot's own trips by tile; forgotten whenever a
        // depot's staffing or a road changes.
        private readonly Dictionary<string, Dictionary<Hex, Trip>> _walks = new(System.StringComparer.Ordinal);
        private readonly Dictionary<Hex, Dictionary<Hex, Trip>> _walksTo = new();
        // Each depot's buildings with a recipe and their trips, in the staffing order; forgotten with the trips and
        // whenever a building is added or moves in the order.
        private readonly Dictionary<Hex, List<(Building Building, Recipe Recipe, long Time)>> _served = new();
        private bool _servedKnown;
        private readonly List<Job> _jobs = new();

        /// <summary>The tiles with a road, in map order.</summary>
        public IReadOnlyCollection<Hex> Roads => _roads;

        public bool HasRoad(Hex tile) => _roads.Contains(tile);

        /// <summary>
        /// The way the carriers walk from a tile: for a producing building to the depot it belongs to, and for any other
        /// tile to the staffed storehouse or jetty on its island that they reach fastest. One that does not reach when
        /// there is none.
        /// </summary>
        public Trip TripFrom(Hex tile)
        {
            if (BuildingAt(tile) is { } building && !IsDepot(building.Kind) && _content.For(building.Kind).Recipe is not null)
            {
                return building.Depot is Hex depot && BuildingAt(depot) is { Carriers: > 0 } ? TripTo(tile, depot) : default;
            }

            return Nearest(tile);
        }

        /// <summary>The way from a tile to the staffed depot on its island that the carriers reach fastest.</summary>
        private Trip Nearest(Hex tile)
        {
            if (MapIslandOf(tile) is not string island)
            {
                return default;
            }

            if (!_walks.TryGetValue(island, out var walk))
            {
                walk = Walk(island, null, null, null);
                _walks.Add(island, walk);
            }

            return TripOn(walk, tile, island);
        }

        /// <summary>The way from a tile to one depot, along the fastest way there.</summary>
        private Trip TripTo(Hex tile, Hex depot)
        {
            if (MapIslandOf(tile) is not string island)
            {
                return default;
            }

            if (!_walksTo.TryGetValue(depot, out var walk))
            {
                walk = Walk(island, null, null, depot);
                _walksTo.Add(depot, walk);
            }

            return TripOn(walk, tile, island);
        }

        /// <summary>
        /// Gives each producing building the depot it belongs to (economy-v2.md, section 7): the staffed depot its carriers
        /// reach fastest. At dawn every building is given one again; otherwise only a new building and one whose depot has
        /// lost its carriers, so that a depot staffed in the day takes over no building before dawn and never lessens what
        /// reaches the store (D-39). A building no staffed depot reaches belongs to none.
        /// </summary>
        private void AssignDepots(bool all)
        {
            foreach (var building in _buildings)
            {
                if (IsDepot(building.Kind) || _content.For(building.Kind).Recipe is null)
                {
                    continue;
                }

                if (all || building.Depot is not Hex depot || BuildingAt(depot) is not { Carriers: > 0 })
                {
                    var nearest = Nearest(building.Tile);
                    var was = building.Depot;
                    building.Depot = nearest.Reaches ? nearest.Depot : null;
                    if (building.Depot != was)
                    {
                        ForgetServed();
                    }
                }
            }
        }

        /// <summary>
        /// The tiles the carriers walk from a tile to the depot it belongs to, the depot last, on their fastest way; of two
        /// equally fast steps the first counter-clockwise from east. Empty when no staffed depot is reached.
        /// </summary>
        public List<Hex> WayToDepot(Hex tile)
        {
            var way = new List<Hex>();
            var trip = TripFrom(tile);
            if (!trip.Reaches)
            {
                return way;
            }

            TripTo(tile, trip.Depot); // The depot's own walk, which the way follows.
            var walk = _walksTo[trip.Depot];

            for (var (at, left) = (tile, trip.Time); at != trip.Depot && left > 0;)
            {
                var step = at;
                for (int direction = 0; direction < Hex.Directions && step == at; direction++)
                {
                    var near = at.Neighbour(direction);
                    if (walk.TryGetValue(near, out var on) && on.Depot == trip.Depot && on.Time + EnterTime(near, null) == left)
                    {
                        step = near;
                    }
                }

                if (step == at)
                {
                    break;
                }

                way.Add(step);
                left -= EnterTime(step, null);
                at = step;
            }

            return way;
        }

        /// <summary>
        /// Whether a road can be laid on <paramref name="tile"/> (D-112): known land without a building, standing or in
        /// the queue, where builders reach, with no road yet or in the queue.
        /// </summary>
        public Rejection CheckRoad(Hex tile)
        {
            if (!_map.Contains(tile) || !Fog.IsKnown(tile) || !_map.TerrainAt(tile).IsLand() || BuildingAt(tile) is not null || IsQueued(tile, ProjectKind.Build))
            {
                return Rejection.SiteUnavailable;
            }

            if (HasRoad(tile) || IsQueued(tile, ProjectKind.Road))
            {
                return Rejection.AlreadyDone;
            }

            // Builders reach another island by its jetty, as they do to build there.
            var island = IslandOf(tile);
            return island != _home && !HasBuilt(BuildingKind.Jetty, island) ? Rejection.SiteUnavailable : Rejection.None;
        }

        /// <summary>
        /// The tiles that the shortcut <em>Road to the storehouse</em> lays a road on (D-112): those between the building and
        /// the depot it belongs to that can take one, nearest the depot first, on the way that is fastest once they have
        /// it, and of those the way with the fewest new tiles. Empty when it belongs to no depot or the way has its roads.
        /// </summary>
        public List<Hex> RoadToDepot(Hex tile)
        {
            var tiles = new List<Hex>();
            var trip = TripFrom(tile);
            if (!trip.Reaches || MapIslandOf(tile) is not string island)
            {
                return tiles;
            }

            // Forwards from the building, with the cost of each tile entered; the queued roads count as laid.
            var best = new Dictionary<Hex, (long Time, int New, Hex From)>();
            var open = new SortedSet<(long Time, int New, Hex Tile)>();
            best[tile] = (0, 0, tile);
            open.Add((0, 0, tile));
            while (open.Count > 0)
            {
                var at = open.Min;
                open.Remove(at);
                if (at.Tile == trip.Depot)
                {
                    break;
                }

                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    var near = at.Tile.Neighbour(direction);
                    if (!_map.Contains(near) || _map.IslandAt(near) != island)
                    {
                        continue;
                    }

                    bool laid = HasRoad(near) || IsQueued(near, ProjectKind.Road);
                    bool layable = !laid && near != trip.Depot && CheckRoad(near) == Rejection.None;
                    var candidate = (at.Time + (laid || layable ? RoadStepTime : StepTime), at.New + (layable ? 1 : 0));
                    if (!best.TryGetValue(near, out var known) || candidate.CompareTo((known.Time, known.New)) < 0)
                    {
                        if (best.ContainsKey(near))
                        {
                            open.Remove((known.Time, known.New, near));
                        }

                        best[near] = (candidate.Item1, candidate.Item2, at.Tile);
                        open.Add((candidate.Item1, candidate.Item2, near));
                    }
                }
            }

            if (!best.TryGetValue(trip.Depot, out var end))
            {
                return tiles;
            }

            for (var step = end.From; step != tile; step = best[step].From)
            {
                if (CheckRoad(step) == Rejection.None)
                {
                    tiles.Add(step);
                }
            }

            return tiles;
        }

        /// <summary>Sets how many carriers a depot may take; above the new cap they go back to building at once (D-23).</summary>
        public static void SetCarrierCap(Building depot, int cap)
        {
            depot.CarrierCap = cap;
            if (depot.Carriers > cap)
            {
                depot.Carriers = cap;
            }
        }

        /// <summary>
        /// Millionths of carrier-hours an active hour that a building's goods need from its depot for everything to reach
        /// the store: what it makes at its level without a workday, and for processing its raw materials too, there and
        /// back along its trip (economy-v2.md, section 7). 0 for a building that is not staffed or reaches no depot.
        /// </summary>
        internal long NeedOf(Building building, Trip trip, int activeTicks)
        {
            if (!building.Staffed || _content.For(building.Kind).Recipe is not Recipe recipe || !trip.Reaches)
            {
                return 0;
            }

            long made = NominalPerHour(building, recipe, activeTicks);
            long units = made;
            foreach (var input in recipe.Inputs)
            {
                units += made * input.PerProduct;
            }

            return units * 2 * trip.Time / Quantity.Units(CarrierLoad);
        }

        /// <summary>Millionths of carrier-hours an active hour that every building belonging to a depot needs.</summary>
        internal long NeedAt(Building depot, int activeTicks)
        {
            long need = 0;
            foreach (var building in _buildings)
            {
                var trip = TripFrom(building.Tile);
                if (!IsDepot(building.Kind) && trip.Reaches && trip.Depot == depot.Tile)
                {
                    need += NeedOf(building, trip, activeTicks);
                }
            }

            return need;
        }

        /// <summary>
        /// Millionths of a unit of its product a building makes in an active hour at its level and without a workday:
        /// for a field or a sheep farm what grows over the whole game day, harvested in the active hours.
        /// </summary>
        internal long NominalPerHour(Building building, Recipe recipe, int activeTicks)
        {
            int level = LevelPercent(building);
            if (recipe.IsSlowProcess)
            {
                return Scale(TileRate(recipe.GrowsPerHour, recipe, building.Tile), level) * Recipe.TicksPerHour * 24 / activeTicks;
            }

            if (recipe.ForestTiles == 0)
            {
                return Scale(TileRate(recipe.PerHour, recipe, building.Tile), level);
            }

            long felled = 0;
            var woods = Woods(building.Tile);
            for (int i = 0; i < woods.Count && i < recipe.ForestTiles; i++)
            {
                felled += Scale(TileRate(recipe.PerHour, recipe, woods[i]), level);
            }

            return felled;
        }

        /// <summary>
        /// At dawn every staffed depot lets go the carriers its buildings' goods no longer need, so that the buildings
        /// waiting for villagers can take them first (D-109).
        /// </summary>
        private void ReleaseCarriers(int activeTicks)
        {
            foreach (var depot in _buildings)
            {
                if (IsDepot(depot.Kind) && depot.Staffed && Wanted(depot, activeTicks) < depot.Carriers)
                {
                    depot.Carriers = Wanted(depot, activeTicks);
                }
            }
        }

        /// <summary>
        /// At dawn, once the buildings are staffed, each staffed depot in the staffing order takes as many more carriers as
        /// its buildings' goods need, up to its cap, from the villagers no building needs (D-109).
        /// </summary>
        private void HireCarriers(int activeTicks)
        {
            foreach (var depot in _buildings)
            {
                if (!IsDepot(depot.Kind) || !depot.Staffed)
                {
                    continue;
                }

                int more = Wanted(depot, activeTicks) - depot.Carriers;
                int free = FreeVillagers;
                depot.Carriers += more > 0 ? (more < free ? more : free) : 0;
            }
        }

        /// <summary>The carriers a depot's buildings' goods need, one to its cap.</summary>
        private int Wanted(Building depot, int activeTicks)
        {
            long need = NeedAt(depot, activeTicks);
            int want = (int)((need + Quantity.PerUnit - 1) / Quantity.PerUnit);
            return want < 1 ? 1 : want > depot.CarrierCap ? depot.CarrierCap : want;
        }

        /// <summary>
        /// The carriers' quarter hour (D-111): each staffed depot in the staffing order, its carriers first where the
        /// player has put a building first and then where most waits, the fullest outbox or the processing with least of a
        /// raw material left; a building's goods first, then its raw materials. A workday on the depot gives them 50 %
        /// more time. What they cannot carry waits at the building (INV-R2).
        /// </summary>
        private void Carry()
        {
            foreach (var depot in _buildings)
            {
                if (!IsDepot(depot.Kind) || depot.Carriers == 0)
                {
                    continue;
                }

                if (!ServedBy(depot.Tile, out var served))
                {
                    continue;
                }

                var store = _stores[depot.Island];
                long capacity = StorageCapacity(depot.Island);
                _jobs.Clear();
                foreach (var (building, recipe, trip) in served)
                {
                    // Urgency as a share of what each holds, on a common scale: waiting / outbox against wanted / input.
                    if (building.Waiting > 0)
                    {
                        AddJob(new Job(building, null, building.Waiting * InputUnits, trip));
                    }

                    if (building.Staffed && building.Held is long[] held)
                    {
                        foreach (var input in recipe.Inputs)
                        {
                            long wanted = Min(Quantity.Units(InputUnits) - held[(int)input.Resource], store[(int)input.Resource] - Quantity.Units(ProcessingReserve));
                            if (wanted > 0)
                            {
                                AddJob(new Job(building, input.Resource, wanted * OutboxUnits, trip));
                            }
                        }
                    }
                }

                long time = depot.Carriers * Scale(CarrierTimePerTick, WorkdayPercent(depot));
                foreach (var job in _jobs)
                {
                    if (time <= 0)
                    {
                        break;
                    }

                    long reach = time * Quantity.Units(CarrierLoad) / (2 * job.Time);
                    long moved;
                    if (job.Input is Resource input)
                    {
                        var held = job.Building.Held!;
                        moved = Min(Min(Quantity.Units(InputUnits) - held[(int)input], store[(int)input] - Quantity.Units(ProcessingReserve)), reach);
                        if (moved <= 0)
                        {
                            continue;
                        }

                        store[(int)input] -= moved;
                        held[(int)input] += moved;
                    }
                    else
                    {
                        var product = _content.For(job.Building.Kind).Recipe!.Product;
                        long room = capacity - store[(int)product];
                        moved = Min(Min(job.Building.Waiting, reach), room);
                        if (moved <= 0)
                        {
                            continue;
                        }

                        job.Building.Waiting -= moved;
                        store[(int)product] += moved;
                    }

                    time -= ((moved * 2 * job.Time) + Quantity.Units(CarrierLoad) - 1) / Quantity.Units(CarrierLoad);
                }
            }
        }

        /// <summary>Puts a job after those that go before it or as early, so equal ones keep the order they came in.</summary>
        private void AddJob(Job job)
        {
            int at = _jobs.Count;
            while (at > 0 && job.GoesBefore(_jobs[at - 1]))
            {
                at--;
            }

            _jobs.Insert(at, job);
        }

        /// <summary>Forgets the carriers' ways, after a depot's staffing or a road has changed.</summary>
        private void ForgetTrips()
        {
            _walks.Clear();
            _walksTo.Clear();
            ForgetServed();
        }

        /// <summary>Forgets which buildings each depot serves, after a building is added or moves in the staffing order.</summary>
        private void ForgetServed()
        {
            _served.Clear();
            _servedKnown = false;
        }

        /// <summary>The buildings a depot serves with their trips one way, in the staffing order; false when it serves none.</summary>
        private bool ServedBy(Hex depot, out List<(Building Building, Recipe Recipe, long Time)> served)
        {
            if (!_servedKnown)
            {
                foreach (var building in _buildings)
                {
                    var trip = TripFrom(building.Tile);
                    if (!IsDepot(building.Kind) && _content.For(building.Kind).Recipe is Recipe recipe && trip.Reaches)
                    {
                        if (!_served.TryGetValue(trip.Depot, out var list))
                        {
                            list = new List<(Building, Recipe, long)>();
                            _served.Add(trip.Depot, list);
                        }

                        list.Add((building, recipe, trip.Time));
                    }
                }

                _servedKnown = true;
            }

            return _served.TryGetValue(depot, out served!);
        }

        /// <summary>
        /// Every land tile's trip on an island, found backwards from the staffed depots, or from <paramref name="only"/>
        /// alone: the time to step from a tile to its neighbour is what entering that neighbour costs. Of two equally fast
        /// depots the first in map order wins. <paramref name="roads"/> and <paramref name="depot"/> add roads and a
        /// staffed depot that are not there, for the diagnosis.
        /// </summary>
        private Dictionary<Hex, Trip> Walk(string island, ICollection<Hex>? roads, Hex? depot, Hex? only)
        {
            var best = new Dictionary<Hex, Trip>();
            var open = new SortedSet<(long Time, Hex Depot, Hex Tile)>();
            foreach (var building in _buildings)
            {
                if (IsDepot(building.Kind) && (only is Hex one ? building.Tile == one : building.Carriers > 0) && MapIslandOf(building.Tile) == island)
                {
                    best[building.Tile] = new Trip(0, building.Tile, 0);
                    open.Add((0, building.Tile, building.Tile));
                }
            }

            if (depot is Hex extra && !best.ContainsKey(extra))
            {
                best[extra] = new Trip(0, extra, 0);
                open.Add((0, extra, extra));
            }

            while (open.Count > 0)
            {
                var at = open.Min;
                open.Remove(at);
                long time = at.Time + EnterTime(at.Tile, roads);
                int steps = best[at.Tile].Steps + 1;
                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    var near = at.Tile.Neighbour(direction);
                    if (!_map.Contains(near) || _map.IslandAt(near) != island)
                    {
                        continue;
                    }

                    if (best.TryGetValue(near, out var known) && (known.Time < time || (known.Time == time && known.Depot <= at.Depot)))
                    {
                        continue;
                    }

                    if (best.ContainsKey(near))
                    {
                        open.Remove((known.Time, known.Depot, near));
                    }

                    best[near] = new Trip(time, at.Depot, steps);
                    open.Add((time, at.Depot, near));
                }
            }

            return best;
        }

        /// <summary>A tile's trip from a walk: its own for land, and for a fishing hut on its stilts the best first step ashore.</summary>
        private Trip TripOn(Dictionary<Hex, Trip> walk, Hex tile, string island, ICollection<Hex>? roads = null)
        {
            if (_map.IslandAt(tile) is not null)
            {
                return walk.TryGetValue(tile, out var trip) && trip.Time > 0 ? trip : default;
            }

            Trip ashore = default;
            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var near = tile.Neighbour(direction);
                if (!_map.Contains(near) || _map.IslandAt(near) != island || !walk.TryGetValue(near, out var land))
                {
                    continue;
                }

                long time = land.Time + EnterTime(near, roads);
                if (!ashore.Reaches || time < ashore.Time || (time == ashore.Time && land.Depot < ashore.Depot))
                {
                    ashore = new Trip(time, land.Depot, land.Steps + 1);
                }
            }

            return ashore;
        }

        /// <summary>What stepping onto a tile costs: half as long with a road, laid or added for the diagnosis.</summary>
        private long EnterTime(Hex tile, ICollection<Hex>? roads) =>
            _roads.Contains(tile) || (roads is not null && roads.Contains(tile)) ? RoadStepTime : StepTime;

        /// <summary>One thing for a depot's carriers to do this quarter hour: fetch a building's goods, or bring it a raw material.</summary>
        private readonly struct Job
        {
            public Job(Building building, Resource? input, long urgency, long time)
            {
                Building = building;
                Input = input;
                Urgency = urgency;
                Time = time;
            }

            public Building Building { get; }

            /// <summary>The raw material brought, or null when the building's goods are fetched.</summary>
            public Resource? Input { get; }

            public long Urgency { get; }

            /// <summary>The trip one way, in millionths of an hour.</summary>
            public long Time { get; }

            /// <summary>
            /// The buildings the player has put first go first, among themselves in the staffing order the jobs come in;
            /// the others go where most waits.
            /// </summary>
            public bool GoesBefore(Job other) =>
                Building.CarriedFirst != other.Building.CarriedFirst ? Building.CarriedFirst : !Building.CarriedFirst && Urgency > other.Urgency;
        }
    }
}
