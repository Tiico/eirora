using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Tests.Saving;
using Eirora.Simulation.Time;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Absence is harmless for any length and any world (INV-V4, AC-02; TASK-049): built-out worlds left for an hour up to
/// a year, with and without self-care and with contributions that arrive late, keep INV-R1, INV-R3, INV-V1 and INV-T5,
/// and an absence read in parts gives the same world as one read at once (INV-D4). The player's say over the work holds
/// while they are away (TASK-044): paused buildings stay paused, the staffing order and the queue keep their order, and
/// never fewer villagers are at work (INV-V2). Some worlds have a store on each of three islands and two sea routes
/// home (TASK-045), whose shipping only moves goods between the stores. The carriers only move goods between the
/// buildings' outboxes, the raw materials held at the processing and the stores, never while the villagers rest
/// (INV-R2, INV-T4; TASK-051), and the roads stay. Their settlements keep
/// their villagers, or give a free one to another over a sea route, and their buildings stay staffed (TASK-050); some
/// worlds come from rules 8 with goods waiting at a depot for room in its store, which only ever move in. Some worlds were left just after a demolition, now and then with more villagers than room,
/// who all stay while the village waits for room to grow (D-38, D-39; TASK-047).
/// </summary>
/// <remarks>
/// Every world and absence is drawn from FsCheck's seed, so shrinking a failing case gives another world rather than a
/// simpler one. Play a failing case again with the replay FsCheck prints, <c>[Property(Replay = "(…)")]</c>, or with
/// <c>BuiltOutWorld.Generate(seed)</c> and <c>new Random(seed)</c> for the seed it reports.
/// </remarks>
public sealed class LongAbsenceTests
{
    private const long Hour = 3600;
    private const long Day = 24 * Hour;
    private const long Year = 365 * Day;
    private const int StoragePerStorehouse = 150; // economy-v1.md, section 2.
    private const int StorageAtJetty = 50; // economy-v2.md, section 6.
    private const int ComfortPerDawn = 2;

    // Ticks read one by one at the start of an absence, so that every quarter hour of the slow processes is seen.
    private const int TicksReadOneByOne = 2 * 96;

    private static readonly Resource[] s_resources = Enum.GetValues<Resource>();

    [Property(MaxTest = 16)]
    public void AnAbsenceOfAnyLengthBreaksNothing(int seed)
    {
        var random = new Random(seed);
        var left = BuiltOutWorld.Generate(seed);
        left = random.Next(4) == 0 ? FromRulesEight(left, random) : left;
        long until = left.Now.UtcSeconds + AbsenceLength(random);
        var before = Measures.Of(left);

        var once = Copy(left);
        foreach (var reading in TestClock.Readings(left.Now.ZoneId, left.Now.UtcSeconds, until))
        {
            once.AdvanceTo(reading);
        }

        // The same absence read tick by tick at first, then at random moments, each reading checked.
        var parts = Copy(left);
        var cuts = OneByOne(left.Now.UtcSeconds)
            .Concat(Enumerable.Range(0, random.Next(12)).Select(_ => left.Now.UtcSeconds + (long)(random.NextDouble() * (until - left.Now.UtcSeconds))));
        var slowProcesses = new SlowProcesses(parts);
        foreach (var reading in TestClock.Readings(left.Now.ZoneId, left.Now.UtcSeconds, until, cuts))
        {
            var was = Stocks.Of(parts);
            var measuresWere = Measures.Of(parts);
            int dayWas = parts.GameDay;
            bool restingThroughout = parts.Phase == Phase.Resting;
            int ticks = Ticks(parts.Now.UtcSeconds, reading.UtcSeconds);
            var events = parts.AdvanceTo(reading);
            restingThroughout &= parts.Phase == Phase.Resting && parts.GameDay == dayWas && !events.OfType<DuskFell>().Any();
            slowProcesses.Check(events, ticks);

            AssertBounds(parts); // INV-R1.
            Stocks.Of(parts).AssertOnlyConsumedSince(was, parts.GameDay - dayWas, parts.Villagers, parts.Settlements.Count, restingThroughout); // INV-R2, INV-R3, INV-T4.
            Measures.Of(parts).AssertNotLowerThan(measuresWere); // INV-V1, INV-F6.
            AssertLeftTheQueueOnlyFinished(measuresWere.Queue, parts, events); // INV-V1, INV-V4.
        }

        Assert.Equal(once.Fingerprint(), parts.Fingerprint()); // INV-D4.

        // Back after the absence: the queue has come as far as villagers allow and the expedition is home.
        if (until - left.Now.UtcSeconds >= 60 * Day)
        {
            Assert.True(parts.Projects.Count == 0 || parts.FreeVillagers == 0, $"{parts.Projects.Count} projects left with {parts.FreeVillagers} free villagers");
            Assert.Equal(0, parts.ExpeditionHoursLeft);
        }

        // Self-care from the days away arrives late, also corrections downward; it takes nothing back that was built.
        foreach (var (key, command) in LateSelfCare(random, parts))
        {
            Assert.Equal(Describe.Result(once.Apply(key, command)), Describe.Result(parts.Apply(key, command)));
            AssertBounds(parts);
            Measures.Of(parts).AssertNotLowerThan(before with { Forces = Measures.Of(parts).Forces });
            Assert.All(Enum.GetValues<Force>(), f => Assert.True(parts.Available(f) >= 0)); // INV-F1.
        }

        Assert.Equal(once.Fingerprint(), parts.Fingerprint());
    }

