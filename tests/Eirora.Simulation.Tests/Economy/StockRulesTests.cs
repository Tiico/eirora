using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// The player's rules for the stores (TASK-046, LOG-06, D-44): the minimum stock a processing building leaves, the order
/// the ships take an island's goods home in, and the reservations that neither the ships nor processing take. They are
/// free planning (D-23), refused while the villagers rest, and they only keep goods where they are or change their order
/// (INV-R1, INV-R2).
/// </summary>
public sealed class StockRulesTests
{
    private const long Unit = Quantity.PerUnit;
    private const long Day = 24 * 3600;
    private static int s_key;

    // Acceptance: a sawmill with the minimum stock 40 and 45 timber processes at most 5 timber in an active hour.
    [Fact]
    public void ASawmillWithTheMinimumStockFortyAndFortyFiveTimberProcessesAtMostFiveTimber()
    {
        var world = Sawmill(timber: 45);
        Assert.True(Apply(world, new SetReserve(L1, 40)).Accepted);
        Assert.Equal(40, world.BuildingAt(L1)!.Reserve);

        world.AdvanceTo(At("2026-10-15 10:00", 2));
        Assert.InRange(Processed(world, 45), 0, 5 * Unit);

        // However long it works, it takes the 5 above the minimum stock and leaves the 40 for building.
        world.AdvanceTo(At("2026-10-15 22:00", 2));
        Assert.Equal(5 * Unit, Processed(world, 45));
        Assert.Equal(40 * Unit, world.Settlement.Stock(Resource.Timber));
    }

    [Fact]
    public void AMinimumStockOfNothingLetsTheSawmillTakeEveryTimber()
    {
        var world = Sawmill(timber: 6);
        Assert.True(Apply(world, new SetReserve(L1, 0)).Accepted);

        world.AdvanceTo(At("2026-10-15 22:00", 2));

        Assert.Equal(6 * Unit, Processed(world, 6));
        Assert.Equal(0, world.Settlement.Stock(Resource.Timber));
    }

    // Processing leaves the larger of its minimum stock and the island's reservation, and the diagnosis says which keeps it.
    [Fact]
    public void ProcessingLeavesTheReservationWhenItIsAboveTheMinimumStockAndTheDiagnosisSaysSo()
    {
        var world = Sawmill(timber: 30);
        Assert.True(Apply(world, new SetReservation(world.HomeIsland, Resource.Timber, 24)).Accepted);

        world.AdvanceTo(At("2026-10-15 22:00", 2));

        Assert.Equal(6 * Unit, Processed(world, 30));
        Assert.Equal(24 * Unit, world.Settlement.Stock(Resource.Timber));

        // With 20 timber, the minimum stock keeps it until the reservation is above it.
        var scarce = Sawmill(timber: 20);
        var waiting = Assert.IsType<WaitingForInput>(Assert.Single(scarce.Diagnose().At(L1)!.Hindrances));
        Assert.Equal((20, KeptBy.Reserve), (waiting.Kept, waiting.KeptBy));

        Assert.True(Apply(scarce, new SetReservation(scarce.HomeIsland, Resource.Timber, 24)).Accepted);
        waiting = Assert.IsType<WaitingForInput>(Assert.Single(scarce.Diagnose().At(L1)!.Hindrances));
        Assert.Equal((24, KeptBy.Reservation, 20L, 6L), (waiting.Kept, waiting.KeptBy, waiting.Stock, waiting.Missing));
    }

    // Review of #167: a raw material the player's rules keep can be the island's bottleneck, unlike a pause, since more of it
    // helps; the workday then goes to the building that makes it (economy-v1.md, section 5).
    [Fact]
    public void ARawMaterialAReservationKeepsCanBeTheBottleneckAndAWorkdayOnItsMakerEasesIt()
    {
        var world = CarriersTests.Edited(Morning(), "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0 20", stock: (Resource.Timber, 22));
        Assert.True(Apply(world, new SetReservation(world.HomeIsland, Resource.Timber, 30)).Accepted);

        var bottleneck = world.Diagnose().BottleneckOn(world.HomeIsland)!;

        Assert.Equal(L1, bottleneck.Building.Building.Tile);
        Assert.Equal(KeptBy.Reservation, Assert.IsType<WaitingForInput>(bottleneck.Hindrance).KeptBy);
        Assert.Equal(F1, bottleneck.Workday!.Tile);
    }

