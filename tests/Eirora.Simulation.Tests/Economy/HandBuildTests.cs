using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using FsCheck.Xunit;
using static Eirora.Simulation.Tests.TestClock;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Building by hand (GLOSSARY: Handkraft; D-65, TASK-065): what the catalog marks can be built without Build Power in a
/// fixed number of game days, beside the free villagers' work, and an absence gives the same world however it is read
/// (INV-D4).
/// </summary>
public sealed class HandBuildTests
{
    private const long Hour = 3600;
    private const long Day = 24 * Hour;
    private const long Year = 365 * Day;
    private static int s_key;

    [Fact]
    public void AWeaveryBuiltByHandWithoutBuildPowerStandsAfterSevenGameDays()
    {
        var world = WithoutBuildPower();
        long planks = world.Stock(Resource.Planks);
        int queuedOn = world.GameDay;

        Assert.Equal(Rejection.InsufficientForces, Apply(world, new Build(BuildingKind.Weavery, L1)).Rejection);
        Assert.True(Apply(world, new BuildByHand(BuildingKind.Weavery, L1)).Accepted);

        var project = Assert.Single(world.Projects, p => p.ByHand);
        Assert.Equal((BuildingKind.Weavery, L1, 7, 0L), (project.Building!.Value, project.Tile, project.DaysLeft, project.WorkLeft));
        Assert.Equal(queuedOn + 7, world.StandsOnGameDay(project));
        Assert.Equal(planks - 10, world.Stock(Resource.Planks));
        Assert.Equal(0, world.Available(Force.BuildPower));

        // Through the sixth game day it is still being built, however many villagers are free.
        while (world.GameDay < queuedOn + 6 || world.Phase == Phase.Active)
        {
            Assert.DoesNotContain(world.AdvanceTo(Shift(world.Now, 1)), e => e is ProjectCompleted);
        }

        Assert.Null(world.BuildingAt(L1));
        var dawn = world.AdvanceTo(Shift(world.Now, 12));
        var broke = Assert.Single(dawn.OfType<DawnBroke>());
        var completed = Assert.Single(dawn.OfType<ProjectCompleted>());
        Assert.Equal((queuedOn + 7, broke.UtcSeconds, BuildingKind.Weavery), (broke.GameDay, completed.UtcSeconds, completed.Building!.Value));
        Assert.Equal(BuildingKind.Weavery, world.BuildingAt(L1)?.Kind);
        Assert.True(world.BuildingAt(L1)!.Staffed, "The weavery is staffed at the dawn it stands.");
        Assert.Empty(world.Projects);
    }

    [Fact]
    public void ABuildingTheCatalogDoesNotMarkIsNotBuiltByHand()
    {
        var world = WithoutBuildPower();
        string before = world.Fingerprint();

        Assert.False(world.Content.For(BuildingKind.Sawmill).HandBuild);
        Assert.Equal(Rejection.NotByHand, Apply(world, new BuildByHand(BuildingKind.Sawmill, L1)).Rejection);
        Assert.Equal(Rejection.NotByHand, Apply(world, new BuildByHand(BuildingKind.Cottage, L1)).Rejection);
        Assert.Equal(before, world.Fingerprint());
    }

    [Fact]
    public void OnlyTheJettyAndTheWeaveryAreBuiltByHandInSevenDays()
    {
        var byHand = ContentCatalog.Current.Buildings.Where(k => ContentCatalog.Current.For(k).HandBuild);

        Assert.Equal([BuildingKind.Weavery, BuildingKind.Jetty], byHand);
        Assert.All(byHand, k => Assert.Equal(7, ContentCatalog.Current.For(k).HandBuildDays));
    }