    // Seed -11 leaves a world with three settlements whose queue holds two fishing huts and then a road on the home
    // island, and the road is finished while the huts before it still have work left (D-113, #175).
    [Fact]
    public void AnAbsenceWhereAProjectFurtherBackFinishesFirstBreaksNothing() => AnAbsenceOfAnyLengthBreaksNothing(-11);

    // The property proves little unless the build-out leaves worlds with work in flight.
    [Fact]
    public void BuiltOutWorldsAreLeftWithWorkInFlight()
    {
        // The carriers take villagers who would otherwise build, so the islands are reached later than before them.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int seed = 0; seed < 40; seed++)
        {
            var world = BuiltOutWorld.Generate(seed);
            void See(string what, bool when)
            {
                if (when)
                {
                    seen.Add(what);
                }
            }

            See("queue", world.Projects.Count >= 3);
            See("expedition at sea", world.ExpeditionHoursLeft > 0);
            See("sea route", world.SeaRoutes.Count > 0);
            See("two sea routes", world.SeaRoutes.Count >= 2);
            See("three stores", world.Islands.Count >= 3);
            See("goods waiting for the ships", world.SeaRoutes.Any(r => s_resources.Any(res => world.Settlement.Stock(r.From, res) > 0)));
            See("paused", world.Buildings.Any(b => b.Paused));
            See("paused off the home island", world.Buildings.Any(b => b.Paused && b.Island != world.HomeIsland));
            See("villagers off the home island", world.Settlements.Any(s => s.Island != world.HomeIsland && s.Villagers > 0));
            See("buildings lacking villagers off the home island", world.Diagnose().Settlements.Any(s => s.Settlement.Island != world.HomeIsland && s.MissingStaff > 0));
            See("materials owed", world.Islands.Any(i => world.Settlement.Owed(i) > 0));
            var migrated = FromRulesEight(world, new Random(seed));
            See("goods waiting at a depot", migrated.Islands.Any(i => s_resources.Any(r => migrated.Settlement.AtDepot(i, r) > 0)));
            See("full store", world.Islands.Any(i => s_resources.Any(r => Capacity(world, i) > 0 && world.Settlement.Stock(i, r) == Capacity(world, i))));
            See("felled forest", world.Fingerprint().Contains("\nforest ", StringComparison.Ordinal));
            See("ripening", world.Buildings.Any(b => b.Kind == BuildingKind.SheepFarm && b.Ripe > 0) && world.Buildings.Any(b => b.Kind == BuildingKind.Field && b.Ripe > 0));
            See("workday", world.Buildings.Any(b => b.WorkdayToday));
            See("upgrade", world.Buildings.Any(b => b.Level == 2));
            See("road", world.Roads.Count > 0);
            See("road in the queue", world.Projects.Any(p => p.Kind == ProjectKind.Road));
            See("goods waiting for the carriers", world.Buildings.Any(b => b.Waiting > 0));
            See("raw materials at the processing", world.Buildings.Any(b => b.Held?.Any(h => h > 0) == true));
            See("several carriers", world.Buildings.Any(b => b.Carriers > 1));
            See("carriers capped", world.Buildings.Any(b => Logistics.IsDepot(b.Kind) && b.CarrierCap < Logistics.MostCarriers));
            See("carried first", world.Buildings.Any(b => b.CarriedFirst));
            See("carriers behind", world.Diagnose().Depots.Any(d => d.SharePercent < 100));
            See("left resting", world.Phase == Phase.Resting);
            See("left active", world.Phase == Phase.Active);
            See("no free villagers", world.FreeVillagers == 0 && world.Projects.Count > 0);
            See("demolished", world.Fingerprint().Contains("\nbuilding-demolished ", StringComparison.Ordinal));
            See("more villagers than room", world.Villagers > world.Housing);
        }

        string[] wanted =
        [
            "queue", "expedition at sea", "sea route", "two sea routes", "three stores", "goods waiting for the ships", "paused", "paused off the home island",
            "full store", "felled forest", "ripening", "workday", "upgrade", "road", "left resting", "left active",
            "villagers off the home island", "buildings lacking villagers off the home island", "materials owed", "goods waiting at a depot",
            "road in the queue", "goods waiting for the carriers", "raw materials at the processing", "several carriers", "carriers capped", "carried first", "carriers behind",
            "demolished", "more villagers than room",
        ];
        Assert.Equal([], wanted.Where(w => !seen.Contains(w)).ToArray());
    }

