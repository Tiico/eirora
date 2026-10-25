using System.Reflection;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Fast-forwarding a long absence (TASK-049): the quarter hours where only the slow processes move are grown at once,
/// which gives the same world as reading every quarter hour, and a year away skips enough of them. The time a year takes,
/// against what a cold start on the weakest target phone can spare, is measured in FastForwardBenchmarks
/// (performance-budgets.md): timed here, the tests would fail on a busy machine (#170).
/// </summary>
/// <remarks>
/// Every world is drawn from FsCheck's seed, so shrinking a failing case gives another world rather than a simpler one.
/// Play a failing case again with the replay FsCheck prints, <c>[Property(Replay = "(…)")]</c>, or with
/// <c>BuiltOutWorld.Generate(seed)</c> for the seed it reports.
/// </remarks>
public sealed class FastForwardTests
{
    private const long Hour = 3600;
    private const long Day = 24 * Hour;
    private const long Year = 365 * Day;

    /// <summary>
    /// The most of the quarter hours the villagers are awake that a year away may work one by one, together in the
    /// built-out worlds; in the rest only the slow processes move, so they are skipped.
    /// </summary>
    /// <remarks>
    /// On 25 October 2026, with demolition (TASK-047), the six worlds worked 78 % of them (105 762 of 135 050). With the skip broken they work every
    /// one, as the year scene's world does anyway: its expedition and its queue change something every quarter hour.
    /// </remarks>
    private const double MostWorkedShare = 0.9;

    // The world the measurement scene year catches up on the phone, as a save the client loads from its resources.
    private static readonly string s_yearScene = Repository.PathTo("client", "Assets", "Eirora", "Resources", "Measurement", "AYearAway.bytes");

    // Read a quarter hour at a time, every tick is worked one by one, as before the fast-forward skipped any. Half the
    // absences last a whole year, so that stores fill and stay full while Nyön goes on shipping, and the readings now
    // and then change zone, also during the rest (INV-T8).
    [Property(MaxTest = 12)]
    public bool FastForwardGivesTheSameWorldAsEveryQuarterHour(int seed)
    {
        var random = new Random(seed);
        var left = BuiltOutWorld.Generate(seed);
        long until = left.Now.UtcSeconds + (random.Next(2) == 0 ? Year : (long)(Hour * Math.Pow(Year / Hour, random.NextDouble())));
        var readings = AcrossZones(random, left.Now, until);
        var fast = Copy(left);
        var slow = Copy(left);

        foreach (var reading in readings)
        {
            fast.AdvanceTo(reading);
        }

        foreach (var reading in readings)
        {
            for (long tick = ((slow.Now.UtcSeconds / Settlement.TickSeconds) + 1) * Settlement.TickSeconds; tick < reading.UtcSeconds; tick += Settlement.TickSeconds)
            {
                slow.AdvanceTo(new TimeReading(tick, reading.ZoneId, reading.OffsetSeconds));
            }

            slow.AdvanceTo(reading);
        }

        return fast.Fingerprint() == slow.Fingerprint();
    }

    // The same from states the build-out seldom leaves: stores full to the brim, so that the villagers' work changes
    // little, and now and then an expedition at sea, which sails on while nothing else changes. The first dawn's meal
    // empties the stores again, so the absences are short.
    [Property(MaxTest = 24)]
    public bool FastForwardGivesTheSameWorldFromFullStores(int seed)
    {
        var random = new Random(seed);
        var left = Saturated(BuiltOutWorld.Generate(seed), random);
        long until = left.Now.UtcSeconds + (Hour * (1 + random.Next(2 * 24)));
        var fast = Copy(left);
        var slow = Copy(left);

        fast.AdvanceTo(new TimeReading(until, left.Now.ZoneId, left.Now.OffsetSeconds));
        for (long tick = ((left.Now.UtcSeconds / Settlement.TickSeconds) + 1) * Settlement.TickSeconds; tick <= until; tick += Settlement.TickSeconds)
        {
            slow.AdvanceTo(new TimeReading(tick, left.Now.ZoneId, left.Now.OffsetSeconds));
        }

        slow.AdvanceTo(new TimeReading(until, left.Now.ZoneId, left.Now.OffsetSeconds));
        return fast.Fingerprint() == slow.Fingerprint();
    }

