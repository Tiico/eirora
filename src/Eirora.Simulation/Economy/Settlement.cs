using System;
using System.Collections.Generic;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using static Eirora.Simulation.Economy.Logistics;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The world's economy (economy-v1.md, economy-v2.md, sections 6 and 7): a settlement on each island built on, with
    /// its villagers and store (D-106), buildings on the map's tiles, the carriers and the roads, the building queue, the
    /// forest, expeditions and the sea routes
    /// between the islands.
    /// </summary>
    /// <remarks>
    /// The economy moves in quarter-hour ticks at fixed UTC times, so advancing time in parts gives the same world as
    /// advancing at once (INV-D4). Rates are in millionths of a unit per quarter hour (INV-D3).
    /// Villagers work, build, carry, sail and explore only while awake (INV-T4); fields, sheep and forests grow day
    /// and night (INV-T5). Nothing is ever lost to time: a full outbox stops production, what the carriers cannot carry
    /// waits at the building, and missing food only stops growth (D-17, D-39, INV-R2).
    /// </remarks>
    internal sealed partial class Settlement
    {
        public const long TickSeconds = 15 * 60;

        private const long WorkPerVillagerPerTick = 250; // Thousandths of a villager-hour.
        private const int StoragePerStorehouse = 150;
        private const int StorageAtJetty = 50; // Every jetty stores goods for the ship (economy-v2.md, section 6).
        private const int ComfortPerDawn = 2;
        private const int StartingHousing = 16;
        private const long ForestPerTile = World.TimberPerForest * Quantity.PerUnit;
        private const long ForestRegrowthPerTick = 62_500; // 0.25 timber an hour on each forest tile.
        private const long SeaRoutePerTick = 625_000; // 40 units a game day over 16 waking hours (economy-v1.md, section 5).
        private const int FoodDays = 2; // The ships keep two game days' food on each island (economy-v2.md, section 6).

        private static readonly int s_resourceCount = Enum.GetValues(typeof(Resource)).Length;
        private static readonly Resource[] s_food = { Resource.Fish, Resource.Grain }; // In the order villagers eat them.

        // The store on each island where something has been built, by the map's id; it stays when what was built goes.
        private readonly Dictionary<string, long[]> _stores = new(StringComparer.Ordinal);
        private readonly List<string> _islands = new(); // The islands with a store, in map order.
        private readonly Dictionary<string, IslandSettlement> _settlements = new(StringComparer.Ordinal); // One with each store.

        // Goods that wait at an island's depots for room in its store, as the migration to rules 8 left them; only islands
        // with any have an entry.
        private readonly Dictionary<string, long[]> _atDepots = new(StringComparer.Ordinal);

        // Materials paid from the home island's store for work on another island, which its sea route carries before
        // anything else, by the island's place in the map's list (D-106, D-113).
        private readonly long[] _owed;
        private readonly Dictionary<string, int> _mapIndex = new(StringComparer.Ordinal);
        private readonly List<Building> _buildings = new();
        private readonly Dictionary<Hex, Building> _buildingAt = new(); // Only looked up, never iterated.
        private readonly List<Project> _projects = new();

        // Timber left on the forest tiles that are not full, in map order; every other forest tile holds ForestPerTile.
        private readonly SortedDictionary<Hex, long> _felled = new();
        private readonly WorldMap _map;
        private readonly string _home;
        private readonly Founding _founding;
        private readonly ContentCatalog _content;
        private readonly List<Hex> _woods = new();
        private readonly List<Hex> _regrowing = new();
        private long[] _before = new long[64]; // The state before and after a tick's work (RecordState).
        private long[] _after = new long[64];
        private readonly Dictionary<string, long> _capacity = new(StringComparer.Ordinal); // Forgotten whenever a building is added.
        private readonly Dictionary<string, int> _unstaffed = new(StringComparer.Ordinal); // Villagers left as each island's are given out.
        private readonly Dictionary<string, long> _labour = new(StringComparer.Ordinal); // Each settlement's free villagers' work in a tick.
        private readonly List<string> _pools = new(); // The islands that build a project (PoolsOf).
        private long _expeditionLeft;

        private Settlement(WorldMap map, Fog fog, Founding founding, ContentCatalog content)
        {
            _map = map;
            _home = map.IslandAt(map.Start) ?? throw new ArgumentException("A map starts on land.", nameof(map));
            _founding = founding;
            _content = content;
            Fog = fog;
            _owed = new long[map.Islands.Count];
            for (int i = 0; i < map.Islands.Count; i++)
            {
                _mapIndex[map.Islands[i].Id] = i;
            }
        }

        /// <summary>The buildings it knows and what they cost and produce.</summary>
        public ContentCatalog Content => _content;

        /// <summary>What of the map is known (TASK-032); only a known tile can be built on.</summary>
        public Fog Fog { get; private set; }

        /// <summary>The villagers of every settlement together.</summary>
        public int Villagers
        {
            get
            {
                int villagers = 0;
                foreach (var island in _islands)
                {
                    villagers += _settlements[island].Villagers;
                }

                return villagers;
            }
        }

        /// <summary>The settlements, in map order: the home island's and one on every island built on (D-106).</summary>
        public List<IslandSettlement> Settlements
        {
            get
            {
                var settlements = new List<IslandSettlement>(_islands.Count);
                foreach (var island in _islands)
                {
                    settlements.Add(_settlements[island]);
                }

                return settlements;
            }
        }

        public int SurveysDone { get; private set; }

        /// <summary>The buildings in the staffing order (economy-v1.md, section 4.1), which is also the order they work in.</summary>
        public IReadOnlyList<Building> Buildings => _buildings;

        /// <summary>The buildings that need staff, in the order villagers are given to them when they do not suffice for all.</summary>
        public List<Building> StaffingOrder
        {
            get
            {
                var order = new List<Building>();
                foreach (var building in _buildings)
                {
                    if (_content.For(building.Kind).Staff > 0)
                    {
                        order.Add(building);
                    }
                }

                return order;
            }
        }

        public IReadOnlyList<Project> Projects => _projects;

        /// <summary>Hours of travel the expedition at sea has left, or 0 when none is out.</summary>
        public int ExpeditionHoursLeft => (int)((_expeditionLeft + 999) / 1000);

        /// <summary>The housing of every settlement together.</summary>
        public int Housing
        {
            get
            {
                int housing = 0;
                foreach (var island in _islands)
                {
                    housing += HousingOf(_settlements[island]);
                }

                return housing;
            }
        }

        /// <summary>The villagers of every settlement together who neither work in a building nor carry, and so build.</summary>
        public int FreeVillagers
        {
            get
            {
                int free = 0;
                foreach (var island in _islands)
                {
                    free += FreeVillagersOf(_settlements[island]);
                }

                return free;
            }
        }

        /// <summary>The map's id of the island the world began on, whose store pays for what another island's cannot.</summary>
        public string HomeIsland => _home;

        /// <summary>The islands with a store of their own, in map order: the home island and every island built on.</summary>
        public IReadOnlyList<string> Islands => _islands;

        /// <summary>
        /// The sea routes, in map order: one between each island with a jetty and the home island, once a jetty stands
        /// there too (economy-v2.md, section 6).
        /// </summary>
        public IReadOnlyList<SeaRoute> SeaRoutes
        {
            get
            {
                var routes = new List<SeaRoute>();
                foreach (var island in _islands)
                {
                    if (HasSeaRoute(island))
                    {
                        routes.Add(new SeaRoute(island, _home));
                    }
                }

                return routes;
            }
        }

        /// <summary>What the world started with on its map, and what its surveys and first expedition find.</summary>
        public Founding Founding => _founding;

        /// <summary>The island the expedition sails for, or null when the map has none worth the journey.</summary>
        private string? ExpeditionTarget => _founding.ExpeditionIsland;

        /// <summary>The settlement a new world starts with on the home island; the surveys' tiles wait under the fog.</summary>
        public static Settlement Found(WorldMap map, ContentCatalog content)
        {
            var founding = Founding.For(map);
            var hidden = new List<Hex>();
            foreach (var survey in founding.Surveys)
            {
                hidden.AddRange(survey.Reveals);
            }

            var settlement = new Settlement(map, Fog.AtStart(map, hidden), founding, content);
            var home = settlement.StoreOn(settlement._home);
            home[(int)Resource.Fish] = Quantity.Units(30);
            home[(int)Resource.Timber] = Quantity.Units(40);
            home[(int)Resource.Planks] = Quantity.Units(20);
            home[(int)Resource.Stone] = Quantity.Units(20);
            settlement._settlements[settlement._home].Villagers = 12;

            foreach (var (kind, tile) in founding.StartBuildings)
            {
                settlement.Complete(kind, tile);
            }

            settlement.StaffBuildings();
            return settlement;
        }

        /// <summary>Stock on the home island.</summary>
        public long Stock(Resource resource) => _stores[_home][(int)resource];

        /// <summary>The settlement on an island, by the map's id, or null when none has been founded there.</summary>
        public IslandSettlement? SettlementOn(string island) => _settlements.TryGetValue(island, out var settlement) ? settlement : null;

        /// <summary>
        /// What of a resource in an island's store waits for the ships to take it home: all of it but the larger of the two
        /// days' food the island keeps for its villagers (economy-v2.md, section 6) and what is reserved there (TASK-046).
        /// </summary>
        public long ForTheShips(string island, Resource resource)
        {
            if (!_stores.TryGetValue(island, out var store) || island == _home)
            {
                return 0;
            }

            var settlement = _settlements[island];
            return Shippable(store, settlement.Reserved, resource, Quantity.Units(FoodDays * settlement.Villagers));
        }

        /// <summary>Goods waiting at an island's depots for room in its store; nothing on most islands.</summary>
        public long AtDepot(string island, Resource resource) => _atDepots.TryGetValue(island, out var waiting) ? waiting[(int)resource] : 0;

        /// <summary>Materials paid from the home island for work on an island that its sea route has yet to carry, in millionths.</summary>
        public long Owed(string island) => _mapIndex.TryGetValue(island, out int at) ? _owed[at] : 0;

        /// <summary>How many villagers a settlement can house (economy-v2.md, section 6).</summary>
        internal int HousingOf(IslandSettlement settlement)
        {
            int housing = settlement.Island == _home ? StartingHousing : IslandSettlement.SettlerHousing;
            foreach (var building in _buildings)
            {
                if (building.Island == settlement.Island)
                {
                    housing += _content.For(building.Kind).Housing;
                }
            }

            return housing;
        }

        /// <summary>A settlement's villagers who neither staff a building on its island nor carry for one of its depots.</summary>
        internal int FreeVillagersOf(IslandSettlement settlement)
        {
            int free = settlement.Villagers;
            foreach (var building in _buildings)
            {
                if (building.Island == settlement.Island)
                {
                    free -= IsDepot(building.Kind) ? building.Carriers : building.Staffed ? _content.For(building.Kind).Staff : 0;
                }
            }

            return free;
        }

        /// <summary>The villagers that the buildings on an island lack: those neither staffed nor paused that need staff.</summary>
        internal int MissingStaffOn(string island)
        {
            int missing = 0;
            foreach (var building in _buildings)
            {
                int staff = _content.For(building.Kind).Staff;
                if (building.Island == island && staff > 0 && !building.Staffed && !building.Paused)
                {
                    missing += staff;
                }
            }

            return missing;
        }

        /// <summary>
        /// Whether a sea route joins two islands' settlements: one of them is the home island and a jetty stands on each
        /// (economy-v2.md, section 6).
        /// </summary>
        internal bool Linked(string island, string other) =>
            island != other && (island == _home || other == _home) && HasBuilt(BuildingKind.Jetty, island) && HasBuilt(BuildingKind.Jetty, other);

        /// <summary>Stock on an island, by the map's id; nothing on an island without a store.</summary>
        public long Stock(string island, Resource resource) => _stores.TryGetValue(island, out var store) ? store[(int)resource] : 0;

        /// <summary>
        /// Whether the fog can be lifted from a tile with Exploration (TASK-032): at the edge of the known, and neither
        /// a tile that a survey will reveal nor land of an island an expedition has yet to find, so surveys and
        /// expeditions keep their worth.
        /// </summary>
        public Rejection CheckLiftFog(Hex hex)
        {
            if (Fog.IsKnown(hex))
            {
                return Rejection.AlreadyDone;
            }

            // An island the expedition finds says so wherever it is tapped, not "lift closer first" only to refuse later.
            if (_map.Contains(hex) && _map.IslandAt(hex) is string island && island != _home && !IslandFound(island))
            {
                return Rejection.FoundByExpedition;
            }

            if (!Fog.CanLift(hex))
            {
                return Rejection.NotNextToTheKnown;
            }

            for (int survey = SurveysDone; survey < _founding.Surveys.Count; survey++)
            {
                foreach (var tile in _founding.Surveys[survey].Reveals)
                {
                    if (tile == hex)
                    {
                        return Rejection.FoundBySurvey;
                    }
                }
            }

            return Rejection.None;
        }

        public bool HasBuilt(BuildingKind kind, string? island = null)
        {
            foreach (var building in _buildings)
            {
                if (building.Kind == kind && (island is null || building.Island == island))
                {
                    return true;
                }
            }

            return false;
        }

        public Building? BuildingAt(Hex tile) => _buildingAt.TryGetValue(tile, out var building) ? building : null;

        /// <summary>
        /// What a known tile is good for now (D-79): what the map gives, except forest when there is no known forest
        /// left that a woodcutter could fell, so that nothing is told about what lies under the fog (D-93).
        /// </summary>
        public TileUse UsesOf(Hex tile)
        {
            if (!Fog.IsKnown(tile))
            {
                return TileUse.None;
            }

            var uses = _map.Features.UsesOf(tile);
            return (uses & TileUse.Forest) != 0 && Woods(tile).Count == 0 ? uses & ~TileUse.Forest : uses;
        }

        /// <summary>
        /// Whether <paramref name="kind"/> can be built on <paramref name="tile"/> right now (D-79): the tile is known,
        /// free and good for what the building requires.
        /// </summary>
        public Rejection CheckBuild(BuildingKind kind, Hex tile)
        {
            if ((UsesOf(tile) & _content.For(kind).Requirement) == 0 || BuildingAt(tile) is not null || IsQueued(tile, ProjectKind.Build))
            {
                return Rejection.SiteUnavailable;
            }

            // Builders reach another island by sea, from a jetty at home to the island's own, so those come first
            // (D-122; economy-v2.md, section 6): the home island's free villagers build the jetty, and then a sea route joins
            // the island's settlement to the others, whose free villagers build there too (D-113).
            var island = IslandOf(tile);
            if (island != _home && (!HasBuilt(BuildingKind.Jetty, _home) || (kind != BuildingKind.Jetty && !HasBuilt(BuildingKind.Jetty, island))))
            {
                return Rejection.SiteUnavailable;
            }

            return Rejection.None;
        }

        /// <summary>
        /// The map's id of the island a building site belongs to: its own for land, and for fishing water the land it
        /// lies off; the home island for open sea, where nothing is built. Each island keeps its own store, joined to the
        /// home island's by a sea route (economy-v1.md, section 5).
        /// </summary>
        public string IslandOf(Hex tile) => MapIslandOf(tile) ?? _home;

        /// <summary>Timber left on a forest tile.</summary>
        public long ForestLeft(Hex tile) => _felled.TryGetValue(tile, out long left) ? left : ForestPerTile;

        /// <summary>
        /// Whether work on <paramref name="tile"/> can be paid: from its island's store when that holds it all, otherwise
        /// from the home island's (economy-v2.md, section 6).
        /// </summary>
        public bool CanAfford(IReadOnlyDictionary<Resource, int> cost, Hex tile) => PayerFor(cost, IslandOf(tile)) is not null;

        public bool IsQueued(Hex tile, ProjectKind kind) => Queued(tile, kind) is not null;

        /// <summary>The project of <paramref name="kind"/> for <paramref name="tile"/> in the queue, or null.</summary>
        public Project? Queued(Hex tile, ProjectKind kind)
        {
            foreach (var project in _projects)
            {
                if (project.Tile == tile && project.Kind == kind)
                {
                    return project;
                }
            }

            return null;
        }

        /// <summary>
        /// Pays and queues the work; the caller has checked tile, cost and forces. Materials from the home island for
        /// another island take room on its sea route, which carries them before anything else (D-106, D-113).
        /// </summary>
        public void Queue(ProjectKind kind, BuildingKind? building, Hex tile, IReadOnlyDictionary<Resource, int> cost, int workHours)
        {
            string island = IslandOf(tile);
            string payer = PayerFor(cost, island) ?? throw new InvalidOperationException("The work cannot be paid.");

            // By resource rather than over the cost, whose order is the dictionary's (docs/development.md#determinism).
            var store = _stores[payer];
            for (int resource = 0; resource < s_resourceCount; resource++)
            {
                if (cost.TryGetValue((Resource)resource, out int amount))
                {
                    store[resource] -= Quantity.Units(amount);
                    if (payer != island)
                    {
                        _owed[_mapIndex[island]] += Quantity.Units(amount);
                    }
                }
            }

            _projects.Add(new Project(kind, building, tile, workHours * 1000L));
        }

        /// <summary>Lifts the fog from the next survey area (economy-v1.md, section 6).</summary>
        public IReadOnlyList<Hex> CompleteSurvey()
        {
            var reveals = _founding.Surveys[SurveysDone].Reveals;
            foreach (var tile in reveals)
            {
                if (_map.Contains(tile))
                {
                    Fog.Lift(tile);
                }
            }

            SurveysDone++;
            return reveals;
        }

        /// <summary>Whether an expedition can set out: a jetty at home, none at sea, and an island left to find.</summary>
        public Rejection CanSendExpedition()
        {
            if (!HasBuilt(BuildingKind.Jetty, _home))
            {
                return Rejection.NoBuildingThere;
            }

            if (_expeditionLeft > 0)
            {
                return Rejection.AlreadyDone;
            }

            return ExpeditionTarget is string island && !IslandFound(island) ? Rejection.None : Rejection.NothingLeftToSurvey;
        }

        public void SendExpedition() => _expeditionLeft = PrototypeIsland.ExpeditionHours * 1000L;

        /// <summary>Pauses or resumes a building and gives the villagers out again (economy-v1.md, section 4.1).</summary>
        public void SetPaused(Building building, bool paused)
        {
            building.Paused = paused;
            StaffBuildings();
        }

        /// <summary>
        /// Moves a building that needs staff to <paramref name="place"/> in the staffing order, counted from 0, and
        /// gives the villagers out again; the buildings that need no staff keep their places among the others. Moved up,
        /// the carriers serve it first, however the others are moved later; moved down, they serve it where most waits
        /// again; left in its place, it stays as it was (D-111). Storehouses and jetties are served by no one.
        /// </summary>
        public void MoveInStaffingOrder(Building building, int place)
        {
            var others = StaffingOrder;
            int was = others.IndexOf(building);
            if (place != was && !IsDepot(building.Kind))
            {
                building.CarriedFirst = place < was;
            }

            others.Remove(building);
            _buildings.Remove(building);
            int at = place < others.Count ? _buildings.IndexOf(others[place]) : _buildings.IndexOf(others[others.Count - 1]) + 1;
            _buildings.Insert(at, building);
            ForgetServed();
            StaffBuildings();
        }

        /// <summary>Moves a project first in the queue, where the free villagers' work goes; its work so far stays with it.</summary>
        public void MoveFirst(Project project)
        {
            _projects.Remove(project);
            _projects.Insert(0, project);
        }

        /// <summary>Shortens the first project in the queue by up to <paramref name="hours"/> hours of work.</summary>
        public void Rush(int hours)
        {
            var first = _projects[0];
            first.WorkLeft = first.WorkLeft > hours * 1000L ? first.WorkLeft - (hours * 1000L) : 0;
        }

        /// <summary>
        /// One quarter hour with the villagers awake; while they rest only <see cref="GrowSlowProcesses"/> moves. Returns
        /// whether their work changed anything: when it did not, nothing but the slow processes changes until the next dawn.
        /// </summary>
        /// <remarks>
        /// The whole state that a tick can change is compared before and after the work (<see cref="RecordState"/>).
        /// Work that changes nothing has full stores or nothing to work with, and the slow processes cannot change that:
        /// what has grown is harvested only into a store with room, and a forest tile that grows back has timber left.
        /// </remarks>
        public bool Tick(long utcSeconds, List<GameEvent> events)
        {
            GrowSlowProcesses(1);
            int recorded = RecordState(ref _before);

            Unload();
            Build(utcSeconds, events);
            Work();
            Explore(utcSeconds, events);

            return RecordState(ref _after) != recorded || !_before.AsSpan(0, recorded).SequenceEqual(_after.AsSpan(0, recorded));
        }

        /// <summary>
        /// The work of a quarter hour with the villagers awake, after the building queue: the buildings make what they
        /// can into their outboxes, the carriers carry, and the ships sail.
        /// </summary>
        private void Work()
        {
            Produce();
            Carry();
            Ship();
        }

        /// <summary>
        /// Writes every value of the state that a tick can change into <paramref name="state"/>, which grows as needed,
        /// and returns how many: the surveys, the expedition, the fog, each settlement's villagers, store and goods at its
        /// depots, what each sea route owes, each building, the queue, the roads and the felled forest, each list after its
        /// length. Roads are only ever added, so their count tells every change. A test fails when a field is added to the
        /// economy, a settlement, a building, a project or the fog that is neither recorded here nor fixed or derived
        /// (FastForwardTests.EveryFieldThatATickCanChangeIsCompared).
        /// </summary>
        private int RecordState(ref long[] state)
        {
            int needed = 9 + (_islands.Count * (1 + (2 * s_resourceCount))) + _owed.Length + ((12 + s_resourceCount) * _buildings.Count) + _projects.Count + (3 * _felled.Count);
            if (state.Length < needed)
            {
                state = new long[2 * needed];
            }

            int i = 0;
            state[i++] = SurveysDone;
            state[i++] = _expeditionLeft;
            state[i++] = Fog.Lifts;
            state[i++] = _islands.Count;
            foreach (var island in _islands)
            {
                state[i++] = _settlements[island].Villagers;
                var store = _stores[island];
                _atDepots.TryGetValue(island, out var atDepots);
                for (int resource = 0; resource < s_resourceCount; resource++)
                {
                    state[i++] = store[resource];
                    state[i++] = atDepots is null ? 0 : atDepots[resource];
                }
            }

            foreach (long owed in _owed)
            {
                state[i++] = owed;
            }

            state[i++] = _buildings.Count;
            foreach (var building in _buildings)
            {
                state[i++] = building.Level;
                state[i++] = building.Staffed ? 1 : 0;
                state[i++] = building.Paused ? 1 : 0;
                state[i++] = building.WorkdayToday ? 1 : 0;
                state[i++] = building.Ripe;
                state[i++] = building.Carriers;
                state[i++] = building.CarrierCap;
                state[i++] = building.CarriedFirst ? 1 : 0;
                state[i++] = building.Depot is Hex depot ? 1 + depot.Q : 0;
                state[i++] = building.Depot?.R ?? 0;
                state[i++] = building.Waiting;
                if (building.Held is long[] held)
                {
                    for (int resource = 0; resource < s_resourceCount; resource++)
                    {
                        state[i++] = held[resource];
                    }
                }
            }

            state[i++] = _projects.Count;
            foreach (var project in _projects)
            {
                state[i++] = project.WorkLeft;
            }

            state[i++] = _roads.Count;

            state[i++] = _felled.Count;
            foreach (var forest in _felled)
            {
                state[i++] = forest.Key.Q;
                state[i++] = forest.Key.R;
                state[i++] = forest.Value;
            }

            return i;
        }

        /// <summary>
        /// Each settlement's villagers eat from its own store and it grows if it can, today's workdays end, the villagers
        /// are given out again, a villager may move over a sea route, and the depots take the carriers they need for a game
        /// day of <paramref name="activeTicks"/> waking quarter hours, each from its own island's free villagers
        /// (economy-v2.md, sections 6 and 7). Missing food stops only that settlement's growth (D-39). A settlement without
        /// villagers does not grow: they come by moving (D-106, D-121).
        /// </summary>
        public void Dawn(long utcSeconds, List<GameEvent> events, int activeTicks)
        {
            foreach (var island in _islands)
            {
                var settlement = _settlements[island];
                var store = _stores[island];
                long need = Quantity.Units(settlement.Villagers);
                need -= Take(store, Resource.Fish, need);
                need -= Take(store, Resource.Grain, need);
                bool fed = need == 0 && settlement.Villagers > 0;

                bool comfort = fed && store[(int)Resource.Wadmal] >= Quantity.Units(ComfortPerDawn);
                if (comfort)
                {
                    store[(int)Resource.Wadmal] -= Quantity.Units(ComfortPerDawn);
                }

                int growth = (fed ? 1 : 0) + (comfort ? 1 : 0);
                int room = HousingOf(settlement) - settlement.Villagers;
                growth = growth < room ? growth : room;
                if (growth > 0)
                {
                    settlement.Villagers += growth;
                    events.Add(new VillagersMovedIn(utcSeconds, growth, island));
                }
            }

            foreach (var building in _buildings)
            {
                building.WorkdayToday = false;
            }

            // Every building to the depot its carriers reach fastest; the carriers its goods no longer need go first to the
            // buildings that wait for villagers, a free villager may move to another settlement, and the depots take more
            // only of their own island's villagers that no building needs (D-106, D-109).
            AssignDepots(all: true);
            ReleaseCarriers(activeTicks);
            StaffBuildings();
            Migrate(utcSeconds, events);
            HireCarriers(activeTicks);
        }

        /// <summary>
        /// At most one villager in the whole world moves at dawn (D-106): a free one, to the settlement whose buildings lack
        /// the most villagers and that has housing for one more, from the settlement joined to it by a sea route that has
        /// the most free; the first in map order when two are alike. Only finished buildings lack villagers, so one in the
        /// queue draws no one yet (D-123). Only a free villager moves, so no building loses its staff (D-39).
        /// </summary>
        private void Migrate(long utcSeconds, List<GameEvent> events)
        {
            var wanting = new List<(int Missing, IslandSettlement Settlement)>();
            foreach (var island in _islands)
            {
                var settlement = _settlements[island];
                int missing = MissingStaffOn(island);
                if (missing > 0 && HousingOf(settlement) > settlement.Villagers)
                {
                    int at = wanting.Count;
                    while (at > 0 && wanting[at - 1].Missing < missing)
                    {
                        at--;
                    }

                    wanting.Insert(at, (missing, settlement));
                }
            }

            foreach (var (_, to) in wanting)
            {
                IslandSettlement? from = null;
                int most = 0;
                foreach (var island in _islands)
                {
                    int free = Linked(island, to.Island) ? FreeVillagersOf(_settlements[island]) : 0;
                    if (free > most)
                    {
                        from = _settlements[island];
                        most = free;
                    }
                }

                if (from is not null)
                {
                    from.Villagers--;
                    to.Villagers++;
                    events.Add(new VillagerMoved(utcSeconds, from.Island, to.Island));
                    StaffBuildings();
                    return;
                }
            }
        }

        public void Write(StateWriter writer)
        {
            writer.Line("economy").Long(SurveysDone).Long(_expeditionLeft);
            foreach (var island in _islands)
            {
                writer.Line("settlement").Text(island).Long(_settlements[island].Villagers);
            }

            foreach (var island in _islands)
            {
                var store = _stores[island];
                for (int i = 0; i < s_resourceCount; i++)
                {
                    writer.Line("stock").Text(island).Enum((Resource)i).Long(store[i]);
                }
            }

            // Only what there is, the islands in map order: most worlds have nothing waiting and nothing owed.
            foreach (var island in _islands)
            {
                if (_atDepots.TryGetValue(island, out var waiting))
                {
                    for (int i = 0; i < s_resourceCount; i++)
                    {
                        if (waiting[i] > 0)
                        {
                            writer.Line("at-depot").Text(island).Enum((Resource)i).Long(waiting[i]);
                        }
                    }
                }
            }

            for (int i = 0; i < _owed.Length; i++)
            {
                if (_owed[i] > 0)
                {
                    writer.Line("owed").Text(_map.Islands[i].Id).Long(_owed[i]);
                }
            }

            WriteStockRules(writer);

            // Buildings and projects keep their order: the staffing order and the queue. What the carriers have brought a
            // building follows it, one raw material a line.
            foreach (var b in _buildings)
            {
                writer.Line("building").Enum(b.Kind).Long(b.Tile.Q).Long(b.Tile.R).Long(b.Level).Bool(b.Staffed).Bool(b.WorkdayToday).Long(b.Ripe).Bool(b.Paused)
                    .Long(b.Carriers).Long(b.CarrierCap).Bool(b.CarriedFirst).Long(b.Waiting)
                    .Bool(b.Depot is not null).Long(b.Depot?.Q ?? 0).Long(b.Depot?.R ?? 0).Long(b.Reserve);

                for (int resource = 0; b.Held is long[] held && resource < s_resourceCount; resource++)
                {
                    if (held[resource] > 0)
                    {
                        writer.Line("held").Enum((Resource)resource).Long(held[resource]);
                    }
                }
            }

            // A road lies on a tile of its own, so its project names no building.
            foreach (var p in _projects)
            {
                var line = writer.Line("project").Enum(p.Kind);
                if (p.Building is BuildingKind kind)
                {
                    line.Enum(kind);
                }

                line.Long(p.Tile.Q).Long(p.Tile.R).Long(p.WorkLeft);
            }

            foreach (var road in _roads)
            {
                writer.Line("road").Long(road.Q).Long(road.R);
            }

            foreach (var forest in _felled)
            {
                writer.Line("forest").Long(forest.Key.Q).Long(forest.Key.R).Long(forest.Value);
            }

            writer.Line("fog").Text(Fog.ToText());
        }

        public static Settlement Read(StateReader reader, WorldMap map, ContentCatalog content)
        {
            var settlement = new Settlement(map, Fog.Unknown(map), Founding.For(map), content);
            reader.Expect("economy");
            settlement.SurveysDone = reader.Int();
            if (settlement.SurveysDone < 0 || settlement.SurveysDone > settlement._founding.Surveys.Count)
            {
                throw new FormatException($"{settlement.SurveysDone} surveys is more than the island has.");
            }

            settlement._expeditionLeft = reader.Long();
            if (settlement._expeditionLeft < 0)
            {
                throw new FormatException("An expedition never has negative time left.");
            }

            // Each settlement, the islands in map order, then each one's store in full in the same order.
            while (reader.Next("settlement"))
            {
                string island = reader.Text();
                int last = settlement._islands.Count > 0 ? IndexOf(map, settlement._islands[settlement._islands.Count - 1]) : -1;
                int villagers = reader.Int();
                if (IndexOf(map, island) <= last || villagers < 0)
                {
                    throw new FormatException($"The settlement on {island} is not on the map after the previous one with villagers.");
                }

                settlement.StoreOn(island);
                settlement._settlements[island].Villagers = villagers;
            }

            if (!settlement._stores.ContainsKey(settlement._home))
            {
                throw new FormatException($"The home island {settlement._home} has no settlement.");
            }

            foreach (var island in settlement._islands)
            {
                var store = settlement._stores[island];
                for (int i = 0; i < s_resourceCount; i++)
                {
                    reader.Expect("stock");
                    bool expected = reader.Text() == island && reader.Enum<Resource>() == (Resource)i;
                    store[i] = expected ? reader.Long() : throw new FormatException($"Expected {(Resource)i} on {island}.");
                }
            }

            (int Island, int Resource) previousWaiting = (-1, -1);
            while (reader.Next("at-depot"))
            {
                string island = reader.Text();
                var resource = reader.Enum<Resource>();
                long amount = reader.Long();
                var at = (IndexOf(map, island), (int)resource);
                if (!settlement._stores.ContainsKey(island) || amount <= 0 || at.CompareTo(previousWaiting) <= 0)
                {
                    throw new FormatException($"{resource} waiting on {island} is not after the previous goods waiting at a settlement.");
                }

                if (!settlement._atDepots.TryGetValue(island, out var waiting))
                {
                    waiting = new long[s_resourceCount];
                    settlement._atDepots.Add(island, waiting);
                }

                waiting[(int)resource] = amount;
                previousWaiting = at;
            }

            int previousOwed = -1;
            while (reader.Next("owed"))
            {
                string island = reader.Text();
                int at = IndexOf(map, island);
                long amount = reader.Long();
                if (at <= previousOwed || amount <= 0)
                {
                    throw new FormatException($"What the sea route owes {island} is not after the previous island's.");
                }

                settlement._owed[at] = amount;
                previousOwed = at;
            }

            settlement.ReadStockRules(reader, map);

            while (reader.Next("building"))
            {
                var kind = reader.Enum<BuildingKind>();
                var tile = settlement.TileOf(reader);
                if (settlement.BuildingAt(tile) is not null)
                {
                    throw new FormatException($"Two buildings stand on {tile}.");
                }

                // A building stays where it stands even on a tile that no longer suits it (INV-V1).
                string island = settlement.IslandOf(tile);
                if (!settlement._stores.ContainsKey(island))
                {
                    throw new FormatException($"{kind} on {tile} stands on {island}, which has no store.");
                }

                var building = new Building(kind, tile, island)
                {
                    Level = reader.Int(),
                    Staffed = reader.Bool(),
                    WorkdayToday = reader.Bool(),
                    Ripe = reader.Long(),
                    Paused = reader.Bool(),
                    Carriers = reader.Int(),
                    CarrierCap = reader.Int(),
                    CarriedFirst = reader.Bool(),
                    Waiting = reader.Long(),
                };
                var depotTile = reader.Bool() ? settlement.TileOf(reader) : SkipTile(reader);
                building.Reserve = reader.Int();
                if (building.Paused && building.Staffed)
                {
                    throw new FormatException($"The paused building on {tile} is staffed.");
                }

                // A staffed depot has one carrier to its cap, an unstaffed one none; no other building has carriers.
                bool carriersFit = IsDepot(kind)
                    ? building.CarrierCap is >= 1 and <= MostCarriers && (building.Staffed ? building.Carriers >= 1 && building.Carriers <= building.CarrierCap : building.Carriers == 0)
                    : building.Carriers == 0 && building.CarrierCap == 0;
                if (!carriersFit)
                {
                    throw new FormatException($"{kind} on {tile} has {building.Carriers} carriers of at most {building.CarrierCap}.");
                }

                if (building.CarriedFirst && IsDepot(kind))
                {
                    throw new FormatException($"The {kind} on {tile} is carried for first, but carries itself.");
                }

                if (building.Waiting < 0 || building.Waiting > Quantity.Units(OutboxUnits) || (building.Waiting > 0 && content.For(kind).Recipe is null))
                {
                    throw new FormatException($"{kind} on {tile} has {building.Waiting} in an outbox of {OutboxUnits}.");
                }

                var recipe = content.For(kind).Recipe;
                if (building.Reserve < 0 || (building.Reserve > 0 && (recipe is null || recipe.Inputs.Count == 0)))
                {
                    throw new FormatException($"{kind} on {tile} leaves {building.Reserve} of a raw material it does not use.");
                }

                if (depotTile is Hex depotAt)
                {
                    building.Depot = recipe is not null && !IsDepot(kind) ? depotAt : throw new FormatException($"{kind} on {tile} belongs to no depot.");
                }

                building.Held = recipe is not null && recipe.Inputs.Count > 0 ? new long[s_resourceCount] : null;
                int last = -1;
                while (reader.Next("held"))
                {
                    var resource = reader.Enum<Resource>();
                    long held = reader.Long();
                    if (building.Held is null || !recipe!.Uses(resource) || (int)resource <= last || held <= 0 || held > Quantity.Units(InputUnits))
                    {
                        throw new FormatException($"{kind} on {tile} cannot hold {held} {resource}.");
                    }

                    building.Held[(int)resource] = held;
                    last = (int)resource;
                }

                settlement._buildings.Add(building);
                settlement._buildingAt.Add(tile, building);
            }

            // Villagers staff only buildings on their own island (INV-V2 in each settlement).
            foreach (var island in settlement._islands)
            {
                if (settlement.FreeVillagersOf(settlement._settlements[island]) < 0)
                {
                    throw new FormatException($"The buildings on {island} have more staff than its settlement has villagers.");
                }
            }

            while (reader.Next("project"))
            {
                var kind = reader.Enum<ProjectKind>();
                BuildingKind? building = kind == ProjectKind.Road ? null : reader.Enum<BuildingKind>();
                settlement._projects.Add(new Project(kind, building, settlement.TileOf(reader), reader.Long()));
            }

            Hex? road = null;
            while (reader.Next("road"))
            {
                var tile = settlement.TileOf(reader);
                if (!map.TerrainAt(tile).IsLand() || (road is Hex before && tile <= before))
                {
                    throw new FormatException($"{tile} is not land after the previous road.");
                }

                settlement._roads.Add(tile);
                road = tile;
            }

            Hex? previous = null;
            while (reader.Next("forest"))
            {
                var tile = settlement.TileOf(reader);
                long left = reader.Long();
                if (map.TerrainAt(tile) != Terrain.Forest || left < 0 || left >= ForestPerTile || (previous is Hex before && tile <= before))
                {
                    throw new FormatException($"{tile} is not a felled forest tile after the previous one.");
                }

                settlement._felled[tile] = left;
                previous = tile;
            }

            reader.Expect("fog");
            settlement.Fog = Fog.Parse(map, reader.Text());
            foreach (var building in settlement._buildings)
            {
                // Only what a building makes is carried, and the settlement gives a building another depot whenever its
                // own loses its carriers.
                if (building.Depot is Hex depot
                    && (IsDepot(building.Kind) || content.For(building.Kind).Recipe is null
                        || settlement.BuildingAt(depot) is not { Carriers: > 0 } at || !IsDepot(at.Kind) || at.Island != building.Island))
                {
                    throw new FormatException($"{building.Kind} on {building.Tile} belongs to no staffed depot at {depot}.");
                }
            }

            // A building without a depot is one no staffed depot reached when it was given one, which the settlement keeps
            // true whenever the staffing changes; one written without by hand gets the nearest, as the migration to
            // rules 8 already gives it.
            settlement.AssignDepots(all: false);
            return settlement;
        }

        /// <summary>
        /// <paramref name="ticks"/> quarter hours of the slow processes, which go on day and night (INV-T5): the same as
        /// as many single ticks, since nothing else changes how fast they grow.
        /// </summary>
        public void GrowSlowProcesses(int ticks)
        {
            _regrowing.Clear();
            _regrowing.AddRange(_felled.Keys);
            foreach (var tile in _regrowing)
            {
                long grown = _felled[tile] + (ticks * ForestRegrowthPerTick);
                if (grown < ForestPerTile)
                {
                    _felled[tile] = grown;
                }
                else
                {
                    _felled.Remove(tile);
                }
            }

            // Level and a workday scale how fast a field or a sheep farm grows, as they scale every other building's
            // production (economy-v1.md, section 3; TASK-038); distance costs the carriers' time instead (D-110). The
            // harvest only empties what has grown. Only a staffed one is tended and grows, so that pausing it costs what
            // it makes (section 4.1; TASK-044).
            foreach (var building in _buildings)
            {
                if (building.Staffed && _content.For(building.Kind).Recipe is Recipe recipe && recipe.IsSlowProcess)
                {
                    long grows = recipe.Richness ? Scale(recipe.GrowsPerTick, _map.RichnessAt(building.Tile)) : recipe.GrowsPerTick;
                    building.Ripe += ticks * Scale(grows, LevelPercent(building), WorkdayPercent(building));
                }
            }
        }

        /// <summary>
        /// The free villagers work on the queue in its order (D-113): each project takes the work of its own island's
        /// settlement first and then of the settlements that a sea route joins to it. On an island that no sea route joins
        /// to the home island yet, the home island's villagers build, as the expedition has reached it: its first jetty
        /// before it has a settlement, or one that a world from rules 7 or earlier lacks. A project with no work left is
        /// finished wherever it stands. The work is what the free
        /// villagers can do as the tick begins, however a finished project gives them out again.
        /// </summary>
        private void Build(long utcSeconds, List<GameEvent> events)
        {
            if (_projects.Count == 0)
            {
                return;
            }

            foreach (var island in _islands)
            {
                _labour[island] = FreeVillagersOf(_settlements[island]) * WorkPerVillagerPerTick;
            }

            for (int i = 0; i < _projects.Count;)
            {
                var project = _projects[i];
                foreach (var island in PoolsOf(project, _pools))
                {
                    Work(project, island);
                }

                if (project.WorkLeft > 0)
                {
                    i++;
                    continue;
                }

                _projects.RemoveAt(i);
                int settlements = _settlements.Count;
                Finish(project);
                events.Add(new ProjectCompleted(utcSeconds, project.Kind, project.Building, project.Tile));
                if (_settlements.Count > settlements)
                {
                    events.Add(new SettlementFounded(utcSeconds, IslandOf(project.Tile)));
                }

                StaffBuildings();
            }
        }

        /// <summary>
        /// The islands whose free villagers build a project, in the order they give their work, into <paramref name="pools"/>:
        /// its own island's settlement, if it has one, and then the settlements that a sea route joins to it, or the home
        /// island's where no sea route joins them yet (D-113).
        /// </summary>
        private List<string> PoolsOf(Project project, List<string> pools)
        {
            pools.Clear();
            string island = IslandOf(project.Tile);
            if (_settlements.ContainsKey(island))
            {
                pools.Add(island);
            }

            if (island != _home && !HasSeaRoute(island))
            {
                pools.Add(_home);
                return pools;
            }

            foreach (var other in _islands)
            {
                if (Linked(island, other))
                {
                    pools.Add(other);
                }
            }

            return pools;
        }

        /// <summary>Gives a project as much of a settlement's work left this tick as it needs.</summary>
        private void Work(Project project, string island)
        {
            // A settlement founded by a project finished earlier in the tick has no work to give until the next.
            _labour.TryGetValue(island, out long labour);
            long used = labour < project.WorkLeft ? labour : project.WorkLeft;
            if (used > 0)
            {
                project.WorkLeft -= used;
                _labour[island] = labour - used;
            }
        }

        private void Finish(Project project)
        {
            switch (project.Kind)
            {
                case ProjectKind.Build:
                    Complete(project.Building!.Value, project.Tile);
                    break;
                case ProjectKind.Upgrade:
                    BuildingAt(project.Tile)!.Level = 2;
                    break;
                case ProjectKind.Road:
                    _roads.Add(project.Tile);
                    ForgetTrips();
                    break;
            }
        }

        /// <summary>
        /// Every staffed building makes what its recipe says into its outbox, scaled by level and a workday, as far as the
        /// outbox has room (economy-v2.md, section 7); processing works from the raw materials the carriers have brought.
        /// The diagnosis passes <paramref name="made"/> and <paramref name="supply"/> to learn, by the building's place,
        /// what each made and what it could have made with room in its outbox.
        /// </summary>
        private void Produce(long[]? made = null, long[]? supply = null)
        {
            for (int i = 0; i < _buildings.Count; i++)
            {
                var b = _buildings[i];
                if (!b.Staffed || _content.For(b.Kind).Recipe is not Recipe recipe)
                {
                    continue;
                }

                long room = Quantity.Units(OutboxUnits) - b.Waiting;
                room = room > 0 ? room : 0;
                if (room == 0 && made is null)
                {
                    continue;
                }

                int level = LevelPercent(b);
                int workday = WorkdayPercent(b);
                long could, did;
                if (recipe.ForestTiles > 0)
                {
                    (could, did) = Fell(b, recipe, room, level, workday);
                }
                else if (recipe.IsSlowProcess)
                {
                    // Richness scales how fast it grows; the harvest only empties what has grown.
                    could = Min(b.Ripe, Scale(recipe.PerTick, level, workday));
                    did = Min(could, room);
                    b.Ripe -= did;
                }
                else
                {
                    long rate = recipe.Richness ? Scale(recipe.PerTick, _map.RichnessAt(b.Tile)) : recipe.PerTick;
                    could = Process(b, recipe, Scale(rate, level, workday), room, out did);
                }

                b.Waiting += did;
                if (made is not null && supply is not null)
                {
                    made[i] = did;
                    supply[i] = could;
                }
            }
        }

        /// <summary>
        /// Fells the richest forest tiles with timber left that the woodcutter reaches, as many as its recipe says, at
        /// its rate from each times the tile's richness (economy-v1.md, section 3.1), up to <paramref name="room"/>; the
        /// forest grows back on every tile. Returns what the forest would give and what it felled.
        /// </summary>
        private (long Could, long Did) Fell(Building woodcutter, Recipe recipe, long room, int level, int workday)
        {
            int felled = 0;
            long could = 0, did = 0;
            foreach (var tile in Woods(woodcutter.Tile))
            {
                long left = ForestLeft(tile);
                if (left == 0)
                {
                    continue;
                }

                long rate = recipe.Richness ? Scale(recipe.PerTick, _map.RichnessAt(tile)) : recipe.PerTick;
                long wanted = Min(Scale(rate, level, workday), left);
                long timber = Min(wanted, room - did);
                could += wanted;
                if (timber > 0)
                {
                    _felled[tile] = left - timber;
                    did += timber;
                }

                if (++felled == recipe.ForestTiles)
                {
                    break;
                }
            }

            return (could, did);
        }

        /// <summary>
        /// The forest tiles a woodcutter on <paramref name="tile"/> can fell: known forest on the tile itself and its six
        /// neighbours that no other building stands on (D-93), richest first, then fullest, then in map order. The list
        /// is reused by the next call.
        /// </summary>
        private List<Hex> Woods(Hex tile)
        {
            _woods.Clear();
            for (int direction = -1; direction < Hex.Directions; direction++)
            {
                var near = direction < 0 ? tile : tile.Neighbour(direction);
                if (!_map.Contains(near) || _map.TerrainAt(near) != Terrain.Forest || !Fog.IsKnown(near)
                    || (near != tile && BuildingAt(near) is not null))
                {
                    continue;
                }

                int at = _woods.Count;
                while (at > 0 && FelledBefore(near, _woods[at - 1]))
                {
                    at--;
                }

                _woods.Insert(at, near);
            }

            return _woods;
        }

        private bool FelledBefore(Hex a, Hex b)
        {
            int richness = _map.RichnessAt(a).CompareTo(_map.RichnessAt(b));
            if (richness != 0)
            {
                return richness > 0;
            }

            int left = ForestLeft(a).CompareTo(ForestLeft(b));
            return left != 0 ? left > 0 : a < b;
        }

        /// <summary>
        /// The ships on each sea route, up to its capacity, which is shared both ways (economy-v2.md, section 6): first
        /// the materials the home island paid for work on the island, then out what the island's settlement needs for
        /// <see cref="FoodDays"/> game days, then the island's goods home in its import order, but as much food as that
        /// (<see cref="Kept"/>) and what is reserved there (TASK-046, D-126, D-127). The routes go in map order.
        /// </summary>
        private void Ship()
        {
            var home = _stores[_home];
            long homeCapacity = StorageCapacity(_home);
            foreach (var island in _islands)
            {
                if (!HasSeaRoute(island))
                {
                    continue;
                }

                var store = _stores[island];
                int at = _mapIndex[island];
                long room = SeaRoutePerTick;
                long owed = Min(room, _owed[at]);
                _owed[at] -= owed;
                room -= owed;

                // A reservation on the home island keeps nothing from going out: it only keeps goods from going home (D-127).
                long food = Quantity.Units(FoodDays * _settlements[island].Villagers);
                room -= SupplyFood(home, store, StorageCapacity(island), food, room);
                var settlement = _settlements[island];
                var order = ShippingOrder(settlement);
                var reserved = settlement.Reserved;
                for (int i = 0; i < order.Length && room > 0; i++)
                {
                    var resource = order[i];
                    long moved = Add(home, resource, Min(room, Shippable(store, reserved, resource, food)), homeCapacity);
                    store[(int)resource] -= moved;
                    room -= moved;
                }
            }
        }

        /// <summary>
        /// Ships food from the home island's store to an island's until it holds <paramref name="food"/> or the ships have
        /// no more <paramref name="room"/>: fish first, then grain, as the villagers eat them. Returns what was shipped.
        /// </summary>
        private static long SupplyFood(long[] home, long[] store, long capacity, long food, long room)
        {
            long shipped = 0;
            long lacking = food - store[(int)Resource.Fish] - store[(int)Resource.Grain];
            foreach (var resource in s_food)
            {
                long wanted = Min(Min(room - shipped, lacking - shipped), home[(int)resource]);
                if (wanted <= 0)
                {
                    continue;
                }

                long moved = Add(store, resource, wanted, capacity);
                home[(int)resource] -= moved;
                shipped += moved;
            }

            return shipped;
        }

        /// <summary>
        /// What of a resource an island keeps when <paramref name="food"/> is the food it keeps in all: fish first, then
        /// grain, as the villagers eat them, so that it keeps no more than two days' food together.
        /// </summary>
        internal static long Kept(long[] store, Resource resource, long food)
        {
            long fish = store[(int)Resource.Fish];
            return resource switch
            {
                Resource.Fish => Min(fish, food),
                Resource.Grain => Min(store[(int)Resource.Grain], food > fish ? food - fish : 0),
                _ => 0,
            };
        }

        /// <summary>Moves goods waiting at each settlement's depots into its store as far as there is room.</summary>
        private void Unload()
        {
            foreach (var island in _islands)
            {
                if (_atDepots.TryGetValue(island, out var waiting))
                {
                    var store = _stores[island];
                    long capacity = StorageCapacity(island);
                    for (int r = 0; r < s_resourceCount; r++)
                    {
                        waiting[r] -= Add(store, (Resource)r, waiting[r], capacity);
                    }
                }
            }
        }

        /// <summary>Whether ships sail between an island and the home island: a jetty stands on each.</summary>
        private bool HasSeaRoute(string island) => Linked(island, _home);

        private void Explore(long utcSeconds, List<GameEvent> events)
        {
            if (_expeditionLeft <= 0)
            {
                return;
            }

            _expeditionLeft -= WorkPerVillagerPerTick;
            if (_expeditionLeft > 0)
            {
                return;
            }

            // The expedition finds the island and the sea along its coast.
            _expeditionLeft = 0;
            if (ExpeditionTarget is string island)
            {
                Fog.LiftIsland(island);
                events.Add(new ExpeditionReturned(utcSeconds, island));
            }
        }

        private bool IslandFound(string island)
        {
            for (int index = 0; index < _map.Disc.Count; index++)
            {
                var hex = _map.Disc[index];
                if (_map.IslandAt(hex) == island && Fog.IsKnown(hex))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Makes up to <paramref name="most"/> of the recipe's product from the raw materials the carriers have brought
        /// the building, as far as <paramref name="room"/> lets it, and returns what the raw materials would have made; a
        /// recipe without raw materials gathers it all.
        /// </summary>
        private static long Process(Building building, Recipe recipe, long most, long room, out long made)
        {
            foreach (var input in recipe.Inputs)
            {
                most = Min(most, building.Held![(int)input.Resource] / input.PerProduct);
            }

            made = Min(most, room);
            foreach (var input in recipe.Inputs)
            {
                building.Held![(int)input.Resource] -= input.PerProduct * made;
            }

            return most;
        }

        /// <summary>Adds up to the store's capacity and returns what fitted; the rest is simply not produced.</summary>
        private static long Add(long[] store, Resource resource, long amount, long capacity)
        {
            long room = capacity - store[(int)resource];
            long added = Min(amount, room > 0 ? room : 0);
            store[(int)resource] += added;
            return added;
        }

        private static long Take(long[] store, Resource resource, long wanted)
        {
            long taken = Min(wanted, store[(int)resource]);
            store[(int)resource] -= taken;
            return taken;
        }

        private long StorageCapacity(string island)
        {
            if (_capacity.TryGetValue(island, out long capacity))
            {
                return capacity;
            }

            int units = 0;
            foreach (var building in _buildings)
            {
                if (building.Island == island)
                {
                    units += building.Kind == BuildingKind.Storehouse ? StoragePerStorehouse : 0;
                    units += building.Kind == BuildingKind.Jetty ? StorageAtJetty : 0;
                }
            }

            capacity = Quantity.Units(units);
            _capacity[island] = capacity;
            return capacity;
        }

        /// <summary>Production at level 2 is 150 % of level 1's.</summary>
        private static int LevelPercent(Building building) => building.Level == 2 ? 150 : 100;

        /// <summary>A workday gives 150 % production until the next dawn.</summary>
        private static int WorkdayPercent(Building building) => building.WorkdayToday ? 150 : 100;

        /// <summary>The map's island of a tile: its own for land, or the first land next to it, counter-clockwise from east.</summary>
        private string? MapIslandOf(Hex tile)
        {
            if (!_map.Contains(tile))
            {
                return null;
            }

            if (_map.IslandAt(tile) is string island)
            {
                return island;
            }

            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var near = tile.Neighbour(direction);
                if (_map.Contains(near) && _map.IslandAt(near) is string coast)
                {
                    return coast;
                }
            }

            return null;
        }

        /// <summary>Reads the two fields of a tile that is not there, which are 0.</summary>
        private static Hex? SkipTile(StateReader reader) =>
            reader.Long() == 0 && reader.Long() == 0 ? null : throw new FormatException("A missing tile is written as 0 0.");

        private Hex TileOf(StateReader reader)
        {
            var tile = new Hex(reader.Int(), reader.Int());
            return _map.Contains(tile) ? tile : throw new FormatException($"{tile} is not on the map.");
        }

        private void Complete(BuildingKind kind, Hex tile)
        {
            var recipe = _content.For(kind).Recipe;
            var building = new Building(kind, tile, IslandOf(tile))
            {
                CarrierCap = IsDepot(kind) ? MostCarriers : 0,
                Held = recipe is not null && recipe.Inputs.Count > 0 ? new long[s_resourceCount] : null,
                Reserve = recipe is not null && recipe.Inputs.Count > 0 ? StockRules.StandardReserve : 0,
            };
            StoreOn(building.Island);
            _buildings.Add(building);
            _buildingAt.Add(tile, building);
            _capacity.Clear();
            ForgetServed();
        }

        /// <summary>
        /// The store on an island, made empty the first time something is built there, with the island's settlement
        /// (D-106): off the home island the first building is the jetty (<see cref="CheckBuild"/>), whose housing the
        /// settlement begins with, and no villagers come with it.
        /// </summary>
        private long[] StoreOn(string island)
        {
            if (_stores.TryGetValue(island, out var store))
            {
                return store;
            }

            store = new long[s_resourceCount];
            _stores.Add(island, store);
            _settlements.Add(island, new IslandSettlement(this, island));
            _islands.Clear();
            foreach (var candidate in _map.Islands)
            {
                if (_stores.ContainsKey(candidate.Id))
                {
                    _islands.Add(candidate.Id);
                }
            }

            return store;
        }

        /// <summary>Where an island comes in the map's list of islands, or -1 when the map has no such island.</summary>
        private static int IndexOf(WorldMap map, string island)
        {
            for (int i = 0; i < map.Islands.Count; i++)
            {
                if (map.Islands[i].Id == island)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Gives each settlement's villagers to the buildings on its island in the staffing order (economy-v1.md, section
        /// 4.1; D-106): each building that is not paused is staffed if enough of its island's villagers are left for it,
        /// and is otherwise passed over for the next one; a storehouse or a jetty is staffed by its first carrier. The
        /// carriers a depot took beyond its first at dawn stay with it until the next dawn, so a new building takes only
        /// free villagers and no building ever loses a villager to a depot (economy-v2.md, section 7; D-39, D-109). The
        /// villagers left over build.
        /// </summary>
        private void StaffBuildings()
        {
            foreach (var island in _islands)
            {
                _unstaffed[island] = _settlements[island].Villagers;
            }

            foreach (var building in _buildings)
            {
                if (IsDepot(building.Kind) && building.Carriers > 1)
                {
                    _unstaffed[building.Island] -= building.Carriers - 1;
                }
            }

            bool depots = false;
            foreach (var building in _buildings)
            {
                int staff = _content.For(building.Kind).Staff;
                int free = _unstaffed[building.Island];
                bool staffed = staff > 0 && !building.Paused && free >= staff;
                _unstaffed[building.Island] = staffed ? free - staff : free;
                if (IsDepot(building.Kind))
                {
                    // A depot that loses its first carrier lets the others go too; they build until the next dawn.
                    depots |= staffed != building.Staffed;
                    building.Carriers = !staffed ? 0 : building.Carriers > 1 ? building.Carriers : 1;
                }

                building.Staffed = staffed;
            }

            if (depots)
            {
                ForgetTrips();
            }

            AssignDepots(all: false);
        }

        /// <summary>
        /// The island whose store pays for work on <paramref name="island"/>: its own when that holds the whole cost,
        /// otherwise the home island's; null when neither does (economy-v2.md, section 6).
        /// </summary>
        private string? PayerFor(IReadOnlyDictionary<Resource, int> cost, string island)
        {
            if (_stores.TryGetValue(island, out var own) && Covers(own, cost))
            {
                return island;
            }

            return Covers(_stores[_home], cost) ? _home : null;
        }

        private static bool Covers(long[] store, IReadOnlyDictionary<Resource, int> cost)
        {
            for (int resource = 0; resource < s_resourceCount; resource++)
            {
                if (cost.TryGetValue((Resource)resource, out int amount) && store[resource] < Quantity.Units(amount))
                {
                    return false;
                }
            }

            return true;
        }

        // Percent by percent, rounding down after each. One overload per arity rather than a params array, which would be
        // allocated for every building every tick.
        private static long Scale(long amount, int percent) => amount * percent / 100;

        private static long Scale(long amount, int first, int second) => Scale(Scale(amount, first), second);

        private static long Scale(long amount, int first, int second, int third) =>
            Scale(Scale(Scale(amount, first), second), third);

        private static long Min(long a, long b) => a < b ? a : b;
    }
}