    // INV-T5 is compared across dusk, where the villagers stop harvesting and the fields go on growing.
    [Fact]
    public void TheSlowProcessesAreComparedAcrossTheRest()
    {
        int compared = 0;
        for (int seed = 0; seed < 8; seed++)
        {
            var world = BuiltOutWorld.Generate(seed);
            var slowProcesses = new SlowProcesses(world);
            foreach (var reading in TestClock.Readings(world.Now.ZoneId, world.Now.UtcSeconds, world.Now.UtcSeconds + (2 * Day), OneByOne(world.Now.UtcSeconds)))
            {
                int ticks = Ticks(world.Now.UtcSeconds, reading.UtcSeconds);
                slowProcesses.Check(world.AdvanceTo(reading), ticks);
            }

            compared += slowProcesses.ComparedAcrossTheRest;
        }

        Assert.True(compared >= 4, $"Only {compared} stretches of slow growth were compared across the rest.");
    }

    /// <summary>An hour to a year, evenly spread on a log scale, and a whole year in one case of four.</summary>
    private static long AbsenceLength(Random random) =>
        random.Next(4) == 0 ? Year : (long)(Hour * Math.Pow(Year / Hour, random.NextDouble()));

    /// <summary>Self-care for the last game days, recorded when the player is back: none in one case of three.</summary>
    private static List<(string Key, GameCommand Command)> LateSelfCare(Random random, World world)
    {
        var late = new List<(string, GameCommand)>();
        int count = random.Next(3) == 0 ? 0 : 1 + random.Next(10);
        for (int i = 0; i < count; i++)
        {
            var habit = (Habit)random.Next(3);
            int total = habit switch
            {
                Habit.Steps => random.Next(30_000),
                Habit.Workouts => random.Next(4),
                _ => random.Next(10 * 60),
            };
            late.Add(($"late-{i}", new RecordSelfCare(world.GameDay - random.Next(RecordSelfCare.DaysBack), habit, (ContributionSource)random.Next(2), total)));
        }

        return late;
    }

