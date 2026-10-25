using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// The population tiers and step 1, Farmers (TASK-061, D-105, D-114, D-115; economy-v2.md, sections 2–6): villagers of
/// each tier, the need for clothes, rising at dawn, the world's unlocked step, staffing by tier, the farmers' move to a
/// settlement without villagers (D-124) and the hunting lodge and the tannery.
/// </summary>
/// <remarks>
/// Most worlds are written as state text on the prototype's map half an hour before a dawn, so that each settlement has
/// exactly the villagers, buildings and goods the test needs; a bare village has only its storehouse, so that nothing
/// produces and the dawn's goods are all that change.
/// </remarks>
public sealed class PopulationTierTests
{
    private static readonly Hex s_farmstead = L3;
    private static int s_key;

    // Acceptance: given a settlement of 20 villagers, when dawn comes, step 1 unlocks for the whole world.
    [Fact]
    public void TwentyVillagersInASettlementUnlockStepOneAtDawn()
    {
        var world = Village(crofters: 20, buildings: [Line(BuildingKind.Longhouse, E1)], bare: true);
        Assert.Equal(0, world.Step);
        Assert.Equal(Rejection.NotUnlocked, world.CheckBuild(BuildingKind.Farmstead, s_farmstead));

        var unlocked = Assert.Single(Dawn(world).OfType<StepUnlocked>());

        Assert.Equal((1, 1), (unlocked.Step, world.Step));
        Assert.Equal(Rejection.None, world.CheckBuild(BuildingKind.Farmstead, s_farmstead));
    }

    // The step needs only the size, no forces and no food (RST-10), and a smaller settlement leaves the world at step 0.
    [Theory]
    [InlineData(19, 0)]
    [InlineData(20, 1)]
    public void TheStepNeedsOnlyTheSettlementsSize(int villagers, int step)
    {
        var world = Village(crofters: villagers, buildings: [Line(BuildingKind.Longhouse, E1)], bare: true, stock: [(Resource.Fish, 0)]);

        Dawn(world);

        Assert.Equal(villagers, world.Villagers);
        Assert.Equal(step, world.Step);
    }

    // Acceptance: given farmers without clothes, when dawn comes, no crofter rises to farmer and no farmer becomes a crofter.
    [Fact]
    public void WithoutClothesNoOneRisesAndNoFarmerGoesBack()
    {
        var world = Village(crofters: 10, farmers: 4, step: 1, buildings: [Line(BuildingKind.Farmstead, s_farmstead)], bare: true);

        var events = Dawn(world);

        var home = world.SettlementOn(PrototypeHome.Home)!;
        Assert.Empty(events.OfType<VillagersRose>());
        Assert.Equal(4, home.VillagersOf(PopulationTier.Farmers));
        Assert.True(home.VillagersOf(PopulationTier.Crofters) >= 10);
    }

    // Acceptance: given clothes and a farmstead with room, when dawn comes, at most two crofters rise to farmers.
    [Fact]
    public void WithClothesAndAFarmsteadAtMostTwoCrofterRiseADawn()
    {
        var world = Village(crofters: 12, step: 1, buildings: [Line(BuildingKind.Farmstead, s_farmstead)], bare: true, stock: [(Resource.Wadmal, 10)]);

        var rose = Assert.Single(Dawn(world).OfType<VillagersRose>());

        var home = world.SettlementOn(PrototypeHome.Home)!;
        Assert.Equal((2, PopulationTier.Farmers, PrototypeHome.Home), (rose.Count, rose.Tier, rose.Island));
        Assert.Equal(2, home.VillagersOf(PopulationTier.Farmers));

        // The next dawn the two farmers use up one wadmal, half each rounded up, and two more rise.
        long wadmal = world.Stock(Resource.Wadmal);
        Dawn(world, 2);
        Assert.Equal(4, home.VillagersOf(PopulationTier.Farmers));
        Assert.True(world.Stock(Resource.Wadmal) <= wadmal - 1, $"The farmers used {wadmal - world.Stock(Resource.Wadmal)} wadmal.");
    }

