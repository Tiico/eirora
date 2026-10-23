using System.Diagnostics;
using Eirora.Simulation.Economy;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Fast-forwarding a long absence (TASK-049): the quarter hours where only the slow processes move are grown at once,
/// which gives the same world as reading every quarter hour, and a year away is caught up within the time a cold start
/// on the weakest target phone can spare (performance-budgets.md).
/// </summary>
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

    // Read a quarter hour at a time, every tick is worked one by one, as before the fast-forward skipped any.
    [Property(MaxTest = 8)]
    public bool FastForwardGivesTheSameWorldAsEveryQuarterHour(int seed)
    {
        var random = new Random(seed);
        var left = BuiltOutWorld.Generate(seed);
        long until = left.Now.UtcSeconds + (Hour * (1 + random.Next(30 * 24)));
        var fast = Copy(left);
        var slow = Copy(left);

        foreach (var reading in TestClock.Readings(left.Now.ZoneId, left.Now.UtcSeconds, until))
        {
            fast.AdvanceTo(reading);
        }

        var everyTick = Enumerable.Range(1, (int)((until - left.Now.UtcSeconds) / Settlement.TickSeconds) + 1)
            .Select(i => ((left.Now.UtcSeconds / Settlement.TickSeconds) + i) * Settlement.TickSeconds);
        foreach (var reading in TestClock.Readings(left.Now.ZoneId, left.Now.UtcSeconds, until, everyTick))
        {
            slow.AdvanceTo(reading);
        }

        return fast.Fingerprint() == slow.Fingerprint();
    }

    [Fact]
    public void AYearAwayIsCaughtUpInTime()
    {
        var worlds = Enumerable.Range(0, 6).Select(BuiltOutWorld.Generate).ToList();
        AdvanceAYear(Copy(worlds[0])); // Compiles the simulation before it is timed.

        // The best of three, since a shared machine only ever slows a run down, and the processor time is read coarsely.
        var slowest = worlds
            .Select(world => (World: world, Milliseconds: Enumerable.Range(0, 3).Min(_ => AdvanceAYear(Copy(world)))))
            .MaxBy(timed => timed.Milliseconds);

        Assert.True(
            slowest.Milliseconds <= MostMilliseconds,
            $"A year away took {slowest.Milliseconds:F0} ms to fast-forward with {slowest.World.Buildings.Count} buildings, more than {MostMilliseconds} ms.");
    }

    /// <summary>
    /// The processor time a year away takes to fast-forward, in milliseconds: the work itself, which a busy machine
    /// does not stretch the way it stretches the wall clock.
    /// </summary>
    private static double AdvanceAYear(World world)
    {
        var readings = TestClock.Readings(world.Now.ZoneId, world.Now.UtcSeconds, world.Now.UtcSeconds + Year).ToList();
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

