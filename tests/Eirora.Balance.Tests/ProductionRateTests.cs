using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;

namespace Eirora.Balance.Tests;

/// <summary>
/// The rates the bots measure and place buildings by (<see cref="Sites.DailyYield"/>, <see cref="Measures.Delivered"/>)
/// against what the simulation actually produces: for each producing building, at a level and a distance from the store,
/// one game day's gain in store equals what the bots say the producers of that resource deliver.
/// </summary>
/// <remarks>
/// A day runs from just before one dawn to just before the next, so it holds every waking quarter hour once; what the
/// villagers eat and use for comfort at the dawn inside it is added back. Grain and wool are measured once the
/// building has stood a whole day and night, when what is harvested in a day is what grew in it.
/// </remarks>
public sealed class ProductionRateTests
{
    private const long Day = 24 * 3600;

    private static readonly Hex s_village = new(0, 0);
    private static readonly Hex s_startFishingHut = new(1, 0);
    private static readonly Hex s_plainMountain = new(-3, -1); // Four steps from the store: half reaches it.
    private static readonly Hex s_pasture = new(-2, -1); // Two steps: three quarters.
    private static readonly Hex s_nextToTheStore = new(-1, 0);

    [Fact]
    public void FishAtLevelTwoAndFromFartherAway()
    {
        var village = new Village();
        village.Dawn(1);
        village.Apply(new Upgrade(s_startFishingHut));
        village.Apply(new Build(BuildingKind.FishingHut, village.FishingWaterThreeQuartersAway()));

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

        /// <summary>A free fishing water tile two or three steps from the store, from where three quarters reach it.</summary>
        public Hex FishingWaterThreeQuartersAway()
        {
            for (int i = 0; i < _world.Map.Disc.Count; i++)
            {
                var tile = _world.Map.Disc[i];
                if ((_world.UsesOf(tile) & TileUse.FishingWater) != 0 && _world.BuildingAt(tile) is null && _world.DepotDistance(tile) is 2 or 3)
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
            var producers = _world.Buildings.Where(b => b.Staffed && Makes(b.Kind) == resource).ToList();
            Assert.Equal(buildings, producers.Count);
            long expected = producers.Sum(b => Measures.Delivered(_world, sites, b.Kind, b.Tile)) / Sites.Weights[(int)resource];

            long before = _world.Stock(resource);
            long used = UsedAtDawn(resource);
            _world.AdvanceTo(At(DawnOf(day + 1) - 1));
            long gained = _world.Stock(resource) - before + used;

            // Stock is read in whole units, so a day's gain is within one unit of what is delivered.
            Assert.True(
                Math.Abs(Quantity.Units(gained) - expected) <= Quantity.PerUnit,
                $"{resource}: the world gained {gained} in a day, the bots count {expected / (double)Quantity.PerUnit:0.###}.");
        }

        /// <summary>What the villagers will eat, and use for comfort, of a resource at the coming dawn (economy-v1.md, section 4).</summary>
        private long UsedAtDawn(Resource resource)
        {
            long fish = Math.Min(_world.Villagers, _world.Stock(Resource.Fish));
            long grain = Math.Min(_world.Villagers - fish, _world.Stock(Resource.Grain));
            bool fed = fish + grain == _world.Villagers;
            return resource switch
            {
                Resource.Fish => fish,
                Resource.Grain => grain,
                Resource.Wadmal => fed && _world.Stock(Resource.Wadmal) >= 2 ? 2 : 0,
                _ => 0,
            };
        }

        private static long DawnOf(int day) => FirstDawn + ((day - 1) * Day);

        private static TimeReading At(long utc) => new(utc, BalanceRun.Zone, 0);

        private static Resource? Makes(BuildingKind kind) => kind switch
        {
            BuildingKind.FishingHut => Resource.Fish,
            BuildingKind.Field => Resource.Grain,
            BuildingKind.Woodcutter => Resource.Timber,
            BuildingKind.SheepFarm => Resource.Wool,
            BuildingKind.Quarry => Resource.Stone,
            BuildingKind.Sawmill => Resource.Planks,
            BuildingKind.Weavery => Resource.Wadmal,
            _ => null,
        };
    }
}
