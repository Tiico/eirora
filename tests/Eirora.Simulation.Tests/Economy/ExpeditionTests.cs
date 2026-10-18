using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>Expeditions, Nyön and the sea route (economy-v1.md; EXP-01, EXP-02, LOG-04, INV-T4).</summary>
public sealed class ExpeditionTests
{
    private const long Hour = 3600;
    private static readonly string[] s_nyon = ["N0", "N1", "N2"];
    private static int s_key;

    [Fact]
    public void AnExpeditionSetsOutFromAJetty()
    {
        var world = Morning();

        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new SendExpedition()).Rejection);
    }

    [Fact]
    public void TheCrewRestsAtNightSoTheVoyageTakesThirtyTwoWakingHours()
    {
        var world = WithJetty();
        Assert.True(Apply(world, new SendExpedition()).Accepted);
        Assert.Equal(PrototypeIsland.ExpeditionHours, world.ExpeditionHoursLeft);

        // A full day later only 16 waking hours have passed.
        var dayLater = world.AdvanceTo(Shift(world.Now, 24));
        Assert.DoesNotContain(dayLater, e => e is ExpeditionReturned);
        Assert.False(world.IsRevealed("N0"));

        var home = world.AdvanceTo(Shift(world.Now, 24));
        Assert.Equal(s_nyon, Assert.Single(home.OfType<ExpeditionReturned>()).SiteIds);
        Assert.True(world.IsRevealed("N1"));
        Assert.Equal(0, world.ExpeditionHoursLeft);
    }

    [Fact]
    public void OnlyOneExpeditionSailsAtATime()
    {
        var world = WithJetty();
        Apply(world, new SendExpedition());

        Assert.Equal(Rejection.AlreadyDone, Apply(world, new SendExpedition()).Rejection);
    }

    [Fact]
    public void ALookoutMakesTheExpeditionAQuarterCheaper()
    {
        var world = Morning();
        Assert.Equal(250, SendExpedition.Cost(world));

        Apply(world, new Build(BuildingKind.Lookout, "L2"));
        world.AdvanceTo(Shift(world.Now, 4));

        Assert.Equal(187, SendExpedition.Cost(world));
    }

    [Fact]
    public void BuildersReachNyonByItsJettyFirst()
    {
        var world = Discovered();

        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new Build(BuildingKind.Quarry, "N2")).Rejection);
        Assert.Equal(Rejection.None, Apply(world, new Build(BuildingKind.Jetty, "N0")).Rejection);
        world.AdvanceTo(Shift(world.Now, 24));
        Assert.Equal(Rejection.None, Apply(world, new Build(BuildingKind.Quarry, "N2")).Rejection);
    }

    [Fact]
    public void TheSeaRouteShipsFortyUnitsAGameDayAndTheRestWaitsOnNyon()
    {
        var world = Discovered();
        Assert.True(Apply(world, new Build(BuildingKind.Jetty, "N0")).Accepted);
        world.AdvanceTo(Shift(world.Now, 24));
        Assert.True(Apply(world, new Build(BuildingKind.Quarry, "N2")).Accepted);
        world.AdvanceTo(Shift(world.Now, 24 * 2));
        Assert.Equal(Rejection.None, Apply(world, new Build(BuildingKind.SheepFarm, "N1")).Rejection);
        world.AdvanceTo(Shift(world.Now, 24));
        Assert.Equal(Rejection.None, Apply(world, new Build(BuildingKind.Storehouse, "E1")).Rejection); // Room at home.
        world.AdvanceTo(Shift(world.Now, 24 * 3));
        Assert.True(world.SeaRouteOpen);

        long Home() => world.Stock(Resource.Stone) + world.Stock(Resource.Wool);
        long Nyon() => world.Stock(IslandId.Nyon, Resource.Stone) + world.Stock(IslandId.Nyon, Resource.Wool);
        long homeBefore = Home(), nyonBefore = Nyon();
        world.AdvanceTo(Shift(world.Now, 24));

        // A rich quarry and a rich pasture make about 43 a day on Nyön; the ships carry 40 and the rest waits there.
        // Stock is read in whole units, so the measured day can differ by one either way.
        Assert.InRange(Home() - homeBefore, 39, 41);
        Assert.True(Nyon() - nyonBefore >= 2, $"Nyön {nyonBefore} → {Nyon()}");
    }

    private static World WithJetty()
    {
        var world = Morning();
        Assert.True(Apply(world, new Build(BuildingKind.Jetty, "H1")).Accepted);
        Assert.True(Apply(world, new Build(BuildingKind.Sawmill, "L1")).Accepted); // Planks for building on Nyön.
        world.AdvanceTo(Shift(world.Now, 6));
        Assert.Contains(world.Buildings, b => b.Kind == BuildingKind.Jetty);
        Record(world, Habit.Steps, 50_000); // 100 + 110 = 210 Exploration.
        world.AdvanceTo(At("2026-10-16 08:00", 2));
        Record(world, Habit.Steps, 50_000); // 320 Exploration.
        return world;
    }

    private static World Discovered()
    {
        var world = WithJetty();
        Record(world, Habit.Workouts, 2); // Build Power for the jetty on Nyön.
        Apply(world, new SendExpedition());
        world.AdvanceTo(Shift(world.Now, 48));
        Assert.True(world.IsRevealed("N0"));

        // A week of timber through the sawmill pays for the jetty and the quarry on Nyön.
        world.AdvanceTo(Shift(world.Now, 24 * 7));
        return world;
    }

    private static World Morning()
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 07:00", 2));
        return world;
    }

    private static TimeReading Shift(TimeReading reading, long hours) =>
        new(reading.UtcSeconds + (hours * Hour), reading.ZoneId, reading.OffsetSeconds);

    private static void Record(World world, Habit habit, int total) =>
        Assert.True(Apply(world, new RecordSelfCare(world.GameDay, habit, ContributionSource.Verified, total)).Accepted);

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"x-{Interlocked.Increment(ref s_key)}", command);
}
