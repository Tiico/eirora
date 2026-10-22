using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;

namespace Eirora.Simulation.Tests.Map;

/// <summary>The map's format (ADR-0010, TASK-029): canonical text, a stable hash and the rules every map keeps.</summary>
public sealed class WorldMapTests
{
    [Fact]
    public void AMapReadsBackToTheSameTextAndHash()
    {
        var map = SmallMap();

        var read = WorldMap.Parse(map.ToText());

        Assert.Equal(map.ToText(), read.ToText());
        Assert.Equal(map.Hash, read.Hash);
        Assert.Equal(Terrain.Mountain, read.TerrainAt(new Hex(1, 0)));
        Assert.Equal(6, read.HeightAt(new Hex(1, 0)));
        Assert.Equal(150, read.RichnessAt(new Hex(-1, 1)));
        Assert.Equal("isle", read.IslandAt(new Hex(1, 0)));
        Assert.Null(read.IslandAt(new Hex(2, 0)));
        Assert.Equal(0x0123456789abcdefUL, read.Seed);
    }

    [Fact]
    public void TheTextHasOneLinePerRowWithATerrainLetterAndHeightPerTile()
    {
        string text = SmallMap().ToText();

        Assert.Equal(
            "eirora-map 1\nradius 2\nsource procedural\ngenerator test 3\nseed 0123456789abcdef\nstart 0 0\nisland isle 0 0\n"
            + "row -2 D0 D0 D0\nrow -1 D0 S1 S1 D0\nrow 0 D0 S1 G3 M6 D0\nrow 1 D0 S1:150 S1 D0\nrow 2 D0 D0 D0\n",
            text);
    }

    [Fact]
    public void AGeographicMapNamesWhereItsDataCameFrom()
    {
        var builder = new WorldMapBuilder(1, MapSource.Geographic, "geodata", 1)
        {
            Provenance = new MapProvenance("Lantmateriet-GSD", "2026", "CC0", 18_000_000, 59_000_000, 18_100_000, 59_050_000),
        };
        builder.Set(Hex.Origin, Terrain.Grassland, 3);
        builder.AddIsland("isle", Hex.Origin);

        var read = WorldMap.Parse(builder.Build().ToText());

        Assert.Equal(MapSource.Geographic, read.Source);
        Assert.Equal("Lantmateriet-GSD", read.Provenance!.Dataset);
        Assert.Equal(59_050_000, read.Provenance.North);
        Assert.Null(read.Seed);
    }

    [Fact]
    public void ANewerMapFormatIsNotReadAndSaysSo()
    {
        string text = SmallMap().ToText().Replace("eirora-map 1", "eirora-map 2", StringComparison.Ordinal);

        Assert.Throws<SavedByNewerVersionException>(() => WorldMap.Parse(text));
    }

    [Theory]
    [InlineData("row 0 D0 S1 G3 M6 D0", "row 0 D0 S1 G3 M6")] // A row too short.
    [InlineData("row 0 D0 S1 G3 M6 D0", "row 0 D0 S1 G3 M6 D0 D0")] // A row too long.
    [InlineData("G3", "X3")] // No such terrain.
    [InlineData("M6", "M8")] // Too high.
    [InlineData("S1:150", "S1:100")] // Normal richness is never written.
    [InlineData("S1:150", "S1:abc")]
    [InlineData("seed 0123456789abcdef", "seed 123")]
    [InlineData("island isle 0 0\n", "")] // Land that belongs to no island.
    public void AnythingButAWholeCanonicalMapIsRejected(string part, string replacement)
    {
        string text = SmallMap().ToText().Replace(part, replacement, StringComparison.Ordinal);

        Assert.Throws<FormatException>(() => WorldMap.Parse(text));
    }

    // The server reads the maps players upload (ADR-0008), so a few bytes must never make it allocate a huge map.
    [Theory]
    [InlineData(5_000)]
    [InlineData(30_000)] // More tiles than an int counts.
    [InlineData(-1)]
    public void ARadiusTheTextCannotHoldIsRejectedBeforeAnyTileIsAllocated(int radius)
    {
        string text = SmallMap().ToText().Replace("radius 2", $"radius {radius}", StringComparison.Ordinal);
        long before = GC.GetAllocatedBytesForCurrentThread();

        Assert.Throws<FormatException>(() => WorldMap.Parse(text));

        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1_000_000, "Parsing allocated the radius' tiles.");
    }

    [Fact]
    public void OnlyAProceduralMapHasASeed()
    {
        var builder = Land(new WorldMapBuilder(1, MapSource.Handmade, "hand", 1));
        builder.Seed = 1;

        Assert.Throws<ArgumentException>(builder.Build);
    }

    [Fact]
    public void TheStartAndEveryAnchorLieOnLand()
    {
        var atSea = Land(new WorldMapBuilder(1, MapSource.Handmade, "hand", 1));
        atSea.Start = new Hex(1, 0);
        var anchoredAtSea = new WorldMapBuilder(1, MapSource.Handmade, "hand", 1);
        anchoredAtSea.Set(Hex.Origin, Terrain.Grassland, 3);
        anchoredAtSea.AddIsland("isle", new Hex(1, 0));

        Assert.Throws<ArgumentException>(atSea.Build);
        Assert.Throws<ArgumentException>(anchoredAtSea.Build);
    }

    [Fact]
    public void TwoIslandsAreNeverTheSameLand()
    {
        var builder = Land(new WorldMapBuilder(1, MapSource.Handmade, "hand", 1));
        builder.Set(new Hex(1, 0), Terrain.Forest, 3);
        builder.AddIsland("other", new Hex(1, 0));

        Assert.Throws<ArgumentException>(builder.Build);
    }

    [Fact]
    public void ATileOffTheMapHasNoTerrain()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SmallMap().TerrainAt(new Hex(3, 0)));
    }

    /// <summary>A map of radius 2: one island of grass and mountain, with shallow water around it.</summary>
    internal static WorldMap SmallMap()
    {
        var builder = new WorldMapBuilder(2, MapSource.Procedural, "test", 3) { Seed = 0x0123456789abcdefUL };
        foreach (var hex in Hex.Origin.Ring(1))
        {
            builder.Set(hex, Terrain.ShallowSea, 1);
        }

        builder.Set(Hex.Origin, Terrain.Grassland, 3);
        builder.Set(new Hex(1, 0), Terrain.Mountain, 6);
        builder.Set(new Hex(-1, 1), Terrain.ShallowSea, 1, richness: 150);
        builder.AddIsland("isle", Hex.Origin);
        return builder.Build();
    }

    private static WorldMapBuilder Land(WorldMapBuilder builder)
    {
        builder.Set(Hex.Origin, Terrain.Grassland, 3);
        builder.AddIsland("isle", Hex.Origin);
        return builder;
    }
}
