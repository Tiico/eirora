using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;

namespace Eirora.Balance.Tests;

/// <summary>
/// The yields the bots measure and place buildings by (<see cref="Sites.DailyYield"/>, <see cref="Measures.Delivered"/>)
/// against what the simulation actually produces: for each recipe in the content catalog, at a level and a distance from
/// the store, one game day's gain equals what the bots say the producers of that resource deliver. The bots read the rates
/// from the catalog, so this holds their reading of it, and the level steps and the carriers' share they cannot read, to
/// the simulation. The gain counts what waits for the carriers at the buildings and at the processing (INV-R2).
/// </summary>
/// <remarks>
/// A day runs from just before one dawn to just before the next, so it holds every waking quarter hour once; what the
/// villagers eat and use for comfort at the dawn inside it is added back. Stock is read in millionths, so that a rate
/// a fraction of a percent off fails. Grain and wool are measured once the
/// building has stood a whole day and night, when what is harvested in a day is what grew in it.
/// </remarks>
public sealed class ProductionRateTests
{
    private const long Day = 24 * 3600;
    private const long TicksADay = 24 * 4;

    private static readonly Hex s_village = new(0, 0);
    private static readonly Hex s_startFishingHut = new(1, 0);
    private static readonly Hex s_plainMountain = new(-3, -1); // Four steps from the store.
    private static readonly Hex s_pasture = new(-2, -1); // Two steps.
    private static readonly Hex s_nextToTheStore = new(-1, 0);

    private static readonly BuildingKind[] s_measured =
    [
        BuildingKind.FishingHut, BuildingKind.Field, BuildingKind.Woodcutter, BuildingKind.Quarry, BuildingKind.SheepFarm,
        BuildingKind.Sawmill, BuildingKind.Weavery,
    ];

    [Fact]
    public void EveryRecipeInTheCatalogIsMeasuredHere()
    {
        var catalog = ContentCatalog.Current;

        Assert.Equal(
            catalog.Buildings.Where(kind => catalog.For(kind).Recipe is not null).Order(),
            s_measured.Order());
    }

    [Fact]
    public void FishAtLevelTwoAndFromFartherAway()
    {
        var village = new Village();
        village.Dawn(1);
        village.Apply(new Upgrade(s_startFishingHut));
        village.Apply(new Build(BuildingKind.FishingHut, village.FishingWaterTwoOrThreeStepsAway()));

        village.AssertDayGain(2, Resource.Fish, buildings: 2);
    }

    [Fact]
    public void GrainFromTheStartField() => new Village().AssertDayGain(2, Resource.Grain, buildings: 1);

    [Fact]
    public void TimberFromTheStartWoodcutter() => new Village().AssertDayGain(1, Resource.Timber, buildings: 1);

    [Fact]
    public void StoneFromAFarMountain()
    {
        var village = new Village();
        village.Dawn(1);
        village.Apply(new Build(BuildingKind.Quarry, s_plainMountain));

        village.AssertDayGain(2, Resource.Stone, buildings: 1);
    }

    [Fact]
    public void WoolFromPastureTwoStepsAway()
    {
        var village = new Village();
        village.Dawn(1);
        village.Apply(new Build(BuildingKind.SheepFarm, s_pasture));

        village.AssertDayGain(3, Resource.Wool, buildings: 1);
    }

    [Fact]
    public void PlanksOnceTimberHasPiledUp()
    {
        var village = new Village();
        village.Dawn(6);
        village.Apply(new Build(BuildingKind.Sawmill, s_nextToTheStore));

        village.AssertDayGain(7, Resource.Planks, buildings: 1);
    }

    [Fact]
    public void WadmalOnceWoolHasPiledUp()
    {
        var village = new Village();
        village.Dawn(1);
        village.Apply(new Build(BuildingKind.SheepFarm, s_pasture));
        village.Dawn(10);
        village.Apply(new Build(BuildingKind.Weavery, s_nextToTheStore));

        village.AssertDayGain(11, Resource.Wadmal, buildings: 1);
    }

    /// <summary>A world on the prototype's map without self-care, played day by day as <see cref="BalanceRun"/> plays it.</summary>
    private sealed class Village
    {
        private readonly World _world = World.Create(PrototypeHome.Map, new TimeReading(BalanceRun.InstallUtc, BalanceRun.Zone, 0), BalanceRun.Rest);
        private int _key;

        private static long FirstDawn => BalanceRun.InstallUtc + (15 * 60);

        public void Dawn(int day) => _world.AdvanceTo(At(DawnOf(day)));

        public void Apply(GameCommand command)
        {
            var result = _world.Apply($"test-{++_key}", command);
            Assert.True(result.Accepted, $"{command.GetType().Name} was rejected: {result.Rejection}.");
        }

        /// <summary>A free fishing water tile two or three steps from the store.</summary>
        public Hex FishingWaterTwoOrThreeStepsAway()
        {
            for (int i = 0; i < _world.Map.Disc.Count; i++)
            {
                var tile = _world.Map.Disc[i];
                if ((_world.UsesOf(tile) & TileUse.FishingWater) != 0 && _world.BuildingAt(tile) is null && _world.TripFrom(tile).Steps is 2 or 3)
                {
                    return tile;
                }
            }

            throw new InvalidOperationException("No fishing water two or three steps from the store.");
        }

        public void AssertDayGain(int day, Resource resource, int buildings)
        {
            _world.AdvanceTo(At(DawnOf(day) - 1));
            var sites = new Sites(_world);
            var producers = _world.Buildings.Where(b => b.Staffed && _world.Content.For(b.Kind).Recipe?.Product == resource).ToList();
            Assert.Equal(buildings, producers.Count);
            long expected = producers.Sum(b => Measures.Delivered(_world, sites, b.Kind, b.Tile)) / Sites.Weights[(int)resource];

            long before = Stock(resource);
            long used = UsedAtDawn(resource);
            _world.AdvanceTo(At(DawnOf(day + 1) - 1));
            long gained = Stock(resource) - before + used;

            // The simulation rounds down each quarter hour and the bots once a day, so they may part by a millionth a tick.
            Assert.True(
                Math.Abs(gained - expected) <= TicksADay,
                $"{resource}: the world gained {gained} millionths in a day, the bots count {expected}.");
        }

        /// <summary>What the villagers will eat, and use for comfort, of a resource at the coming dawn (economy-v1.md, section 4).</summary>
        private long UsedAtDawn(Resource resource)
        {
            long need = Quantity.Units(_world.Villagers);
            long fish = Math.Min(need, Stock(Resource.Fish));
            long grain = Math.Min(need - fish, Stock(Resource.Grain));
            bool fed = fish + grain == need;
            return resource switch
            {
                Resource.Fish => fish,
                Resource.Grain => grain,
                Resource.Wadmal => fed && Stock(Resource.Wadmal) >= Quantity.Units(2) ? Quantity.Units(2) : 0,
                _ => 0,
            };
        }

        /// <summary>Stock at home in millionths, finer than the whole units the world shows.</summary>
        private long Stock(Resource resource) =>
            _world.Settlement.Stock(resource)
            + _world.Buildings.Where(b => _world.Content.For(b.Kind).Recipe?.Product == resource).Sum(b => b.Waiting)
            + _world.Buildings.Sum(b => b.Held?[(int)resource] ?? 0);

        private static long DawnOf(int day) => FirstDawn + ((day - 1) * Day);

        private static TimeReading At(long utc) => new(utc, BalanceRun.Zone, 0);
    }
}