    /// <summary>
    /// A project leaves the queue while the player is away only when it is finished: told in a <see cref="ProjectCompleted"/>
    /// and standing as the building, the upgrade or the road it was, wherever it stood in the queue (D-113). The player's
    /// commands that take one out otherwise, moving it first or demolishing a building with an upgrade queued, never come
    /// during an absence.
    /// </summary>
    private static void AssertLeftTheQueueOnlyFinished(string[] queueWas, World world, IReadOnlyList<GameEvent> events)
    {
        var completed = events.OfType<ProjectCompleted>().ToList();
        var left = queueWas.ToList();
        foreach (string project in Measures.Of(world).Queue)
        {
            left.Remove(project);
        }

        Assert.Equal(left.Order(StringComparer.Ordinal), completed.Select(c => $"{c.Kind} {c.Building}@{c.Tile}").Order(StringComparer.Ordinal));
        Assert.All(completed, c => Assert.True(
            c.Kind switch
            {
                ProjectKind.Build => world.BuildingAt(c.Tile)?.Kind == c.Building,
                ProjectKind.Upgrade => world.BuildingAt(c.Tile) is { Level: > 1 } upgraded && upgraded.Kind == c.Building,
                _ => world.Roads.Contains(c.Tile),
            },
            $"{c.Kind} {c.Building}@{c.Tile} was completed but does not stand."));
    }

    private static void AssertBounds(World world)
    {
        foreach (var island in world.Islands)
        {
            long capacity = Capacity(world, island);
            Assert.All(s_resources, r => Assert.InRange(world.Settlement.Stock(island, r), 0, capacity));
        }

        // What waits at a building, the carriers' plan and the villagers they take (economy-v2.md, section 7; INV-V2).
        foreach (var building in world.Buildings)
        {
            Assert.InRange(building.Waiting, 0, Quantity.Units(Logistics.OutboxUnits));
            Assert.All(building.Held ?? [], held => Assert.InRange(held, 0, Quantity.Units(Logistics.InputUnits)));
            if (Logistics.IsDepot(building.Kind))
            {
                Assert.InRange(building.CarrierCap, 1, Logistics.MostCarriers);
                Assert.InRange(building.Carriers, building.Staffed ? 1 : 0, building.Staffed ? building.CarrierCap : 0);
            }
        }

        Assert.InRange(world.FreeVillagers, 0, world.Villagers);
    }

    /// <summary>What an island's stores hold of each resource, in millionths (economy-v1.md, sections 2 and 5).</summary>
    private static long Capacity(World world, string island) => Quantity.Units(
        world.Buildings.Where(b => b.Island == island).Sum(b => b.Kind switch
        {
            BuildingKind.Storehouse => StoragePerStorehouse,
            BuildingKind.Jetty => StorageAtJetty,
            _ => 0,
        }));

    private static World Copy(World world) =>
        SimulationSnapshot.FromBytes(SimulationSnapshot.Take(world).ToBytes()).Restore(world.Map);

    /// <summary>
    /// The world as rules 7 would have left it, migrated to the current rules: the stores off the home island filled at
    /// random up to what rules 7 let their depots hold, so that goods wait at a depot for room (TASK-050).
    /// </summary>
    private static World FromRulesEight(World world, Random random)
    {
        var lines = SettlementsPerIslandTests.RulesEight(world.Fingerprint()).Split('\n').Select(line =>
        {
            string[] fields = line.Split(' ');
            if (fields[0] != "stock" || fields[1] == world.HomeIsland)
            {
                return line;
            }

            long rulesSeven = Quantity.Units(world.Buildings.Where(b => b.Island == fields[1]).Sum(b => b.Kind is BuildingKind.Storehouse or BuildingKind.Jetty ? 150 : 0));
            return $"stock {fields[1]} {fields[2]} {(long)(random.NextDouble() * rulesSeven)}";
        });
        var reader = new StateReader(new SettlementsPerIsland().Migrate(string.Join('\n', lines), world.Map));
        var migrated = World.Read(reader, world.Map);
        reader.End();
        return migrated;
    }

