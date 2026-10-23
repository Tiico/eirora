using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>Economy v1 on the home island (economy-v1.md; INV-V1, INV-R1, INV-R3, INV-T4, INV-T5, INV-D4, INV-D7).</summary>
public sealed class EconomyTests
{
    private const long Hour = 3600;
    private static readonly long[] s_startingStock = [30, 0, 40, 20, 20, 0, 0];
    private static readonly string[] s_startingBuildings = ["Storehouse@(0, 0)", "FishingHut@(1, 0)", "Field@(0, 1)", "Woodcutter@(1, -1)"];
    private static readonly Hex[] s_firstSurvey = [M2, F3, new(-3, 1)];
    private static int s_key;

    [Fact]
    public void TheWorldStartsAsTheDesignSays()
    {
        var world = Afternoon();

        Assert.Equal((12, 16, 5), (world.Villagers, world.Housing, world.FreeVillagers));
        Assert.Equal(
            s_startingStock,
            Enum.GetValues<Resource>().Select(world.Stock).ToArray());
        Assert.Equal(
            s_startingBuildings,
            world.Buildings.Select(b => $"{b.Kind}@{b.Tile}"));
    }

    [Fact]
    public void AWeekWithoutSelfCareOrPlayDecaysNothingAndTheVillageGrowsToItsHousing()
    {
        var world = Afternoon();

        AdvanceHourlyCheckingNothingDecays(world, days: 7);

        Assert.Equal(16, world.Villagers);
    }

    [Fact]
    public void AYearAwayBreaksNothing()
    {
        var world = Afternoon();
        var start = world.Now;

        world.AdvanceTo(new TimeReading(start.UtcSeconds + (365 * 24 * Hour), Stockholm, 3600));

        Assert.Equal(16, world.Villagers);
        Assert.All(Enum.GetValues<Resource>(), r => Assert.InRange(world.Stock(r), 0, 150));
        Assert.Equal(4, world.Buildings.Count);
    }

    [Fact]
    public void VillagersRestAtNightWhileTheGrainKeepsRipening()
    {
        var world = Afternoon();
        world.AdvanceTo(At("2026-10-14 23:00", 2));
        var atDusk = Stocks(world);

        world.AdvanceTo(At("2026-10-15 06:59", 2));
        Assert.Equal(atDusk, Stocks(world));

        world.AdvanceTo(At("2026-10-15 07:00", 2));
        long grainAtDawn = world.Stock(Resource.Grain);
        world.AdvanceTo(At("2026-10-15 09:00", 2));

        // A field yields 0.5 grain an hour; the night's ripe grain makes the first morning hours' harvest far larger.
        Assert.True(world.Stock(Resource.Grain) - grainAtDawn >= 3, $"harvested {world.Stock(Resource.Grain) - grainAtDawn}");
    }

