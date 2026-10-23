using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Buildings and recipes are content (TASK-042, ADR-0006): the settlement produces what the catalog says, so a new
/// building needs nothing but its entry in the catalog, and every change to the catalog shows in review.
/// </summary>
public sealed class ContentCatalogTests
{
    // Buildings that only these tests' catalogs know: not even the enum has a name for them.
    private const BuildingKind Kiln = (BuildingKind)100;
    private const BuildingKind Shed = (BuildingKind)101;

    private static readonly string s_golden = Repository.PathTo("tests", "Eirora.Simulation.Tests", "Economy", "Golden", "content-catalog.txt");
    private static int s_key;

    // The catalog's canonical text changes only with the version that says so: an added building bumps the content
    // version, and changing how existing content works bumps the rules version (ADR-0006). A missing golden is written,
    // to be reviewed and committed.
    [Fact]
    public void TheCatalogIsTheCheckedInContentOfItsVersion()
    {
        string text = ContentCatalog.Current.ToText();
        if (!File.Exists(s_golden))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(s_golden)!);
            File.WriteAllText(s_golden, text);
            Assert.Fail($"Wrote the golden catalog {s_golden}; review it and commit it.");
        }

        Assert.Equal(ContentVersion.Current, ContentCatalog.Current.Version);
        Assert.Equal(File.ReadAllText(s_golden).ReplaceLineEndings("\n"), text);
    }

    [Fact]
    public void TheCatalogListsEveryBuildingOnce()
    {
        Assert.Equal(Enum.GetValues<BuildingKind>(), ContentCatalog.Current.Buildings);
    }

    [Fact]
    public void ANewBuildingWithARecipeInTheCatalogProducesItWithoutAnyOtherChange()
    {
        var world = Built(Kiln, Spec(new Recipe(Resource.Stone, perHour: 1_000_000)));
        var shed = Built(Shed, Spec(recipe: null));

        // Beside the storehouse, a unit an hour reaches the store, and the shed beside it makes nothing.
        Assert.Equal(Quantity.Units(2), TwoHours(world, Resource.Stone) - TwoHours(shed, Resource.Stone));
        Assert.True(Apply(world, new Upgrade(L1)).Accepted);
        Assert.Equal(Rejection.NoBuildingThere, Apply(shed, new Upgrade(L1)).Rejection);
    }

    [Fact]
    public void ARecipeWithTwoRawMaterialsUsesEachForWhatItMakes()
    {
        var kiln = Spec(new Recipe(Resource.Planks, perHour: 1_000_000, inputs: [new(Resource.Timber, 1), new(Resource.Fish, 3)]));
        var world = Built(Kiln, kiln);
        var shed = Built(Shed, Spec(recipe: null));
        var before = (Timber: world.Settlement.Stock(Resource.Timber) - shed.Settlement.Stock(Resource.Timber), Fish: world.Settlement.Stock(Resource.Fish) - shed.Settlement.Stock(Resource.Fish));

        long made = TwoHours(world, Resource.Planks) - TwoHours(shed, Resource.Planks);

        Assert.Equal(Quantity.Units(2), made);
        Assert.Equal(before.Timber - made, world.Settlement.Stock(Resource.Timber) - shed.Settlement.Stock(Resource.Timber));
        Assert.Equal(before.Fish - (3 * made), world.Settlement.Stock(Resource.Fish) - shed.Settlement.Stock(Resource.Fish));
    }

    [Fact]
    public void ARecipeWithTwoRawMaterialsKeepsTheReserveOfEach()
    {
        // Stone starts at the reserve of 20 and nothing quarries it, so the kiln makes nothing although timber abounds.
        var kiln = Spec(new Recipe(Resource.Planks, perHour: 1_000_000, inputs: [new(Resource.Timber, 1), new(Resource.Stone, 1)]));
        var world = Built(Kiln, kiln);

        Assert.Equal(0, TwoHours(world, Resource.Planks));
        Assert.Equal(Quantity.Units(20), world.Settlement.Stock(Resource.Stone));
    }

    [Fact]
    public void ARecipeIsOneKindOnlyAndSplitsIntoQuarterHours()
    {
        Assert.Throws<ArgumentException>(() => new Recipe(Resource.Grain, perHour: 1_000_002));
        Assert.Throws<ArgumentException>(() => new Recipe(Resource.Grain, perHour: 1_000_000, growsPerHour: 500_000, forestTiles: 2));
        Assert.Throws<ArgumentException>(() => new Recipe(Resource.Planks, perHour: 1_000_000, inputs: [new(Resource.Timber, 2)], growsPerHour: 500_000));
    }

    /// <summary>A building that costs only timber, needs one villager and stands on any land.</summary>
    private static BuildingSpec Spec(Recipe? recipe) =>
        new(new Dictionary<Resource, int> { [Resource.Timber] = 4 }, workHours: 2, buildPower: 0, staff: 1, TileUse.Land, maxLevel: 2, recipe: recipe);

    /// <summary>A world whose catalog has one building more, built beside the storehouse and staffed by the morning.</summary>
    private static World Built(BuildingKind kind, BuildingSpec spec)
    {
        var world = World.Create(PrototypeHome.Map, At("2026-10-15 07:00", 2), Night, ContentCatalog.Current.With(kind, spec));
        Assert.True(Apply(world, new Build(kind, L1)).Accepted);
        world.AdvanceTo(At("2026-10-15 10:00", 2));
        Assert.True(world.BuildingAt(L1)!.Staffed);
        return world;
    }

    /// <summary>How much of a resource the store gains from 10:00 to noon, in millionths.</summary>
    private static long TwoHours(World world, Resource resource)
    {
        long before = world.Settlement.Stock(resource);
        world.AdvanceTo(At("2026-10-15 12:00", 2));
        return world.Settlement.Stock(resource) - before;
    }

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"cmd-{Interlocked.Increment(ref s_key)}", command);
}