    // Acceptance: with stone before wool on a sea route, the ships take the stone first; with wool first, the wool.
    [Theory]
    [InlineData(Resource.Stone, Resource.Wool)]
    [InlineData(Resource.Wool, Resource.Stone)]
    public void TheShipsTakeTheGoodsInTheIslandsImportOrder(Resource first, Resource then)
    {
        var world = SeaRouteTests.OnlyShips((island, r) => island == Archipelago.First && r is Resource.Stone or Resource.Wool ? 30 : 0);
        Assert.True(Apply(world, new MoveImport(Archipelago.First, then, 0)).Accepted);
        Assert.True(Apply(world, new MoveImport(Archipelago.First, first, 0)).Accepted);
        Assert.Equal([first, then], world.ImportOrder(Archipelago.First).Take(2));
        Assert.Equal(Enum.GetValues<Resource>(), world.ImportOrder(Archipelago.Second));

        // Half a game day of the route: 20 units, all of the first good, and then the rest of the day of both.
        world.AdvanceTo(Shift(world.Now, 8 * 3600));
        Assert.Equal((10 * Unit, 30 * Unit), (world.Settlement.Stock(Archipelago.First, first), world.Settlement.Stock(Archipelago.First, then)));
        Assert.Equal(20 * Unit, world.Settlement.Stock(first));
        Assert.Equal(0, world.Settlement.Stock(then));

        var diagnosis = Assert.Single(world.Diagnose().SeaRoutes, r => r.Route.From == Archipelago.First);
        Assert.Equal([(first, 10L), (then, 30L)], diagnosis.Waiting);
    }

    // Acceptance: with 30 timber reserved on an island, the sea route leaves 30 timber there.
    [Fact]
    public void TheShipsLeaveWhatIsReservedOnTheIsland()
    {
        var world = SeaRouteTests.OnlyShips((island, r) => island == Archipelago.First && r == Resource.Timber ? 50 : 0);
        Assert.True(Apply(world, new SetReservation(Archipelago.First, Resource.Timber, 30)).Accepted);

        world.AdvanceTo(Shift(world.Now, Day));

        Assert.Equal(30 * Unit, world.Settlement.Stock(Archipelago.First, Resource.Timber));
        Assert.Equal(20 * Unit, world.Settlement.Stock(Resource.Timber));

        // The route has room for the timber it leaves, and the diagnosis says the reservation holds it.
        var route = Assert.Single(world.Diagnose().SeaRoutes, r => r.Route.From == Archipelago.First);
        var reserved = Assert.IsType<Reserved>(Assert.Single(route.Hindrances));
        Assert.Equal((Resource.Timber, 30), (reserved.Resource, reserved.Units));
        Assert.Equal(route.CapacityPerHour, reserved.LostPerHour);
        Assert.Empty(route.Waiting);
    }

    // Review of #167, with a settlement on each island (TASK-050): the ships leave the larger of the reservation and the two
    // days' food the island keeps (D-125), and a reservation at home keeps no food from going out to an island (D-127).
    [Theory]
    [InlineData(20, 10)] // The reservation keeps more than the food: 20 stay, 10 go home.
    [InlineData(5, 20)] // The food keeps more: 10 stay, and in eight hours the route takes 20 of the other 20.
    public void TheShipsLeaveTheLargerOfTheReservationAndTheFoodTheIslandKeeps(int reserved, int shipped)
    {
        var world = WithFishOnFirst(villagers: 5, fish: 30);
        Assert.True(Apply(world, new SetReservation(Archipelago.First, Resource.Fish, reserved)).Accepted);
        long home = world.Settlement.Stock(Resource.Fish);

        world.AdvanceTo(Shift(world.Now, 8 * 3600));

        Assert.Equal((30 - shipped) * Unit, world.Settlement.Stock(Archipelago.First, Resource.Fish));
        Assert.Equal(home + (shipped * Unit), world.Settlement.Stock(Resource.Fish));
        Assert.Equal(30 - shipped - Math.Max(reserved, 10), world.ForTheShips(Archipelago.First, Resource.Fish));
    }

    [Fact]
    public void AReservationAtHomeKeepsNoFoodFromGoingOutToAnIsland()
    {
        var world = WithFishOnFirst(villagers: 5, fish: 0);
        Assert.True(Apply(world, new SetReservation(world.HomeIsland, Resource.Fish, (int)Quantity.Whole(world.Settlement.Stock(Resource.Fish)))).Accepted);

        world.AdvanceTo(Shift(world.Now, 4 * 3600));

        Assert.Equal(10 * Unit, world.Settlement.Stock(Archipelago.First, Resource.Fish)); // Two days' food for five.
    }

