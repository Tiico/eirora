using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using FsCheck.Xunit;
using static Eirora.Simulation.Tests.Economy.TierVillage;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Step 3, Seafaring (TASK-063; economy-v2.md, sections 4–5): it unlocks once a settlement has 45 villagers, of them 6
/// craftsmen, and a sea route joins the home island to another settlement; farmers burn tar at the tar kiln, and craftsmen
/// at the shipyard build knarrs of planks and tar, one at a time in 32 active hours, each carrying 10 more a game day on
/// the sea routes to and from the shipyard's island, at most four on a route. The knarrs only change how much a route
/// carries, never the sum of the goods (INV-R2), and a knarr is never lost (INV-V1).
/// </summary>
public sealed class SeafaringTests
{
    private const long Day = 24 * 3600;
    private const int KnarrHours = 32;
    private const int MostKnarrs = 4;

    private static readonly WorldMap s_map = PrototypeHome.Map;
    private static readonly Hex[] s_start = [new(0, 0), new(1, 0), new(0, 1), new(1, -1)];

    // Acceptance: given a world with 45 villagers, of them 6 craftsmen, but no sea route, when dawn comes, then step 3 is
    // not unlocked; with a sea route to Nyön it is.
    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void StepThreeAsksForASeaRouteBesidesTheVillagers(bool seaRoute, int step)
    {
        string[] jetties = seaRoute ? [Line(BuildingKind.Jetty, Harbour(PrototypeHome.Home)), Line(BuildingKind.Jetty, Harbour(PrototypeHome.Nyon), staffed: true)] : [];
        var world = Village(crofters: 30, farmers: 9, craftsmen: 6, step: 2, buildings: jetties, stock: [(Resource.Fish, 120)], nyon: seaRoute ? 1 : null);
        Assert.Equal(seaRoute, world.SeaRoutes.Count == 1);

        var events = Dawn(world);

        Assert.Equal(step, world.Step);
        Assert.Equal(seaRoute, events.OfType<StepUnlocked>().Any(e => e.Step == 3));
    }

    // Too few craftsmen keep step 3 locked even with a sea route.
    [Fact]
    public void StepThreeAsksForSixCraftsmen()
    {
        string[] jetties = [Line(BuildingKind.Jetty, Harbour(PrototypeHome.Home)), Line(BuildingKind.Jetty, Harbour(PrototypeHome.Nyon), staffed: true)];
        var world = Village(crofters: 31, farmers: 9, craftsmen: 5, step: 2, buildings: jetties, stock: [(Resource.Fish, 120)], nyon: 1);

        Dawn(world);

        Assert.Equal(2, world.Step);
    }

    // Acceptance: given a shipyard on the home island and two knarrs, when a game day is simulated, then every sea route
    // to the home island carries at most 60 units, and more than its own 40.
    [Fact]
    public void TwoKnarrsAtHomeCarrySixtyADayOnEveryRoute()
    {
        var world = WithKnarrs(SeaRouteTests.OnlyShips((island, r) => island == Archipelago.Home ? 0 : 100, storehouses: [1, 1, 1]), (Archipelago.Home, 2));
        Assert.All(world.SeaRoutes, route => Assert.Equal((2, 60), (route.Knarrs, route.PerGameDay)));
        var before = SeaRouteTests.Stores.Of(world);

        world.AdvanceTo(Shift(world.Now, Day));

        var after = SeaRouteTests.Stores.Of(world);
        foreach (var route in world.SeaRoutes)
        {
            Assert.InRange(before.Total(route.From) - after.Total(route.From), Quantity.Units(SeaRoute.UnitsPerGameDay) + 1, Quantity.Units(60));
        }

        after.AssertSameGoodsAs(before);
    }

    // A knarr of another island's shipyard carries only on that island's route; the home island's on every route.
    [Fact]
    public void AnotherIslandsKnarrsCarryOnlyOnItsOwnRoute()
    {
        var world = WithKnarrs(SeaRouteTests.OnlyShips((island, r) => 0), (Archipelago.Home, 1), (Archipelago.First, 2));

        Assert.Equal(
            [(Archipelago.First, 3, 70), (Archipelago.Second, 1, 50)],
            world.SeaRoutes.Select(r => (r.From, r.Knarrs, r.PerGameDay)));
    }

    // No route carries more than four knarrs, however many both its islands have.
    [Fact]
    public void ARouteCarriesAtMostFourKnarrs()
    {
        var world = WithKnarrs(SeaRouteTests.OnlyShips((island, r) => 0), (Archipelago.Home, 3), (Archipelago.First, 3));

        Assert.Equal((MostKnarrs, 80), (world.SeaRoutes[0].Knarrs, world.SeaRoutes[0].PerGameDay));
    }

