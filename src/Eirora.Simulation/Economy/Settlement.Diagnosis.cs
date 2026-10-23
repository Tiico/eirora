using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>The bottleneck diagnosis (TASK-043): why a building stands still, read from the settlement.</summary>
    /// <remarks>
    /// The diagnosis walks the next quarter hour with the villagers awake as <see cref="Tick"/> would, on copies of the
    /// stores and the forest, so it tells exactly what the rules would make and never changes the state (INV-D2). Its
    /// only writes are the caches of depot distances and the woods list, which are not state.
    /// DiagnosticsTests.TheDiagnosisForetellsTheNextQuarterHour keeps it in step with the rules.
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
            int free = FreeVillagers;
            foreach (var building in _buildings)
            {
                var spec = _content.For(building.Kind);
                MissingStaff? missing = null;
                if (!building.Staffed && spec.Staff > 0)
                {
                    // As StaffBuildings takes them on: in line, from the villagers that no building employs.
                    int lacking = free >= spec.Staff ? 0 : spec.Staff - free;
                    free -= lacking == 0 ? spec.Staff : 0;
                    missing = new MissingStaff(lacking, spec.Recipe is Recipe recipe ? PerHour(Potential(building, recipe, DistancePercent(building))) : 0);
                }

                buildings.Add(spec.Recipe is Recipe made
                    ? DiagnoseProduction(building, made, missing, stores[building.Island], forest)
                    : new BuildingDiagnosis(building, Production.None, null, 0, missing is null ? System.Array.Empty<Hindrance>() : new Hindrance[] { missing }));
            }

            var seaRoute = DiagnoseSeaRoute(stores);
            var bottlenecks = new Dictionary<IslandId, Bottleneck?>();
            foreach (var island in s_islands)
            {
                bottlenecks[island] = BottleneckOn(island, buildings);
            }

            return new Diagnostics(buildings, seaRoute, bottlenecks, stores);
        }

        private BuildingDiagnosis DiagnoseProduction(Building building, Recipe recipe, MissingStaff? missing, long[] store, Dictionary<Hex, long> forest)
        {
            var hindrances = new List<Hindrance>();
            int distance = DistancePercent(building);
            long full = Potential(building, recipe, distance);
            long made = 0;
            if (missing is not null)
            {
                hindrances.Add(missing);
            }
            else if (recipe.ForestTiles > 0)
            {
                made = DiagnoseFelling(building, recipe, store, forest, full, distance, hindrances);
            }
            else if (recipe.IsSlowProcess)
            {
                made = DiagnoseHarvest(building, recipe, store, full, distance, hindrances);
            }
            else
            {
                made = DiagnoseProcessing(recipe, store, StorageCapacity(building.Island), full, hindrances);
            }

            if (distance < 100)
            {
                int steps = DepotDistance(building.Tile);
                int? road = !building.HasRoad && steps > 1 && steps != Unreachable ? DistancePercent(steps, true) : null;
                long withRoad = road is int percent ? Potential(building, recipe, percent) : full;
                hindrances.Add(new LongWay(steps, distance, road, PerHour(full), PerHour(withRoad), PerHour(Potential(building, recipe, 100))));
            }

            var production = made == 0 ? Production.Stopped : hindrances.Count == 0 ? Production.Full : Production.Reduced;
            return new BuildingDiagnosis(building, production, recipe.Product, PerHour(made), hindrances);
        }

        /// <summary>What a gathering or processing building makes: its rate, the raw materials above the reserve, the room.</summary>
        private static long DiagnoseProcessing(Recipe recipe, long[] store, long capacity, long full, List<Hindrance> hindrances)
        {
            long most = full;
            foreach (var input in recipe.Inputs)
            {
                long available = store[(int)input.Resource] - Quantity.Units(ProcessingReserve);
                long enough = available > 0 ? available / input.PerProduct : 0;
                if (enough < full)
                {
                    // What a full hour takes above the reserve, in whole units rounded up.
                    long wanted = Quantity.Units(ProcessingReserve) + (input.PerProduct * full * Recipe.TicksPerHour) - store[(int)input.Resource];
                    hindrances.Add(new WaitingForInput(
                        input.Resource, Quantity.Whole(store[(int)input.Resource]), ProcessingReserve, (wanted + Quantity.PerUnit - 1) / Quantity.PerUnit, PerHour(full - enough)));
                }

                most = Min(most, enough);
            }

            long made = Fill(store, recipe.Product, most, capacity, hindrances);
            foreach (var input in recipe.Inputs)
            {
                store[(int)input.Resource] -= input.PerProduct * made;
            }

            return made;
        }

        /// <summary>What a field or a sheep farm harvests: what has grown by the next quarter hour, up to its pace and the room.</summary>
        private long DiagnoseHarvest(Building building, Recipe recipe, long[] store, long grows, int distance, List<Hindrance> hindrances)
        {
            long ripe = building.Ripe + grows;
            long wanted = Min(ripe, Scale(recipe.PerTick, distance, LevelPercent(building), WorkdayPercent(building)));
            if (wanted == 0)
            {
                hindrances.Add(new NothingRipe(recipe.Product));
                return 0;
            }

            // What a full store costs a slow process over time is what grows; what has grown waits on the field.
            int before = hindrances.Count;
            long made = Fill(store, recipe.Product, wanted, StorageCapacity(building.Island), hindrances);
            if (hindrances.Count > before && hindrances[before] is StoreFull full)
            {
                hindrances[before] = new StoreFull(full.Resource, full.Capacity, PerHour(Min(wanted - made, grows)));
            }

            return made;
        }

        /// <summary>What a woodcutter fells, as <see cref="Fell"/> does: the richest, then fullest, forest tiles it reaches.</summary>
        private long DiagnoseFelling(Building building, Recipe recipe, long[] store, Dictionary<Hex, long> forest, long full, int distance, List<Hindrance> hindrances)
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

            long capacity = StorageCapacity(building.Island);
            long felling = 0, made = 0;
            int felled = 0, bare = 0;
            foreach (var tile in woods)
            {
                long rate = Scale(TileRate(recipe, tile), distance, LevelPercent(building), WorkdayPercent(building));
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

            if (felling < full)
            {
                hindrances.Add(new ForestFelled(bare, woods.Count, PerHour(full - felling)));
            }

            if (made < felling)
            {
                hindrances.Add(new StoreFull(recipe.Product, (int)Quantity.Whole(capacity), PerHour(felling - made)));
            }

            return made;
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
        /// The hindrance on the island that costs most; first in the settlement's order when two cost the same. A missing
        /// raw material is eased by a workday on the building that makes it.
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
                    if (hindrance.LostPerHour > 0 && (why is null || hindrance.LostPerHour > why.LostPerHour))
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
                long grows = recipe.Richness ? Scale(recipe.GrowsPerTick, _map.RichnessAt(building.Tile)) : recipe.GrowsPerTick;
                return Scale(grows, distance, level, workday);
            }

            if (recipe.ForestTiles == 0)
            {
                return Scale(TileRate(recipe, building.Tile), distance, level, workday);
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
                potential += Scale(TileRate(recipe, woods[i]), distance, level, workday);
            }

            return potential;
        }

        private long TileRate(Recipe recipe, Hex tile) => recipe.Richness ? Scale(recipe.PerTick, _map.RichnessAt(tile)) : recipe.PerTick;

        /// <summary>Adds what fits, as <see cref="Add"/> does, and notes a full store when not everything did.</summary>
        private static long Fill(long[] store, Resource product, long amount, long capacity, List<Hindrance> hindrances)
        {
            long made = Add(store, product, amount, capacity);
            if (made < amount)
            {
                hindrances.Add(new StoreFull(product, (int)Quantity.Whole(capacity), PerHour(amount - made)));
            }

            return made;
        }

        private static long PerHour(long perTick) => perTick * Recipe.TicksPerHour;
    }
}
