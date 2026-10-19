using Eirora.Simulation.Map;
using Eirora.WorldGeneration;
using Eirora.WorldGeneration.Validation;

namespace Eirora.Simulation.Tests.WorldGeneration;

/// <summary>
/// The map validator (TASK-030): a map built to pass every rule passes, breaking one thing fails exactly the rule for
/// it, the prototype's Hemön gets a report of what it lacks, and a bulk run is the same every time.
/// </summary>
public sealed class MapValidatorTests
{
    private static readonly Hex s_first = new(15, -7);
    private static readonly Hex s_second = new(-19, 10);
    private static readonly Hex s_crest = new(0, -2);

    [Fact]
    public void AMapBuiltToTheRulesPassesThemAll()
    {
        var report = MapValidator.Validate(GoodMap().Build());

        Assert.True(report.Passed, report.ToText());
        Assert.Equal(("first", 10), (report["first-island"].Detail, (int)report["first-island"].Value));
        Assert.Equal(("second", 14), (report["second-island"].Detail, (int)report["second-island"].Value));
    }

    [Fact]
    public void TheAnalysisShowsWhatTheReportMeasured()
    {
        var analysis = MapValidator.Analyse(GoodMap().Build());

        Assert.Equal(analysis.InnerSea.Count, analysis.Report["inner-sea"].Value);
        Assert.Equal(analysis.StartHarbours.Count, analysis.Report["start-harbours"].Value);
        Assert.Equal(("first", "second", s_crest), (analysis.FirstIsland, analysis.SecondIsland, analysis.Crest));
        Assert.Equal(analysis.Report["first-island"].Value, analysis.Distances["first"]);
    }

    [Fact]
    public void AFairwayIsAsLongAsTheDistanceBySea()
    {
        var analysis = MapValidator.Analyse(GoodMap().Build());
        var harbours = analysis.Features.LandOf("first").Where(analysis.Features.IsHarborSpot);

        var route = analysis.Features.SeaRoute(analysis.StartHarbours, harbours);

        Assert.Equal(analysis.Distances["first"] + 1, route.Count);
        Assert.Contains(route[0], analysis.StartHarbours);
        Assert.All(route.Skip(1).SkipLast(1), hex => Assert.False(analysis.Map.TerrainAt(hex).IsLand()));
        Assert.All(route.Zip(route.Skip(1)), step => Assert.Equal(1, step.First.DistanceTo(step.Second)));
    }

    [Theory]
    [InlineData(10, 8L, null, 2L)] // At least 8: two above the limit.
    [InlineData(9, 8L, 12L, 1L)] // Within 8..12: one from the lower limit.
    [InlineData(11, 8L, 12L, 1L)] // One from the upper limit.
    [InlineData(0, 0L, 0L, null)] // Only one value passes: no margin.
    [InlineData(7, 8L, 12L, null)] // Failing: no margin.
    public void AMarginIsTheDistanceToTheNearestLimit(long value, long? minimum, long? maximum, long? margin)
    {
        bool passed = (minimum is null || value >= minimum) && (maximum is null || value <= maximum);

        Assert.Equal(margin, new RuleResult("rule", value, "-", passed, string.Empty, minimum, maximum).Margin);
    }

    [Fact]
    public void AStartIslandWithoutAHarbourFailsForItsHarbour()
    {
        var map = GoodMap();
        foreach (var hex in Hex.Origin.Ring(4))
        {
            map.Set(hex, Terrain.Grassland, 3);
        }

        var report = MapValidator.Validate(map.Build());

        Assert.Contains("start-harbours", report.Failed.Select(r => r.Rule));
        Assert.Equal(0, report["start-harbours"].Value);
    }

    [Fact]
    public void AnIslandWithoutAHarbourCannotBeReachedAndIsNamed()
    {
        var map = GoodMap();
        foreach (var hex in s_first.Ring(1))
        {
            map.Set(hex, Terrain.Grassland, 3);
        }

        var report = MapValidator.Validate(map.Build());

        Assert.Equal(["islands-have-harbours", "islands-reachable", "first-island"], report.Failed.Select(r => r.Rule));
        Assert.Equal("first", report["islands-have-harbours"].Detail);
    }

