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
/// home (TASK-045), whose shipping only moves goods between the stores. Their settlements keep their villagers, or
/// give a free one to another over a sea route, and their buildings stay staffed (TASK-050); some worlds come from
/// rules 7 with goods waiting at a depot for room in its store, which only ever move in.
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
        left = random.Next(4) == 0 ? FromRulesSeven(left, random) : left;
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
            Stocks.Of(parts).AssertOnlyConsumedSince(was, parts.GameDay - dayWas, parts.Villagers, parts.Settlements.Count, restingThroughout); // INV-R3, INV-T4.
            Measures.Of(parts).AssertNotLowerThan(measuresWere); // INV-V1, INV-F6.
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

    // The property proves little unless the build-out leaves worlds with work in flight.
    [Fact]
    public void BuiltOutWorldsAreLeftWithWorkInFlight()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int seed = 0; seed < 30; seed++)
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
            var migrated = FromRulesSeven(world, new Random(seed));
            See("goods waiting at a depot", migrated.Islands.Any(i => s_resources.Any(r => migrated.Settlement.AtDepot(i, r) > 0)));
            See("full store", world.Islands.Any(i => s_resources.Any(r => Capacity(world, i) > 0 && world.Settlement.Stock(i, r) == Capacity(world, i))));
            See("felled forest", world.Fingerprint().Contains("\nforest ", StringComparison.Ordinal));
            See("ripening", world.Buildings.Any(b => b.Kind == BuildingKind.SheepFarm && b.Ripe > 0) && world.Buildings.Any(b => b.Kind == BuildingKind.Field && b.Ripe > 0));
            See("workday", world.Buildings.Any(b => b.WorkdayToday));
            See("upgrade", world.Buildings.Any(b => b.Level == 2));
            See("road", world.Buildings.Any(b => b.HasRoad));
            See("left resting", world.Phase == Phase.Resting);
            See("left active", world.Phase == Phase.Active);
            See("no free villagers", world.FreeVillagers == 0 && world.Projects.Count > 0);
        }

        Assert.Superset(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "queue", "expedition at sea", "sea route", "two sea routes", "three stores", "goods waiting for the ships", "paused", "paused off the home island",
                "villagers off the home island", "buildings lacking villagers off the home island", "materials owed", "goods waiting at a depot",
                "full store", "felled forest", "ripening", "workday", "upgrade", "road", "left resting", "left active",
            },
            seen);
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

    private static void AssertBounds(World world)
    {
        foreach (var island in world.Islands)
        {
            long capacity = Capacity(world, island);
            Assert.All(s_resources, r => Assert.InRange(world.Settlement.Stock(island, r), 0, capacity));
        }
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
    private static World FromRulesSeven(World world, Random random)
    {
        var lines = SettlementsPerIslandTests.RulesSeven(world.Fingerprint()).Split('\n').Select(line =>
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
            world.Buildings.Count(b => b.HasRoad),
            Enumerable.Range(0, world.Map.Disc.Count).Count(i => world.IsKnown(world.Map.Disc[i])),
            world.Settlement.SurveysDone,
            world.GameDay,
            [.. Enum.GetValues<Force>().Select(world.Available)],
            world.Projects.Count,
            world.Projects.Sum(p => p.WorkLeft),
            world.ExpeditionHoursLeft,
            world.Villagers - world.FreeVillagers,
            [.. world.StaffingOrder.Select(b => $"{b.Kind}@{b.Tile}")],
            [.. world.Buildings.Where(b => b.Paused).Select(b => $"{b.Kind}@{b.Tile}")],
            [.. world.Projects.Select(p => $"{p.Kind} {p.Building}@{p.Tile}")]);

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
            // the front of the queue, nothing is paused or resumed, and villagers only ever go to work (INV-V2).
            Assert.True(AtWork >= earlier.AtWork && AtWork <= Villagers, $"villagers at work {earlier.AtWork} → {AtWork} of {Villagers}");
            Assert.Equal(earlier.StaffingOrder, StaffingOrder.Take(earlier.StaffingOrder.Length));
            Assert.Equal(earlier.Paused, Paused);
            Assert.Equal(earlier.Queue.Skip(earlier.Queue.Length - Queue.Length), Queue);
        }
    }

    /// <summary>The stock of each resource on every island together, in the stores and waiting at the depots, in millionths.</summary>
    private sealed class Stocks
    {
        private readonly long[] _total;

        private Stocks(long[] total) => _total = total;

        public static Stocks Of(World world) =>
            new([.. s_resources.Select(r => world.Islands.Sum(i => world.Settlement.Stock(i, r) + world.Settlement.AtDepot(i, r)))]);

        /// <summary>
        /// A store shrinks with time only by what recipes and basic needs consume (INV-R3): the sawmill takes two timber
        /// for a plank, the weavery two wool for a wadmal, and each dawn every villager eats one fish or grain and
        /// comfort takes two wadmal in each settlement. The ships only move goods between the islands, and goods waiting at
        /// a depot only move into its store. Nothing changes while the villagers rest throughout (INV-T4).
        /// </summary>
        public void AssertOnlyConsumedSince(Stocks earlier, int dawns, int villagers, int settlements, bool restingThroughout)
        {
            long Change(Resource r) => _total[(int)r] - earlier._total[(int)r];
            if (restingThroughout)
            {
                Assert.All(s_resources, r => Assert.Equal(0, Change(r)));
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
    /// What a field or a sheep farm grows each tick is what ripened plus what reached a store, the weavery's wool
    /// counted in its wadmal; it stays the same from one tick to the next until a dawn ends the workdays or a finished
    /// project changes a level, a road or the way to a store. A forest tile grows back a quarter timber an hour until
    /// full, whoever fells it.
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
            long Total(Resource r) => w.Islands.Sum(i => w.Settlement.Stock(i, r));

            long crops = w.Buildings.Where(b => b.Kind is BuildingKind.Field or BuildingKind.SheepFarm).Sum(b => b.Ripe)
                + Total(Resource.Grain) + Total(Resource.Wool) + (2 * Total(Resource.Wadmal));

            // The forest left on every tile plus the timber felled from it, the sawmill's timber counted in its planks.
            long forest = forestTiles.Sum(w.Settlement.ForestLeft) + Total(Resource.Timber) + (2 * Total(Resource.Planks));
            long regrowth = forestTiles.Sum(h => Math.Min(ForestRegrowthPerTick, ForestPerTile - w.Settlement.ForestLeft(h)));
            return (crops, forest, regrowth, w.GameDay);
        }
    }
}
