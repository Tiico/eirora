using System.Collections.Generic;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Map;
using static Eirora.Simulation.Economy.Logistics;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The bottleneck diagnosis (TASK-043, TASK-050, TASK-051): why a building or a settlement stands still, read from the
    /// economy.
    /// </summary>
    /// <remarks>
    /// The diagnosis plays the work of the next quarter hour with the villagers awake on a copy of the settlement, with
    /// the same code as <see cref="Tick"/>, so it cannot drift from the rules and never changes the state (INV-D2). What
    /// reaches the store over time is the carriers' share, as the depots plan their carriers at dawn: every building's goods
    /// need carrier-hours, and a depot whose carriers have fewer brings that share of them (economy-v2.md, section 7.1).
    /// What a road, one more carrier or a new storehouse would give is the same reckoning with the road, the carrier or the
    /// storehouse added. The walk leaves out the building queue: a quarter hour in which a project finishes can staff,
    /// upgrade or lay a road before the buildings work, and the diagnosis then tells what they would make as they stand
    /// now. Its only writes are the caches of the carriers' ways and the woods list, which are not state.
    /// </remarks>
    internal sealed partial class Settlement
    {
        public Diagnostics Diagnose(int activeTicks)
        {
            var next = CopyForWork();
            next.GrowSlowProcesses(1);
            next.Unload();
            var made = new long[_buildings.Count];
            var supply = new long[_buildings.Count];
            next.Produce(made, supply);
            next.Carry();
            var seaRoutes = DiagnoseSeaRoutes(next._stores);

            var reaches = new Dictionary<string, Reach>(System.StringComparer.Ordinal);
            Reach ReachOf(string island)
            {
                if (!reaches.TryGetValue(island, out var reach))
                {
                    reach = ReachOn(island, activeTicks, null, null, null);
                    reaches.Add(island, reach);
                }

                return reach;
            }

            var advice = new Dictionary<Hex, Advice>();
            var buildings = new List<BuildingDiagnosis>(_buildings.Count);
            // As StaffBuildings gives them out: each island's in the staffing order, after the carriers the depots took at
            // dawn.
            var free = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var island in _islands)
            {
                free[island] = _settlements[island].Villagers;
            }

            foreach (var building in _buildings)
            {
                free[building.Island] -= IsDepot(building.Kind) && building.Carriers > 1 ? building.Carriers - 1 : 0;
            }

            for (int i = 0; i < _buildings.Count; i++)
            {
                var building = _buildings[i];
                var spec = _content.For(building.Kind);
                int left = free[building.Island];
                int lacking = !building.Staffed && !building.Paused && spec.Staff > left ? spec.Staff - left : 0;
                free[building.Island] = left - (building.Staffed ? spec.Staff : 0);

                // Staffing matters only to what a building makes: a storehouse or a jetty without its villagers still
                // stores goods, so it never stands still for want of them.
                if (spec.Recipe is not Recipe recipe)
                {
                    buildings.Add(new BuildingDiagnosis(building, Production.None, null, 0, System.Array.Empty<Hindrance>()));
                }
                else if (!building.Staffed)
                {
                    // What it would make with its villagers, here and now, is what the pause or the missing villagers cost.
                    long would = PerHour(Supply(building, recipe, null));
                    var why = building.Paused ? (Hindrance)new Paused(would) : new MissingStaff(lacking, would);
                    buildings.Add(new BuildingDiagnosis(building, Production.Stopped, recipe.Product, 0, new[] { why }));
                }
                else
                {
                    buildings.Add(DiagnoseProduction(building, recipe, made[i], supply[i], activeTicks, ReachOf(MapIslandOf(building.Tile) ?? _home), advice));
                }
            }

            var depots = new List<DepotDiagnosis>();
            foreach (var depot in _buildings)
            {
                if (IsDepot(depot.Kind))
                {
                    depots.Add(DiagnoseDepot(depot, activeTicks, ReachOf(MapIslandOf(depot.Tile) ?? _home), advice));
                }
            }

            var settlements = new List<SettlementDiagnosis>(_islands.Count);
            foreach (var island in _islands)
            {
                settlements.Add(DiagnoseSettlement(_settlements[island], BottleneckOn(island, buildings)));
            }

            // The queue stands still when no project has a free villager to build it, and the carriers took those who
            // would have: every villager that no building on those islands needs carries (D-109).
            // A building built by hand takes no villagers (D-65): it never stands still, and it is told with its dawns left.
            bool queueStandsStill = FirstWorked is not null;
            bool carrying = false;
            var pools = new List<string>();
            var byHand = new List<Project>();
            foreach (var project in _projects)
            {
                if (project.ByHand)
                {
                    byHand.Add(project);
                    continue;
                }

                foreach (var island in PoolsOf(project, pools))
                {
                    queueStandsStill &= FreeVillagersOf(_settlements[island]) == 0;
                    foreach (var depot in _buildings)
                    {
                        carrying |= depot.Island == island && depot.Carriers > 1;
                    }
                }
            }

            return new Diagnostics(buildings, depots, seaRoutes, settlements, next._stores, queueStandsStill && carrying, byHand);
        }

        /// <summary>
        /// Whether a settlement would grow if dawn came now and what holds it back (economy-v2.md, section 6): villagers,
        /// food in its own store for every one of them and housing for one more. Where its buildings lack villagers, the
        /// free villagers of the settlements a sea route joins to it can move there, one a dawn in the whole world.
        /// </summary>
        private SettlementDiagnosis DiagnoseSettlement(IslandSettlement settlement, Bottleneck? bottleneck)
        {
            var store = _stores[settlement.Island];
            long food = Available(store, Need.Food);
            bool fed = settlement.Villagers > 0 && food >= Quantity.Units(settlement.Villagers);
            bool room = MayMoveToAny(settlement);
            var growth = settlement.Villagers == 0 ? Growth.Empty
                : !fed ? Growth.Hungry
                : CroftersRoom(settlement) <= 0 ? Growth.Full
                : Growth.Grows;
            int nearby = 0;
            foreach (var island in _islands)
            {
                nearby += Linked(island, settlement.Island) ? FreeVillagersOf(_settlements[island]) : 0;
            }

            var tiers = new List<TierDiagnosis>(_tiers - 1);
            for (int tier = 1; tier < _tiers; tier++)
            {
                tiers.Add(DiagnoseTier(settlement, store, (PopulationTier)tier, fed));
            }

            return new SettlementDiagnosis(settlement, Quantity.Whole(food), growth, room, MissingStaffOn(settlement.Island), nearby, bottleneck, tiers);
        }

        /// <summary>
        /// Whether villagers would rise to a tier if dawn came now (economy-v2.md, section 2): the first need its villagers
        /// would lack, and the first thing that keeps the tier below from rising, in the order dawn checks them. The tiers
        /// below take a need they share first, as at dawn: the farmers' clothes before the craftsmen's.
        /// </summary>
        private TierDiagnosis DiagnoseTier(IslandSettlement settlement, long[] store, PopulationTier tier, bool fed)
        {
            var spec = _content.For(tier);
            int villagers = settlement.VillagersOf(tier);
            Need? lacks = null;
            foreach (var need in spec.Needs)
            {
                if (need.Need != Need.Food && villagers > 0 && lacks is null
                    && Available(store, need.Need) - Quantity.Units(UsesBelow(settlement, need.Need, tier)) < Quantity.Units(spec.Uses(need.Need, villagers)))
                {
                    lacks = need.Need;
                }
            }

            // What the new ones would need is what is left once every tier has its own.
            Need? lacksForNew = null;
            foreach (var need in spec.Needs)
            {
                long left = Available(store, need.Need) - Quantity.Units(Uses(settlement, need.Need));
                if (need.Need != Need.Food && lacksForNew is null && left < Quantity.Units(spec.Uses(need.Need, 1)))
                {
                    lacksForNew = need.Need;
                }
            }

            int room = HousingOf(settlement, tier) - villagers;
            int below = settlement.VillagersOf((PopulationTier)((int)tier - 1));
            var rising = Step < spec.Step ? Rising.Locked
                : !fed ? Rising.Hungry
                : lacks is not null ? Rising.LacksNeed
                : room <= 0 ? Rising.NoHousing
                : below == 0 ? Rising.NoneBelow
                : lacksForNew is not null ? Rising.LacksNeed
                : Rising.Rises;
            return new TierDiagnosis(tier, villagers, HousingOf(settlement, tier), lacks, rising, rising == Rising.LacksNeed ? lacks ?? lacksForNew : null);
        }

        /// <summary>Whole units of a need that the tiers below <paramref name="tier"/> use up at a dawn, before it takes its own.</summary>
        private int UsesBelow(IslandSettlement settlement, Need need, PopulationTier tier)
        {
            int uses = 0;
            for (int below = 0; below < (int)tier; below++)
            {
                uses += _content.Tiers[below].Uses(need, settlement.VillagersOf((PopulationTier)below));
            }

            return uses;
        }

        private BuildingDiagnosis DiagnoseProduction(Building building, Recipe recipe, long made, long supply, int activeTicks, Reach reach, Dictionary<Hex, Advice> advice)
        {
            var hindrances = new List<Hindrance>();
            var store = _stores[building.Island];
            long full = Potential(building, recipe);

            // Raw materials and nature, against what it makes at full pace with nothing else holding it back.
            if (recipe.Inputs.Count > 0)
            {
                foreach (var input in recipe.InputSpan)
                {
                    long stock = store[(int)input.Resource];
                    long held = building.Held![(int)input.Resource];
                    var (kept, by) = Leaves(building, input.Resource);
                    long available = held + Max(stock - kept, 0);
                    long enough = available / input.PerProduct;
                    if (enough < full)
                    {
                        // What a full hour at full pace takes above what is kept and what it holds, in whole units rounded up.
                        long wanted = kept + (input.PerProduct * full * Recipe.TicksPerHour) - stock - held;
                        hindrances.Add(new WaitingForInput(
                            input.Resource, Quantity.Whole(stock), (int)Quantity.Whole(kept), by, (wanted + Quantity.PerUnit - 1) / Quantity.PerUnit, PerHour(full - enough)));
                    }
                }
            }
            else if (recipe.ForestTiles > 0 && supply < full)
            {
                var (bare, inReach) = Bare(building, recipe);
                hindrances.Add(new ForestFelled(bare, inReach, PerHour(full - supply)));
            }

            // The way to the store: a depot whose carriers reach it, room in the store, and carriers enough.
            var trip = TripFrom(building.Tile);
            long room = StorageCapacity(building.Island) - store[(int)recipe.Product];
            bool explained = true;
            if (!trip.Reaches)
            {
                hindrances.Add(new NoDepot(DepotOn(building.Island), PerHour(full)));
            }
            else if (room < PerHour(full))
            {
                hindrances.Add(new StoreFull(recipe.Product, (int)Quantity.Whole(StorageCapacity(building.Island)), PerHour(full) - Max(room, 0)));
            }
            else if (reach.Share[trip.Depot] < Quantity.PerUnit)
            {
                hindrances.Add(Behind(building, recipe, trip, activeTicks, reach, advice));
            }
            else
            {
                explained = false;
            }

            // An outbox the carriers have yet to empty, though they keep up over time.
            if (made < supply && !explained)
            {
                hindrances.Add(new OutboxFull(OutboxUnits, PerHour(supply - made)));
            }

            bool hindered = false;
            foreach (var hindrance in hindrances)
            {
                hindered |= hindrance.LostPerHour > 0;
            }

            var production = made == 0 ? Production.Stopped : hindered || made < full ? Production.Reduced : Production.Full;
            return new BuildingDiagnosis(building, production, recipe.Product, PerHour(made), hindrances);
        }

        /// <summary>Why the carriers do not bring a building's goods to the store, and what a road, a carrier or a storehouse would give.</summary>
        private CarriersBehind Behind(Building building, Recipe recipe, Trip trip, int activeTicks, Reach reach, Dictionary<Hex, Advice> advice)
        {
            var depot = BuildingAt(trip.Depot)!;
            var help = AdviceFor(building, depot, activeTicks, reach, advice);
            long now = reach.PerHour[building.Tile];
            return new CarriersBehind(
                trip,
                (int)(reach.Share[trip.Depot] * 100 / Quantity.PerUnit),
                now,
                NominalPerHour(building, recipe, activeTicks),
                help.Road.Count,
                help.WithRoad?.PerHour[building.Tile] ?? now,
                help.WithCarrier?.PerHour[building.Tile],
                help.Site,
                help.Beside?.PerHour[building.Tile] ?? now,
                FreeVillagersOf(_settlements[depot.Island]) > 0);
        }

        /// <summary>What a road along a building's way, one more carrier at its depot and a storehouse beside it would give.</summary>
        private Advice AdviceFor(Building building, Building depot, int activeTicks, Reach reach, Dictionary<Hex, Advice> advice)
        {
            if (advice.TryGetValue(building.Tile, out var known))
            {
                return known;
            }

            string island = MapIslandOf(building.Tile)!;
            var road = RoadToDepot(building.Tile);
            var help = new Advice(road)
            {
                WithRoad = road.Count > 0 ? ReachOn(island, activeTicks, new HashSet<Hex>(road), null, null) : null,
                WithCarrier = depot.Carriers < MostCarriers ? ReachOn(island, activeTicks, null, null, depot.Tile) : null,
            };

            // The site beside it where a storehouse would bring most of its goods; the first counter-clockwise from east.
            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var site = building.Tile.Neighbour(direction);
                if (CheckBuild(BuildingKind.Storehouse, site) != Rejection.None || MapIslandOf(site) != island)
                {
                    continue;
                }

                var beside = ReachOn(island, activeTicks, null, site, null);
                if (help.Beside is null || beside.PerHour[building.Tile] > help.Beside.PerHour[building.Tile])
                {
                    help.Site = site;
                    help.Beside = beside;
                }
            }

            advice[building.Tile] = help;
            return help;
        }

        private DepotDiagnosis DiagnoseDepot(Building depot, int activeTicks, Reach reach, Dictionary<Hex, Advice> advice)
        {
            var serves = new List<Building>();
            foreach (var building in _buildings)
            {
                var trip = TripFrom(building.Tile);
                if (!IsDepot(building.Kind) && trip.Reaches && trip.Depot == depot.Tile && _content.For(building.Kind).Recipe is not null)
                {
                    serves.Add(building);
                }
            }

            long need = reach.Need.TryGetValue(depot.Tile, out long n) ? n : 0;
            long share = reach.Share.TryGetValue(depot.Tile, out long s) ? s : Quantity.PerUnit;
            IReadOnlyList<Gain>? withCarrier = null;
            RoadAdvice? road = null;
            StorehouseAdvice? storehouse = null;
            if (depot.Carriers > 0 && depot.Carriers < MostCarriers)
            {
                withCarrier = Gains(reach, ReachOn(MapIslandOf(depot.Tile)!, activeTicks, null, null, depot.Tile));
            }

            if (share < Quantity.PerUnit)
            {
                long bestRoad = 0, bestSite = 0;
                foreach (var building in serves)
                {
                    if (!building.Staffed)
                    {
                        continue;
                    }

                    var help = AdviceFor(building, depot, activeTicks, reach, advice);
                    if (help.WithRoad is Reach withRoad && Total(reach, withRoad) is long gained && gained > bestRoad)
                    {
                        bestRoad = gained;
                        road = new RoadAdvice(building, help.Road, Gains(reach, withRoad));
                    }

                    if (help.Beside is Reach beside && help.Site is Hex site && Total(reach, beside) is long more && more > bestSite)
                    {
                        bestSite = more;
                        storehouse = new StorehouseAdvice(site, building, Gains(reach, beside));
                    }
                }
            }

            return new DepotDiagnosis(depot, need, (int)(share * 100 / Quantity.PerUnit), serves, withCarrier, road, storehouse, FreeVillagersOf(_settlements[depot.Island]) > 0);
        }

        /// <summary>
        /// What reaches the store from each building on an island an active hour, as the depots plan their carriers:
        /// each depot's carriers, a workday counted, against the carrier-hours its buildings need, with
        /// <paramref name="roads"/>, a staffed <paramref name="depot"/> with one carrier, or one more carrier at
        /// <paramref name="carrier"/> added. Each building keeps its depot, with the roads too; a new depot takes over
        /// the buildings it reaches fastest, as it would at the next dawn.
        /// </summary>
        private Reach ReachOn(string island, int activeTicks, ICollection<Hex>? roads, Hex? depot, Hex? carrier)
        {
            var nearest = depot is not null ? Walk(island, roads, depot, null) : null;
            var walks = new Dictionary<Hex, Dictionary<Hex, Trip>>();
            var reach = new Reach();
            foreach (var building in _buildings)
            {
                if (!IsDepot(building.Kind) && MapIslandOf(building.Tile) == island)
                {
                    var trip = TripFrom(building.Tile);
                    if (nearest is not null)
                    {
                        trip = TripOn(nearest, building.Tile, island, roads);
                    }
                    else if (roads is not null && trip.Reaches)
                    {
                        if (!walks.TryGetValue(trip.Depot, out var walk))
                        {
                            walk = Walk(island, roads, null, trip.Depot);
                            walks.Add(trip.Depot, walk);
                        }

                        trip = TripOn(walk, building.Tile, island, roads);
                    }

                    reach.Trips[building.Tile] = trip;
                    if (trip.Reaches)
                    {
                        reach.Need[trip.Depot] = (reach.Need.TryGetValue(trip.Depot, out long need) ? need : 0) + NeedOf(building, trip, activeTicks);
                    }
                }
            }

            void Share(Hex tile, int carriers, int workday)
            {
                long hours = carriers * Quantity.PerUnit * workday / 100;
                long need = reach.Need.TryGetValue(tile, out long n) ? n : 0;
                reach.Share[tile] = need <= hours ? Quantity.PerUnit : hours * Quantity.PerUnit / need;
            }

            foreach (var building in _buildings)
            {
                if (IsDepot(building.Kind) && building.Carriers > 0 && MapIslandOf(building.Tile) == island)
                {
                    int carriers = building.Carriers + (building.Tile == carrier ? 1 : 0);
                    Share(building.Tile, carriers, WorkdayPercent(building));
                }
            }

            if (depot is Hex site)
            {
                Share(site, 1, 100);
            }

            foreach (var building in _buildings)
            {
                if (reach.Trips.TryGetValue(building.Tile, out var trip) && _content.For(building.Kind).Recipe is Recipe recipe)
                {
                    long nominal = building.Staffed ? NominalPerHour(building, recipe, activeTicks) : 0;
                    reach.PerHour[building.Tile] = trip.Reaches ? nominal * reach.Share[trip.Depot] / Quantity.PerUnit : 0;
                }
            }

            return reach;
        }

        /// <summary>How much more of each resource reaches the store in <paramref name="then"/> than <paramref name="now"/>, in resource order.</summary>
        private List<Gain> Gains(Reach now, Reach then)
        {
            var more = new long[s_resourceCount];
            foreach (var building in _buildings)
            {
                if (now.PerHour.TryGetValue(building.Tile, out long before) && _content.For(building.Kind).Recipe is Recipe recipe)
                {
                    more[(int)recipe.Product] += then.PerHour[building.Tile] - before;
                }
            }

            var gains = new List<Gain>();
            for (int resource = 0; resource < s_resourceCount; resource++)
            {
                if (more[resource] != 0)
                {
                    gains.Add(new Gain((Resource)resource, more[resource]));
                }
            }

            return gains;
        }

        private long Total(Reach now, Reach then)
        {
            long total = 0;
            foreach (var gain in Gains(now, then))
            {
                total += gain.PerHour;
            }

            return total;
        }

        /// <summary>
        /// The ships' next quarter hour on every sea route, as <see cref="Ship"/> does, after the islands' work: the routes
        /// in map order, each sharing the room in the home island's store with those before it, and the goods in the
        /// island's import order. What waits is what the ships may take; a reservation holds back what they had room for.
        /// </summary>
        private List<SeaRouteDiagnosis> DiagnoseSeaRoutes(Dictionary<string, long[]> stores)
        {
            var routes = new List<SeaRouteDiagnosis>();
            var home = stores[_home];
            long capacity = StorageCapacity(_home);
            foreach (var island in _islands)
            {
                if (!HasSeaRoute(island))
                {
                    continue;
                }

                // What the route owes for building and the food it takes out come first.
                long room = SeaRoutePerTick;
                long owed = Min(room, _owed[_mapIndex[island]]);
                room -= owed;
                var settlement = _settlements[island];
                long supplies = 0;
                foreach (var need in _content.Needs)
                {
                    supplies += Supply(home, stores[island], StorageCapacity(island), need, Quantity.Units(FoodDays * Housed(settlement, need.Need)), room - supplies);
                }

                room -= supplies;

                var keeps = Keeps(settlement);

                // What the island has above what it keeps, in its store now.
                var waiting = new List<(Resource, long)>();
                var now = _stores[island];
                var order = ImportOrder(island);
                var reserved = _settlements[island].Reserved;
                foreach (var resource in order)
                {
                    long shippable = Quantity.Whole(Shippable(now, reserved, resource, keeps));
                    if (shippable > 0)
                    {
                        waiting.Add((resource, shippable));
                    }
                }

                var hindrances = new List<Hindrance>();
                var from = stores[island];
                for (int i = 0; i < order.Count && room > 0; i++)
                {
                    var resource = order[i];
                    long wanted = Min(room, Shippable(from, reserved, resource, keeps));
                    long moved = Add(home, resource, wanted, capacity);
                    if (moved < wanted)
                    {
                        hindrances.Add(new StoreFull(resource, (int)Quantity.Whole(capacity), PerHour(wanted - moved)));
                    }

                    from[(int)resource] -= moved;
                    room -= moved;
                }

                // What the ships would take besides, in the same order, were nothing reserved: all above the needs it keeps.
                for (int i = 0; i < order.Count && room > 0 && reserved is not null; i++)
                {
                    var resource = order[i];
                    long held = reserved[(int)resource] > 0 ? Max(from[(int)resource] - Kept(from, resource, keeps), 0) : 0;
                    long more = Min(Min(room, held), Max(capacity - home[(int)resource], 0));
                    if (more > 0)
                    {
                        hindrances.Add(new Reserved(resource, reserved[(int)resource], PerHour(more)));
                        room -= more;
                    }
                }

                routes.Add(new SeaRouteDiagnosis(
                    new SeaRoute(island, _home), PerHour(SeaRoutePerTick), PerHour(SeaRoutePerTick - room), waiting, hindrances, Quantity.Whole(_owed[_mapIndex[island]] + Quantity.PerUnit - 1), PerHour(supplies)));
            }

            return routes;
        }

        /// <summary>
        /// The hindrance on the island that costs most; first in the settlement's order when two cost the same. A pause is
        /// the player's own choice and never the bottleneck. A missing raw material is eased by a workday on the building
        /// that makes it, and carriers that cannot keep up by a workday on their depot.
        /// </summary>
        private Bottleneck? BottleneckOn(string island, List<BuildingDiagnosis> buildings)
        {
            BuildingDiagnosis? worst = null;
            Hindrance? why = null;
            foreach (var building in buildings)
            {
                if (building.Building.Island != island)
                {
                    continue;
                }

                foreach (var hindrance in building.Hindrances)
                {
                    if (hindrance is not Paused && hindrance.LostPerHour > 0 && (why is null || hindrance.LostPerHour > why.LostPerHour))
                    {
                        worst = building;
                        why = hindrance;
                    }
                }
            }

            if (worst is null || why is null)
            {
                return null;
            }

            Building? workday = null;
            if (why is WaitingForInput input)
            {
                long most = 0;
                foreach (var maker in buildings)
                {
                    var b = maker.Building;
                    if (b.Island == island && maker.Product == input.Resource && b.Staffed && !b.WorkdayToday && maker.PerHour > most)
                    {
                        workday = b;
                        most = maker.PerHour;
                    }
                }
            }
            else if (why is CarriersBehind behind && BuildingAt(behind.Trip.Depot) is { Staffed: true, WorkdayToday: false } depot)
            {
                workday = depot;
            }

            return new Bottleneck(worst, why, workday);
        }

        /// <summary>
        /// What the building makes in a quarter hour when nothing holds it back: for a slow process what grows, and for a
        /// woodcutter what the forest tiles it reaches give when full.
        /// </summary>
        private long Potential(Building building, Recipe recipe)
        {
            int level = LevelPercent(building), workday = WorkdayPercent(building);
            if (recipe.IsSlowProcess)
            {
                return Scale(TileRate(recipe.GrowsPerTick, recipe, building.Tile), level, workday);
            }

            if (recipe.HuntTiles > 0)
            {
                return Scale(HuntPerTick(building, recipe), level, workday);
            }

            if (recipe.ForestTiles == 0)
            {
                return Scale(TileRate(recipe.PerTick, recipe, building.Tile), level, workday);
            }

            // Woods lists the richest first; with no forest left in reach, what it would fell from plain forest.
            var woods = Woods(building.Tile);
            if (woods.Count == 0)
            {
                return recipe.ForestTiles * Scale(recipe.PerTick, level, workday);
            }

            long potential = 0;
            for (int i = 0; i < woods.Count && i < recipe.ForestTiles; i++)
            {
                potential += Scale(TileRate(recipe.PerTick, recipe, woods[i]), level, workday);
            }

            return potential;
        }

        /// <summary>
        /// What a building could make in the next quarter hour if it were staffed, with room in its outbox: as fast as it
        /// works, as far as the raw materials in the store and at the building and the forest after it grows let it.
        /// </summary>
        private long Supply(Building building, Recipe recipe, long[]? store)
        {
            long full = Potential(building, recipe);
            store ??= _stores[building.Island];
            if (recipe.Inputs.Count > 0)
            {
                foreach (var input in recipe.InputSpan)
                {
                    long available = building.Held![(int)input.Resource] + Max(store[(int)input.Resource] - Leaves(building, input.Resource).Kept, 0);
                    full = Min(full, available / input.PerProduct);
                }

                return full;
            }

            if (recipe.ForestTiles == 0)
            {
                return full;
            }

            long felling = 0;
            int felled = 0;
            int level = LevelPercent(building), workday = WorkdayPercent(building);
            foreach (var tile in Woods(building.Tile))
            {
                long left = Min(ForestLeft(tile) + ForestRegrowthPerTick, ForestPerTile);
                felling += Min(Scale(TileRate(recipe.PerTick, recipe, tile), level, workday), left);
                if (++felled == recipe.ForestTiles)
                {
                    break;
                }
            }

            return felling;
        }

        /// <summary>The woodcutter's forest tiles with too little timber for a full quarter hour once grown, and all it reaches.</summary>
        private (int Bare, int InReach) Bare(Building building, Recipe recipe)
        {
            int bare = 0;
            var woods = Woods(building.Tile);
            foreach (var tile in woods)
            {
                long left = Min(ForestLeft(tile) + ForestRegrowthPerTick, ForestPerTile);
                bare += left < Scale(TileRate(recipe.PerTick, recipe, tile), LevelPercent(building), WorkdayPercent(building)) ? 1 : 0;
            }

            return (bare, woods.Count);
        }

        /// <summary>Whether a storehouse or a jetty stands on the island, staffed or not.</summary>
        private bool DepotOn(string island)
        {
            foreach (var building in _buildings)
            {
                if (IsDepot(building.Kind) && building.Island == island)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The settlement as it is, for the diagnosis to play a quarter hour of work on; the fog is shared, since work never lifts it.</summary>
        private Settlement CopyForWork()
        {
            var copy = new Settlement(_map, Fog, _founding, _content) { SurveysDone = SurveysDone, Step = Step };
            foreach (var island in _islands)
            {
                copy._stores.Add(island, (long[])_stores[island].Clone());
                copy._islands.Add(island);
                var settlement = _settlements[island];
                var twin = new IslandSettlement(copy, island, _tiers)
                {
                    Reserved = (int[]?)settlement.Reserved?.Clone(),
                    Imports = settlement.Imports,
                };
                for (int tier = 0; tier < _tiers; tier++)
                {
                    twin.Add((PopulationTier)tier, settlement.VillagersOf((PopulationTier)tier));
                }

                copy._settlements.Add(island, twin);
                if (_atDepots.TryGetValue(island, out var atDepots))
                {
                    copy._atDepots.Add(island, (long[])atDepots.Clone());
                }
            }

            _owed.CopyTo(copy._owed, 0);

            foreach (var building in _buildings)
            {
                var twin = building.Copy();
                copy._buildings.Add(twin);
                copy._buildingAt.Add(twin.Tile, twin);
            }

            foreach (var forest in _felled)
            {
                copy._felled.Add(forest.Key, forest.Value);
            }

            foreach (var road in _roads)
            {
                copy._roads.Add(road);
            }

            return copy;
        }

        private long TileRate(long perTick, Recipe recipe, Hex tile) => recipe.Richness ? Scale(perTick, _map.RichnessAt(tile)) : perTick;

        private static long PerHour(long perTick) => perTick * Recipe.TicksPerHour;

        private static long Max(long a, long b) => a > b ? a : b;

        /// <summary>What reaches the store on an island by the carriers' plan: each building's trip and rate, each depot's need and share.</summary>
        private sealed class Reach
        {
            public Dictionary<Hex, Trip> Trips { get; } = new();

            /// <summary>Millionths of carrier-hours an active hour, by the depot's tile.</summary>
            public Dictionary<Hex, long> Need { get; } = new();

            /// <summary>Millionths of what its buildings make that reaches the store, by the depot's tile.</summary>
            public Dictionary<Hex, long> Share { get; } = new();

            /// <summary>Millionths of a unit an active hour, by the building's tile.</summary>
            public Dictionary<Hex, long> PerHour { get; } = new();
        }

        /// <summary>What a road, a carrier and a storehouse would give, worked out once for each building.</summary>
        private sealed class Advice
        {
            public Advice(List<Hex> road) => Road = road;

            public List<Hex> Road { get; }

            public Reach? WithRoad { get; set; }

            public Reach? WithCarrier { get; set; }

            public Hex? Site { get; set; }

            public Reach? Beside { get; set; }
        }
    }
}