    public static TheoryData<string> Breakages =>
    [
        "deep-sea-edge", "start-land", "start-pasture", "start-farmland", "crest", "start-quarries",
        "first-island", "second-island", "trade-offs",
    ];

    [Theory]
    [MemberData(nameof(Breakages))]
    public void BreakingOneThingFailsExactlyItsRule(string rule)
    {
        // Too near: the first island within six steps of home, the second within seven.
        var map = rule switch
        {
            "first-island" => GoodMap(first: new Hex(9, -4)),
            "second-island" => GoodMap(second: new Hex(-12, 2)),
            _ => GoodMap(),
        };
        Break(map, rule);

        var report = MapValidator.Validate(map.Build());

        Assert.Equal([rule], report.Failed.Select(r => r.Rule));
    }

    [Fact]
    public void ThePrototypesHemonGetsAReportOfWhatItLacks()
    {
        var report = MapValidator.Validate(PrototypeHome.Map);

        // Ytterön comes when the sea around Hemön is filled once (D-81); with the crest bearing only the Vårdkase,
        // Hemön has only the stone vein to quarry (D-92, O-16).
        Assert.Equal(["start-quarries", "second-island"], report.Failed.Select(r => r.Rule));
        Assert.Equal(("nyon", 9), (report["first-island"].Detail, (int)report["first-island"].Value));
        Assert.Equal(6, report["crest"].Value);
        Assert.Equal(46, report["start-land"].Value);
    }

