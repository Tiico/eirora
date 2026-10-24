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
    /// The settlement's economy (economy-v1.md, economy-v2.md, section 7): stock on each island, villagers, buildings on
    /// the map's tiles, the carriers and the roads, the building queue, the forest, expeditions and the sea routes
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
        private const int StorageAtJetty = 150; // A jetty off the home island stores goods for the ship.
        private const int ProcessingReserve = 20; // LOG-06 minimum stock left for building (D-44).
        private const int ComfortPerDawn = 2;
        private const int StartingHousing = 16;
        private const long ForestPerTile = World.TimberPerForest * Quantity.PerUnit;
        private const long ForestRegrowthPerTick = 62_500; // 0.25 timber an hour on each forest tile.
        private const long SeaRoutePerTick = 625_000; // 40 units a game day over 16 waking hours (economy-v1.md, section 5).

        private static readonly int s_resourceCount = Enum.GetValues(typeof(Resource)).Length;

        // The store on each island where something has been built, by the map's id; it stays when what was built goes.
        private readonly Dictionary<string, long[]> _stores = new(StringComparer.Ordinal);
        private readonly List<string> _islands = new(); // The islands with a store, in map order.
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
        private long _expeditionLeft;

        private Settlement(WorldMap map, Fog fog, Founding founding, ContentCatalog content)
        {
            _map = map;
            _home = map.IslandAt(map.Start) ?? throw new ArgumentException("A map starts on land.", nameof(map));
            _founding = founding;
            _content = content;
            Fog = fog;
        }

        /// <summary>The buildings it knows and what they cost and produce.</summary>
        public ContentCatalog Content => _content;

        /// <summary>What of the map is known (TASK-032); only a known tile can be built on.</summary>
        public Fog Fog { get; private set; }

        public int Villagers { get; private set; }

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

        public int Housing
        {
            get
            {
                int housing = StartingHousing;
                foreach (var building in _buildings)
                {
                    housing += _content.For(building.Kind).Housing;
                }

                return housing;
            }
        }

        /// <summary>The villagers who neither work in a building nor carry, and so build.</summary>
        public int FreeVillagers
        {
            get
            {
                int free = Villagers;
                foreach (var building in _buildings)
                {
                    free -= IsDepot(building.Kind) ? building.Carriers : building.Staffed ? _content.For(building.Kind).Staff : 0;
                }

                return free;
            }
        }

        /// <summary>The map's id of the island the settlement began on, where materials are paid from and villagers eat.</summary>
        public string HomeIsland => _home;

        /// <summary>The islands with a store of their own, in map order: the home island and every island built on.</summary>
        public IReadOnlyList<string> Islands => _islands;

        /// <summary>
        /// The sea routes, in map order: one from each island with a jetty to the home island, once a jetty stands there too
        /// (economy-v1.md, section 5).
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
            settlement.Villagers = 12;

            foreach (var (kind, tile) in founding.StartBuildings)
            {
                settlement.Complete(kind, tile);
            }

            settlement.StaffBuildings();
            return settlement;
        }

        /// <summary>Stock on the home island, where materials are paid from and villagers eat.</summary>
        public long Stock(Resource resource) => _stores[_home][(int)resource];

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

            // Builders reach another island by its jetty, so the jetty comes first there.
            var island = IslandOf(tile);
            if (island != _home && kind != BuildingKind.Jetty && !HasBuilt(BuildingKind.Jetty, island))
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

        public bool CanAfford(IReadOnlyDictionary<Resource, int> cost)
        {
            var home = _stores[_home];
            for (int resource = 0; resource < s_resourceCount; resource++)
            {
                if (cost.TryGetValue((Resource)resource, out int amount) && home[resource] < Quantity.Units(amount))
                {
                    return false;
                }
            }

            return true;
        }

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

        /// <summary>Pays from the home island's store and queues the work; the caller has checked tile, cost and forces.</summary>
        public void Queue(ProjectKind kind, BuildingKind? building, Hex tile, IReadOnlyDictionary<Resource, int> cost, int workHours)
        {
            // By resource rather than over the cost, whose order is the dictionary's (docs/development.md#determinism).
            var home = _stores[_home];
            for (int resource = 0; resource < s_resourceCount; resource++)
            {
                if (cost.TryGetValue((Resource)resource, out int amount))
                {
                    home[resource] -= Quantity.Units(amount);
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
        /// and returns how many: the villagers, the surveys, the expedition, the fog, the stores, each building, the
        /// queue, the roads and the felled forest, each list after its length. Roads are only ever added, so their count
        /// tells every change. A test fails when a field is added to the settlement, a building, a project or the fog
        /// that is neither recorded here nor fixed or derived (FastForwardTests.EveryFieldThatATickCanChangeIsCompared).
        /// </summary>
        private int RecordState(ref long[] state)
        {
            int needed = 9 + (_islands.Count * s_resourceCount) + ((12 + s_resourceCount) * _buildings.Count) + _projects.Count + (3 * _felled.Count);
            if (state.Length < needed)
            {
                state = new long[2 * needed];
            }

            int i = 0;
            state[i++] = Villagers;
            state[i++] = SurveysDone;
            state[i++] = _expeditionLeft;
            state[i++] = Fog.Lifts;
            state[i++] = _islands.Count;
            foreach (var island in _islands)
            {
                var store = _stores[island];
                for (int resource = 0; resource < s_resourceCount; resource++)
                {
                    state[i++] = store[resource];
                }
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
        /// Villagers eat, the settlement grows if it can, today's workdays end, the villagers are given out again and the
        /// depots take the carriers they need for a game day of <paramref name="activeTicks"/> waking quarter hours.
        /// </summary>
        public void Dawn(long utcSeconds, List<GameEvent> events, int activeTicks)
        {
            var home = _stores[_home];
            long need = Quantity.Units(Villagers);
            need -= Take(home, Resource.Fish, need);
            need -= Take(home, Resource.Grain, need);
            bool fed = need == 0;

            bool comfort = fed && home[(int)Resource.Wadmal] >= Quantity.Units(ComfortPerDawn);
            if (comfort)
            {
                home[(int)Resource.Wadmal] -= Quantity.Units(ComfortPerDawn);
            }

            int growth = (fed ? 1 : 0) + (comfort ? 1 : 0);
            int room = Housing - Villagers;
            growth = growth < room ? growth : room;
            if (growth > 0)
            {
                Villagers += growth;
                events.Add(new VillagersMovedIn(utcSeconds, growth));
            }

            foreach (var building in _buildings)
            {
                building.WorkdayToday = false;
            }

            // Every building to the depot its carriers reach fastest; the carriers its goods no longer need go first to the
            // buildings that wait for villagers, and the depots take more only of those no building needs (D-109).
            AssignDepots(all: true);
            ReleaseCarriers(activeTicks);
            StaffBuildings();
            HireCarriers(activeTicks);
        }

        public void Write(StateWriter writer)
        {
            writer.Line("settlement").Long(Villagers).Long(SurveysDone).Long(_expeditionLeft);
            foreach (var island in _islands)
            {
                var store = _stores[island];
                for (int i = 0; i < s_resourceCount; i++)
                {
                    writer.Line("stock").Text(island).Enum((Resource)i).Long(store[i]);
                }
            }

            // Buildings and projects keep their order: the staffing order and the queue. What the carriers have brought a
            // building follows it, one raw material a line.
            foreach (var b in _buildings)
            {
                writer.Line("building").Enum(b.Kind).Long(b.Tile.Q).Long(b.Tile.R).Long(b.Level).Bool(b.Staffed).Bool(b.WorkdayToday).Long(b.Ripe).Bool(b.Paused)
                    .Long(b.Carriers).Long(b.CarrierCap).Bool(b.CarriedFirst).Long(b.Waiting)
                    .Bool(b.Depot is not null).Long(b.Depot?.Q ?? 0).Long(b.Depot?.R ?? 0);

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
            reader.Expect("settlement");
            settlement.Villagers = reader.Int();
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

            // Each island's store in full, the islands in map order.
            while (reader.Peek == "stock")
            {
                string? island = null;
                long[] store = Array.Empty<long>();
                for (int i = 0; i < s_resourceCount; i++)
                {
                    reader.Expect("stock");
                    string on = reader.Text();
                    if (island is null)
                    {
                        island = on;
                        int last = settlement._islands.Count > 0 ? IndexOf(map, settlement._islands[settlement._islands.Count - 1]) : -1;
                        store = IndexOf(map, island) > last ? settlement.StoreOn(island) : throw new FormatException($"The store on {island} is not on the map after the previous one.");
                    }

                    bool expected = on == island && reader.Enum<Resource>() == (Resource)i;
                    store[i] = expected ? reader.Long() : throw new FormatException($"Expected {(Resource)i} on {island}.");
                }
            }

            if (!settlement._stores.ContainsKey(settlement._home))
            {
                throw new FormatException($"The home island {settlement._home} has no store.");
            }

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

        private void Build(long utcSeconds, List<GameEvent> events)
        {
            if (_projects.Count == 0)
            {
                return;
            }

            long labour = FreeVillagers * WorkPerVillagerPerTick;
            while (_projects.Count > 0 && (labour > 0 || _projects[0].WorkLeft == 0))
            {
                var project = _projects[0];
                long used = labour < project.WorkLeft ? labour : project.WorkLeft;
                project.WorkLeft -= used;
                labour -= used;
                if (project.WorkLeft > 0)
                {
                    break;
                }

                _projects.RemoveAt(0);
                Finish(project);
                events.Add(new ProjectCompleted(utcSeconds, project.Kind, project.Building, project.Tile));
                StaffBuildings();
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
        /// Ships goods home over each sea route, up to its capacity (economy-v1.md, section 5): the routes in map order,
        /// and on each the goods in their fixed order until the player sets import priorities (TASK-046).
        /// </summary>
        private void Ship()
        {
            var home = _stores[_home];
            long capacity = StorageCapacity(_home);
            foreach (var island in _islands)
            {
                if (!HasSeaRoute(island))
                {
                    continue;
                }

                var from = _stores[island];
                long room = SeaRoutePerTick;
                for (int r = 0; r < s_resourceCount && room > 0; r++)
                {
                    long moved = Add(home, (Resource)r, Min(room, from[r]), capacity);
                    from[r] -= moved;
                    room -= moved;
                }
            }
        }

        /// <summary>Whether ships sail from an island to the home island: a jetty stands on each.</summary>
        private bool HasSeaRoute(string island) =>
            island != _home && HasBuilt(BuildingKind.Jetty, island) && HasBuilt(BuildingKind.Jetty, _home);

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
                units += building.Island == island ? StorageUnits(building) : 0;
            }

            capacity = Quantity.Units(units);
            _capacity[island] = capacity;
            return capacity;
        }

        /// <summary>Whole units of each resource a building adds to its island's store.</summary>
        private int StorageUnits(Building building) => building.Kind switch
        {
            BuildingKind.Storehouse => StoragePerStorehouse,
            BuildingKind.Jetty when building.Island != _home => StorageAtJetty,
            _ => 0,
        };

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
            };
            StoreOn(building.Island);
            _buildings.Add(building);
            _buildingAt.Add(tile, building);
            _capacity.Clear();
            ForgetServed();
        }

        /// <summary>The store on an island, made empty the first time something is built there.</summary>
        private long[] StoreOn(string island)
        {
            if (_stores.TryGetValue(island, out var store))
            {
                return store;
            }

            store = new long[s_resourceCount];
            _stores.Add(island, store);
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
        /// Gives the villagers to the buildings in the staffing order (economy-v1.md, section 4.1): each building that is
        /// not paused is staffed if enough villagers are left for it, and is otherwise passed over for the next one; a
        /// storehouse or a jetty is staffed by its first carrier. The carriers a depot took beyond its first at dawn stay
        /// with it until the next dawn, so a new building takes only free villagers and no building ever loses a villager
        /// to a depot (economy-v2.md, section 7; D-39, D-109). The villagers left over build.
        /// </summary>
        private void StaffBuildings()
        {
            int free = Villagers;
            foreach (var building in _buildings)
            {
                free -= IsDepot(building.Kind) && building.Carriers > 1 ? building.Carriers - 1 : 0;
            }

            bool depots = false;
            foreach (var building in _buildings)
            {
                int staff = _content.For(building.Kind).Staff;
                bool staffed = staff > 0 && !building.Paused && free >= staff;
                free -= staffed ? staff : 0;
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

        // Percent by percent, rounding down after each. One overload per arity rather than a params array, which would be
        // allocated for every building every tick.
        private static long Scale(long amount, int percent) => amount * percent / 100;

        private static long Scale(long amount, int first, int second) => Scale(Scale(amount, first), second);

        private static long Scale(long amount, int first, int second, int third) =>
            Scale(Scale(Scale(amount, first), second), third);

        private static long Min(long a, long b) => a < b ? a : b;
    }
}
