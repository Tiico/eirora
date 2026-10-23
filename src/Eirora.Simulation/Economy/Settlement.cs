using System;
using System.Collections.Generic;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The settlement's economy (economy-v1.md): stock on each island, villagers, buildings on the map's tiles, the
    /// building queue, the forest, expeditions and the sea route between the islands.
    /// </summary>
    /// <remarks>
    /// The economy moves in quarter-hour ticks at fixed UTC times, so advancing time in parts gives the same world as
    /// advancing at once (INV-D4). Rates are in millionths of a unit per quarter hour (INV-D3).
    /// Villagers work, build, carry, sail and explore only while awake (INV-T4); fields, sheep and forests grow day
    /// and night (INV-T5). Nothing is ever lost to time: a full store stops production and missing food only stops
    /// growth (D-17, D-39).
    /// </remarks>
    internal sealed class Settlement
    {
        public const long TickSeconds = 15 * 60;

        /// <summary>What <see cref="DepotDistance"/> gives when no depot can be reached over land.</summary>
        public const int Unreachable = int.MaxValue;

        private const long WorkPerVillagerPerTick = 250; // Thousandths of a villager-hour.
        private const int StoragePerStorehouse = 150;
        private const int StorageAtJetty = 150; // A jetty on an island without a storehouse stores goods for the ship.
        private const int ProcessingReserve = 20; // LOG-06 minimum stock left for building (D-44).
        private const int ComfortPerDawn = 2;
        private const int StartingHousing = 16;
        private const long ForestPerTile = World.TimberPerForest * Quantity.PerUnit;
        private const long ForestRegrowthPerTick = 62_500; // 0.25 timber an hour on each forest tile.
        private const long SeaRoutePerTick = 625_000; // 40 units a game day over 16 waking hours (economy-v1.md, section 5).

        private static readonly int s_resourceCount = Enum.GetValues(typeof(Resource)).Length;
        private static readonly IslandId[] s_islands = { IslandId.Home, IslandId.Nyon };

        private readonly Dictionary<IslandId, long[]> _stores = new()
        {
            [IslandId.Home] = new long[s_resourceCount],
            [IslandId.Nyon] = new long[s_resourceCount],
        };

        private readonly List<Building> _buildings = new();
        private readonly Dictionary<Hex, Building> _buildingAt = new(); // Only looked up, never iterated.
        private readonly List<Project> _projects = new();

        // Timber left on the forest tiles that are not full, in map order; every other forest tile holds ForestPerTile.
        private readonly SortedDictionary<Hex, long> _felled = new();
        private readonly WorldMap _map;
        private readonly string? _homeIsland;
        private readonly Founding _founding;
        private readonly ContentCatalog _content;
        private readonly Dictionary<Hex, int> _depotDistances = new(); // Forgotten whenever a depot is added.
        private readonly List<Hex> _woods = new();
        private readonly List<Hex> _regrowing = new();
        private long[] _before = new long[64]; // The state before and after a tick's work (RecordState).
        private long[] _after = new long[64];
        private readonly Dictionary<IslandId, long> _capacity = new(); // Forgotten whenever a building is added.
        private long _expeditionLeft;

        private Settlement(WorldMap map, Fog fog, Founding founding, ContentCatalog content)
        {
            _map = map;
            _homeIsland = map.IslandAt(map.Start);
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

        public IReadOnlyList<Building> Buildings => _buildings;

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

        public int FreeVillagers
        {
            get
            {
                int free = Villagers;
                foreach (var building in _buildings)
                {
                    if (building.Staffed)
                    {
                        free -= _content.For(building.Kind).Staff;
                    }
                }

                return free;
            }
        }

        /// <summary>Whether ships sail between the islands: a jetty stands on each.</summary>
        public bool SeaRouteOpen => HasBuilt(BuildingKind.Jetty, IslandId.Home) && HasBuilt(BuildingKind.Jetty, IslandId.Nyon);

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
            var home = settlement._stores[IslandId.Home];
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
        public long Stock(Resource resource) => _stores[IslandId.Home][(int)resource];

        public long Stock(IslandId island, Resource resource) => _stores[island][(int)resource];

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
            if (_map.Contains(hex) && _map.IslandAt(hex) is string island && island != _homeIsland && !IslandFound(island))
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

        public bool HasBuilt(BuildingKind kind, IslandId? island = null)
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
            if (island != IslandId.Home && kind != BuildingKind.Jetty && !HasBuilt(BuildingKind.Jetty, island))
            {
                return Rejection.SiteUnavailable;
            }

            return Rejection.None;
        }

        /// <summary>
        /// The island a building site belongs to: its own for land, and for fishing water the land it lies off. The
        /// economy keeps the start island apart from the land it reaches by sea (economy-v1.md, section 5).
        /// </summary>
        public IslandId IslandOf(Hex tile) =>
            MapIslandOf(tile) is string island && island != _homeIsland ? IslandId.Nyon : IslandId.Home;

        /// <summary>Timber left on a forest tile.</summary>
        public long ForestLeft(Hex tile) => _felled.TryGetValue(tile, out long left) ? left : ForestPerTile;

        public bool CanAfford(IReadOnlyDictionary<Resource, int> cost)
        {
            var home = _stores[IslandId.Home];
            for (int resource = 0; resource < s_resourceCount; resource++)
            {
                if (cost.TryGetValue((Resource)resource, out int amount) && home[resource] < Quantity.Units(amount))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsQueued(Hex tile, ProjectKind kind)
        {
            foreach (var project in _projects)
            {
                if (project.Tile == tile && project.Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Pays from the home island's store and queues the work; the caller has checked tile, cost and forces.</summary>
        public void Queue(ProjectKind kind, BuildingKind building, Hex tile, IReadOnlyDictionary<Resource, int> cost, int workHours)
        {
            // By resource rather than over the cost, whose order is the dictionary's (docs/development.md#determinism).
            var home = _stores[IslandId.Home];
            for (int resource = 0; resource < s_resourceCount; resource++)
            {
                if (cost.TryGetValue((Resource)resource, out int amount))
                {
                    home[resource] -= Quantity.Units(amount);
                }
            }

            _projects.Add(new Project(kind, building, tile, workHours * 1000L));
        }

        /// <summary>
        /// Steps over land from a tile to the nearest storehouse or jetty on the same island (economy-v1.md, section 5),
        /// so that a bay or a sound makes the way longer than as the crow flies; a fishing hut takes its first step
        /// ashore. <see cref="Unreachable"/> when no depot can be reached.
        /// </summary>
        public int DepotDistance(Hex tile)
        {
            if (!_depotDistances.TryGetValue(tile, out int distance))
            {
                distance = StepsToDepot(tile);
                _depotDistances[tile] = distance;
            }

            return distance;
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
            if (!HasBuilt(BuildingKind.Jetty, IslandId.Home))
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
            Produce();
            Ship();
            Explore(utcSeconds, events);

            return RecordState(ref _after) != recorded || !_before.AsSpan(0, recorded).SequenceEqual(_after.AsSpan(0, recorded));
        }

        /// <summary>
        /// Writes every value of the state that a tick can change into <paramref name="state"/>, which grows as needed,
        /// and returns how many: the villagers, the surveys, the expedition, the fog, the stores, each building, the
        /// queue and the felled forest, each list after its length. A test fails when a field is added to the
        /// settlement, a building, a project or the fog that is neither recorded here nor fixed or derived
        /// (FastForwardTests.EveryFieldThatATickCanChangeIsCompared).
        /// </summary>
        private int RecordState(ref long[] state)
        {
            int needed = 7 + (2 * s_resourceCount) + (5 * _buildings.Count) + _projects.Count + (3 * _felled.Count);
            if (state.Length < needed)
            {
                state = new long[2 * needed];
            }

            int i = 0;
            state[i++] = Villagers;
            state[i++] = SurveysDone;
            state[i++] = _expeditionLeft;
            state[i++] = Fog.Lifts;
            foreach (var island in s_islands)
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
                state[i++] = building.HasRoad ? 1 : 0;
                state[i++] = building.WorkdayToday ? 1 : 0;
                state[i++] = building.Ripe;
            }

            state[i++] = _projects.Count;
            foreach (var project in _projects)
            {
                state[i++] = project.WorkLeft;
            }

            state[i++] = _felled.Count;
            foreach (var forest in _felled)
            {
                state[i++] = forest.Key.Q;
                state[i++] = forest.Key.R;
                state[i++] = forest.Value;
            }

            return i;
        }

        /// <summary>Villagers eat, the settlement grows if it can, and today's workdays end.</summary>
        public void Dawn(long utcSeconds, List<GameEvent> events)
        {
            var home = _stores[IslandId.Home];
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

            StaffBuildings();
        }

        public void Write(StateWriter writer)
        {
            writer.Line("settlement").Long(Villagers).Long(SurveysDone).Long(_expeditionLeft);
            foreach (var island in s_islands)
            {
                var store = _stores[island];
                for (int i = 0; i < s_resourceCount; i++)
                {
                    writer.Line("stock").Enum(island).Enum((Resource)i).Long(store[i]);
                }
            }

            // Buildings and projects keep their order: staffing and the queue follow it.
            foreach (var b in _buildings)
            {
                writer.Line("building").Enum(b.Kind).Long(b.Tile.Q).Long(b.Tile.R).Long(b.Level).Bool(b.Staffed).Bool(b.HasRoad).Bool(b.WorkdayToday).Long(b.Ripe);
            }

            foreach (var p in _projects)
            {
                writer.Line("project").Enum(p.Kind).Enum(p.Building).Long(p.Tile.Q).Long(p.Tile.R).Long(p.WorkLeft);
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

            foreach (var island in s_islands)
            {
                var store = settlement._stores[island];
                for (int i = 0; i < s_resourceCount; i++)
                {
                    reader.Expect("stock");
                    bool expected = reader.Enum<IslandId>() == island && reader.Enum<Resource>() == (Resource)i;
                    store[i] = expected ? reader.Long() : throw new FormatException($"Expected {(Resource)i} on {island}.");
                }
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
                var building = new Building(kind, tile, settlement.IslandOf(tile))
                {
                    Level = reader.Int(),
                    Staffed = reader.Bool(),
                    HasRoad = reader.Bool(),
                    WorkdayToday = reader.Bool(),
                    Ripe = reader.Long(),
                };
                settlement._buildings.Add(building);
                settlement._buildingAt.Add(tile, building);
            }

            while (reader.Next("project"))
            {
                var kind = reader.Enum<ProjectKind>();
                var building = reader.Enum<BuildingKind>();
                settlement._projects.Add(new Project(kind, building, settlement.TileOf(reader), reader.Long()));
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

            // Distance, level and a workday scale how fast a field or a sheep farm grows, as they scale every other
            // building's production (economy-v1.md, section 3; TASK-038); the harvest only empties what has grown.
            foreach (var building in _buildings)
            {
                if (_content.For(building.Kind).Recipe is Recipe recipe && recipe.IsSlowProcess)
                {
                    long grows = recipe.Richness ? Scale(recipe.GrowsPerTick, _map.RichnessAt(building.Tile)) : recipe.GrowsPerTick;
                    building.Ripe += ticks * Scale(grows, DistancePercent(building), LevelPercent(building), WorkdayPercent(building));
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
                    Complete(project.Building, project.Tile);
                    break;
                case ProjectKind.Upgrade:
                    BuildingAt(project.Tile)!.Level = 2;
                    break;
                case ProjectKind.Road:
                    BuildingAt(project.Tile)!.HasRoad = true;
                    break;
            }
        }

        /// <summary>Every staffed building makes what its recipe says, scaled by distance, level and a workday.</summary>
        private void Produce()
        {
            foreach (var b in _buildings)
            {
                if (!b.Staffed || _content.For(b.Kind).Recipe is not Recipe recipe)
                {
                    continue;
                }

                var store = _stores[b.Island];
                long capacity = StorageCapacity(b.Island);
                int distance = DistancePercent(b);
                int level = LevelPercent(b);
                int workday = WorkdayPercent(b);
                if (recipe.ForestTiles > 0)
                {
                    Fell(b, recipe, store, capacity, distance, level, workday);
                }
                else if (recipe.IsSlowProcess)
                {
                    // Richness scales how fast it grows; the harvest only empties what has grown.
                    b.Ripe -= Add(store, recipe.Product, Min(b.Ripe, Scale(recipe.PerTick, distance, level, workday)), capacity);
                }
                else
                {
                    long rate = recipe.Richness ? Scale(recipe.PerTick, _map.RichnessAt(b.Tile)) : recipe.PerTick;
                    Process(store, recipe, Scale(rate, distance, level, workday), capacity);
                }
            }
        }

        /// <summary>
        /// Fells the richest forest tiles with timber left that the woodcutter reaches, as many as its recipe says, at
        /// its rate from each times the tile's richness (economy-v1.md, section 3.1); the forest grows back on every tile.
        /// </summary>
        private void Fell(Building woodcutter, Recipe recipe, long[] store, long capacity, int distance, int level, int workday)
        {
            int felled = 0;
            foreach (var tile in Woods(woodcutter.Tile))
            {
                long left = ForestLeft(tile);
                if (left == 0)
                {
                    continue;
                }

                long rate = recipe.Richness ? Scale(recipe.PerTick, _map.RichnessAt(tile)) : recipe.PerTick;
                long timber = Add(store, recipe.Product, Min(Scale(rate, distance, level, workday), left), capacity);
                if (timber > 0)
                {
                    _felled[tile] = left - timber;
                }

                if (++felled == recipe.ForestTiles)
                {
                    break;
                }
            }
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

        /// <summary>Ships goods from Nyön home over the sea route, up to its capacity (economy-v1.md, section 5).</summary>
        private void Ship()
        {
            if (!SeaRouteOpen)
            {
                return;
            }

            var from = _stores[IslandId.Nyon];
            var home = _stores[IslandId.Home];
            long capacity = StorageCapacity(IslandId.Home);
            long room = SeaRoutePerTick;
            for (int r = 0; r < s_resourceCount && room > 0; r++)
            {
                long moved = Add(home, (Resource)r, Min(room, from[r]), capacity);
                from[r] -= moved;
                room -= moved;
            }
        }

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
        /// Makes up to <paramref name="rate"/> of the recipe's product from its raw materials, keeping the reserve of each;
        /// a recipe without raw materials gathers the whole rate.
        /// </summary>
        private static void Process(long[] store, Recipe recipe, long rate, long capacity)
        {
            long most = rate;
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

        private long StorageCapacity(IslandId island)
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
                    units += building.Kind == BuildingKind.Jetty && island != IslandId.Home ? StorageAtJetty : 0;
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

        /// <summary>Share of production that reaches the store: 100, 75 or 50 % by distance, one step better with a road.</summary>
        private int DistancePercent(Building building)
        {
            int distance = DepotDistance(building.Tile);
            int step = distance <= 1 ? 0 : distance <= 3 ? 1 : 2;
            if (building.HasRoad && step > 0)
            {
                step--;
            }

            return step == 0 ? 100 : step == 1 ? 75 : 50;
        }

        /// <summary>Walks breadth first over the land of the tile's island to the nearest storehouse or jetty there.</summary>
        private int StepsToDepot(Hex tile)
        {
            string? island = MapIslandOf(tile);
            var depots = new HashSet<Hex>();
            foreach (var building in _buildings)
            {
                if (building.Kind is BuildingKind.Storehouse or BuildingKind.Jetty && island is not null && MapIslandOf(building.Tile) == island)
                {
                    depots.Add(building.Tile);
                }
            }

            var seen = new HashSet<Hex> { tile };
            var frontier = new List<Hex> { tile };
            for (int steps = 0; frontier.Count > 0 && depots.Count > 0; steps++)
            {
                var next = new List<Hex>();
                foreach (var hex in frontier)
                {
                    if (depots.Contains(hex))
                    {
                        return steps;
                    }

                    for (int direction = 0; direction < Hex.Directions; direction++)
                    {
                        var near = hex.Neighbour(direction);
                        if (_map.Contains(near) && _map.IslandAt(near) == island && seen.Add(near))
                        {
                            next.Add(near);
                        }
                    }
                }

                frontier = next;
            }

            return Unreachable;
        }

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

        private Hex TileOf(StateReader reader)
        {
            var tile = new Hex(reader.Int(), reader.Int());
            return _map.Contains(tile) ? tile : throw new FormatException($"{tile} is not on the map.");
        }

        private void Complete(BuildingKind kind, Hex tile)
        {
            var building = new Building(kind, tile, IslandOf(tile));
            _buildings.Add(building);
            _buildingAt.Add(tile, building);
            _capacity.Clear();
            if (kind is BuildingKind.Storehouse or BuildingKind.Jetty)
            {
                _depotDistances.Clear();
            }
        }

        private void StaffBuildings()
        {
            foreach (var building in _buildings)
            {
                int staff = _content.For(building.Kind).Staff;
                if (!building.Staffed && staff > 0 && FreeVillagers >= staff)
                {
                    building.Staffed = true;
                }
            }
        }

        // Percent by percent, rounding down after each. One overload per arity rather than a params array, which would be
        // allocated for every building every tick.
        private static long Scale(long amount, int percent) => amount * percent / 100;

        private static long Scale(long amount, int first, int second, int third) =>
            Scale(Scale(Scale(amount, first), second), third);

        private static long Min(long a, long b) => a < b ? a : b;
    }
}
