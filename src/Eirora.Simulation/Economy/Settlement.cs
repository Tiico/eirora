using System;
using System.Collections.Generic;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The settlement's economy on the home island (economy-v1.md): stock, villagers, buildings and the building queue.
    /// </summary>
    /// <remarks>
    /// The economy moves in quarter-hour ticks at fixed UTC times, so advancing time in parts gives the same world as
    /// advancing at once (INV-D4). Rates are in millionths of a unit per quarter hour (INV-D3).
    /// Villagers work, build and carry only while awake (INV-T4); fields, sheep and forests grow day and night (INV-T5).
    /// Nothing is ever lost to time: a full store stops production and missing food only stops growth (D-17, D-39).
    /// </remarks>
    internal sealed class Settlement
    {
        public const long TickSeconds = 15 * 60;

        private const long WorkPerVillagerPerTick = 250; // Thousandths of a villager-hour.
        private const int StoragePerStorehouse = 150;
        private const int ProcessingReserve = 20; // LOG-06 minimum stock left for building (D-44).
        private const int ComfortPerDawn = 2;
        private const int StartingHousing = 16;
        private const long ForestPerTile = 40 * Quantity.PerUnit;

        private static readonly int s_resourceCount = Enum.GetValues(typeof(Resource)).Length;

        private readonly long[] _stock = new long[s_resourceCount];
        private readonly List<Building> _buildings = new();
        private readonly List<Project> _projects = new();
        private readonly SortedDictionary<string, long> _forest = new(StringComparer.Ordinal);
        private readonly SortedSet<string> _revealed = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Site> _sites = new(StringComparer.Ordinal);

        public Settlement()
        {
            foreach (var site in PrototypeIsland.Sites)
            {
                _sites[site.Id] = site;
                if (!site.Hidden)
                {
                    _revealed.Add(site.Id);
                }

                if (site.ForestTiles > 0)
                {
                    _forest[site.Id] = ForestPerTile * site.ForestTiles;
                }
            }

            _stock[(int)Resource.Fish] = Quantity.Units(30);
            _stock[(int)Resource.Timber] = Quantity.Units(40);
            _stock[(int)Resource.Planks] = Quantity.Units(20);
            _stock[(int)Resource.Stone] = Quantity.Units(20);
            Villagers = 12;

            Complete(BuildingKind.Storehouse, _sites[PrototypeIsland.Settlement]);
            Complete(BuildingKind.FishingHut, _sites["K1"]);
            Complete(BuildingKind.Field, _sites["O1"]);
            Complete(BuildingKind.Woodcutter, _sites["F1"]);
            StaffBuildings();
        }

        public int Villagers { get; private set; }

        public int SurveysDone { get; private set; }

        public IReadOnlyList<Building> Buildings => _buildings;

        public IReadOnlyList<Project> Projects => _projects;

        public int Housing
        {
            get
            {
                int housing = StartingHousing;
                foreach (var building in _buildings)
                {
                    housing += BuildingSpec.For(building.Kind).Housing;
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
                        free -= BuildingSpec.For(building.Kind).Staff;
                    }
                }

                return free;
            }
        }

        public long Stock(Resource resource) => _stock[(int)resource];

        public bool IsRevealed(string siteId) => _revealed.Contains(siteId);

        public bool HasBuilt(BuildingKind kind)
        {
            foreach (var building in _buildings)
            {
                if (building.Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        public Building? BuildingAt(string siteId)
        {
            foreach (var building in _buildings)
            {
                if (building.Site.Id == siteId)
                {
                    return building;
                }
            }

            return null;
        }

        /// <summary>Whether <paramref name="kind"/> can stand on <paramref name="siteId"/> right now.</summary>
        public Rejection CheckSite(BuildingKind kind, string siteId)
        {
            if (!_sites.TryGetValue(siteId, out var site) || !_revealed.Contains(siteId))
            {
                return Rejection.SiteUnavailable;
            }

            if ((site.Kinds & BuildingSpec.For(kind).Site) == 0 || BuildingAt(siteId) is not null || IsQueued(siteId, ProjectKind.Build))
            {
                return Rejection.SiteUnavailable;
            }

            return Rejection.None;
        }

        public bool CanAfford(IReadOnlyDictionary<Resource, int> cost)
        {
            foreach (var item in cost)
            {
                if (_stock[(int)item.Key] < Quantity.Units(item.Value))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsQueued(string siteId, ProjectKind kind)
        {
            foreach (var project in _projects)
            {
                if (project.Site.Id == siteId && project.Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Pays and queues the work; the caller has checked the site, cost and forces.</summary>
        public void Queue(ProjectKind kind, BuildingKind building, string siteId, IReadOnlyDictionary<Resource, int> cost, int workHours)
        {
            foreach (var item in cost)
            {
                _stock[(int)item.Key] -= Quantity.Units(item.Value);
            }

            _projects.Add(new Project(kind, building, _sites[siteId], workHours * 1000L));
        }

        /// <summary>Distance from a site to the nearest storehouse or jetty, in hex steps.</summary>
        public int DepotDistance(Site site)
        {
            int best = int.MaxValue;
            foreach (var building in _buildings)
            {
                if (building.Kind is BuildingKind.Storehouse or BuildingKind.Jetty)
                {
                    int distance = site.DistanceTo(building.Site);
                    best = distance < best ? distance : best;
                }
            }

            return best;
        }

        /// <summary>Reveals the next survey area (economy-v1.md, section 6).</summary>
        public IReadOnlyList<string> CompleteSurvey()
        {
            var reveals = PrototypeIsland.Surveys[SurveysDone].Reveals;
            foreach (var id in reveals)
            {
                _revealed.Add(id);
            }

            SurveysDone++;
            return reveals;
        }

        /// <summary>Shortens the first project in the queue by up to <paramref name="hours"/> hours of work.</summary>
        public void Rush(int hours)
        {
            var first = _projects[0];
            first.WorkLeft = first.WorkLeft > hours * 1000L ? first.WorkLeft - (hours * 1000L) : 0;
        }

        /// <summary>One quarter hour; villagers work only when <paramref name="awake"/>.</summary>
        public void Tick(bool awake, long utcSeconds, List<GameEvent> events)
        {
            GrowSlowProcesses();
            if (!awake)
            {
                return;
            }

            Build(utcSeconds, events);
            Produce();
        }

        /// <summary>Villagers eat, the settlement grows if it can, and today's workdays end.</summary>
        public void Dawn(long utcSeconds, List<GameEvent> events)
        {
            long need = Quantity.Units(Villagers);
            need -= Take(Resource.Fish, need);
            need -= Take(Resource.Grain, need);
            bool fed = need == 0;

            bool comfort = fed && _stock[(int)Resource.Wadmal] >= Quantity.Units(ComfortPerDawn);
            if (comfort)
            {
                _stock[(int)Resource.Wadmal] -= Quantity.Units(ComfortPerDawn);
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

        public void AppendFingerprint(StringBuilder text)
        {
            text.Append("settlement:").Append(Villagers).Append(',').Append(SurveysDone).Append(";stock:");
            foreach (long amount in _stock)
            {
                text.Append(amount).Append(',');
            }

            text.Append(";buildings:");
            foreach (var b in _buildings)
            {
                text.Append((int)b.Kind).Append('@').Append(b.Site.Id).Append('/').Append(b.Level).Append('/')
                    .Append(b.Staffed ? 1 : 0).Append(b.HasRoad ? 1 : 0).Append(b.WorkdayToday ? 1 : 0).Append('/').Append(b.Ripe).Append(',');
            }

            text.Append(";projects:");
            foreach (var p in _projects)
            {
                text.Append((int)p.Kind).Append('/').Append((int)p.Building).Append('@').Append(p.Site.Id).Append('/').Append(p.WorkLeft).Append(',');
            }

            text.Append(";forest:");
            foreach (var forest in _forest)
            {
                text.Append(forest.Key).Append('=').Append(forest.Value).Append(',');
            }

            text.Append(";revealed:").Append(string.Join(",", _revealed)).Append(';');
        }

        private void GrowSlowProcesses()
        {
            foreach (var site in PrototypeIsland.Sites)
            {
                if (site.ForestTiles > 0)
                {
                    long grown = _forest[site.Id] + (62_500L * site.ForestTiles);
                    long cap = ForestPerTile * site.ForestTiles;
                    _forest[site.Id] = grown < cap ? grown : cap;
                }
            }

            foreach (var building in _buildings)
            {
                if (building.Kind == BuildingKind.Field)
                {
                    building.Ripe += Scale(125_000, building.Site.RichnessPercent);
                }
                else if (building.Kind == BuildingKind.SheepFarm)
                {
                    building.Ripe += Scale(100_000, building.Site.RichnessPercent);
                }
            }
        }

        private void Build(long utcSeconds, List<GameEvent> events)
        {
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
                events.Add(new ProjectCompleted(utcSeconds, project.Kind, project.Building, project.Site.Id));
                StaffBuildings();
            }
        }

        private void Finish(Project project)
        {
            switch (project.Kind)
            {
                case ProjectKind.Build:
                    Complete(project.Building, project.Site);
                    break;
                case ProjectKind.Upgrade:
                    BuildingAt(project.Site.Id)!.Level = 2;
                    break;
                case ProjectKind.Road:
                    BuildingAt(project.Site.Id)!.HasRoad = true;
                    break;
            }
        }

        private void Produce()
        {
            long capacity = StorageCapacity();
            foreach (var b in _buildings)
            {
                if (!b.Staffed)
                {
                    continue;
                }

                int distance = DistancePercent(b);
                int level = b.Level == 2 ? 150 : 100;
                int workday = b.WorkdayToday ? 150 : 100;
                int richness = b.Site.RichnessPercent;

                switch (b.Kind)
                {
                    case BuildingKind.FishingHut:
                        Add(Resource.Fish, Scale(312_500, richness, distance, level, workday), capacity);
                        break;
                    case BuildingKind.Field:
                        b.Ripe -= Add(Resource.Grain, Min(b.Ripe, Scale(500_000, distance, level, workday)), capacity);
                        break;
                    case BuildingKind.SheepFarm:
                        b.Ripe -= Add(Resource.Wool, Min(b.Ripe, Scale(500_000, distance, level, workday)), capacity);
                        break;
                    case BuildingKind.Woodcutter:
                        long rate = Scale(b.Site.ForestTiles >= 2 ? 250_000 : 125_000, richness, distance, level, workday);
                        _forest[b.Site.Id] -= Add(Resource.Timber, Min(rate, _forest[b.Site.Id]), capacity);
                        break;
                    case BuildingKind.Quarry:
                        Add(Resource.Stone, Scale(187_500, richness, distance, level, workday), capacity);
                        break;
                    case BuildingKind.Sawmill:
                        Process(Resource.Timber, Resource.Planks, Scale(250_000, distance, level, workday), capacity);
                        break;
                    case BuildingKind.Weavery:
                        Process(Resource.Wool, Resource.Wadmal, Scale(125_000, distance, level, workday), capacity);
                        break;
                }
            }
        }

        /// <summary>Turns two of <paramref name="input"/> into one of <paramref name="output"/>, keeping the reserve.</summary>
        private void Process(Resource input, Resource output, long rate, long capacity)
        {
            long available = _stock[(int)input] - Quantity.Units(ProcessingReserve);
            long made = Add(output, Min(rate, available > 0 ? available / 2 : 0), capacity);
            _stock[(int)input] -= 2 * made;
        }

        /// <summary>Adds up to the store's capacity and returns what fitted; the rest is simply not produced.</summary>
        private long Add(Resource resource, long amount, long capacity)
        {
            long room = capacity - _stock[(int)resource];
            long added = Min(amount, room > 0 ? room : 0);
            _stock[(int)resource] += added;
            return added;
        }

        private long Take(Resource resource, long wanted)
        {
            long taken = Min(wanted, _stock[(int)resource]);
            _stock[(int)resource] -= taken;
            return taken;
        }

        private long StorageCapacity()
        {
            int storehouses = 0;
            foreach (var building in _buildings)
            {
                storehouses += building.Kind == BuildingKind.Storehouse ? 1 : 0;
            }

            return Quantity.Units(StoragePerStorehouse * storehouses);
        }

        /// <summary>Share of production that reaches the store: 100, 75 or 50 % by distance, one step better with a road.</summary>
        private int DistancePercent(Building building)
        {
            int distance = DepotDistance(building.Site);
            int step = distance <= 1 ? 0 : distance <= 3 ? 1 : 2;
            if (building.HasRoad && step > 0)
            {
                step--;
            }

            return step == 0 ? 100 : step == 1 ? 75 : 50;
        }

        private void Complete(BuildingKind kind, Site site) => _buildings.Add(new Building(kind, site));

        private void StaffBuildings()
        {
            foreach (var building in _buildings)
            {
                int staff = BuildingSpec.For(building.Kind).Staff;
                if (!building.Staffed && staff > 0 && FreeVillagers >= staff)
                {
                    building.Staffed = true;
                }
            }
        }

        private static long Scale(long amount, params int[] percents)
        {
            foreach (int percent in percents)
            {
                amount = amount * percent / 100;
            }

            return amount;
        }

        private static long Min(long a, long b) => a < b ? a : b;
    }
}