    // Rising takes the goods for those who rise and room in the farmstead: one wadmal clothes two, and the farmstead
    // houses six. Hide is clothes as wadmal is.
    [Theory]
    [InlineData(Resource.Wadmal, 1, 0, 2)]
    [InlineData(Resource.Hide, 1, 0, 2)]
    [InlineData(Resource.Hide, 1, 5, 1)]
    [InlineData(Resource.Wadmal, 0, 0, 0)]
    public void RisingTakesClothesForTheNewOnesAndRoom(Resource clothes, int units, int farmers, int rising)
    {
        // With farmers there already, their own clothes come first; with none there is nothing for them to use.
        var world = Village(crofters: 10, farmers: farmers, step: 1, buildings: [Line(BuildingKind.Farmstead, s_farmstead)], bare: true, stock: [(clothes, units + ((farmers + 1) / 2))]);

        Dawn(world);

        Assert.Equal(farmers + rising, world.SettlementOn(PrototypeHome.Home)!.VillagersOf(PopulationTier.Farmers));
    }

    // Before step 1 no one rises, however much wadmal and room there is (D-114).
    [Fact]
    public void NoOneRisesToATierTheWorldHasNotUnlocked()
    {
        var world = Village(crofters: 12, buildings: [Line(BuildingKind.Farmstead, s_farmstead)], bare: true, stock: [(Resource.Wadmal, 10)]);

        Assert.Empty(Dawn(world).OfType<VillagersRose>());
        Assert.Equal(0, world.SettlementOn(PrototypeHome.Home)!.VillagersOf(PopulationTier.Farmers));
    }

    // The villagers eat from the good there is most of, so the goods even out (economy-v2.md, section 2); v1 ate fish first.
    [Theory]
    [InlineData(30, 10, 0, 18, 10, 0)]
    [InlineData(20, 20, 0, 14, 14, 0)]
    [InlineData(10, 10, 10, 6, 6, 6)]
    public void TheVillagersEatFromTheGoodThereIsMostOf(int fish, int grain, int game, int fishLeft, int grainLeft, int gameLeft)
    {
        var world = Village(crofters: 12, bare: true, stock: [(Resource.Fish, fish), (Resource.Grain, grain), (Resource.Game, game)]);

        Dawn(world);

        Assert.Equal((fishLeft, grainLeft, gameLeft), (world.Stock(Resource.Fish), world.Stock(Resource.Grain), world.Stock(Resource.Game)));
    }

    // Two clothes give one crofter more only when a second fits, and only what is over once the farmers have theirs for
    // the next dawn: with two farmers who rise to four, the two wadmal left are theirs.
    [Theory]
    [InlineData(15, 0, 10, 16, 10)] // Room for one: the wadmal stays.
    [InlineData(12, 0, 10, 14, 8)] // Room for two: two wadmal give the second.
    [InlineData(10, 2, 3, 13, 2)] // The farmers use one, two rise, and the two left are the four farmers' next dawn.
    public void ComfortIsUsedOnlyWhenTheSecondCrofterFits(int crofters, int farmers, int wadmal, int villagersAfter, int wadmalAfter)
    {
        string[] farmstead = farmers > 0 ? [Line(BuildingKind.Farmstead, s_farmstead)] : [];
        var world = Village(crofters: crofters, farmers: farmers, step: farmers > 0 ? 1 : 0, buildings: farmstead, bare: true, stock: [(Resource.Wadmal, wadmal)]);

        Dawn(world);

        Assert.Equal((villagersAfter, wadmalAfter), (world.Villagers, (int)world.Stock(Resource.Wadmal)));
    }