    // The fast-forward skips the quarter hours after one whose work changed nothing (Settlement.Tick), so every field
    // that a tick can change must be compared there. A new field fails here until it is recorded in
    // Settlement.RecordState, or listed below as fixed or derived; then FastForwardGivesTheSameWorldAsEveryQuarterHour
    // and BuiltOutWorld should reach the state it holds.
    [Fact]
    public void EveryFieldThatATickCanChangeIsCompared()
    {
        var expected = new Dictionary<Type, string[]>
        {
            [typeof(Settlement)] =
            [
                // Recorded.
                "_stores", "_islands", "_settlements", "_atDepots", "_owed", "_buildings", "_projects", "_felled", "_roads", "_expeditionLeft", "<SurveysDone>k__BackingField", "<Fog>k__BackingField",

                // Fixed for the world.
                "_map", "_home", "_founding", "_content", "_mapIndex",

                // Derived, or scratch for a single call.
                "_buildingAt", "_capacity", "_walks", "_walksTo", "_served", "_servedKnown", "_woods", "_jobs", "_needs", "_free", "_before", "_unstaffed", "_labour", "_pools",
            ],
            [typeof(IslandSettlement)] = ["<Villagers>k__BackingField", "<Island>k__BackingField", "_economy"],
            [typeof(Building)] =
            [
                "<Level>k__BackingField", "<Staffed>k__BackingField", "<Paused>k__BackingField", "<WorkdayToday>k__BackingField", "<Ripe>k__BackingField",
                "<Carriers>k__BackingField", "<CarrierCap>k__BackingField", "<CarriedFirst>k__BackingField", "<Depot>k__BackingField", "<Waiting>k__BackingField", "<Held>k__BackingField",
                "<Kind>k__BackingField", "<Tile>k__BackingField", "<Island>k__BackingField",
            ],
            [typeof(Project)] = ["<WorkLeft>k__BackingField", "<Kind>k__BackingField", "<Building>k__BackingField", "<Tile>k__BackingField"],

            // The fog is recorded by its lifts, which count every change of what is known.
            [typeof(Fog)] = ["_known", "<Lifts>k__BackingField", "_map"],
        };

        Assert.All(expected, type => Assert.Equal(
            type.Value.Order(StringComparer.Ordinal),
            type.Key.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Select(f => f.Name).Order(StringComparer.Ordinal)));
    }

    // The measurement scene year (MeasurementScene) catches up this world on the phone, so the phone's catch-up can be
    // set against this one: a built-out world left in daylight, a year later at the same time of day in one reading, as
    // the scene's fixed clock gives it.
    [Fact]
    public void TheYearSceneCatchesUpAHeavyWorld()
    {
        var world = YearSceneWorld();

        Assert.True(world.Buildings.Count >= 30, $"The year scene's world has only {world.Buildings.Count} buildings.");
        Assert.Equal((Phase.Active, TestClock.Stockholm), (world.Phase, world.Now.ZoneId));
        Assert.True(world.SeaRoutes.Count > 0 || world.ExpeditionHoursLeft > 0, "The year scene's world has neither the sea route nor an expedition at sea.");
    }

    // Counted, the work is the same on every machine however busy it is, and a regression in the skip is caught where
    // a timing would also fail for a busy machine (#170). What a worked quarter hour costs is timed in
    // FastForwardBenchmarks.
    [Fact]
    public void AYearAwaySkipsTheQuarterHoursWhereOnlyTheSlowProcessesMove()
    {
        var years = Enumerable.Range(0, 6).Select(BuiltOutWorld.Generate).Select(world =>
        {
            long awake = Year / Day * world.ActiveTicks;
            int buildings = world.Buildings.Count;
            long before = world.QuarterHoursWorked;
            foreach (var reading in DeviceReadings(world))
            {
                world.AdvanceTo(reading);
            }

            return (Awake: awake, Worked: world.QuarterHoursWorked - before, Buildings: buildings);
        }).ToList();
        string worked = string.Join(", ", years.Select(year => $"{year.Worked} of {year.Awake} with {year.Buildings} buildings"));

        Assert.All(years, year => Assert.InRange(year.Worked, 0, year.Awake));
        Assert.True(
            years.Sum(year => year.Worked) <= MostWorkedShare * years.Sum(year => year.Awake),
            $"A year away worked more than {MostWorkedShare:P0} of the quarter hours the villagers are awake: {worked}.");
    }

