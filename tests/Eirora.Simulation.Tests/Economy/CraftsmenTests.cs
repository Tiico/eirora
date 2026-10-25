using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using static Eirora.Simulation.Tests.Economy.TierVillage;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Step 2, Craftsmen (TASK-062, D-105, D-116; economy-v2.md, sections 2–5): the step's condition, the need for drink,
/// farmers rising to craftsmen, the craftsmen's double pace on the building queue, the apiary on a meadow, and the brewery
/// and the meadery that only farmers and craftsmen staff.
/// </summary>
/// <remarks>The worlds are villages as <see cref="TierVillage"/> writes them.</remarks>
public sealed class CraftsmenTests
{
    private static readonly Hex s_farmstead = L3;
    private static readonly Hex s_house = E2;
    private static int s_key;

    // Acceptance: given a settlement with 30 villagers, of them 6 farmers, when dawn comes, step 2 unlocks.
    [Fact]
    public void ThirtyVillagersOfThemSixFarmersUnlockStepTwoAtDawn()
    {
        var world = Village(crofters: 24, farmers: 6, step: 1, buildings: [Line(BuildingKind.Longhouse, E1), Line(BuildingKind.Farmstead, s_farmstead)], bare: true);
        Assert.Equal(Rejection.NotUnlocked, world.CheckBuild(BuildingKind.CraftsmensHouse, s_house));

        var unlocked = Assert.Single(Dawn(world).OfType<StepUnlocked>());

        Assert.Equal((2, 2), (unlocked.Step, world.Step));
        Assert.Equal(Rejection.None, world.CheckBuild(BuildingKind.CraftsmensHouse, s_house));
    }

    // The step needs both the size and the farmers, and nothing else: no forces and no food (RST-10). Hungry, the village
    // does not grow into the condition at the dawn.
    [Theory]
    [InlineData(24, 6, 2)]
    [InlineData(25, 5, 1)]
    [InlineData(23, 6, 1)]
    [InlineData(30, 0, 1)]
    public void TheStepNeedsTheSizeAndTheFarmers(int crofters, int farmers, int step)
    {
        var world = Village(
            crofters: crofters,
            farmers: farmers,
            step: 1,
            buildings: [Line(BuildingKind.Longhouse, E1), Line(BuildingKind.Longhouse, P1), Line(BuildingKind.Farmstead, s_farmstead)],
            bare: true,
            stock: [(Resource.Fish, 0), (Resource.Grain, 0), (Resource.Game, 0)]);

        Dawn(world);

        Assert.Equal((crofters + farmers, step), (world.Villagers, world.Step));
    }

    // Acceptance: given a free craftsman, when an active hour is simulated, the building queue gets two hours of work from
    // it (D-116), and a free farmer gives one.
    [Theory]
    [InlineData(PopulationTier.Craftsmen, 2000)]
    [InlineData(PopulationTier.Farmers, 1000)]
    public void AFreeCraftsmanGivesTheQueueTwoHoursAnActiveHour(PopulationTier tier, long work)
    {
        // The crofter carries for the storehouse, so the one of the tier is the only free villager; hungry, none grows.
        var world = Village(
            crofters: 1,
            farmers: tier == PopulationTier.Farmers ? 1 : 0,
            craftsmen: tier == PopulationTier.Craftsmen ? 1 : 0,
            step: 2,
            buildings: [Line(BuildingKind.Farmstead, s_farmstead), Line(BuildingKind.CraftsmensHouse, s_house)],
            bare: true,
            stock: [(Resource.Fish, 0), (Resource.Grain, 0), (Resource.Game, 0)]);
        Dawn(world);
        Assert.Equal(1, world.FreeVillagers);
        Assert.True(world.Apply($"craftsmen-{Interlocked.Increment(ref s_key)}", new Build(BuildingKind.Cottage, L1)).Accepted);
        long before = world.Projects[0].WorkLeft;

        world.AdvanceTo(At("2026-10-16 09:00", 2));
        long hourBefore = world.Projects[0].WorkLeft;
        world.AdvanceTo(At("2026-10-16 10:00", 2));

        Assert.True(before > hourBefore, "Nothing was built before the hour.");
        Assert.Equal(work, hourBefore - world.Projects[0].WorkLeft);
    }

