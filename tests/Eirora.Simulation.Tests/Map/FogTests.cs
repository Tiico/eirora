using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Time;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Map;

/// <summary>
/// Fog per tile in the world's state (TASK-032, MAP-04): what a new world knows, lifting it tile by tile at the edge
/// of the known, that it never comes back, and that a migrated prototype world knows what it knew.
/// </summary>
public sealed class FogTests
{
    private static readonly WorldMap s_map = PrototypeHome.Map;
    private static readonly RestSchedule s_night = new(23 * 60, 8 * 60);

    [Fact]
    public void ANewWorldKnowsHemonAndItsCoastButNotWhatTheSurveysFindOrBeyond()
    {
        var world = NewWorld();
        var coast = Tiles().Where(h => !s_map.TerrainAt(h).IsLand() && Neighbours(h).Any(n => s_map.IslandAt(n) == PrototypeHome.Home));
        var hidden = PrototypeIsland.Surveys.SelectMany(s => s.Reveals).ToHashSet();

        Assert.All(Tiles().Where(h => s_map.IslandAt(h) == PrototypeHome.Home && !hidden.Contains(h)), h => Assert.True(world.IsKnown(h), h.ToString()));
        Assert.All(coast.Where(h => !hidden.Contains(h)), h => Assert.True(world.IsKnown(h), h.ToString()));
        Assert.All(hidden, h => Assert.False(world.IsKnown(h), h.ToString()));
        Assert.False(world.IsKnown(PrototypeHome.NyonCentre));
        Assert.False(world.IsKnown(new Hex(6, 0)));
    }

    [Fact]
    public void LiftingATileAtTheEdgeCostsTenExplorationAndRevealsIt()
    {
        var world = NewWorld();
        var tile = Liftable(world).First();
        int before = world.Available(Force.Exploration);

        var result = world.Apply("lift", new LiftFog(tile.Q, tile.R));

        Assert.True(result.Accepted);
        Assert.True(world.IsKnown(tile));
        Assert.Equal(before - LiftFog.Cost, world.Available(Force.Exploration));
        Assert.Contains(result.Events, e => e is Events.FogLifted f && f.Q == tile.Q && f.R == tile.R);
    }

    [Fact]
    public void ATileNotNextToTheKnownIsRefusedWithoutSideEffects()
    {
        var world = NewWorld();
        string before = world.Fingerprint();

        var result = world.Apply("far", new LiftFog(0, 20));

        Assert.Equal(Rejection.NotNextToTheKnown, result.Rejection);
        Assert.Equal(Rejection.NotNextToTheKnown, world.Apply("off the map", new LiftFog(100, 0)).Rejection);
        Assert.Equal(before, world.Fingerprint());
    }

    [Fact]
    public void AnUnfoundIslandSaysSoWhereverItIsTapped()
    {
        // Lifting closer first would only end in the same refusal at the island's shore.
        var world = NewWorld();

        Assert.Equal(Rejection.FoundByExpedition, world.Apply("nyon", new LiftFog(PrototypeHome.NyonCentre.Q, PrototypeHome.NyonCentre.R)).Rejection);
    }

    [Fact]
    public void AKnownTileOrTooLittleExplorationIsRefused()
    {
        var world = NewWorld();
        Assert.Equal(Rejection.AlreadyDone, world.Apply("known", new LiftFog(0, 0)).Rejection);

        int lifts = 0;
        Rejection last = Rejection.None;
        foreach (var tile in Liftable(world).Take(20))
        {
            last = world.Apply($"lift-{lifts++}", new LiftFog(tile.Q, tile.R)).Rejection;
            if (last != Rejection.None)
            {
                break;
            }
        }

        Assert.Equal(Rejection.InsufficientForces, last);
    }

    [Fact]
    public void ATileASurveyRevealsIsNotLiftedTileByTile()
    {
        var world = NewWorld();
        var vein = new Hex(3, -1);

        Assert.False(world.CanLiftFog(vein));
        Assert.Equal(Rejection.FoundBySurvey, world.Apply("vein", new LiftFog(vein.Q, vein.R)).Rejection);
    }

    [Fact]
    public void AnIslandAnExpeditionFindsIsNotLiftedTileByTile()
    {
        var world = NewWorld();
        var bay = PrototypeHome.NyonBay;
        var route = new MapFeatures(s_map).SeaRoute(Tiles().Where(world.IsKnown).Where(h => !s_map.TerrainAt(h).IsLand()), [bay]);
        int lifted = 0;
        foreach (var tile in route.Where(h => !world.IsKnown(h)))
        {
            world.Apply($"sea-{lifted++}", new LiftFog(tile.Q, tile.R));
        }

        // The sea up to Nyön's bay can be lifted; Nyön itself waits for the expedition.
        Assert.True(world.IsKnown(bay));
        Assert.Equal(Rejection.FoundByExpedition, world.Apply("nyon", new LiftFog(PrototypeHome.NyonCentre.Q, PrototypeHome.NyonCentre.R)).Rejection);
    }