    // INV-V2: a building is staffed by its tier or a higher one, the lowest first, so the farmers are the free ones and no
    // tier staffs more than its villagers and those below.
    [Fact]
    public void BuildingsTakeTheLowestTierFirstAndFarmersStaffCroftersBuildings()
    {
        var world = Village(crofters: 3, farmers: 9, step: 1, buildings: [Line(BuildingKind.Farmstead, s_farmstead)]);

        var home = world.SettlementOn(PrototypeHome.Home)!;
        Assert.All(world.Buildings.Where(b => world.Content.For(b.Kind).Staff > 0), b => Assert.True(b.Staffed, $"{b.Kind} is not staffed."));
        Assert.Equal(12 - world.Buildings.Sum(b => b.Staffed ? world.Content.For(b.Kind).Staff : 0), home.FreeVillagers);
        var free = new int[2];
        world.Settlement.FreeVillagersOf(home, free);
        Assert.Equal((0, home.FreeVillagers), (free[0], free[1]));
    }

    // D-124: a free farmer moves to a settlement without villagers, keeps its tier and lives in a crofter's home there,
    // which the island's crofters then do not grow into; to a settlement with villagers only crofters move.
    [Fact]
    public void AFreeFarmerMovesToASettlementWithoutVillagersAndKeepsItsTier()
    {
        // Home's eight crofters staff its buildings and jetty, so its free villagers are four farmers.
        var world = Village(
            crofters: 8,
            farmers: 4,
            step: 1,
            buildings: [Line(BuildingKind.Farmstead, s_farmstead), Line(BuildingKind.Jetty, H1, staffed: true), Line(BuildingKind.Jetty, N0)],
            nyon: 0,
            stock: [(Resource.Wadmal, 10)]);

        var moved = Assert.Single(Dawn(world).OfType<VillagerMoved>());

        var nyon = world.SettlementOn(PrototypeHome.Nyon)!;
        Assert.Equal((PrototypeHome.Home, PrototypeHome.Nyon), (moved.From, moved.To));
        Assert.Equal((0, 1), (nyon.VillagersOf(PopulationTier.Crofters), nyon.VillagersOf(PopulationTier.Farmers)));
        Assert.True(world.BuildingAt(N0)!.Staffed, "The farmer who moved does not staff Nyön's jetty.");
        Assert.Equal(IslandSettlement.SettlerHousing - 1, nyon.CroftersRoom);

        // Nyön now has a villager, so only a crofter may move there, though home's free villagers are farmers.
        Dawn(world, 2);
        Assert.Equal(1, nyon.VillagersOf(PopulationTier.Farmers));
    }

    // A hunting lodge hunts the forest it reaches without felling it, and shares each tile evenly with the other lodges that
    // reach it (economy-v2.md, section 4), as lodges on L2 and E2 share the start woodcutter's second tile.
    [Fact]
    public void HuntingLodgesShareTheForestTheyBothReach()
    {
        var alone = Village(crofters: 12, step: 1, buildings: [Line(BuildingKind.HuntingLodge, L2, staffed: true)]);
        var together = Village(crofters: 14, step: 1, buildings: [Line(BuildingKind.HuntingLodge, L2, staffed: true), Line(BuildingKind.HuntingLodge, E2, staffed: true)]);
        var recipe = ContentCatalog.Current.For(BuildingKind.HuntingLodge).Recipe!;

        long Hunts(World world, Hex lodge) => world.Settlement.HuntPerTick(world.BuildingAt(lodge)!, recipe);

        Assert.True(Hunts(alone, L2) > 0);
        Assert.True(Hunts(together, L2) < Hunts(alone, L2), $"{Hunts(together, L2)} together, {Hunts(alone, L2)} alone.");
        long forest = alone.TimberLeft(F1);
        alone.AdvanceTo(At("2026-10-16 12:00", 2));
        Assert.True(alone.Stock(Resource.Game) > 0, "The lodge brought no game home.");
        Assert.True(alone.TimberLeft(F1) <= forest, "Hunting grew the forest.");
    }

