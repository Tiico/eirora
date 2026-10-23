using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// A field and a sheep farm grow round the clock and are harvested in the active hours (INV-T5), and level 2 and a
/// workday scale how fast they grow, as they scale every other building's production (economy-v1.md, section 3;
/// TASK-038); distance costs the carriers' time instead (D-110).
/// </summary>
public sealed class SlowProcessTests
{
    // A field yields 0.5 grain an hour times its tile's richness, so it grows 4 grain in the eight-hour night.
    private const long NightAtFullRate = 4 * Quantity.PerUnit;

    private static int s_key;

    [Fact]
    public void AFieldTwoStepsFromTheStorehouseGrowsAsFastAsOneBesideIt()
    {
        var world = WithFieldAtO2();
        Assert.Equal(1, world.TripFrom(O1).Steps);
        Assert.Equal(2, world.TripFrom(O2).Steps);

        var growth = NightGrowth(world, O1, O2);

        Assert.Equal([NightAtFullRate, NightAtFullRate], growth);
    }

    [Fact]
    public void ASecondLevelFieldGrowsHalfAsMuchAgain()
    {
        var world = Morning();
        world.Settlement.BuildingAt(O1)!.Level = 2;

        Assert.Equal([NightAtFullRate * 3 / 2], NightGrowth(world, O1));
    }

    [Fact]
    public void AWorkdayOnAFieldMakesItGrowHalfAsMuchAgainUntilTheNextDawn()
    {
        var world = Morning();
        Assert.True(Apply(world, new Workday(O1)).Accepted);

        Assert.Equal([NightAtFullRate * 3 / 2], NightGrowth(world, O1));

        // The workday ends at dawn, so the next night grows at the plain rate.
        world.AdvanceTo(At("2026-10-16 12:00", 2));
        Assert.False(world.Settlement.BuildingAt(O1)!.WorkdayToday);
        Assert.Equal([NightAtFullRate], NightGrowth(world, O1, night: 16));
    }

    [Fact]
    public void AFarFieldDeliversAsMuchAsANearFieldWhileTheCarriersKeepUp()
    {
        var near = Morning();
        var far = WithFieldAtO2();

        long nearGrain = GrainHarvestedOnTheSixteenth(near), bothGrain = GrainHarvestedOnTheSixteenth(far);

        // A game day of growth, 12 grain at full rate, ripens round the clock and is all harvested by dusk.
        Assert.Equal(12 * Quantity.PerUnit, nearGrain);
        Assert.Equal(12 * Quantity.PerUnit, bothGrain - nearGrain);
    }

    /// <summary>How much grain each field grows in the night's 32 ticks while the villagers sleep, 23:00 to 07:00.</summary>
    private static long[] NightGrowth(World world, Hex first, Hex? second = null, int night = 15)
    {
        Hex[] fields = second is Hex other ? [first, other] : [first];
        world.AdvanceTo(At($"2026-10-{night:00} 22:59", 2));
        long[] atDusk = [.. fields.Select(f => world.Settlement.BuildingAt(f)!.Ripe)];
        world.AdvanceTo(At($"2026-10-{night + 1:00} 06:59", 2));
        return [.. fields.Select((f, i) => world.Settlement.BuildingAt(f)!.Ripe - atDusk[i])];
    }

    /// <summary>The grain harvested from dusk on the 15th to dusk on the 16th, in the store or waiting for the carriers.</summary>
    private static long GrainHarvestedOnTheSixteenth(World world)
    {
        long Grain() => world.Settlement.Stock(Resource.Grain) + world.Buildings.Where(b => b.Kind == BuildingKind.Field).Sum(f => f.Waiting);
        world.AdvanceTo(At("2026-10-15 22:59", 2));
        long atDusk = Grain();
        world.AdvanceTo(At("2026-10-16 22:59", 2));
        Assert.All(world.Buildings.Where(b => b.Kind == BuildingKind.Field), f => Assert.Equal(0, f.Ripe));
        return Grain() - atDusk;
    }

    private static World WithFieldAtO2()
    {
        var world = Morning();
        world.Settlement.CompleteSurvey();
        world.Settlement.CompleteSurvey(); // The second survey finds the farmland at O2.
        Assert.True(Apply(world, new Build(BuildingKind.Field, O2)).Accepted);
        world.AdvanceTo(At("2026-10-15 22:00", 2));
        Assert.True(world.Settlement.BuildingAt(O2)!.Staffed);
        return world;
    }

    private static World Morning()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 07:00", 2));
        return world;
    }

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"slow-{Interlocked.Increment(ref s_key)}", command);
}
