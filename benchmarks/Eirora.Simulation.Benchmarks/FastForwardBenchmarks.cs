using BenchmarkDotNet.Attributes;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Benchmarks;

/// <summary>
/// A year away fast-forwarded in the measurement scene year's world (MeasurementScene), the built-out world the phone
/// catches up on its cold start after a year away: a year later at the same time of day, in one reading as the scene's
/// fixed clock gives it (TASK-049).
/// </summary>
/// <remarks>
/// The floor phone, a Galaxy A06 (Helio G85), has five seconds for a cold start, of which catching up may take one;
/// Unity's own start takes most of the rest. Its cores score a quarter to a fifth of a CI runner's or a developer
/// laptop's in single-threaded benchmarks, and IL2CPP runs this code somewhat slower than .NET, so one second there is
/// about 150 ms on a developer machine (performance-budgets.md). The test suite counts the quarter hours a year works
/// instead, which a busy machine cannot change (FastForwardTests, #170).
/// </remarks>
[MemoryDiagnoser]
public class FastForwardBenchmarks
{
    private const long Year = 365 * 24 * 3600;

    // Years fast-forwarded in one iteration, so that it lasts long enough to time well.
    private const int Worlds = 4;

    private readonly World[] _worlds = new World[Worlds];
    private readonly TimeReading[] _yearLater = new TimeReading[Worlds];
    private byte[] _save = null!;

    [GlobalSetup]
    public void Setup() => _save = File.ReadAllBytes(PathFromRoot("client", "Assets", "Eirora", "Resources", "Measurement", "AYearAway.bytes"));

    // A world fast-forwarded is a year on, so every iteration loads the saved world again; loading is not timed.
    [IterationSetup]
    public void Load()
    {
        for (int i = 0; i < Worlds; i++)
        {
            var world = Save.Load(_save).World;
            _worlds[i] = world;
            _yearLater[i] = new TimeReading(world.Now.UtcSeconds + Year, world.Now.ZoneId, world.Now.OffsetSeconds);
        }
    }

    [Benchmark(OperationsPerInvoke = Worlds)]
    public int AYearAway()
    {
        int events = 0;
        for (int i = 0; i < Worlds; i++)
        {
            events += _worlds[i].AdvanceTo(_yearLater[i]).Count;
        }

        return events;
    }

    /// <summary>A path under the repository's root, the folder with Eirora.slnx, wherever BenchmarkDotNet runs from.</summary>
    private static string PathFromRoot(params string[] parts)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Eirora.slnx")))
        {
            folder = folder.Parent;
        }

        return Path.Combine([folder?.FullName ?? throw new InvalidOperationException("The benchmarks run outside the repository."), .. parts]);
    }
}