    // Building pays from what is reserved: the reservation keeps goods from the ships and processing, not from the queue.
    [Fact]
    public void BuildingStillPaysFromWhatIsReserved()
    {
        var world = Morning();
        long timber = world.Settlement.Stock(Resource.Timber);
        Assert.True(Apply(world, new SetReservation(world.HomeIsland, Resource.Timber, (int)Quantity.Whole(timber))).Accepted);

        Assert.True(Apply(world, new Build(BuildingKind.Cottage, L2)).Accepted);

        Assert.True(world.Settlement.Stock(Resource.Timber) < timber);
    }

    [Fact]
    public void TheRulesAreFreePlanningThatTheRestLockRefuses()
    {
        var world = Sawmill(timber: 30);
        var forces = Enum.GetValues<Force>().Select(world.Available).ToList();

        Assert.True(Apply(world, new SetReserve(L1, 35)).Accepted);
        Assert.True(Apply(world, new SetReservation(world.HomeIsland, Resource.Stone, 10)).Accepted);
        Assert.Equal(forces, Enum.GetValues<Force>().Select(world.Available));

        world.AdvanceTo(At("2026-10-15 23:30", 2));
        string resting = world.Fingerprint();
        Assert.Equal(Rejection.Resting, Apply(world, new SetReserve(L1, 10)).Rejection);
        Assert.Equal(Rejection.Resting, Apply(world, new SetReservation(world.HomeIsland, Resource.Stone, 0)).Rejection);
        Assert.Equal(Rejection.Resting, Apply(world, new MoveImport(PrototypeHome.Nyon, Resource.Stone, 0)).Rejection);
        Assert.Equal(35, world.BuildingAt(L1)!.Reserve);
        Assert.Equal(10, world.ReservationOf(world.HomeIsland, Resource.Stone));
    }