    // The tannery makes hide of game, as the weavery makes wadmal of wool, and leaves its minimum stock of game (LOG-06):
    // 5, not processing's 20, since game is food too and a lodge seldom brings more (product owner, 2026-10-25).
    [Fact]
    public void TheTanneryMakesHideOfGameAndLeavesFive()
    {
        Assert.Equal(5, ContentCatalog.Current.For(BuildingKind.Tannery).Recipe!.Reserve);
        var world = Village(crofters: 12, step: 1, buildings: [Line(BuildingKind.Tannery, L1, staffed: true)], stock: [(Resource.Game, 12)]);
        Dawn(world);

        world.AdvanceTo(At("2026-10-16 23:00", 2));

        Assert.Equal(5, world.BuildingAt(L1)!.Reserve);
        Assert.True(world.Stock(Resource.Hide) > 0, "The tannery made no hide.");
        Assert.Equal(5, world.Stock(Resource.Game));
    }

    // Farmers carry: a depot hires free villagers of every tier, since a villager can do the work of its tier and those
    // below (D-109; product owner, 2026-10-25). Here every crofter staffs a building, and the farmers are the free ones.
    [Fact]
    public void ADepotHiresFreeFarmersAsCarriers()
    {
        var probe = Village(crofters: 12);
        var far = Enumerable.Range(0, probe.Map.Disc.Count).Select(i => probe.Map.Disc[i])
            .Where(h => (probe.UsesOf(h) & TileUse.FishingWater) != 0 && probe.BuildingAt(h) is null && probe.TripFrom(h).Steps == 4)
            .Take(3).ToArray();
        var world = Village(
            crofters: 13,
            farmers: 4,
            step: 1,
            buildings: [Line(BuildingKind.Farmstead, s_farmstead), .. far.Select(h => Line(BuildingKind.FishingHut, h, staffed: true))]);
        Assert.Equal(4, world.FreeVillagers);

        Dawn(world);

        var storehouse = world.BuildingAt(S)!;
        // The dawn's one new crofter carries first, and then the farmers.
        Assert.True(storehouse.Carriers > 2, $"The storehouse has {storehouse.Carriers} carriers.");
        Assert.Equal(5 - (storehouse.Carriers - 1), world.SettlementOn(PrototypeHome.Home)!.FreeVillagers);
    }

    // The diagnosis says which need a tier lacks and why no one rises (TASK-043): first the step, then food, the tier's
    // needs, its housing and the tier below, as dawn checks them.
    [Fact]
    public void TheDiagnosisSaysWhatATierLacksAndWhyNoOneRises()
    {
        TierDiagnosis Farmers(World world) => world.Diagnose().SettlementOn(PrototypeHome.Home)!.TierOf(PopulationTier.Farmers)!;
        string[] farmstead = [Line(BuildingKind.Farmstead, s_farmstead)];

        var locked = Farmers(Village(crofters: 12, buildings: farmstead, stock: [(Resource.Wadmal, 10)]));
        var lacking = Farmers(Village(crofters: 10, farmers: 4, step: 1, buildings: farmstead, stock: [(Resource.Wadmal, 1)]));
        var forNew = Farmers(Village(crofters: 10, farmers: 4, step: 1, buildings: farmstead, stock: [(Resource.Wadmal, 2)]));
        var full = Farmers(Village(crofters: 10, farmers: 6, step: 1, buildings: farmstead, stock: [(Resource.Wadmal, 10)]));
        var homeless = Farmers(Village(crofters: 12, step: 1, stock: [(Resource.Wadmal, 10)]));
        var hungry = Farmers(Village(crofters: 12, step: 1, buildings: farmstead, stock: [(Resource.Wadmal, 10), (Resource.Fish, 0), (Resource.Grain, 0)]));
        var rises = Farmers(Village(crofters: 12, step: 1, buildings: farmstead, stock: [(Resource.Wadmal, 10)]));

        Assert.Equal((Rising.Locked, (Need?)null), (locked.Rising, locked.Lacks));
        Assert.Equal((Rising.LacksNeed, Need.Clothes, Need.Clothes), (lacking.Rising, lacking.Lacks, lacking.Missing));
        Assert.Equal((Rising.LacksNeed, (Need?)null, Need.Clothes), (forNew.Rising, forNew.Lacks, forNew.Missing));
        Assert.Equal((Rising.NoHousing, 6, 6), (full.Rising, full.Villagers, full.Housing));
        Assert.Equal((Rising.NoHousing, 0), (homeless.Rising, homeless.Housing));
        Assert.Equal(Rising.Hungry, hungry.Rising);
        Assert.Equal((Rising.Rises, (Need?)null), (rises.Rising, rises.Missing));
    }