    [Fact]
    public void BuildingByHandIsRefusedWhereABuildWouldBe()
    {
        var world = WithoutBuildPower();

        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new BuildByHand(BuildingKind.Weavery, S)).Rejection);
        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new BuildByHand(BuildingKind.Jetty, L1)).Rejection);
        Assert.True(Apply(world, new BuildByHand(BuildingKind.Weavery, L1)).Accepted);
        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new BuildByHand(BuildingKind.Weavery, L1)).Rejection);
        Assert.Equal(Rejection.SiteUnavailable, Apply(world, new Build(BuildingKind.Cottage, L1)).Rejection);

        world.AdvanceTo(At("2026-10-15 23:30", 2));
        Assert.Equal(Rejection.Resting, Apply(world, new BuildByHand(BuildingKind.Weavery, L2)).Rejection);
    }

    [Fact]
    public void BuildingByHandTakesNoneOfTheFreeVillagersWork()
    {
        var plain = WithoutBuildPower();
        var byHand = WithoutBuildPower();
        Assert.True(Apply(byHand, new BuildByHand(BuildingKind.Weavery, L1)).Accepted);
        foreach (var world in new[] { plain, byHand })
        {
            Assert.True(Apply(world, new Build(BuildingKind.Cottage, L2)).Accepted);
        }

        var cottage = byHand.Projects.Single(p => !p.ByHand);
        Assert.Equal(cottage, byHand.Settlement.FirstWorked);
        Assert.Equal(Rejection.BuiltByHand, Apply(byHand, new QueueFirst(ProjectKind.Build, L1)).Rejection);

        // The cottage behind the weavery is built in the same quarter hour as without it, and rushed alike.
        Assert.True(Apply(plain, new Rush()).Accepted);
        Assert.True(Apply(byHand, new Rush()).Accepted);
        Assert.Equal(plain.Projects.Single().WorkLeft, cottage.WorkLeft);
        while (plain.BuildingAt(L2) is null)
        {
            plain.AdvanceTo(Shift(plain.Now, 0.25));
            byHand.AdvanceTo(Shift(byHand.Now, 0.25));
            Assert.Equal(plain.BuildingAt(L2) is null, byHand.BuildingAt(L2) is null);
        }

        Assert.Equal(7, byHand.Projects.Single().DaysLeft);
        Assert.Equal(Rejection.NothingToRush, Apply(byHand, new Rush()).Rejection);
    }

    [Fact]
    public void TheDiagnosisTellsWhatIsBuiltByHandAndThatItNeverStandsStill()
    {
        var world = WithoutBuildPower();
        Assert.True(Apply(world, new BuildByHand(BuildingKind.Weavery, L1)).Accepted);
        string before = world.Fingerprint();

        var diagnosis = world.Diagnose();

        var project = Assert.Single(diagnosis.ByHand);
        Assert.Equal((BuildingKind.Weavery, 7), (project.Building!.Value, project.DaysLeft));
        Assert.False(diagnosis.QueueStandsStill);
        Assert.Equal(before, world.Fingerprint()); // INV-D2.
    }

    [Fact]
    public void AJettyBuiltByHandOpensTheSeaWithoutTraining()
    {
        var world = WithoutBuildPower(jetty: false);
        Assert.Equal(Rejection.NoBuildingThere, Apply(world, new SendExpedition()).Rejection);
        Assert.True(Apply(world, new BuildByHand(BuildingKind.Jetty, H1)).Accepted);

        world.AdvanceTo(Shift(world.Now, 7 * 24));

        Assert.Equal(BuildingKind.Jetty, world.BuildingAt(H1)?.Kind);
        Assert.NotEqual(Rejection.NoBuildingThere, Apply(world, new SendExpedition()).Rejection);
    }

    [Fact]
    public void ABuildingBuiltByHandIsSavedWithItsDaysLeft()
    {
        var world = WithoutBuildPower();
        Assert.True(Apply(world, new BuildByHand(BuildingKind.Weavery, L1)).Accepted);
        world.AdvanceTo(Shift(world.Now, 2 * 24));

        var again = Copy(world);
        Assert.Equal(world.Fingerprint(), again.Fingerprint());
        Assert.Equal(5, again.Projects.Single().DaysLeft);
        Assert.Contains("project Build Weavery -1 0 0 5", world.Fingerprint(), StringComparison.Ordinal);
    }

    [Fact]
    public void AStateWithABuildingByHandThatTheCatalogDoesNotMarkIsRefused()
    {
        var world = WithoutBuildPower();
        Assert.True(Apply(world, new BuildByHand(BuildingKind.Weavery, L1)).Accepted);
        string state = world.Fingerprint();

        foreach (string wrong in new[] { "project Build Sawmill -1 0 0 7", "project Build Weavery -1 0 0 8", "project Build Weavery -1 0 8000 7", "project Build Weavery -1 0 0 -1" })
        {
            Assert.Throws<FormatException>(() => Read(state.Replace("project Build Weavery -1 0 0 7", wrong, StringComparison.Ordinal), world.Map));
        }
    }

    [Fact]
    public void AJournalWithABuildingByHandReplaysToTheSameWorld()
    {
        var journal = Journal.Start(At("2026-10-14 14:00", 2), Night);
        journal.AdvanceTo(At("2026-10-15 07:00", 2));
        Assert.True(journal.Apply("jetty", new Build(BuildingKind.Jetty, H1)).Accepted);
        Assert.True(journal.Apply("lookout", new Build(BuildingKind.Lookout, L2)).Accepted);
        Assert.True(journal.Apply("weavery", new BuildByHand(BuildingKind.Weavery, L1)).Accepted);
        journal.AdvanceTo(At("2026-10-23 12:00", 2));
        journal.RecordNow();

        var replayed = Journal.Load(journal.ToText());

        Assert.Equal(BuildingKind.Weavery, journal.World.BuildingAt(L1)?.Kind);
        Assert.Equal(journal.World.Fingerprint(), replayed.World.Fingerprint());
    }

    // The acceptance of TASK-065: a year of absence read at random moments gives the same world as one reading, with
    // buildings built by hand in a built-out world (INV-D4).
    [Property(MaxTest = 8)]
    public void AYearAwayReadInPartsGivesTheSameWorldAsOneReading(int seed)
    {
        var random = new Random(seed);

        // The first built-out world from the seed on that can pay for something to build by hand.
        World left = null!;
        int queued = 0;
        for (int candidate = seed; queued == 0; candidate++)
        {
            left = BuiltOutWorld.Generate(candidate);
            while (left.Phase != Phase.Active)
            {
                left.AdvanceTo(Shift(left.Now, 0.25));
            }

            // Every weavery or jetty the world can take and pay for, by hand, up to three, half a day apart now and then.
            foreach (var kind in new[] { BuildingKind.Weavery, BuildingKind.Jetty, BuildingKind.Weavery })
            {
                var site = Scenario.SitesOf(left).Where(h => left.CheckBuild(kind, h) == Rejection.None && left.CanAfford(left.Content.For(kind).Cost, h)).ToList();
                if (left.Phase == Phase.Active && site.Count > 0 && left.Apply($"hand-{queued}", new BuildByHand(kind, site[random.Next(site.Count)])).Accepted)
                {
                    queued++;
                    left.AdvanceTo(Shift(left.Now, random.Next(2) * 12));
                }
            }

            Assert.True(candidate < seed + 20, "No built-out world could build anything by hand.");
        }

        long until = left.Now.UtcSeconds + Year;
        var once = Copy(left);
        foreach (var reading in Readings(left.Now.ZoneId, left.Now.UtcSeconds, until))
        {
            once.AdvanceTo(reading);
        }

        var parts = Copy(left);
        var cuts = Enumerable.Range(0, 1 + random.Next(40)).Select(_ => left.Now.UtcSeconds + (long)(random.NextDouble() * Year));
        var standing = new List<ProjectCompleted>();
        foreach (var reading in Readings(left.Now.ZoneId, left.Now.UtcSeconds, until, cuts))
        {
            standing.AddRange(parts.AdvanceTo(reading).OfType<ProjectCompleted>());
        }

        Assert.Equal(once.Fingerprint(), parts.Fingerprint());
        Assert.DoesNotContain(parts.Projects, p => p.ByHand);
        Assert.True(standing.Count(c => left.Projects.Any(p => p.ByHand && p.Tile == c.Tile)) == left.Projects.Count(p => p.ByHand));
    }

    /// <summary>A morning with the guided tour's 100 Build Power spent on a jetty and a lookout, or on the lookout and a sawmill.</summary>
    private static World WithoutBuildPower(bool jetty = true)
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-15 07:00", 2));
        Assert.True(Apply(world, new Build(BuildingKind.Lookout, L3)).Accepted);
        Assert.True(Apply(world, jetty ? new Build(BuildingKind.Jetty, H1) : new Build(BuildingKind.Sawmill, E1)).Accepted);
        while (world.Projects.Count > 0)
        {
            world.AdvanceTo(Shift(world.Now, 1));
        }

        Assert.Equal(jetty ? 0 : 30, world.Available(Force.BuildPower));
        if (!jetty)
        {
            Assert.Equal(Rejection.InsufficientForces, Apply(world, new Build(BuildingKind.Jetty, H1)).Rejection);
        }

        return world;
    }

    private static World Copy(World world) =>
        SimulationSnapshot.FromBytes(SimulationSnapshot.Take(world).ToBytes()).Restore(world.Map);

    private static World Read(string state, WorldMap map) => World.Read(new StateReader(state), map);

    private static TimeReading Shift(TimeReading reading, double hours) =>
        new(reading.UtcSeconds + (long)(hours * Hour), reading.ZoneId, reading.OffsetSeconds);

    private static CommandResult Apply(World world, GameCommand command) =>
        world.Apply($"h-{Interlocked.Increment(ref s_key)}", command);
}
