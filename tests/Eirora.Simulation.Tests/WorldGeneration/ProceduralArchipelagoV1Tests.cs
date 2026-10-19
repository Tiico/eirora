using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Time;
using Eirora.WorldGeneration;
using Eirora.WorldGeneration.Validation;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.WorldGeneration;

/// <summary>
/// Generator v1 (TASK-031): every map it hands out passes the validator, the same seed always gives the same map, and
/// maps differ from seed to seed. The bulk run over 10 000 seeds is run locally with tools/Eirora.MapCheck.
/// </summary>
public sealed class ProceduralArchipelagoV1Tests
{
    private static readonly ProceduralArchipelagoV1 s_generator = new();

    [Fact]
    public void EveryMapOfTwoHundredSeedsPassesTheValidator()
    {
        var report = BulkValidation.Run(s_generator, 0, 200);

        Assert.Equal(200, report.Passed);
        Assert.InRange(report.Rules.Single(r => r.Rule == "attempts").Maximum, 1, ProceduralArchipelagoV1.MaximumAttempts);
    }

    [Property(MaxTest = 30)]
    public bool TheSameSeedGivesTheSameMapAfterTheSameNumberOfAttempts(ulong seed)
    {
        var first = s_generator.Generate(seed, out int attempts);
        var second = s_generator.Generate(seed, out int again);

        return first.ToText() == second.ToText() && attempts == again && MapValidator.Validate(first).Passed;
    }

    [Fact]
    public void MapsDifferFromSeedToSeed()
    {
        var hashes = Enumerable.Range(0, 50).Select(seed => s_generator.Generate((ulong)seed).Hash).ToHashSet();

        Assert.Equal(50, hashes.Count);
    }

    [Fact]
    public void AMapNamesItsGeneratorSeedAndIslands()
    {
        var map = s_generator.Generate(20261018);

        Assert.Equal((MapSource.Procedural, "procedural-archipelago", 1, 20261018UL), (map.Source, map.GeneratorId, map.GeneratorVersion, map.Seed!.Value));
        Assert.Equal(["home", "first", "second"], map.Islands.Take(3).Select(i => i.Id));
        Assert.All(map.Islands.Skip(3), island => Assert.StartsWith("skerry-", island.Id, StringComparison.Ordinal));
        Assert.Equal("home", map.IslandAt(map.Start));
    }

    [Fact]
    public void AWorldCanBeMadeOnAGeneratedMapAndSaved()
    {
        var map = s_generator.Generate(7);
        var save = Save.Begin(map, new TimeReading(1_792_000_000, "Europe/Stockholm", 7200), new RestSchedule(23 * 60, 8 * 60));

        var loaded = Save.Load(save.ToBytes());

        Assert.Equal(map.Hash, loaded.World.Map.Hash);
    }
}