    // Acceptance: given a meadow beside the storehouse, when the player builds an apiary, the honey grows day and night and
    // is gathered in the active hours (INV-T5).
    [Fact]
    public void AnApiaryOnAMeadowGrowsHoneyDayAndNightAndGathersItByDay()
    {
        Assert.Equal(TileUse.Meadow, ContentCatalog.Current.For(BuildingKind.Apiary).Requirement);
        var world = Village(crofters: 12, step: 2);
        Dawn(world);
        Assert.NotEqual(TileUse.None, world.UsesOf(L1) & TileUse.Meadow);
        Assert.Equal(Rejection.SiteUnavailable, world.CheckBuild(BuildingKind.Apiary, H1));
        Assert.True(world.Apply($"craftsmen-{Interlocked.Increment(ref s_key)}", new Build(BuildingKind.Apiary, L1)).Accepted);
        world.AdvanceTo(At("2026-10-16 22:45", 2));
        var apiary = world.BuildingAt(L1)!;
        Assert.True(apiary.Staffed, "The apiary is not staffed.");

        // At night the honey grows, and nothing is gathered.
        long honey = world.Stock(Resource.Honey);
        long ripe = apiary.Ripe;
        world.AdvanceTo(At("2026-10-17 06:30", 2));
        Assert.True(apiary.Ripe > ripe, $"The honey did not grow at night: {apiary.Ripe} after {ripe}.");
        Assert.Equal(honey, world.Stock(Resource.Honey));

        // By day it is gathered and carried to the storehouse beside it.
        world.AdvanceTo(At("2026-10-17 12:00", 2));
        Assert.True(world.Stock(Resource.Honey) > honey, "No honey reached the store by day.");
    }

    // Only a meadow takes an apiary: grassland, not forest, farmland, pasture, beach or mountain (economy-v2.md, section 4).
    [Fact]
    public void OnlyGrasslandIsAMeadow()
    {
        var map = World.Create(At("2026-10-14 14:00", 2), Night).Map;
        for (int i = 0; i < map.Disc.Count; i++)
        {
            var hex = map.Disc[i];
            bool meadow = (map.Features.UsesOf(hex) & TileUse.Meadow) != 0;
            Assert.Equal(map.TerrainAt(hex) == Terrain.Grassland && map.Features.UsesOf(hex) != TileUse.Crest, meadow);
        }
    }

    // Acceptance of the need: given craftsmen without drink, when dawn comes, no farmer rises and no craftsman goes back.
    [Fact]
    public void WithoutDrinkNoFarmerRisesAndNoCraftsmanGoesBack()
    {
        var world = Village(
            crofters: 10,
            farmers: 4,
            craftsmen: 2,
            step: 2,
            buildings: [Line(BuildingKind.Farmstead, s_farmstead), Line(BuildingKind.CraftsmensHouse, s_house)],
            bare: true,
            stock: [(Resource.Wadmal, 10)]);

        var events = Dawn(world);

        var home = world.SettlementOn(PrototypeHome.Home)!;
        Assert.DoesNotContain(events.OfType<VillagersRose>(), e => e.Tier == PopulationTier.Craftsmen);
        Assert.Equal(2, home.VillagersOf(PopulationTier.Craftsmen));
    }

    // With clothes, drink of either kind and a craftsmen's house with room, up to two farmers rise to craftsmen a dawn, and
    // the craftsmen then use up half a drink each, rounded up for the settlement.
    [Theory]
    [InlineData(Resource.Mead)]
    [InlineData(Resource.Ale)]
    public void WithDrinkAndClothesTwoFarmersRiseToCraftsmen(Resource drink)
    {
        var world = Village(
            crofters: 10,
            farmers: 6,
            step: 2,
            buildings: [Line(BuildingKind.Farmstead, s_farmstead), Line(BuildingKind.CraftsmensHouse, s_house)],
            bare: true,
            stock: [(Resource.Wadmal, 10), (drink, 4)]);

        var rose = Dawn(world).OfType<VillagersRose>().Single(e => e.Tier == PopulationTier.Craftsmen);

        var home = world.SettlementOn(PrototypeHome.Home)!;
        Assert.Equal(2, rose.Count);
        Assert.Equal(2, home.VillagersOf(PopulationTier.Craftsmen));
        Assert.Equal(4, world.Stock(drink));

        Dawn(world, 2);
        Assert.Equal(4, home.VillagersOf(PopulationTier.Craftsmen));
        Assert.Equal(3, world.Stock(drink));
    }

    // Before step 2 no farmer rises to craftsman, however much drink there is (D-114).
    [Fact]
    public void NoFarmerRisesToCraftsmanBeforeStepTwo()
    {
        var world = Village(
            crofters: 10,
            farmers: 6,
            step: 1,
            buildings: [Line(BuildingKind.Farmstead, s_farmstead), Line(BuildingKind.CraftsmensHouse, s_house)],
            bare: true,
            stock: [(Resource.Wadmal, 10), (Resource.Mead, 10)]);

        Dawn(world);

        Assert.Equal(0, world.SettlementOn(PrototypeHome.Home)!.VillagersOf(PopulationTier.Craftsmen));
    }