    // The diagnosis shows the route's capacity with its knarrs (economy-v2.md, section 7.1).
    [Fact]
    public void TheDiagnosisShowsARoutesCapacityWithItsKnarrs()
    {
        var world = WithKnarrs(SeaRouteTests.OnlyShips((island, r) => island == Archipelago.Home ? 0 : 100), (Archipelago.Home, 2));

        var route = world.Diagnose().SeaRouteFrom(Archipelago.First)!;

        Assert.Equal((2, Quantity.Units(60) / 16, 100), (route.Knarrs, route.CapacityPerHour, route.UsedPercent));
    }

    // INV-R2 with any goods on the three islands and any knarrs: the ships move more, and never make or lose any.
    [Property(MaxTest = 30)]
    public void KnarrsChangeOnlyHowMuchTheShipsCarry(int seed)
    {
        var random = new Random(seed);
        var world = WithKnarrs(
            SeaRouteTests.OnlyShips((island, r) => random.Next(51), storehouses: [1, 1, 1], secondOfDay: random.Next(24 * 4) * 900),
            (Archipelago.Home, random.Next(MostKnarrs + 1)),
            (Archipelago.First, random.Next(MostKnarrs + 1)),
            (Archipelago.Second, random.Next(MostKnarrs + 1)));
        var before = SeaRouteTests.Stores.Of(world);

        world.AdvanceTo(Shift(world.Now, Day));

        var after = SeaRouteTests.Stores.Of(world);
        foreach (var route in world.SeaRoutes)
        {
            Assert.InRange(before.Total(route.From) - after.Total(route.From), 0, Quantity.Units(route.PerGameDay));
        }

        after.AssertSameGoodsAs(before);
    }

    // The shipyard pays a knarr's planks and tar when it begins it, and two craftsmen finish it in 32 active hours.
    [Fact]
    public void AShipyardBuildsAKnarrOfPlanksAndTarInThirtyTwoActiveHours()
    {
        var world = Shipyard(planks: 45, tar: 32);

        // The quarter hour that begins at dawn is the first one worked.
        world.AdvanceTo(At("2026-10-16 06:45", 2));
        var shipyard = world.BuildingAt(Harbour(PrototypeHome.Home))!;
        Assert.Equal((45, 32, 0), (world.Stock(Resource.Planks), world.Stock(Resource.Tar), shipyard.KnarrHoursLeft));
        world.AdvanceTo(At("2026-10-16 07:00", 2));
        Assert.True(shipyard.Staffed);
        Assert.Equal((35, 27, KnarrHours), (world.Stock(Resource.Planks), world.Stock(Resource.Tar), shipyard.KnarrHoursLeft));

        world.AdvanceTo(At("2026-10-17 22:30", 2));
        Assert.Equal((0, 1), (world.Knarrs(PrototypeHome.Home), shipyard.KnarrHoursLeft));

        var events = world.AdvanceTo(At("2026-10-17 22:45", 2)).ToList();
        Assert.Equal((1, 0), (world.Knarrs(PrototypeHome.Home), shipyard.KnarrHoursLeft));
        Assert.Contains(events, e => e is KnarrLaunched { Island: PrototypeHome.Home, Knarrs: 1 });

        // The next begins at the next dawn, while the store still holds a knarr's materials above the minimum stock, and
        // none after it.
        world.AdvanceTo(At("2026-10-18 07:00", 2));
        Assert.Equal((25, 22, KnarrHours), (world.Stock(Resource.Planks), world.Stock(Resource.Tar), shipyard.KnarrHoursLeft));
    }

    // Acceptance: given four knarrs, when the shipyard goes on, then no fifth is built; and an island's shipyards together
    // begin no more than a route has room for.
    [Fact]
    public void WithFourKnarrsNoFifthIsBuilt()
    {
        var world = WithKnarrs(Shipyard(planks: 60, tar: 30, shipyards: 2), (PrototypeHome.Home, MostKnarrs - 1));

        world.AdvanceTo(At("2026-10-16 07:15", 2));
        Assert.Equal(1, world.Buildings.Count(b => b.KnarrHoursLeft > 0));
        world.AdvanceTo(At("2026-10-23 07:15", 2));

        Assert.Equal(MostKnarrs, world.Knarrs(PrototypeHome.Home));
        Assert.All(world.Buildings, b => Assert.Equal(0, b.KnarrHoursLeft));
        Assert.Equal((50, 25), (world.Stock(Resource.Planks), world.Stock(Resource.Tar)));
    }

