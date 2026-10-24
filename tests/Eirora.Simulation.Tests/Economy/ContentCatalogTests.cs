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

        // Beside the storehouse, it makes a unit an hour, and the shed beside it makes nothing.
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
        var before = (Timber: Everywhere(world, Resource.Timber) - Everywhere(shed, Resource.Timber), Fish: Everywhere(world, Resource.Fish) - Everywhere(shed, Resource.Fish));

        long made = TwoHours(world, Resource.Planks) - TwoHours(shed, Resource.Planks);

        // The carriers have brought the raw materials since the morning, so it makes a plank an hour.
        Assert.Equal(Quantity.Units(2), made);
        Assert.Equal(before.Timber - made, Everywhere(world, Resource.Timber) - Everywhere(shed, Resource.Timber));
        Assert.Equal(before.Fish - (3 * made), Everywhere(world, Resource.Fish) - Everywhere(shed, Resource.Fish));
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

    // Processing checks each raw material's reserve on its own and before adding the product, so either would take the
    // store below the reserve (LOG-06).
    [Fact]
    public void ARecipeRefusesARawMaterialTwiceOrOneThatIsItsProduct()
    {
        Assert.Throws<ArgumentException>(() => new Recipe(Resource.Planks, perHour: 1_000_000, inputs: [new(Resource.Timber, 1), new(Resource.Timber, 1)]));
        Assert.Throws<ArgumentException>(() => new Recipe(Resource.Timber, perHour: 1_000_000, inputs: [new(Resource.Timber, 2)]));
    }

    public static TheoryData<string, Func<BuildingSpec>> UnworkableSpecs => new()
    {
        { "a negative cost, which a queued build would add to the store", () => new(Timber(-4), workHours: 2, buildPower: 0, staff: 1, TileUse.Land) },
        { "a resource listed at no units", () => new(Timber(0), workHours: 2, buildPower: 0, staff: 1, TileUse.Land) },
        { "no work", () => new(Timber(4), workHours: 0, buildPower: 0, staff: 1, TileUse.Land) },
        { "negative Build Power", () => new(Timber(4), workHours: 2, buildPower: -1, staff: 1, TileUse.Land) },
        { "negative staff", () => new(Timber(4), workHours: 2, buildPower: 0, staff: -1, TileUse.Land) },
        { "negative housing", () => new(Timber(4), workHours: 2, buildPower: 0, staff: 0, TileUse.Land, housing: -4) },
        { "no level", () => new(Timber(4), workHours: 2, buildPower: 0, staff: 1, TileUse.Land, maxLevel: 0) },
        { "a recipe nobody runs, so it never produces", () => new(Timber(4), workHours: 2, buildPower: 0, staff: 0, TileUse.Land, recipe: new(Resource.Stone, perHour: 1_000_000)) },
    };

    [Theory]
    [MemberData(nameof(UnworkableSpecs))]
    public void ABuildingSpecRefusesWhatCouldNeverWork(string what, Func<BuildingSpec> spec)
    {
        Assert.NotEmpty(what);
        Assert.Throws<ArgumentException>(spec);
    }

    [Fact]
    public void ABuildingIsInTheCatalogOnlyOnce()
    {
        Assert.Throws<ArgumentException>(() => ContentCatalog.Current.With(BuildingKind.Field, Spec(recipe: null)));
    }

    private static Dictionary<Resource, int> Timber(int amount) => new() { [Resource.Timber] = amount };

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

    /// <summary>How much of a resource the world gains from 10:00 to noon, in millionths, wherever it lies.</summary>
    private static long TwoHours(World world, Resource resource)
    {
        long before = Everywhere(world, resource);
        world.AdvanceTo(At("2026-10-15 12:00", 2));
        return Everywhere(world, resource) - before;
    }

    /// <summary>A resource in the store, waiting in the outboxes and held at the processing, in millionths (INV-R2).</summary>
    private static long Everywhere(World world, Resource resource) =>
        world.Settlement.Stock(resource)
        + world.Buildings.Where(b => world.Content.For(b.Kind).Recipe?.Product == resource).Sum(b => b.Waiting)
        + world.Buildings.Sum(b => b.Held?[(int)resource] ?? 0);

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"cmd-{Interlocked.Increment(ref s_key)}", command);
}