    // The brewery and the meadery need farmers or craftsmen (economy-v2.md, section 4): with only crofters they stand
    // unstaffed, and the craftsmen stay free to build while farmers staff them, the lowest tier first (INV-V2).
    [Theory]
    [InlineData(BuildingKind.Brewery)]
    [InlineData(BuildingKind.Meadery)]
    public void FarmersStaffTheBreweryAndTheMeadery(BuildingKind kind)
    {
        Assert.Equal(PopulationTier.Farmers, ContentCatalog.Current.For(kind).Tier);
        var crofters = Village(crofters: 12, step: 2, buildings: [Line(kind, L1)]);
        var tiers = Village(
            crofters: 8,
            farmers: 2,
            craftsmen: 2,
            step: 2,
            buildings: [Line(BuildingKind.Farmstead, s_farmstead), Line(BuildingKind.CraftsmensHouse, s_house), Line(kind, L1)]);
        Dawn(crofters);
        Dawn(tiers);

        Assert.False(crofters.BuildingAt(L1)!.Staffed, $"Crofters staff the {kind}.");
        Assert.True(tiers.BuildingAt(L1)!.Staffed, $"The farmers do not staff the {kind}.");
        var free = new int[3];
        tiers.Settlement.FreeVillagersOf(tiers.SettlementOn(PrototypeHome.Home)!, free);
        Assert.Equal((0, 2), (free[1], free[2]));
    }

    // The brewery makes ale of two grain and the meadery mead of one honey, each one every two hours (economy-v2.md,
    // section 4), and each leaves its minimum stock (LOG-06): the brewery 5 grain, as the tannery leaves 5 game, since grain
    // is food too (product owner, 2026-10-25), and the meadery processing's 20 honey.
    [Theory]
    [InlineData(BuildingKind.Brewery, Resource.Grain, Resource.Ale, 5)]
    [InlineData(BuildingKind.Meadery, Resource.Honey, Resource.Mead, StockRules.StandardReserve)]
    public void TheBreweryAndTheMeaderyMakeDrink(BuildingKind kind, Resource raw, Resource drink, int reserve)
    {
        Assert.Equal(reserve, ContentCatalog.Current.For(kind).Recipe!.Reserve);
        var world = Village(
            crofters: 10,
            farmers: 2,
            step: 2,
            buildings: [Line(BuildingKind.Farmstead, s_farmstead), Line(kind, L1, staffed: true)],
            bare: true,
            stock: [(raw, 30), (Resource.Wadmal, 10)]);
        Dawn(world);

        world.AdvanceTo(At("2026-10-16 23:00", 2));

        Assert.True(world.Stock(drink) > 0, $"The {kind} made no {drink}.");
        Assert.True(world.Stock(raw) >= reserve, $"The {kind} took the {raw} below its minimum stock: {world.Stock(raw)} left.");
    }

    // The diagnosis says that the craftsmen lack drink (TASK-062), and that clothes go to the farmers first, as at dawn.
    [Fact]
    public void TheDiagnosisSaysTheCraftsmenLackDrinkAndTheFarmersTakeClothesFirst()
    {
        TierDiagnosis Craftsmen(World world) => world.Diagnose().SettlementOn(PrototypeHome.Home)!.TierOf(PopulationTier.Craftsmen)!;
        string[] homes = [Line(BuildingKind.Farmstead, s_farmstead), Line(BuildingKind.CraftsmensHouse, s_house)];

        var thirsty = Craftsmen(Village(crofters: 10, farmers: 4, craftsmen: 2, step: 2, buildings: homes, stock: [(Resource.Wadmal, 10)]));
        var naked = Craftsmen(Village(crofters: 10, farmers: 4, craftsmen: 2, step: 2, buildings: homes, stock: [(Resource.Wadmal, 2), (Resource.Mead, 10)]));
        var forNew = Craftsmen(Village(crofters: 10, farmers: 4, step: 2, buildings: homes, stock: [(Resource.Wadmal, 10)]));
        var rises = Craftsmen(Village(crofters: 10, farmers: 4, step: 2, buildings: homes, stock: [(Resource.Wadmal, 10), (Resource.Ale, 4)]));

        Assert.Equal((Rising.LacksNeed, Need.Drink, Need.Drink), (thirsty.Rising, thirsty.Lacks, thirsty.Missing));
        Assert.Equal((Rising.LacksNeed, Need.Clothes), (naked.Rising, naked.Lacks));
        Assert.Equal((Rising.LacksNeed, (Need?)null, Need.Drink), (forNew.Rising, forNew.Lacks, forNew.Missing));
        Assert.Equal(Rising.Rises, rises.Rising);
    }
}