    [Fact]
    public void ACommandOutsideTheRulesIsRefusedAndChangesNothing()
    {
        var world = Sawmill(timber: 30);
        int capacity = world.StorageUnits(world.HomeIsland);
        string before = world.Fingerprint();

        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new SetReserve(K1, 10)).Rejection); // A fishing hut has no raw material.
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new SetReserve(L2, 10)).Rejection);
        Assert.Equal(Rejection.AmountOutOfRange, Apply(world, new SetReserve(L1, -1)).Rejection);
        Assert.Equal(Rejection.AmountOutOfRange, Apply(world, new SetReserve(L1, capacity + 1)).Rejection);
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new SetReserve(L1, StockRules.StandardReserve)).Rejection);

        Assert.Equal(Rejection.NoSuchIsland, Apply(world, new SetReservation("nowhere", Resource.Timber, 10)).Rejection);
        Assert.Equal(Rejection.NoSuchIsland, Apply(world, new SetReservation(PrototypeHome.Nyon, Resource.Timber, 10)).Rejection); // No store yet.
        Assert.Equal(Rejection.AmountOutOfRange, Apply(world, new SetReservation(world.HomeIsland, Resource.Timber, capacity + 1)).Rejection);
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new SetReservation(world.HomeIsland, Resource.Timber, 0)).Rejection);

        Assert.Equal(Rejection.NoSuchIsland, Apply(world, new MoveImport(world.HomeIsland, Resource.Stone, 0)).Rejection); // Nothing ships home from home.
        Assert.Equal(Rejection.NoSuchIsland, Apply(world, new MoveImport(PrototypeHome.Nyon, Resource.Stone, 0)).Rejection);

        Assert.Equal(before, world.Fingerprint());
    }

    // Review of #167: a command is written to the journal before it runs, so one that a journal could not keep is never made,
    // and one that names an island without a store is refused and read back.
    [Fact]
    public void ACommandThatAJournalCouldNotKeepIsNeverMadeAndARefusedOneIsReadBack()
    {
        Assert.Throws<ArgumentException>(() => new SetReservation(PrototypeHome.Home, (Resource)99, 5));
        Assert.Throws<ArgumentException>(() => new SetReservation("two words", Resource.Timber, 5));
        Assert.Throws<ArgumentException>(() => new SetReservation(string.Empty, Resource.Timber, 5));
        Assert.Throws<ArgumentException>(() => new MoveImport(PrototypeHome.Nyon, (Resource)(-1), 0));
        Assert.Throws<ArgumentException>(() => new MoveImport(null!, Resource.Stone, 0));

        var journal = Journal.Start(At("2026-10-14 14:00", 2), Night);
        Assert.Equal(Rejection.NoSuchIsland, journal.Apply("nowhere", new SetReservation("nowhere", Resource.Timber, 5)).Rejection);
        Assert.Equal(Rejection.NoSuchIsland, journal.Apply("nyon", new MoveImport(PrototypeHome.Nyon, Resource.Stone, 0)).Rejection);
        Assert.True(journal.Apply("timber", new SetReservation(PrototypeHome.Home, Resource.Timber, 5)).Accepted);

        Assert.Equal(journal.World.Fingerprint(), Journal.Load(journal.ToText()).World.Fingerprint());
    }

    // Review of #167: a demolition (TASK-047) can leave a setting above what the store holds; it stays, and the player can
    // always lower it, but never raise it above the store.
    [Fact]
    public void ASettingAboveTheStoreAfterADemolitionCanBeLoweredButNotRaised()
    {
        var storehouse = new Hex(2, -2);
        var world = CarriersTests.Edited(Morning(), "building Storehouse 2 -2 1 1 0 0 0 1 4 0 0 0 0 0 0\nbuilding Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0 20", villagers: Morning().Villagers + 1);
        Assert.Equal(300, world.StorageUnits(world.HomeIsland));
        Assert.True(Apply(world, new SetReserve(L1, 200)).Accepted);
        Assert.True(Apply(world, new SetReservation(world.HomeIsland, Resource.Stone, 250)).Accepted);

        Assert.True(Apply(world, new Demolish(storehouse)).Accepted);
        Assert.Equal(150, world.StorageUnits(world.HomeIsland));
        Assert.Equal((200, 250), (world.BuildingAt(L1)!.Reserve, world.ReservationOf(world.HomeIsland, Resource.Stone)));

        Assert.True(Apply(world, new SetReserve(L1, 195)).Accepted);
        Assert.Equal(Rejection.AmountOutOfRange, Apply(world, new SetReserve(L1, 196)).Rejection);
        Assert.True(Apply(world, new SetReservation(world.HomeIsland, Resource.Stone, 245)).Accepted);
        Assert.Equal(Rejection.AmountOutOfRange, Apply(world, new SetReservation(world.HomeIsland, Resource.Stone, 246)).Rejection);
        Assert.True(Apply(world, new SetReserve(L1, 150)).Accepted);
        Assert.Equal(Rejection.AmountOutOfRange, Apply(world, new SetReserve(L1, 151)).Rejection);
    }

    [Fact]
    public void AnImportOrderNeedsAPlaceInTheOrderAndMovedBackToTheFixedOrderIsNoLongerWritten()
    {
        var world = SeaRouteTests.OnlyShips((_, _) => 0);
        string fixedOrder = world.Fingerprint();
        Assert.Equal(Rejection.NoSuchPlace, Apply(world, new MoveImport(Archipelago.First, Resource.Stone, -1)).Rejection);
        Assert.Equal(Rejection.NoSuchPlace, Apply(world, new MoveImport(Archipelago.First, Resource.Stone, Enum.GetValues<Resource>().Length)).Rejection);
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new MoveImport(Archipelago.First, Resource.Fish, 0)).Rejection);

        Assert.True(Apply(world, new MoveImport(Archipelago.First, Resource.Stone, 0)).Accepted);
        Assert.Contains($"imports {Archipelago.First} Stone Fish Grain Timber Planks Wool Wadmal Game Hide Honey Mead Ale", world.Fingerprint().Split('\n'));

        Assert.True(Apply(world, new MoveImport(Archipelago.First, Resource.Stone, (int)Resource.Stone)).Accepted);
        Assert.Equal(Enum.GetValues<Resource>(), world.ImportOrder(Archipelago.First));
        Assert.DoesNotContain(world.Fingerprint().Split('\n'), l => l.StartsWith("imports ", StringComparison.Ordinal));
        Assert.Equal(StripKeys(fixedOrder), StripKeys(world.Fingerprint()));
    }

    // The rules are state: a save keeps them as they are (the journal's replays are in ReplayCorpusTests).
    [Fact]
    public void TheRulesAreKeptBySaves()
    {
        var world = SeaRouteTests.OnlyShips((_, _) => 0);
        foreach (var command in new GameCommand[]
        {
            new SetReservation(Archipelago.Second, Resource.Wool, 15),
            new SetReservation(Archipelago.Home, Resource.Timber, 30),
            new SetReservation(Archipelago.Home, Resource.Fish, 5),
            new MoveImport(Archipelago.Second, Resource.Wool, 1),
        })
        {
            Assert.True(Apply(world, command).Accepted);
        }

        var lines = world.Fingerprint().Split('\n');
        Assert.Equal(
            [$"reserved {Archipelago.Home} Fish 5", $"reserved {Archipelago.Home} Timber 30", $"reserved {Archipelago.Second} Wool 15", $"imports {Archipelago.Second} Fish Wool Grain Timber Planks Stone Wadmal Game Hide Honey Mead Ale"],
            lines.Where(l => l.StartsWith("reserved ", StringComparison.Ordinal) || l.StartsWith("imports ", StringComparison.Ordinal)));
        var reader = new StateReader(world.Fingerprint());
        var read = World.Read(reader, world.Map);
        reader.End();
        Assert.Equal(world.Fingerprint(), read.Fingerprint());
        Assert.Equal(15, read.ReservationOf(Archipelago.Second, Resource.Wool));
        Assert.Equal(Resource.Wool, read.ImportOrder(Archipelago.Second)[1]);
    }

    [Theory]
    [InlineData("reserved home Timber 0")] // Nothing reserved is not written.
    [InlineData("reserved nowhere Timber 5")] // An island without a store.
    [InlineData("reserved home Timber 5\nreserved home Timber 6")] // Twice.
    [InlineData("reserved home Stone 5\nreserved home Timber 6")] // Out of order.
    [InlineData("imports home Stone Fish Grain Timber Planks Wool Wadmal Game Hide")] // Nothing ships home from home.
    [InlineData("imports first Fish Grain Timber Planks Stone Wool Wadmal Game Hide")] // The fixed order is not written.
    [InlineData("imports first Stone Stone Grain Timber Planks Wool Wadmal Game Hide")] // Not every resource.
    public void AStateWithRulesThatCannotBeIsRefused(string rules)
    {
        var world = SeaRouteTests.OnlyShips((_, _) => 0);
        var lines = world.Fingerprint().TrimEnd('\n').Split('\n').ToList();
        lines.Insert(lines.FindIndex(l => l.StartsWith("building ", StringComparison.Ordinal)), rules);

        Assert.Throws<FormatException>(() => World.Read(new StateReader(string.Join('\n', lines) + "\n"), world.Map));
    }

    [Fact]
    public void AProcessingBuildingStartsWithTheStandardMinimumStockAndOtherBuildingsWithNone()
    {
        var world = Morning();
        Assert.True(Apply(world, new Build(BuildingKind.Sawmill, L1)).Accepted);
        world.AdvanceTo(At("2026-10-16 12:00", 2));

        Assert.Equal(StockRules.StandardReserve, world.BuildingAt(L1)!.Reserve);
        Assert.All(world.Buildings.Where(b => b.Kind != BuildingKind.Sawmill), b => Assert.Equal(0, b.Reserve));
    }

    /// <summary>
    /// A sawmill beside the storehouse with <paramref name="timber"/> on the home island, and the woodcutter paused, so
    /// that only the carriers move timber.
    /// </summary>
    private static World Sawmill(int timber)
    {
        var world = CarriersTests.Edited(Morning(), "building Sawmill -1 0 1 1 0 0 0 0 0 0 0 0 0 0 20", stock: (Resource.Timber, timber));
        Assert.True(Apply(world, new PauseBuilding(F1)).Accepted);
        return world;
    }

    /// <summary>The timber the sawmill has taken from the store and sawn, of the <paramref name="timber"/> there was.</summary>
    private static long Processed(World world, int timber) =>
        (timber * Unit) - world.Settlement.Stock(Resource.Timber) - world.BuildingAt(L1)!.Held![(int)Resource.Timber];

    private static World Morning() => CarriersTests.Morning();

    /// <summary>The world where only the ships work, with <paramref name="villagers"/> on the first island and its fish.</summary>
    private static World WithFishOnFirst(int villagers, int fish)
    {
        var lines = SeaRouteTests.OnlyShips((_, _) => 0).Fingerprint().Split('\n').Select(line =>
            line == $"settlement {Archipelago.First} 1 0 0" ? $"settlement {Archipelago.First} {villagers} 0 0"
            : line.StartsWith($"stock {Archipelago.First} Fish ", StringComparison.Ordinal) ? $"stock {Archipelago.First} Fish {fish * Unit}"
            : line);
        var reader = new StateReader(string.Join('\n', lines));
        var world = World.Read(reader, Archipelago.Map);
        reader.End();
        return world;
    }

    /// <summary>The state without the commands' keys and outcomes, which every accepted command adds.</summary>
    private static string StripKeys(string state) =>
        string.Join('\n', state.Split('\n').Where(l => !l.StartsWith("command ", StringComparison.Ordinal)));

    private static TimeReading Shift(TimeReading reading, long seconds) => new(reading.UtcSeconds + seconds, reading.ZoneId, reading.OffsetSeconds);

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"stock-rules-{Interlocked.Increment(ref s_key)}", command);
}