    // The shipyard leaves its minimum stock of planks and tar, and the island's reservation when that is larger, as
    // processing does (D-125, D-127; produktägaren 2026-10-25, PR #185); the player can lower the minimum stock.
    [Fact]
    public void AShipyardLeavesItsMinimumStockAndTheIslandsReservation()
    {
        var world = Shipyard(planks: 29, tar: 40);
        var tile = Harbour(PrototypeHome.Home);
        world.AdvanceTo(At("2026-10-16 07:00", 2));
        Assert.Equal((0, 29), (world.BuildingAt(tile)!.KnarrHoursLeft, world.Stock(Resource.Planks)));

        Assert.True(world.Apply("reserve", new SetReserve(tile, 15)).Accepted);
        Assert.True(world.Apply("reservation", new SetReservation(PrototypeHome.Home, Resource.Tar, 36)).Accepted);
        world.AdvanceTo(At("2026-10-16 08:00", 2));
        Assert.Equal((0, 29, 40), (world.BuildingAt(tile)!.KnarrHoursLeft, world.Stock(Resource.Planks), world.Stock(Resource.Tar)));

        Assert.True(world.Apply("less", new SetReservation(PrototypeHome.Home, Resource.Tar, 35)).Accepted);
        world.AdvanceTo(At("2026-10-16 08:15", 2));
        Assert.Equal((KnarrHours, 19, 35), (world.BuildingAt(tile)!.KnarrHoursLeft, world.Stock(Resource.Planks), world.Stock(Resource.Tar)));
    }

    // A shipyard without craftsmen keeps the knarr it began and goes on with it when it is staffed again.
    [Fact]
    public void APausedShipyardKeepsItsKnarr()
    {
        var world = Shipyard(planks: 10, tar: 5, reserve: 0);
        var tile = Harbour(PrototypeHome.Home);
        world.AdvanceTo(At("2026-10-16 10:45", 2));
        Assert.Equal(KnarrHours - 4, world.BuildingAt(tile)!.KnarrHoursLeft);

        Assert.True(world.Apply("pause", new PauseBuilding(tile)).Accepted);
        world.AdvanceTo(At("2026-10-17 10:45", 2));
        Assert.Equal(KnarrHours - 4, world.BuildingAt(tile)!.KnarrHoursLeft);

        Assert.True(world.Apply("resume", new ResumeBuilding(tile)).Accepted);
        world.AdvanceTo(At("2026-10-17 14:45", 2));
        Assert.Equal(KnarrHours - 8, world.BuildingAt(tile)!.KnarrHoursLeft);
    }

    // INV-V1: demolishing the shipyard gives back the materials of the knarr it was building with the rest, and the
    // island keeps every knarr it has.
    [Fact]
    public void ADemolishedShipyardGivesBackItsKnarrsMaterialsAndTheKnarrsStay()
    {
        var world = WithKnarrs(Shipyard(planks: 10, tar: 5, reserve: 0), (PrototypeHome.Home, 2));
        var tile = Harbour(PrototypeHome.Home);
        world.AdvanceTo(At("2026-10-16 09:00", 2));
        Assert.Equal((0, 0), (world.Stock(Resource.Planks), world.Stock(Resource.Tar)));

        var demolition = world.Demolition(tile)!;
        Assert.Equal((10, 5), (demolition.Emptied(Resource.Planks), demolition.Emptied(Resource.Tar)));
        Assert.True(world.Apply("demolish", new Demolish(tile)).Accepted);

        Assert.Equal((10 + demolition.Returned(Resource.Planks), 5L), (world.Stock(Resource.Planks), world.Stock(Resource.Tar)));
        Assert.Equal(2, world.Knarrs(PrototypeHome.Home));
    }

    // The knarrs and a knarr in progress are state: a save keeps them, and one that could not be is refused.
    [Fact]
    public void KnarrsAndAKnarrInProgressAreKeptBySaves()
    {
        var world = WithKnarrs(Shipyard(planks: 10, tar: 5, reserve: 0), (PrototypeHome.Home, 1));
        world.AdvanceTo(At("2026-10-16 08:45", 2));
        string state = world.Fingerprint();
        Assert.Contains("knarrs home 1", state.Split('\n'));
        Assert.Contains($"knarr {(KnarrHours - 2) * 1000}", state.Split('\n'));

        Assert.Equal(state, Read(state).Fingerprint());
        Assert.Throws<FormatException>(() => Read(state.Replace("knarrs home 1", $"knarrs home {MostKnarrs + 1}", StringComparison.Ordinal)));
        Assert.Throws<FormatException>(() => Read(state.Replace($"knarr {(KnarrHours - 2) * 1000}", $"knarr {(KnarrHours * 1000) + 1}", StringComparison.Ordinal)));
        Assert.Throws<FormatException>(() => Read(state.Replace("knarrs home 1", $"knarrs {PrototypeHome.Nyon} 1", StringComparison.Ordinal)));
    }

