using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>The bottleneck diagnosis (TASK-043): why a building stands still, read from the settlement.</summary>
    /// <remarks>
    /// The diagnosis walks the work of the next quarter hour with the villagers awake as <see cref="Tick"/> does it,
    /// on copies of the stores and the forest, and never changes the state (INV-D2). What a road, a storehouse beside
    /// the building or its villagers would give is the same walk on further copies, so every gain is limited by the
    /// raw materials, the forest and the room as the rules would limit it. The walk leaves out the building queue: a
    /// quarter hour in which a project finishes can staff, upgrade or give a road to a building before it works, and
    /// the diagnosis then tells what the buildings would make as they stand now. Its only writes are the caches of
    /// depot distances and the woods list, which are not state. DiagnosticsTests.TheDiagnosisForetellsTheNextQuarterHour
    /// keeps it in step with the rules.
    /// </remarks>
    internal sealed partial class Settlement
    {
        public Diagnostics Diagnose()
        {
            var stores = new Dictionary<IslandId, long[]>();
            foreach (var island in s_islands)
            {
                stores[island] = (long[])_stores[island].Clone();
            }

            var forest = new Dictionary<Hex, long>(); // Timber left on the tiles felled so far this quarter hour.
            var buildings = new List<BuildingDiagnosis>(_buildings.Count);
            int free = Villagers; // As StaffBuildings gives them out: in the staffing order.
            foreach (var building in _buildings)
            {
                var spec = _content.For(building.Kind);
                int lacking = !building.Staffed && !building.Paused && spec.Staff > free ? spec.Staff - free : 0;
                free -= building.Staffed ? spec.Staff : 0;

                // Staffing matters only to what a building makes: a storehouse or a jetty without its villagers still
                // stores goods and is a depot, so it never stands still for want of them.
                buildings.Add(spec.Recipe is Recipe recipe
                    ? DiagnoseProduction(building, recipe, lacking, stores[building.Island], forest)
                    : new BuildingDiagnosis(building, Production.None, null, 0, System.Array.Empty<Hindrance>()));
            }

            var seaRoute = DiagnoseSeaRoute(stores);
            var bottlenecks = new Dictionary<IslandId, Bottleneck?>();
            foreach (var island in s_islands)
            {
                bottlenecks[island] = BottleneckOn(island, buildings);
            }

            return new Diagnostics(buildings, seaRoute, bottlenecks, stores);
        }

        private BuildingDiagnosis DiagnoseProduction(Building building, Recipe recipe, int lacking, long[] store, Dictionary<Hex, long> forest)
        {
            var hindrances = new List<Hindrance>();
            int distance = DistancePercent(building);
            if (!building.Staffed)
            {
                // What it would make with its villagers, here and now, is what the pause or the missing villagers cost.
                long would = WorkOf(building, recipe, distance, (long[])store.Clone(), new Dictionary<Hex, long>(forest)).Made;
                hindrances.Add(building.Paused ? new Paused(PerHour(would)) : new MissingStaff(lacking, PerHour(would)));
                return new BuildingDiagnosis(building, Production.Stopped, recipe.Product, 0, hindrances);
            }

            // Beside a depot and with a road, from the same stores and forest as the work itself.
            var ideal = WorkOf(building, recipe, 100, (long[])store.Clone(), new Dictionary<Hex, long>(forest));
            int steps = DepotDistance(building.Tile);
            int? road = distance < 100 && !building.HasRoad && steps > 1 && steps != Unreachable ? DistancePercent(steps, true) : null;
            var withRoad = road is int percent ? WorkOf(building, recipe, percent, (long[])store.Clone(), new Dictionary<Hex, long>(forest)) : default;
            var now = WorkOf(building, recipe, distance, store, forest);

            // Raw materials and nature, against what it makes at full pace beside a depot with nothing else holding it back.
            long full = Potential(building, recipe, 100);
            if (recipe.Inputs.Count > 0)
            {
                foreach (var input in recipe.Inputs)
                {
                    long stock = now.Stocks[input.Resource];
                    long available = stock - Quantity.Units(ProcessingReserve);
                    long enough = available > 0 ? available / input.PerProduct : 0;
                    if (enough < full)
                    {
                        // What a full hour at full pace takes above the reserve, in whole units rounded up.
                        long wanted = Quantity.Units(ProcessingReserve) + (input.PerProduct * full * Recipe.TicksPerHour) - stock;
                        hindrances.Add(new WaitingForInput(
                            input.Resource, Quantity.Whole(stock), ProcessingReserve, (wanted + Quantity.PerUnit - 1) / Quantity.PerUnit, PerHour(full - enough)));
                    }
                }
            }
            else if (recipe.ForestTiles > 0 && ideal.Supply < full)
            {
                hindrances.Add(new ForestFelled(now.Felled, now.InReach, PerHour(full - ideal.Supply)));
            }

            if (now.Made < now.Supply)
            {
                // What a full store costs a slow process over time is what grows; what has grown waits on the field.
                long lost = now.Supply - now.Made;
                hindrances.Add(new StoreFull(recipe.Product, (int)Quantity.Whole(StorageCapacity(building.Island)), PerHour(recipe.IsSlowProcess ? Min(lost, now.Grows) : lost)));
            }

            if (ideal.Made > now.Made)
            {
                hindrances.Add(new LongWay(steps, distance, road, PerHour(now.Made), PerHour(road is null ? now.Made : withRoad.Made), PerHour(ideal.Made)));
            }

            var production = now.Made == 0 ? Production.Stopped : hindrances.Count == 0 ? Production.Full : Production.Reduced;
            return new BuildingDiagnosis(building, production, recipe.Product, PerHour(now.Made), hindrances);
        }

        /// <summary>
        /// A staffed building's work in the next quarter hour at <paramref name="distance"/> percent, as <see cref="Produce"/>
        /// does it, on <paramref name="store"/> and <paramref name="forest"/>, which it changes as the work would.
        /// </summary>
        private QuarterOfWork WorkOf(Building building, Recipe recipe, int distance, long[] store, Dictionary<Hex, long> forest)
        {
            long capacity = StorageCapacity(building.Island);
            int level = LevelPercent(building), workday = WorkdayPercent(building);
            var stocks = new Dictionary<Resource, long>();
            foreach (var input in recipe.Inputs)
            {
                stocks[input.Resource] = store[(int)input.Resource];
            }

            if (recipe.ForestTiles > 0)
            {
                return Felling(building, recipe, store, forest, capacity, distance, stocks);
            }

            if (recipe.IsSlowProcess)
            {
                // A staffed field or sheep farm grows before the villagers harvest it (Tick).
                long grows = Scale(TileRate(recipe.GrowsPerTick, recipe, building.Tile), distance, level, workday);
                long ripe = Min(building.Ripe + grows, Scale(recipe.PerTick, distance, level, workday));
                return new QuarterOfWork(ripe, Add(store, recipe.Product, ripe, capacity), stocks, grows);
            }

            long most = Scale(TileRate(recipe.PerTick, recipe, building.Tile), distance, level, workday);
            foreach (var input in recipe.Inputs)
            {
                long available = store[(int)input.Resource] - Quantity.Units(ProcessingReserve);
                most = Min(most, available > 0 ? available / input.PerProduct : 0);
            }

            long made = Add(store, recipe.Product, most, capacity);
            foreach (var input in recipe.Inputs)
            {
                store[(int)input.Resource] -= input.PerProduct * made;
            }

            return new QuarterOfWork(most, made, stocks);
        }

        /// <summary>What a woodcutter fells, as <see cref="Fell(Building, Recipe, long[], long, int, int, int)"/> does.</summary>
        private QuarterOfWork Felling(Building building, Recipe recipe, long[] store, Dictionary<Hex, long> forest, long capacity, int distance, Dictionary<Resource, long> stocks)
        {
            long Left(Hex tile)
            {
                if (forest.TryGetValue(tile, out long left))
                {
                    return left;
                }

                // The forest grows back before the villagers work (Tick).
                long grown = ForestLeft(tile) + ForestRegrowthPerTick;
                return grown < ForestPerTile ? grown : ForestPerTile;
            }

            // Sorted as Woods sorts them, but by the timber left once the forest has grown and others have felled.
            var woods = new List<Hex>();
            foreach (var tile in Woods(building.Tile))
            {
                int at = woods.Count;
                while (at > 0 && FellsBefore(tile, woods[at - 1], Left))
                {
                    at--;
                }

                woods.Insert(at, tile);
            }

            long felling = 0, made = 0;
            int felled = 0, bare = 0;
            foreach (var tile in woods)
            {
                long rate = Scale(TileRate(recipe.PerTick, recipe, tile), distance, LevelPercent(building), WorkdayPercent(building));
                long left = Left(tile);
                bare += left < rate ? 1 : 0;
                if (left == 0 || felled == recipe.ForestTiles)
                {
                    continue;
                }

                long wanted = Min(rate, left);
                long timber = Add(store, recipe.Product, wanted, capacity);
                forest[tile] = left - timber;
                felling += wanted;
                made += timber;
                felled++;
            }

            return new QuarterOfWork(felling, made, stocks, felled: bare, inReach: woods.Count);
        }

        private bool FellsBefore(Hex a, Hex b, System.Func<Hex, long> left)
        {
            int richness = _map.RichnessAt(a).CompareTo(_map.RichnessAt(b));
            if (richness != 0)
            {
                return richness > 0;
            }

            int timber = left(a).CompareTo(left(b));
            return timber != 0 ? timber > 0 : a < b;
        }

        /// <summary>The ships' next quarter hour, as <see cref="Ship"/> does, after the islands' production.</summary>
        private SeaRouteDiagnosis DiagnoseSeaRoute(Dictionary<IslandId, long[]> stores)
        {
            var waiting = new List<(Resource, long)>();
            var nyon = _stores[IslandId.Nyon];
            for (int r = 0; r < s_resourceCount; r++)
            {
                if (Quantity.Whole(nyon[r]) > 0)
                {
                    waiting.Add(((Resource)r, Quantity.Whole(nyon[r])));
                }
            }

            var hindrances = new List<Hindrance>();
            long room = SeaRoutePerTick;
            if (SeaRouteOpen)
            {
                var from = stores[IslandId.Nyon];
                var home = stores[IslandId.Home];
                long capacity = StorageCapacity(IslandId.Home);
                for (int r = 0; r < s_resourceCount && room > 0; r++)
                {
                    long wanted = Min(room, from[r]);
                    long moved = Add(home, (Resource)r, wanted, capacity);
                    if (moved < wanted)
                    {
                        hindrances.Add(new StoreFull((Resource)r, (int)Quantity.Whole(capacity), PerHour(wanted - moved)));
                    }

                    from[r] -= moved;
                    room -= moved;
                }
            }

            long shipped = SeaRouteOpen ? SeaRoutePerTick - room : 0;
            return new SeaRouteDiagnosis(SeaRouteOpen, PerHour(SeaRoutePerTick), PerHour(shipped), waiting, hindrances);
        }

        /// <summary>
        /// The hindrance on the island that costs most; first in the settlement's order when two cost the same. A pause is
        /// the player's own choice and never the bottleneck. A missing raw material is eased by a workday on the building
        /// that makes it.
        /// </summary>
        private static Bottleneck? BottleneckOn(IslandId island, List<BuildingDiagnosis> buildings)
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

            return new Bottleneck(worst, why, workday);
        }

        /// <summary>
        /// What the building makes in a quarter hour at <paramref name="distance"/> percent when nothing holds it back: for a
        /// slow process what grows, and for a woodcutter what the forest tiles it reaches give when full.
        /// </summary>
        private long Potential(Building building, Recipe recipe, int distance)
        {
            int level = LevelPercent(building), workday = WorkdayPercent(building);
            if (recipe.IsSlowProcess)
            {
                return Scale(TileRate(recipe.GrowsPerTick, recipe, building.Tile), distance, level, workday);
            }

            if (recipe.ForestTiles == 0)
            {
                return Scale(TileRate(recipe.PerTick, recipe, building.Tile), distance, level, workday);
            }

            // Woods lists the richest first; with no forest left in reach, what it would fell from plain forest.
            var woods = Woods(building.Tile);
            if (woods.Count == 0)
            {
                return recipe.ForestTiles * Scale(recipe.PerTick, distance, level, workday);
            }

            long potential = 0;
            for (int i = 0; i < woods.Count && i < recipe.ForestTiles; i++)
            {
                potential += Scale(TileRate(recipe.PerTick, recipe, woods[i]), distance, level, workday);
            }

            return potential;
        }

        private long TileRate(long perTick, Recipe recipe, Hex tile) => recipe.Richness ? Scale(perTick, _map.RichnessAt(tile)) : perTick;

        private static long PerHour(long perTick) => perTick * Recipe.TicksPerHour;
    }

    /// <summary>One building's work in a quarter hour, as the diagnosis walks it.</summary>
    internal readonly struct QuarterOfWork
    {
        public QuarterOfWork(long supply, long made, Dictionary<Resource, long> stocks, long grows = 0, int felled = 0, int inReach = 0)
        {
            Supply = supply;
            Made = made;
            Stocks = stocks;
            Grows = grows;
            Felled = felled;
            InReach = inReach;
        }

        /// <summary>What the raw materials, the forest or the harvest let it make before the room in the store.</summary>
        public long Supply { get; }

        /// <summary>What reaches the store.</summary>
        public long Made { get; }

        /// <summary>Each raw material in the store when it starts.</summary>
        public Dictionary<Resource, long> Stocks { get; }

        /// <summary>What a slow process grows in the quarter hour.</summary>
        public long Grows { get; }

        /// <summary>The woodcutter's forest tiles with too little timber for a full quarter hour, and all it reaches.</summary>
        public int Felled { get; }

        public int InReach { get; }
    }
}