    /// <summary>The first ticks after <paramref name="utcSeconds"/>, to read one by one.</summary>
    private static IEnumerable<long> OneByOne(long utcSeconds) =>
        Enumerable.Range(1, TicksReadOneByOne).Select(i => ((utcSeconds / Settlement.TickSeconds) + i) * Settlement.TickSeconds);

    /// <summary>The economy's ticks after <paramref name="fromUtc"/> up to and including <paramref name="toUtc"/>.</summary>
    private static int Ticks(long fromUtc, long toUtc) => (int)((toUtc / Settlement.TickSeconds) - (fromUtc / Settlement.TickSeconds));

    /// <summary>Everything that never decreases while the player is away (INV-V1, INV-F6), and the work left.</summary>
    private sealed record Measures(
        int Villagers, int Housing, int Settlements, int Buildings, int Levels, int Roads, int Revealed, int Surveys, int GameDay, int[] Forces, int Projects, long WorkLeft,
        int ExpeditionHoursLeft, int AtWork, string[] StaffingOrder, string[] Paused, string[] Queue)
    {
        public static Measures Of(World world) => new(
            world.Villagers,
            world.Housing,
            world.Settlements.Count,
            world.Buildings.Count,
            world.Buildings.Sum(b => b.Level),
            world.Roads.Count,
            Enumerable.Range(0, world.Map.Disc.Count).Count(i => world.IsKnown(world.Map.Disc[i])),
            world.Settlement.SurveysDone,
            world.GameDay,
            [.. Enum.GetValues<Force>().Select(world.Available)],
            world.Projects.Count,
            world.Projects.Sum(p => p.WorkLeft),
            world.ExpeditionHoursLeft,
            InBuildings(world),
            [.. world.StaffingOrder.Select(b => $"{b.Kind}@{b.Tile}")],
            [.. world.Buildings.Where(b => b.Paused).Select(b => $"{b.Kind}@{b.Tile}")],
            [.. world.Projects.Select(p => $"{p.Kind} {p.Building}@{p.Tile}")]);

        /// <summary>
        /// The villagers who staff a building, a depot's first carrier counted: the carriers it takes beyond that follow
        /// the goods at each dawn and go back to building when the goods need fewer (D-109).
        /// </summary>
        private static int InBuildings(World world) =>
            world.Buildings.Where(b => b.Staffed).Sum(b => Logistics.IsDepot(b.Kind) ? 1 : world.Content.For(b.Kind).Staff);

        public void AssertNotLowerThan(Measures earlier)
        {
            Assert.True(Villagers >= earlier.Villagers, $"villagers {earlier.Villagers} → {Villagers}");
            Assert.True(Housing >= earlier.Housing, $"housing {earlier.Housing} → {Housing}");
            Assert.True(Settlements >= earlier.Settlements, $"settlements {earlier.Settlements} → {Settlements}");
            Assert.True(Buildings >= earlier.Buildings, $"buildings {earlier.Buildings} → {Buildings}");
            Assert.True(Levels >= earlier.Levels, $"levels {earlier.Levels} → {Levels}");
            Assert.True(Roads >= earlier.Roads, $"roads {earlier.Roads} → {Roads}");
            Assert.True(Revealed >= earlier.Revealed, $"revealed {earlier.Revealed} → {Revealed}");
            Assert.True(Surveys >= earlier.Surveys, $"surveys {earlier.Surveys} → {Surveys}");
            Assert.True(GameDay >= earlier.GameDay, $"game day {earlier.GameDay} → {GameDay}");
            Assert.True(Forces.Zip(earlier.Forces).All(f => f.First >= f.Second), $"forces {string.Join(",", earlier.Forces)} → {string.Join(",", Forces)}");

            // Work only goes forward: the queue never grows and no project or voyage gets longer.
            Assert.True(Projects <= earlier.Projects, $"projects {earlier.Projects} → {Projects}");
            Assert.True(WorkLeft <= earlier.WorkLeft, $"work left {earlier.WorkLeft} → {WorkLeft}");
            Assert.True(ExpeditionHoursLeft <= earlier.ExpeditionHoursLeft, $"expedition {earlier.ExpeditionHoursLeft} → {ExpeditionHoursLeft}");

            // The player's say holds: a completed building comes last in the staffing order, a completed project leaves
            // the queue and the rest keep their order, nothing is paused or resumed, and villagers only ever go to work
            // (INV-V2). A project further back can finish first, since each takes the work of the villagers who reach it
            // that the ones before it have not taken (D-113).
            Assert.True(AtWork >= earlier.AtWork && AtWork <= Villagers, $"villagers at work {earlier.AtWork} → {AtWork} of {Villagers}");
            Assert.Equal(earlier.StaffingOrder, StaffingOrder.Take(earlier.StaffingOrder.Length));
            Assert.Equal(earlier.Paused, Paused);
            Assert.True(IsLeftOf(earlier.Queue, Queue), $"queue {string.Join(" | ", earlier.Queue)} → {string.Join(" | ", Queue)}");
        }

        /// <summary>Whether <paramref name="queue"/> is <paramref name="earlier"/> with some projects taken out, in its order.</summary>
        private static bool IsLeftOf(string[] earlier, string[] queue)
        {
            int next = 0;
            foreach (string project in earlier)
            {
                if (next < queue.Length && project == queue[next])
                {
                    next++;
                }
            }

            return next == queue.Length;
        }
    }