    [Property(MaxTest = 40)]
    public bool LiftedFogStaysLiftedThroughTimeAndReloading(int seed)
    {
        var random = new Random(seed);
        var save = Save.Begin(Reading("2026-10-14 14:00"), s_night);
        for (int i = 0; i < 8; i++)
        {
            var edge = Liftable(save.World).ToList();
            var tile = edge[random.Next(edge.Count)];
            if (!save.Apply($"lift-{i}", new LiftFog(tile.Q, tile.R)).Accepted)
            {
                return false;
            }
        }

        var known = Tiles().Where(save.World.IsKnown).ToList();
        save.AdvanceTo(Reading("2026-10-17 10:00"));
        var loaded = Save.Load(save.ToBytes());

        return known.All(loaded.World.IsKnown) && Tiles().Count(loaded.World.IsKnown) == known.Count;
    }

    [Fact]
    public void ASurveyLiftsTheFogFromItsTiles()
    {
        var world = NewWorld();

        Assert.True(world.Apply("survey", new Survey()).Accepted);

        Assert.All(PrototypeIsland.Surveys[0].Reveals, tile => Assert.True(world.IsKnown(tile), tile.ToString()));
        Assert.All(PrototypeIsland.Surveys[1].Reveals, tile => Assert.False(world.IsKnown(tile), tile.ToString()));
    }

    [Fact]
    public void TheExpeditionFindsNyonAndTheSeaAlongItsCoast()
    {
        var fog = Fog.AtStart(s_map, []);

        fog.LiftIsland(PrototypeHome.Nyon);

        Assert.All(Tiles().Where(h => s_map.IslandAt(h) == PrototypeHome.Nyon), h => Assert.True(fog.IsKnown(h)));
        Assert.True(fog.IsKnown(PrototypeHome.NyonBay));
        Assert.False(fog.IsKnown(new Hex(6, 0)));
    }

    [Fact]
    public void AMigratedPrototypeWorldRevealsTheSameSitesAsBefore()
    {
        // The golden save from rules 1 had surveyed everything on Hemön but not sent the expedition.
        string folder = Path.Combine(Root(), "tests", "Eirora.Simulation.Tests", "Saving", "Golden");
        var world = Save.Load(File.ReadAllBytes(Path.Combine(folder, "save-1-rules-1.save"))).World;

        Assert.All(PrototypeSites.All, site => Assert.Equal(!PrototypeSites.ExpeditionFound.Contains(site.Id), world.IsKnown(site.Hex)));
    }

    [Fact]
    public void TheFogIsTheSameTextAfterARoundTrip()
    {
        var fog = Fog.AtStart(s_map, PrototypeIsland.Surveys.SelectMany(s => s.Reveals));
        fog.Lift(new Hex(6, 0));

        var read = Fog.Parse(s_map, fog.ToText());

        Assert.Equal(fog.ToText(), read.ToText());
        Assert.Equal(fog.KnownCount, read.KnownCount);
        Assert.Throws<FormatException>(() => Fog.Parse(s_map, fog.ToText() + "0"));
        Assert.Throws<FormatException>(() => Fog.Parse(s_map, "x" + fog.ToText()[1..]));
    }

    [Fact]
    public void LiftingFogIsJournalled()
    {
        var journal = Journal.Start(Reading("2026-10-14 14:00"), s_night);
        var tile = Liftable(journal.World).First();
        journal.Apply("lift", new LiftFog(tile.Q, tile.R));

        var replayed = Journal.Load(journal.ToText());

        Assert.Contains($"fog {tile.Q} {tile.R}", journal.ToText(), StringComparison.Ordinal);
        Assert.True(replayed.World.IsKnown(tile));
    }

    private static World NewWorld() => World.Create(Reading("2026-10-14 14:00"), s_night);

    private static IEnumerable<Hex> Tiles() => Enumerable.Range(0, s_map.Disc.Count).Select(i => s_map.Disc[i]);

    private static IEnumerable<Hex> Liftable(World world) => Tiles().Where(world.CanLiftFog);

    private static IEnumerable<Hex> Neighbours(Hex hex) => Enumerable.Range(0, Hex.Directions).Select(hex.Neighbour).Where(s_map.Contains);

    private static TimeReading Reading(string local)
    {
        var wallClock = DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        return new TimeReading(new DateTimeOffset(wallClock, TimeSpan.FromHours(2)).ToUnixTimeSeconds(), "Europe/Stockholm", 7200);
    }

    private static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Eirora.slnx")))
        {
            folder = folder.Parent;
        }

        return folder!.FullName;
    }
}
