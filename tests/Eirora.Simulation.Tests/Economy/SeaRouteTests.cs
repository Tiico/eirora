using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using FsCheck.Xunit;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Each island built on has its own store, and each island with a jetty its own sea route home (TASK-045, LOG-04,
/// economy-v1.md, section 5): the ships carry 40 units a game day on each route, and they only move goods between the
/// stores (INV-R1, INV-R2).
/// </summary>
public sealed class SeaRouteTests
{
    private const long Day = 24 * 3600;
    private const int StoragePerStorehouse = 150; // economy-v1.md, section 2.
    private const int StorageAtJetty = 150;

    // The goods the villagers neither make nor use in a world where only the ships work; they eat fish and grain and
    // wear wadmal at dawn.
    private static readonly Resource[] s_kept = [Resource.Timber, Resource.Planks, Resource.Stone, Resource.Wool];

    [Fact]
    public void AGameDayOnThreeIslandsShipsFortyUnitsOnEachRouteAndKeepsEveryGood()
    {
        var world = OnlyShips((island, r) => island == Archipelago.Home ? 0 : 60);
        Assert.Equal([Archipelago.Home, Archipelago.First, Archipelago.Second], world.Islands);
        Assert.Equal(new[] { (Archipelago.First, Archipelago.Home), (Archipelago.Second, Archipelago.Home) }, world.SeaRoutes.Select(r => (r.From, r.To)));
        var before = Stores.Of(world);

        world.AdvanceTo(Shift(world.Now, Day));

        // The routes ship in the goods' fixed order: 40 timber from each island, and the planks wait (TASK-046).
        var after = Stores.Of(world);
        Assert.Equal(Quantity.Units(SeaRoute.UnitsPerGameDay), before.Total(Archipelago.First) - after.Total(Archipelago.First));
        Assert.Equal(Quantity.Units(SeaRoute.UnitsPerGameDay), before.Total(Archipelago.Second) - after.Total(Archipelago.Second));
        Assert.Equal(Quantity.Units(2 * SeaRoute.UnitsPerGameDay), world.Settlement.Stock(Resource.Timber));
        Assert.Equal(Quantity.Units(60), world.Settlement.Stock(Archipelago.First, Resource.Planks));
        after.AssertSameGoodsAs(before);
    }

    // INV-R1 and INV-R2 with any goods on any of the three islands, any room and any time of day.
    [Property(MaxTest = 40)]
    public void AGameDayShipsAtMostFortyUnitsOnEachRouteAndNeitherLosesNorMakesGoods(int seed)
    {
        var random = new Random(seed);
        int[] storehouses = [random.Next(3), random.Next(3), random.Next(3)];
        var world = OnlyShips((island, r) => random.Next(Capacity(island, storehouses) + 1), storehouses, random.Next(24 * 4) * 900);
        var before = Stores.Of(world);

        world.AdvanceTo(Shift(world.Now, Day));

        var after = Stores.Of(world);
        foreach (var route in world.SeaRoutes)
        {
            Assert.InRange(before.Total(route.From) - after.Total(route.From), 0, Quantity.Units(SeaRoute.UnitsPerGameDay));
        }

        after.AssertSameGoodsAs(before);
        Assert.All(world.Islands, island => Assert.All(s_kept, r => Assert.InRange(world.Settlement.Stock(island, r), 0, Quantity.Units(Capacity(island, storehouses)))));
    }

    [Fact]
    public void TheRoutesFillTheHomeStoreInMapOrder()
    {
        // Room at home for 30 more planks, and 60 waiting on each of the other islands.
        var world = OnlyShips((island, r) => r == Resource.Planks ? island == Archipelago.Home ? 120 : 60 : 0);

        world.AdvanceTo(Shift(world.Now, Day));

        long first = Quantity.Units(60) - world.Settlement.Stock(Archipelago.First, Resource.Planks);
        long second = Quantity.Units(60) - world.Settlement.Stock(Archipelago.Second, Resource.Planks);
        Assert.Equal(Quantity.Units(StoragePerStorehouse), world.Settlement.Stock(Resource.Planks));
        Assert.Equal(Quantity.Units(30), first + second);
        Assert.InRange(first - second, 0, Quantity.Units(SeaRoute.UnitsPerGameDay) / 64);
    }

    [Fact]
    public void WithoutAJettyAtHomeNoShipSailsAndTheGoodsWait()
    {
        var world = OnlyShips((island, r) => island == Archipelago.Home ? 0 : 60, homeJetty: false);
        Assert.Empty(world.SeaRoutes);

        world.AdvanceTo(Shift(world.Now, Day));

        Assert.Equal(Quantity.Units(60), world.Settlement.Stock(Archipelago.First, Resource.Timber));
        Assert.Equal(Quantity.Units(60), world.Settlement.Stock(Archipelago.Second, Resource.Timber));
    }

    [Fact]
    public void AnIslandGetsItsStoreWhenItsFirstBuildingIsFinished()
    {
        var world = Archipelago.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 08:00", 2));
        Assert.Equal([Archipelago.Home], world.Islands);
        var landing = Archipelago.Map.Features.LandOf(Archipelago.Second).First(Archipelago.Map.Features.IsHarborSpot);
        Assert.True(world.Apply("steps", new RecordSelfCare(world.GameDay, Habit.Workouts, ContributionSource.Verified, 3)).Accepted);
        Assert.True(world.Apply("jetty", new Build(BuildingKind.Jetty, landing)).Accepted);
        Assert.Equal([Archipelago.Home], world.Islands);

        world.AdvanceTo(Shift(world.Now, 3 * Day));

        Assert.Equal(Archipelago.Second, world.BuildingAt(landing)!.Island);
        Assert.Equal([Archipelago.Home, Archipelago.Second], world.Islands);
        Assert.All(Enum.GetValues<Resource>(), r => Assert.Equal(0, world.Stock(Archipelago.Second, r)));
        Assert.Empty(world.SeaRoutes); // The home island has no jetty yet.
    }

    [Fact]
    public void BuildersReachAnotherIslandOnlyByItsOwnJetty()
    {
        var world = OnlyShips((island, r) => 0, firstJetty: false);
        world.Settlement.Fog.LiftIsland(Archipelago.First);
        Hex Free(string island) => Archipelago.Map.Features.LandOf(island).First(h => (world.UsesOf(h) & TileUse.Land) != 0 && world.BuildingAt(h) is null);

        // The jetty on the second island is no way onto the first.
        Assert.Equal(Rejection.SiteUnavailable, world.CheckBuild(BuildingKind.Lookout, Free(Archipelago.First)));
        Assert.Equal(Rejection.None, world.CheckBuild(BuildingKind.Lookout, Free(Archipelago.Second)));
    }

    [Fact]
    public void AStateWhoseStoresAreNotTheMapsIslandsInOrderIsRefused()
    {
        string state = OnlyShips((island, r) => 1).Fingerprint();

        Assert.Throws<FormatException>(() => Read(state.Replace("stock second ", "stock nowhere ", StringComparison.Ordinal)));
        Assert.Throws<FormatException>(() => Read(Swapped(state, Archipelago.First, Archipelago.Second)));
        Assert.Throws<FormatException>(() => Read(Without(state, "stock home ")));
        Assert.Throws<FormatException>(() => Read(Without(state, "stock second ")));
        Assert.Throws<FormatException>(() => Read(Without(state, "stock second Wadmal ")));
    }

    /// <summary>
    /// A world on the archipelago where nothing works but the ships: at <paramref name="secondOfDay"/> past midnight
    /// UTC on a day when the villagers are awake, with the founding's storehouse at home, a jetty on each island and
    /// as many storehouses on each as <paramref name="storehouses"/> says, and the goods <paramref name="stock"/> gives
    /// in whole units; the founding's other buildings are taken away. Written into its state.
    /// </summary>
    internal static World OnlyShips(
        Func<string, Resource, int> stock,
        int[]? storehouses = null,
        int secondOfDay = 7 * 3600,
        bool homeJetty = true,
        bool firstJetty = true)
    {
        var map = Archipelago.Map;
        var world = Archipelago.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(new TimeReading(((At("2026-10-15 00:00", 0).UtcSeconds / Day) * Day) + Day + secondOfDay, Stockholm, 7200));
        var islands = new[] { Archipelago.Home, Archipelago.First, Archipelago.Second };
        var free = map.Islands.Where(i => islands.Contains(i.Id)).ToDictionary(
            i => i.Id,
            i => new Queue<Hex>(map.Features.LandOf(i.Id).Where(h => map.TerrainAt(h) != Terrain.Mountain && world.BuildingAt(h) is null)));

        var buildings = new List<string>();
        foreach (var island in islands)
        {
            if (island == Archipelago.Home ? homeJetty : island != Archipelago.First || firstJetty)
            {
                var landing = map.Features.LandOf(island).First(h => map.Features.IsHarborSpot(h) && world.BuildingAt(h) is null);
                buildings.Add(Line(BuildingKind.Jetty, landing));
                free[island] = new Queue<Hex>(free[island].Where(h => h != landing));
            }

            for (int i = island == Archipelago.Home ? 1 : 0; i < (storehouses?[Array.IndexOf(islands, island)] ?? (island == Archipelago.Home ? 1 : 0)); i++)
            {
                buildings.Add(Line(BuildingKind.Storehouse, free[island].Dequeue()));
            }
        }

        var lines = world.Fingerprint().Split('\n').ToList();
        int at = lines.FindIndex(l => l.StartsWith("stock ", StringComparison.Ordinal));
        lines.RemoveAll(l => l.StartsWith("stock ", StringComparison.Ordinal) || (l.StartsWith("building ", StringComparison.Ordinal) && !l.StartsWith("building Storehouse ", StringComparison.Ordinal)));
        lines.InsertRange(at, map.Islands.Where(i => islands.Contains(i.Id)).SelectMany(i => Enum.GetValues<Resource>().Select(r =>
            $"stock {i.Id} {r} {Quantity.Units(s_kept.Contains(r) ? stock(i.Id, r) : i.Id == Archipelago.Home ? 100 : 0)}")));
        lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, buildings);
        return Read(string.Join('\n', lines));

        static string Line(BuildingKind kind, Hex tile) => $"building {kind} {tile.Q} {tile.R} 1 0 0 0 0 0 4 0 0 0 0 0 0";
    }

    private static int Capacity(string island, int[] storehouses)
    {
        int index = Array.IndexOf([Archipelago.Home, Archipelago.First, Archipelago.Second], island);
        return (Math.Max(storehouses[index], island == Archipelago.Home ? 1 : 0) * StoragePerStorehouse) + (island == Archipelago.Home ? 0 : StorageAtJetty);
    }

    private static World Read(string state)
    {
        var reader = new StateReader(state);
        var world = World.Read(reader, Archipelago.Map);
        reader.End();
        return world;
    }

    private static string Without(string state, string prefix) =>
        string.Join('\n', state.Split('\n').Where(l => !l.StartsWith(prefix, StringComparison.Ordinal)));

    /// <summary>The state with the stores of two islands in each other's places.</summary>
    private static string Swapped(string state, string a, string b)
    {
        var lines = state.Split('\n').ToList();
        var ofA = lines.Where(l => l.StartsWith($"stock {a} ", StringComparison.Ordinal)).ToList();
        var ofB = lines.Where(l => l.StartsWith($"stock {b} ", StringComparison.Ordinal)).ToList();
        int at = lines.IndexOf(ofA[0]);
        lines.RemoveAll(l => ofA.Contains(l) || ofB.Contains(l));
        lines.InsertRange(at, ofB.Concat(ofA));
        return string.Join('\n', lines);
    }

    private static TimeReading Shift(TimeReading reading, long seconds) => new(reading.UtcSeconds + seconds, reading.ZoneId, reading.OffsetSeconds);

    /// <summary>The goods the ships move, on each island with a store.</summary>
    private sealed class Stores
    {
        private readonly Dictionary<string, long[]> _stock;

        private Stores(Dictionary<string, long[]> stock) => _stock = stock;

        public static Stores Of(World world) =>
            new(world.Islands.ToDictionary(i => i, i => s_kept.Select(r => world.Settlement.Stock(i, r)).ToArray()));

        public long Total(string island) => _stock[island].Sum();

        /// <summary>The same amount of each good on all the islands together (INV-R2).</summary>
        public void AssertSameGoodsAs(Stores earlier)
        {
            for (int r = 0; r < s_kept.Length; r++)
            {
                Assert.Equal(earlier._stock.Values.Sum(s => s[r]), _stock.Values.Sum(s => s[r]));
            }
        }
    }
}