    /// <summary>
    /// The stock of each resource on every island together, in millionths, with what waits at the depots for room in the
    /// store, in the buildings' outboxes and what the carriers have brought to the processing; and where each lies, to see
    /// that nothing moves during the rest.
    /// </summary>
    private sealed class Stocks
    {
        private readonly long[] _total;
        private readonly string _where;

        private Stocks(long[] total, string where)
        {
            _total = total;
            _where = where;
        }

        public static Stocks Of(World world)
        {
            long Waiting(Resource r) => world.Buildings.Where(b => world.Content.For(b.Kind).Recipe?.Product == r).Sum(b => b.Waiting);
            long Held(Resource r) => world.Buildings.Sum(b => b.Held?[(int)r] ?? 0);
            string where = string.Join(
                " ",
                world.Islands.SelectMany(i => s_resources.SelectMany(r => new[] { world.Settlement.Stock(i, r), world.Settlement.AtDepot(i, r) }))
                    .Concat(world.Buildings.SelectMany(b => (b.Held ?? []).Prepend(b.Waiting))));
            return new([.. s_resources.Select(r => world.Islands.Sum(i => world.Settlement.Stock(i, r) + world.Settlement.AtDepot(i, r)) + Waiting(r) + Held(r))], where);
        }

        /// <summary>
        /// A store shrinks with time only by what recipes and basic needs consume (INV-R3), and the carriers only move goods
        /// between a building and a store (INV-R2): the sawmill takes two timber
        /// for a plank, the weavery two wool for a wadmal, and each dawn every villager eats one fish or grain and
        /// comfort takes two wadmal in each settlement. The ships only move goods between the islands, and goods waiting at
        /// a depot only move into its store. Nothing changes while the villagers rest throughout (INV-T4).
        /// </summary>
        public void AssertOnlyConsumedSince(Stocks earlier, int dawns, int villagers, int settlements, bool restingThroughout)
        {
            long Change(Resource r) => _total[(int)r] - earlier._total[(int)r];
            if (restingThroughout)
            {
                Assert.Equal(earlier._where, _where);
            }

            long wadmalMade = Change(Resource.Wadmal) + Quantity.Units(ComfortPerDawn * dawns * settlements);
            Assert.True(Change(Resource.Planks) >= 0, $"planks {Change(Resource.Planks)}");
            Assert.True(Change(Resource.Stone) >= 0, $"stone {Change(Resource.Stone)}");
            Assert.True(Change(Resource.Timber) >= -2 * Change(Resource.Planks), $"timber {Change(Resource.Timber)} for {Change(Resource.Planks)} planks");
            Assert.True(wadmalMade >= 0, $"wadmal {Change(Resource.Wadmal)} over {dawns} dawns");
            Assert.True(Change(Resource.Wool) >= -2 * wadmalMade, $"wool {Change(Resource.Wool)} for {wadmalMade} wadmal");
            Assert.True(Change(Resource.Fish) + Change(Resource.Grain) >= -Quantity.Units((long)villagers * dawns), $"food {Change(Resource.Fish) + Change(Resource.Grain)} over {dawns} dawns");
        }
    }