    // A building of a step the world has not reached is refused, so the build menu can say why (economy-v2.md, section 5).
    [Fact]
    public void ABuildingOfALaterStepIsRefused()
    {
        var world = Village(crofters: 12);
        Dawn(world);

        var result = world.Apply($"tiers-{Interlocked.Increment(ref s_key)}", new Build(BuildingKind.HuntingLodge, L2));

        Assert.Equal(Rejection.NotUnlocked, result.Rejection);
    }

    /// <summary>
    /// The prototype's world half an hour before its second dawn, with Nyön known and its settlement of <paramref name="nyon"/>
    /// villagers (none when null), the home island's crofters and farmers, the step, the buildings after the founding's
    /// (only the storehouse when <paramref name="bare"/>) and the home island's stock in whole units; read back as a save
    /// would be.
    /// </summary>
    private static World Village(
        int crofters,
        int farmers = 0,
        int step = 0,
        string[]? buildings = null,
        (Resource Resource, int Units)[]? stock = null,
        bool bare = false,
        int? nyon = null)
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-16 06:30", 2));
        world.Settlement.Fog.LiftIsland(PrototypeHome.Nyon);
        var lines = world.Fingerprint().TrimEnd('\n').Split('\n').ToList();
        int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
        lines[economy] = $"{lines[economy][..lines[economy].LastIndexOf(' ')]} {step}";
        lines[lines.FindIndex(l => l.StartsWith("settlement home ", StringComparison.Ordinal))] = $"settlement home {crofters} {farmers}";
        if (nyon is int moved)
        {
            lines.Insert(lines.FindLastIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal)) + 1, $"settlement {PrototypeHome.Nyon} {moved} 0");
            lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("stock ", StringComparison.Ordinal)) + 1, Enum.GetValues<Resource>().Select(r => $"stock {PrototypeHome.Nyon} {r} 0"));
        }

        foreach (var (resource, units) in stock ?? [])
        {
            lines[lines.FindIndex(l => l.StartsWith($"stock home {resource} ", StringComparison.Ordinal))] = $"stock home {resource} {Quantity.Units(units)}";
        }

        if (bare)
        {
            lines.RemoveAll(l => l.StartsWith("building ", StringComparison.Ordinal) && !l.StartsWith("building Storehouse ", StringComparison.Ordinal));
            lines.RemoveAll(l => l.StartsWith("forest ", StringComparison.Ordinal));
        }

        lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, buildings ?? []);
        var reader = new StateReader(string.Join('\n', lines) + "\n");
        var read = World.Read(reader, world.Map);
        reader.End();
        return read;
    }

    /// <summary>Lets the world reach the dawn of the <paramref name="day"/>th day after the village was written and returns its events.</summary>
    private static List<GameEvent> Dawn(World world, int day = 1)
    {
        var events = world.AdvanceTo(At($"2026-10-{15 + day} 07:00", 2)).ToList();
        Assert.Contains(events, e => e is DawnBroke);
        return events;
    }

    /// <summary>A building as the state writes it, a storehouse or a jetty with its one carrier when staffed, no other with a depot yet.</summary>
    private static string Line(BuildingKind kind, Hex tile, bool staffed = false) => Logistics.IsDepot(kind)
        ? $"building {kind} {tile.Q} {tile.R} 1 {(staffed ? 1 : 0)} 0 0 0 {(staffed ? 1 : 0)} 4 0 0 0 0 0 0"
        : $"building {kind} {tile.Q} {tile.R} 1 {(staffed ? 1 : 0)} 0 0 0 0 0 0 0 0 0 0 {ContentCatalog.Current.For(kind).Recipe?.Reserve ?? 0}";
}