    [Fact]
    public void ABulkRunGivesTheSameReportEveryTime()
    {
        var generator = new ScatteredIslands();

        string first = BulkValidation.Run(generator, 1000, 40).ToText();
        string second = BulkValidation.Run(generator, 1000, 40).ToText();

        Assert.Equal(first, second);
        Assert.StartsWith("scattered-islands 1 seeds 00000000000003e8+40 passed ", first, StringComparison.Ordinal);
        Assert.Equal(MapValidator.Validate(GoodMap().Build()).Rules.Count + 3, first.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    /// <summary>
    /// CI's small bulk run over every generator there is; tools/Eirora.MapCheck runs tens of thousands locally. Until
    /// generator v1 (TASK-031) only the prototype's handmade map is listed, and it does not pass yet.
    /// </summary>
    [Fact]
    public void EveryGeneratorIsBulkValidatedTheSameWayTwice()
    {
        foreach (var generator in WorldGenerators.All)
        {
            var report = BulkValidation.Run(generator, 0, 20);

            Assert.Equal(report.ToText(), BulkValidation.Run(generator, 0, 20).ToText());
            Assert.Equal(20, report.Rules.Single(r => r.Rule == "radius").Passed);
        }
    }

    [Fact]
    public void ABulkRunCountsPassesAndSpreadsPerRule()
    {
        var report = BulkValidation.Run(new ScatteredIslands(), 7, 25);

        Assert.All(report.Rules, rule => Assert.InRange(rule.Passed, 0, 25));
        Assert.All(report.Rules, rule => Assert.True(rule.Minimum <= rule.Median && rule.Median <= rule.Maximum, rule.Rule));
        Assert.Equal(25, report.Rules.Single(r => r.Rule == "radius").Passed);
    }

    /// <summary>
    /// A map made to pass every rule: a start island of radius 4 with every kind of ground and a single crest, and two
    /// small islands 10 and 14 steps away by sea with richer ground than home.
    /// </summary>
    internal static WorldMapBuilder GoodMap(Hex? first = null, Hex? second = null)
    {
        var firstCentre = first ?? s_first;
        var secondCentre = second ?? s_second;
        var map = new WorldMapBuilder(24, MapSource.Handmade, "good", 1) { Start = Hex.Origin };
        Island(map, Hex.Origin, 4);
        map.Set(s_crest, Terrain.Mountain, 6);
        map.Set(new Hex(2, -1), Terrain.Mountain, 5);
        map.Set(new Hex(-1, -1), Terrain.Mountain, 5);
        foreach (var hex in new[] { new Hex(-2, 1), new Hex(-2, 2), new Hex(-1, 2) })
        {
            map.Set(hex, Terrain.Forest, 3);
        }

        map.Set(new Hex(1, 1), Terrain.Farmland, 3);
        map.Set(new Hex(2, 0), Terrain.Farmland, 3);
        map.Set(new Hex(2, -2), Terrain.Pasture, 3);

        Island(map, firstCentre, 1);
        map.Set(firstCentre, Terrain.Forest, 3, richness: 150);
        Island(map, secondCentre, 1);
        map.Set(secondCentre, Terrain.Mountain, 5, richness: 200);
        map.Set(secondCentre + new Hex(0, 2), Terrain.ShallowSea, 1, richness: 150);

        map.AddIsland("home", Hex.Origin);
        map.AddIsland("first", firstCentre);
        map.AddIsland("second", secondCentre);
        return map;
    }

    /// <summary>Grassland within the radius, a beach on its edge and shallow water around it.</summary>
    private static void Island(WorldMapBuilder map, Hex centre, int radius)
    {
        foreach (var hex in centre.Within(radius + 1))
        {
            int distance = hex.DistanceTo(centre);
            map.Set(hex, distance > radius ? Terrain.ShallowSea : distance == radius ? Terrain.Beach : Terrain.Grassland, distance > radius ? 1 : distance == radius ? 2 : 3);
        }
    }

    private static void Break(WorldMapBuilder map, string rule)
    {
        switch (rule)
        {
            case "deep-sea-edge":
                map.Set(new Hex(0, 24), Terrain.ShallowSea, 1);
                break;
            case "start-land":
                // Hemön shrinks to radius 3: 37 tiles of land.
                foreach (var hex in Hex.Origin.Ring(3))
                {
                    map.Set(hex, Terrain.Beach, 2);
                }

                foreach (var hex in Hex.Origin.Ring(4))
                {
                    map.Set(hex, Terrain.ShallowSea, 1);
                }

                break;
            case "start-pasture":
                map.Set(new Hex(2, -2), Terrain.Grassland, 3);
                break;
            case "start-farmland":
                map.Set(new Hex(2, 0), Terrain.Grassland, 3);
                break;
            case "start-quarries":
                map.Set(new Hex(-1, -1), Terrain.Grassland, 3);
                break;
            case "crest":
                map.Set(new Hex(-2, 0), Terrain.Mountain, 6);
                break;
            case "trade-offs":
                map.Set(s_first, Terrain.Grassland, 3);
                map.Set(s_second, Terrain.Grassland, 3);
                break;
        }
    }

    /// <summary>A throwaway generator for the bulk run's tests: a few round islands at random places, some of them rich.</summary>
    private sealed class ScatteredIslands : IWorldGenerator
    {
        public string Id => "scattered-islands";

        public int Version => 1;

        public MapSource Source => MapSource.Procedural;

        public WorldMap Generate(ulong seed)
        {
            var random = Pcg32.ForStep(seed, "islands");
            var map = new WorldMapBuilder(24, Source, Id, Version) { Seed = seed, Start = Hex.Origin };
            Island(map, Hex.Origin, random.Between(3, 5));
            map.Set(s_crest, Terrain.Mountain, random.Between(4, 6));
            map.AddIsland("home", Hex.Origin);
            int count = random.Between(1, 4);
            for (int i = 0; i < count; i++)
            {
                var centre = Hex.Origin.Ring(random.Between(12, 19)).ElementAt(random.Below(6 * 12));
                if (centre.Within(4).Any(h => map.Contains(h) && map.TerrainAt(h) != Terrain.DeepSea))
                {
                    continue;
                }

                Island(map, centre, 1);
                map.Set(centre, random.Chance(50) ? Terrain.Forest : Terrain.Mountain, 4, richness: 100 + (50 * random.Below(3)));
                map.AddIsland("isle" + i, centre);
            }

            return map.Build();
        }
    }
}