    [Fact]
    public void ATileUnderTheFogCanBeBuiltOnOnlyAfterSurveying()
    {
        var world = Morning();

        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new Build(BuildingKind.Woodcutter, F3)).Rejection);

        var survey = Apply(world, new Survey());
        Assert.Equal(s_firstSurvey, Assert.IsType<TilesRevealed>(Assert.Single(survey.Events)).Tiles);
        Assert.Equal(40, world.Available(Force.Exploration));
        Assert.True(Apply(world, new Build(BuildingKind.Woodcutter, F3)).Accepted);

        var events = world.AdvanceTo(At("2026-10-15 12:00", 2));
        Assert.Contains(events, e => e is ProjectCompleted { Building: BuildingKind.Woodcutter } done && done.Tile == F3);
        Assert.True(world.BuildingAt(F3)!.Staffed);
    }

    [Fact]
    public void BuildingPaysMaterialsAndBuildPowerTogetherOrNotAtAll()
    {
        var world = Morning();
        var before = world.Fingerprint();
        Apply(world, new Upgrade(K1)); // Spends 50 of 100 Build Power.
        var afterUpgrade = Stocks(world);

        var rejected = Apply(world, new Build(BuildingKind.Weavery, L2)); // Needs 60 Build Power.

        Assert.Equal(Rejection.InsufficientForces, rejected.Rejection);
        Assert.Equal(afterUpgrade, Stocks(world));
        Assert.Equal(50, world.Available(Force.BuildPower));
        Assert.NotEqual(before, world.Fingerprint());
    }

    [Fact]
    public void OnlyProducingBuildingsHaveASecondLevel()
    {
        BuildingKind[] producing = [BuildingKind.FishingHut, BuildingKind.Field, BuildingKind.Woodcutter, BuildingKind.SheepFarm, BuildingKind.Quarry, BuildingKind.Sawmill, BuildingKind.Weavery];

        Assert.All(Enum.GetValues<BuildingKind>(), kind => Assert.Equal(producing.Contains(kind) ? 2 : 1, ContentCatalog.Current.For(kind).MaxLevel));
    }

    [Fact]
    public void AnUpgradeStopsAtTheHighestLevel()
    {
        var world = Morning();
        Assert.True(Apply(world, new Upgrade(K1)).Accepted);

        world.AdvanceTo(At("2026-10-16 20:00", 2));

        var hut = world.BuildingAt(K1)!;
        Assert.Equal(ContentCatalog.Current.For(hut.Kind).MaxLevel, hut.Level);
        Assert.Equal(Rejection.AlreadyDone, Apply(world, new Upgrade(K1)).Rejection);
    }

    [Fact]
    public void ACottageNeedsNoBuildPower()
    {
        var world = Morning();
        world.Apply("spend", new SpendAll(Force.BuildPower));

        Assert.True(Apply(world, new Build(BuildingKind.Cottage, L1)).Accepted);
    }

    [Fact]
    public void AWorkdayGivesFiftyPercentMoreForTheDay()
    {
        var plain = Morning();
        var boosted = Morning();
        Assert.True(Apply(boosted, new Workday(K1)).Accepted);
        long plainBefore = plain.Stock(Resource.Fish), boostedBefore = boosted.Stock(Resource.Fish);

        plain.AdvanceTo(At("2026-10-15 15:00", 2));
        boosted.AdvanceTo(At("2026-10-15 15:00", 2));

        Assert.Equal(10, plain.Stock(Resource.Fish) - plainBefore); // 8 hours × 1.25 fish.
        Assert.Equal(15, boosted.Stock(Resource.Fish) - boostedBefore);
        Assert.Equal(Rejection.AlreadyDone, Apply(boosted, new Workday(K1)).Rejection);
    }

    [Fact]
    public void RushingPutsEightHoursOfWorkIntoTheFirstProject()
    {
        var world = Morning();
        Apply(world, new Build(BuildingKind.Cottage, L1));

        Assert.True(Apply(world, new Rush()).Accepted);

        Assert.Equal((32 - 8) * 1000, world.Projects.Single().WorkLeft);
        Assert.Equal(Rejection.NothingToRush, Apply(Morning(), new Rush()).Rejection);
    }

    [Fact]
    public void TheSawmillLeavesTimberForBuilding()
    {
        var world = Morning();
        Apply(world, new Build(BuildingKind.Sawmill, L1));

        world.AdvanceTo(At("2026-10-16 22:00", 2));

        Assert.Contains(world.Buildings, b => b.Kind == BuildingKind.Sawmill && b.Staffed);
        Assert.True(world.Stock(Resource.Timber) >= 19, $"timber {world.Stock(Resource.Timber)}");
        Assert.True(world.Stock(Resource.Planks) > 20, $"planks {world.Stock(Resource.Planks)}");
    }

    [Fact]
    public void ARoadIsOnlyBuiltWhereItShortensTheCarry()
    {
        var world = Morning();

        Assert.Equal(Rejection.AlreadyDone, Apply(world, new BuildRoad(K1)).Rejection); // Next to the store.
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new BuildRoad(M1)).Rejection);
    }

    // INV-D4 with play: the same commands at the same times give the same world however time is advanced.
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(25)]
    public void AWeekOfPlayIsTheSameWhateverTheTimeSteps(int stepHours)
    {
        var direct = Afternoon();
        var stepped = Afternoon();

        PlayAWeek(direct, (world, until) => world.AdvanceTo(until));
        PlayAWeek(stepped, (world, until) =>
        {
            for (long t = world.Now.UtcSeconds + (stepHours * Hour); t < until.UtcSeconds; t += stepHours * Hour)
            {
                world.AdvanceTo(new TimeReading(t, Stockholm, 7200));
            }

            world.AdvanceTo(until);
        });

        Assert.Equal(direct.Fingerprint(), stepped.Fingerprint());
        Assert.True(direct.Buildings.Count > 6, $"{direct.Buildings.Count} buildings");
    }

    private static void PlayAWeek(World world, Action<World, TimeReading> advance)
    {
        var morning = At("2026-10-15 08:00", 2);
        for (int day = 0; day < 7; day++)
        {
            var at = new TimeReading(morning.UtcSeconds + (day * 24 * Hour), Stockholm, 7200);
            advance(world, at);
            world.Apply($"steps-{day}", new RecordSelfCare(world.GameDay, Habit.Steps, ContributionSource.Verified, 7_500));
            world.Apply($"survey-{day}", new Survey());
            world.Apply($"f3-{day}", new Build(BuildingKind.Woodcutter, F3));
            world.Apply($"m2-{day}", new Build(BuildingKind.Quarry, M2));
            world.Apply($"e1-{day}", new Build(BuildingKind.Storehouse, E1));
            world.Apply($"e2-{day}", new Build(BuildingKind.Sawmill, E2));
            world.Apply($"l1-{day}", new Build(BuildingKind.Cottage, L1));
            world.Apply($"workday-{day}", new Workday(K1));
            world.Apply($"rush-{day}", new Rush());
        }

        advance(world, new TimeReading(morning.UtcSeconds + (7 * 24 * Hour), Stockholm, 7200));
    }

    private static void AdvanceHourlyCheckingNothingDecays(World world, int days)
    {
        long start = world.Now.UtcSeconds;
        var previous = Monotone(world);
        for (long t = start + Hour; t <= start + (days * 24 * Hour); t += Hour)
        {
            world.AdvanceTo(new TimeReading(t, Stockholm, 7200));
            var current = Monotone(world);
            Assert.True(current.Zip(previous).All(pair => pair.First >= pair.Second), $"decay at {Utc(t)}");
            Assert.All(Enum.GetValues<Resource>(), r => Assert.True(world.Stock(r) >= 0));
            previous = current;
        }
    }

    private static int[] Monotone(World world) =>
        [world.Villagers, world.Buildings.Count, world.Buildings.Sum(b => b.Level), Enumerable.Range(0, world.Map.Disc.Count).Count(i => world.IsKnown(world.Map.Disc[i]))];

    private static long[] Stocks(World world) => Enum.GetValues<Resource>().Select(world.Stock).ToArray();

    private static World Afternoon() => World.Create(At("2026-10-14 14:00", 2), Night);

    private static World Morning()
    {
        var world = Afternoon();
        world.AdvanceTo(At("2026-10-15 07:00", 2));
        return world;
    }

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"cmd-{Interlocked.Increment(ref s_key)}", command);

    private sealed class SpendAll(Force force) : GameCommand
    {
        public override CommandCategory Category => CommandCategory.Gameplay;

        internal override Rejection Execute(World world) =>
            world.TrySpend(force, world.Available(force)) ? Rejection.None : Rejection.InsufficientForces;
    }
}
