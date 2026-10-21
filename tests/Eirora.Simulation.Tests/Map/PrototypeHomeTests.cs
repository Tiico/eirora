using System.Text;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests.Map;

/// <summary>
/// The prototype's handmade map (TASK-029, D-81): every site the prototype named lies where it did, on ground that fits
/// it, and every world carries its map through snapshots and saves.
/// </summary>
public sealed class PrototypeHomeTests
{
    private static readonly WorldMap s_map = PrototypeHome.Map;
    private static readonly string[] s_islands = ["home", "nyon"];

    public static TheoryData<string> SiteIds => [.. PrototypeSites.All.Select(site => site.Id)];

    [Theory]
    [MemberData(nameof(SiteIds))]
    public void EverySiteLiesOnGroundThatFitsItWithItsRichness(string id)
    {
        var site = PrototypeSites.All.Single(s => s.Id == id);

        Assert.Equal(site.Terrain, s_map.TerrainAt(site.Hex));
        Assert.Equal(site.Richness, s_map.RichnessAt(site.Hex));
    }

    [Theory]
    [MemberData(nameof(SiteIds))]
    public void EveryLandSiteLiesOnItsIslandAndEveryWaterSiteOffShore(string id)
    {
        var site = PrototypeSites.All.Single(s => s.Id == id);
        string island = PrototypeSites.ExpeditionFound.Contains(site.Id) ? PrototypeHome.Nyon : PrototypeHome.Home;

        Assert.Equal(site.Terrain.IsLand() ? island : null, s_map.IslandAt(site.Hex));
    }

    [Fact]
    public void HemonAndNyonAreTheMapsIslandsWithTheSettlementAsStart()
    {
        Assert.Equal(s_islands, s_map.Islands.Select(island => island.Id));
        Assert.Equal(PrototypeIsland.Settlement, s_map.Start);
        Assert.Equal((MapSource.Handmade, "prototype-home", 1, 24), (s_map.Source, s_map.GeneratorId, s_map.GeneratorVersion, s_map.Radius));
    }

    [Fact]
    public void BeyondTheSeaThePrototypeDrewThereIsOnlyDeepSea()
    {
        var beyond = s_map.Disc.Count == 1801
            ? Enumerable.Range(0, s_map.Disc.Count).Select(i => s_map.Disc[i]).Where(hex => !PrototypeHome.WasDrawn(hex))
            : throw new InvalidOperationException("The map has radius 24 (D-80).");

        Assert.All(beyond, hex => Assert.Equal(Terrain.DeepSea, s_map.TerrainAt(hex)));
    }

    [Fact]
    public void ANewWorldIsOnThePrototypeMapAndItsStateNamesIt()
    {
        var world = World.Create(Reading(), new RestSchedule(23 * 60, 8 * 60));

        Assert.Same(s_map, world.Map);
        Assert.StartsWith($"map {s_map.Hash}\n", world.Fingerprint(), StringComparison.Ordinal);
    }

    [Fact]
    public void AWorldsStateCannotBeRestoredOnAnotherMap()
    {
        var snapshot = SimulationSnapshot.Take(World.Create(Reading(), new RestSchedule(23 * 60, 8 * 60)));

        Assert.Throws<FormatException>(() => snapshot.Restore(WorldMapTests.SmallMap()));
    }

    [Fact]
    public void ASaveCarriesItsWorldsMapOnce()
    {
        var save = Save.Begin(Reading(), new RestSchedule(23 * 60, 8 * 60));

        string text = Encoding.UTF8.GetString(save.ToBytes());

        Assert.Single(AllIndexesOf(text, s_map.ToText()));
        Assert.Equal(s_map.Hash, Save.Load(save.ToBytes()).World.Map.Hash);
    }

    [Fact]
    public void AWorldOnAnotherMapKeepsThatMapThroughASave()
    {
        var map = WorldMapTests.SmallMap();
        var save = Save.Begin(map, Reading(), new RestSchedule(23 * 60, 8 * 60));

        var loaded = Save.Load(save.ToBytes());

        Assert.Equal(map.Hash, loaded.World.Map.Hash);
        Assert.Equal(save.World.Fingerprint(), loaded.World.Fingerprint());
    }

    private static IEnumerable<int> AllIndexesOf(string text, string part)
    {
        for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + 1, StringComparison.Ordinal))
        {
            yield return at;
        }
    }

    private static TimeReading Reading() => new(1_792_000_000, "Europe/Stockholm", 7200);
}