    // INV-V4: a world whose shipyard builds is the same advanced in quarter hours or at once.
    [Fact]
    public void AShipyardsWorldIsTheSameAdvancedInPartsOrAtOnce()
    {
        var once = Shipyard(planks: 30, tar: 15, reserve: 0);
        var parts = Shipyard(planks: 30, tar: 15, reserve: 0);
        var until = At("2026-10-19 12:00", 2);

        once.AdvanceTo(until);
        for (var now = parts.Now; now.UtcSeconds < until.UtcSeconds; now = parts.Now)
        {
            parts.AdvanceTo(new TimeReading(now.UtcSeconds + 900, now.ZoneId, now.OffsetSeconds));
        }

        Assert.Equal(1, once.Knarrs(PrototypeHome.Home));
        Assert.Equal(once.Fingerprint(), parts.Fingerprint());
    }

    // INV-V1 and INV-V4: a year away, the shipyard builds the knarrs that the store has the materials for, at most four,
    // and the island loses none of them.
    [Fact]
    public void AYearAwayBuildsTheKnarrsTheStoreHasMaterialsFor()
    {
        var world = Shipyard(planks: 60, tar: 30, reserve: 0);

        world.AdvanceTo(At("2027-10-16 12:00", 2));

        Assert.Equal((MostKnarrs, 3), (world.Knarrs(PrototypeHome.Home), world.Step));
        Assert.Equal((20, 10), (world.Stock(Resource.Planks), world.Stock(Resource.Tar)));
        Assert.All(world.Buildings, b => Assert.Equal(0, b.KnarrHoursLeft));
    }

    /// <summary>
    /// The prototype's village half an hour before a dawn, with step 3, two craftsmen among its villagers and a staffed
    /// shipyard on each of the first <paramref name="shipyards"/> free harbour spots at home with a minimum stock of
    /// <paramref name="reserve"/>, and planks and tar in store.
    /// </summary>
    private static World Shipyard(int planks, int tar, int shipyards = 1, int reserve = StockRules.StandardReserve) => Village(
        crofters: 10,
        craftsmen: 2 * shipyards,
        step: 3,
        buildings: [.. Enumerable.Range(0, shipyards).Select(i => Line(BuildingKind.Shipyard, Harbour(PrototypeHome.Home, i), staffed: true))
            .Select(line => $"{line[..line.LastIndexOf(' ')]} {reserve}")],
        stock: [(Resource.Planks, planks), (Resource.Tar, tar), (Resource.Fish, 80)]);

    /// <summary>A free harbour spot on an island of the prototype's map, past the start's buildings.</summary>
    private static Hex Harbour(string island, int skip = 0) =>
        s_map.Features.LandOf(island).Where(h => s_map.Features.IsHarborSpot(h) && !s_start.Contains(h)).Skip(skip).First();

    /// <summary>The world with knarrs on islands, as a save of rules 15 writes them, read back.</summary>
    private static World WithKnarrs(World world, params (string Island, int Knarrs)[] knarrs)
    {
        var lines = world.Fingerprint().Split('\n').ToList();
        int at = lines.FindIndex(l => l.StartsWith("reserved ", StringComparison.Ordinal) || l.StartsWith("imports ", StringComparison.Ordinal) || l.StartsWith("building ", StringComparison.Ordinal));
        var order = world.Map.Islands.Select(i => i.Id).ToList();
        lines.InsertRange(at, knarrs.Where(k => k.Knarrs > 0).OrderBy(k => order.IndexOf(k.Island)).Select(k => $"knarrs {k.Island} {k.Knarrs}"));
        return Read(string.Join('\n', lines), world.Map);
    }

    private static TimeReading Shift(TimeReading reading, long seconds) => new(reading.UtcSeconds + seconds, reading.ZoneId, reading.OffsetSeconds);

    private static World Read(string state, WorldMap? map = null)
    {
        var reader = new StateReader(state);
        var world = World.Read(reader, map ?? s_map);
        reader.End();
        return world;
    }
}
