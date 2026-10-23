using System.Diagnostics;
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
/// which gives the same world as reading every quarter hour, and a year away is caught up within the time a cold start
/// on the weakest target phone can spare (performance-budgets.md).
/// </summary>
/// <remarks>
/// Every world is drawn from FsCheck's seed, so shrinking a failing case gives another world rather than a simpler one.
/// Play a failing case again with the replay FsCheck prints, <c>[Property(Replay = "(…)")]</c>, or with
/// <c>BuiltOutWorld.Generate(seed)</c> for the seed it reports.
/// </remarks>
[Collection(nameof(TimedAlone))]
public sealed class FastForwardTests
{
    private const long Hour = 3600;
    private const long Year = 365 * 24 * Hour;

    /// <summary>
    /// The most a year away may take to fast-forward here, in the slowest of the built-out worlds.
    /// </summary>
    /// <remarks>
    /// The floor phone, a Galaxy A06 (Helio G85), has five seconds for a cold start, of which catching up may take one;
    /// Unity's own start takes most of the rest. Its cores score a quarter to a fifth of a CI runner's or a developer
    /// laptop's in single-threaded benchmarks, and IL2CPP runs this code somewhat slower than .NET, so one second there
    /// is about 150 ms here. Unoptimized code (Debug) runs about three times slower.
    /// </remarks>
#if DEBUG
    private const int MostMilliseconds = 3 * 150;
#else
    private const int MostMilliseconds = 150;
#endif

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
                "_stores", "_buildings", "_projects", "_felled", "_expeditionLeft", "<Villagers>k__BackingField", "<SurveysDone>k__BackingField", "<Fog>k__BackingField",

                // Fixed for the world.
                "_map", "_homeIsland", "_founding",

                // Derived, or scratch for a single call.
                "_buildingAt", "_capacity", "_depotDistances", "_woods", "_regrowing", "_before", "_after",
            ],
            [typeof(Building)] =
            [
                "<Level>k__BackingField", "<Staffed>k__BackingField", "<HasRoad>k__BackingField", "<WorkdayToday>k__BackingField", "<Ripe>k__BackingField",
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
        Assert.True(world.SeaRouteOpen || world.ExpeditionHoursLeft > 0, "The year scene's world has neither the sea route nor an expedition at sea.");
    }

    [Fact]
    public void AYearAwayIsCaughtUpInTime()
    {
        var worlds = Enumerable.Range(0, 6).Select(BuiltOutWorld.Generate).ToList();
        var scene = YearSceneWorld();
        AdvanceAYear(Copy(worlds[0]), DeviceReadings); // Compiles the simulation before it is timed.

        // The best of three, since a shared machine only ever slows a run down, and the processor time is read coarsely.
        double Best(World world, Func<World, IReadOnlyList<TimeReading>> readings) =>
            Enumerable.Range(0, 3).Min(_ => AdvanceAYear(Copy(world), readings));
        var slowest = worlds.Select(world => (World: world, Milliseconds: Best(world, DeviceReadings))).MaxBy(timed => timed.Milliseconds);
        double sceneMilliseconds = Best(scene, SceneReading);

        Assert.True(
            slowest.Milliseconds <= MostMilliseconds,
            $"A year away took {slowest.Milliseconds:F0} ms to fast-forward with {slowest.World.Buildings.Count} buildings, more than {MostMilliseconds} ms.");
        Assert.True(sceneMilliseconds <= MostMilliseconds, $"A year away took {sceneMilliseconds:F0} ms in the year scene's world, more than {MostMilliseconds} ms.");
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

    /// <summary>A year in one reading, as the year scene's fixed clock gives it (MeasurementScene).</summary>
    private static List<TimeReading> SceneReading(World world) =>
        [new TimeReading(world.Now.UtcSeconds + Year, world.Now.ZoneId, world.Now.OffsetSeconds)];

    /// <summary>
    /// <paramref name="world"/> with most of its stores filled to their capacity or, in one case of two, an expedition at
    /// sea with a random part of its voyage left, every store full and the queue done, so that the voyage is all that
    /// moves; written into its state.
    /// </summary>
    private static World Saturated(World world, Random random)
    {
        long Capacity(IslandId island) => Quantity.Units(world.Buildings.Where(b => b.Island == island).Sum(b => b.Kind switch
        {
            BuildingKind.Storehouse => 150,
            BuildingKind.Jetty when island != IslandId.Home => 150,
            _ => 0,
        }));

        long voyage = random.Next(2) == 0 ? 1 + random.Next(PrototypeIsland.ExpeditionHours * 1000) : -1;
        var lines = world.Fingerprint().Split('\n').Where(line => voyage < 0 || !line.StartsWith("project ", StringComparison.Ordinal)).Select(line =>
        {
            string[] parts = line.Split(' ');
            if (parts[0] == "stock" && (voyage > 0 || random.Next(10) < 8))
            {
                return $"stock {parts[1]} {parts[2]} {Capacity(Enum.Parse<IslandId>(parts[1]))}";
            }

            return parts[0] == "settlement" && voyage > 0 ? $"settlement {parts[1]} {parts[2]} {voyage}" : line;
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

    /// <summary>
    /// The processor time a year away takes to fast-forward, in milliseconds: the work itself, which a busy machine
    /// does not stretch the way it stretches the wall clock.
    /// </summary>
    private static double AdvanceAYear(World world, Func<World, IReadOnlyList<TimeReading>> readingsOf)
    {
        var readings = readingsOf(world);
        using var process = Process.GetCurrentProcess();
        var before = process.TotalProcessorTime;
        foreach (var reading in readings)
        {
            world.AdvanceTo(reading);
        }

        process.Refresh();
        return (process.TotalProcessorTime - before).TotalMilliseconds;
    }

    private static World Copy(World world) =>
        SimulationSnapshot.FromBytes(SimulationSnapshot.Take(world).ToBytes()).Restore(world.Map);
}

/// <summary>Times the fast-forward alone, after the tests that run in parallel.</summary>
[CollectionDefinition(nameof(TimedAlone), DisableParallelization = true)]
public sealed class TimedAlone;