    /// <summary>
    /// INV-T5 read a tick at a time: fields, sheep and forests grow as much in a quarter hour of rest as in one of work.
    /// </summary>
    /// <remarks>
    /// What a field or a sheep farm grows each tick is what ripened plus what was harvested, waiting at the building, held
    /// at the weavery or in a store, the weavery's wool counted in its wadmal; it stays the same from one tick to the next
    /// until a dawn ends the workdays or a finished project changes a level. A forest tile grows back a quarter timber an
    /// hour until full, whoever fells it.
    /// </remarks>
    private sealed class SlowProcesses(World world)
    {
        private const long ForestPerTile = 40 * Quantity.PerUnit;
        private const long ForestRegrowthPerTick = 62_500;

        private (long Crops, long Forest, long ForestGrowth, int GameDay) _was = Read(world, world.Map);
        private long? _growth;
        private Phase? _firstPhase;
        private bool _acrossTheRest;

        public int ComparedAcrossTheRest { get; private set; }

        /// <summary>
        /// Checks a reading that has just passed with its <paramref name="ticks"/> and <paramref name="events"/>; only a
        /// reading of a single tick is compared, any other starts the comparison over.
        /// </summary>
        public void Check(IReadOnlyList<GameEvent> events, int ticks)
        {
            var now = Read(world, world.Map);
            if (ticks == 1)
            {
                Assert.Equal(_was.ForestGrowth, now.Forest - _was.Forest);
            }

            if (ticks != 1 || now.GameDay != _was.GameDay || events.Any(e => e is ProjectCompleted))
            {
                _growth = null;
                _firstPhase = null;
                _acrossTheRest = false;
            }
            else
            {
                long growth = now.Crops - _was.Crops;
                Assert.True(_growth is null || _growth == growth, $"{_growth} grew in a tick before, {growth} in this tick in phase {world.Phase}");
                _growth = growth;
                _firstPhase ??= world.Phase;
                if (!_acrossTheRest && world.Phase != _firstPhase && growth > 0)
                {
                    _acrossTheRest = true;
                    ComparedAcrossTheRest++;
                }
            }

            _was = now;
        }

        private static (long Crops, long Forest, long ForestGrowth, int GameDay) Read(World w, WorldMap map)
        {
            var forestTiles = Enumerable.Range(0, map.Disc.Count).Select(i => map.Disc[i]).Where(h => map.TerrainAt(h) == Terrain.Forest).ToList();
            long Total(Resource r) => w.Islands.Sum(i => w.Settlement.Stock(i, r))
                + w.Buildings.Where(b => w.Content.For(b.Kind).Recipe?.Product == r).Sum(b => b.Waiting)
                + w.Buildings.Sum(b => b.Held?[(int)r] ?? 0);

            long crops = w.Buildings.Where(b => b.Kind is BuildingKind.Field or BuildingKind.SheepFarm).Sum(b => b.Ripe)
                + Total(Resource.Grain) + Total(Resource.Wool) + (2 * Total(Resource.Wadmal));

            // The forest left on every tile plus the timber felled from it, the sawmill's timber counted in its planks.
            long forest = forestTiles.Sum(w.Settlement.ForestLeft) + Total(Resource.Timber) + (2 * Total(Resource.Planks));
            long regrowth = forestTiles.Sum(h => Math.Min(ForestRegrowthPerTick, ForestPerTile - w.Settlement.ForestLeft(h)));
            return (crops, forest, regrowth, w.GameDay);
        }
    }
}