    /// <summary>
    /// The year scene's world from its save in the client's resources; created from the heaviest of a few built-out
    /// worlds left in daylight in Stockholm when it is missing, which fails the test so that the save is checked in.
    /// </summary>
    private static World YearSceneWorld()
    {
        if (!File.Exists(s_yearScene))
        {
            var heaviest = Enumerable.Range(0, 24)
                .Select(BuiltOutWorld.GenerateSave)
                .Where(save => save.World.Phase == Phase.Active && save.World.Now.ZoneId == TestClock.Stockholm && LocalHour(save.World.Now) is >= 9 and <= 16)
                .MaxBy(save => save.World.Buildings.Count)!;
            File.WriteAllBytes(s_yearScene, heaviest.ToBytes());
            Assert.Fail($"Created the year scene's world in {s_yearScene}; check it in.");
        }

        return Save.Load(File.ReadAllBytes(s_yearScene)).World;
    }

    private static int LocalHour(TimeReading reading) => (int)((reading.LocalSeconds % (24 * Hour)) / Hour);

    /// <summary>A year as the phone's clock reads it, split where the offset changes.</summary>
    private static List<TimeReading> DeviceReadings(World world) =>
        TestClock.Readings(world.Now.ZoneId, world.Now.UtcSeconds, world.Now.UtcSeconds + Year).ToList();

    /// <summary>
    /// <paramref name="world"/> with most of its stores filled to their capacity or, in one case of two, an expedition at
    /// sea with a random part of its voyage left, every store full and the queue done, so that the voyage is all that
    /// moves; written into its state.
    /// </summary>
    private static World Saturated(World world, Random random)
    {
        long Capacity(string island) => Quantity.Units(world.Buildings.Where(b => b.Island == island).Sum(b => b.Kind switch
        {
            BuildingKind.Storehouse => 150,
            BuildingKind.Jetty => 50,
            _ => 0,
        }));

        long voyage = random.Next(2) == 0 ? 1 + random.Next(PrototypeIsland.ExpeditionHours * 1000) : -1;
        var lines = world.Fingerprint().Split('\n').Where(line => voyage < 0 || !line.StartsWith("project ", StringComparison.Ordinal)).Select(line =>
        {
            string[] parts = line.Split(' ');
            if (parts[0] == "stock" && (voyage > 0 || random.Next(10) < 8))
            {
                return $"stock {parts[1]} {parts[2]} {Capacity(parts[1])}";
            }

            return parts[0] == "economy" && voyage > 0 ? $"economy {parts[1]} {voyage}" : line;
        });

        var reader = new StateReader(string.Join('\n', lines));
        var saturated = World.Read(reader, world.Map);
        reader.End();
        return saturated;
    }

    /// <summary>
    /// Readings from <paramref name="from"/> to <paramref name="untilUtc"/> as the phone's clock gives them, in its zone
    /// or now and then in the other one, as when the player travels.
    /// </summary>
    private static List<TimeReading> AcrossZones(Random random, TimeReading from, long untilUtc)
    {
        var ends = Enumerable.Range(0, random.Next(7)).Select(_ => from.UtcSeconds + (long)(random.NextDouble() * (untilUtc - from.UtcSeconds)))
            .Append(untilUtc).Order().ToList();
        var readings = new List<TimeReading>();
        long start = from.UtcSeconds;
        string zone = from.ZoneId;
        foreach (long end in ends)
        {
            readings.AddRange(TestClock.Readings(zone, start, end));
            start = end;
            zone = random.Next(2) == 0 ? zone : zone == TestClock.Stockholm ? TestClock.NewYork : TestClock.Stockholm;
        }

        return readings;
    }

    private static World Copy(World world) =>
        SimulationSnapshot.FromBytes(SimulationSnapshot.Take(world).ToBytes()).Restore(world.Map);
}

