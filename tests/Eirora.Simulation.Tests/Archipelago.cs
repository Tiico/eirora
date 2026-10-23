using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using Eirora.WorldGeneration;

namespace Eirora.Simulation.Tests;

/// <summary>
/// A generated map with three islands to build on (TASK-045): the home island, the first island, which the expedition
/// finds, and the second, which a world here knows from the start, as later expeditions will find more islands (M5).
/// </summary>
internal static class Archipelago
{
    public const string Home = "home";
    public const string First = "first";
    public const string Second = "second";

    private static readonly Lazy<WorldMap> s_map = new(() => WorldGenerators.Get(ProceduralArchipelagoV1.GeneratorId, 1).Generate(ulong.MaxValue));

    private static readonly Lazy<Hex[]> s_sites = new(() =>
    [
        .. Enumerable.Range(0, Map.Disc.Count).Select(i => Map.Disc[i])
            .Where(h => Map.IslandAt(h) is Home or First or Second || Coast(h)),
    ]);

    public static WorldMap Map => s_map.Value;

    /// <summary>The land of the three islands and the sea along their coasts, in map order.</summary>
    public static IReadOnlyList<Hex> Sites => s_sites.Value;

    /// <summary>A new world on the map that knows the second island and its coast.</summary>
    public static World Create(TimeReading install, RestSchedule schedule)
    {
        var world = World.Create(Map, install, schedule);
        world.Settlement.Fog.LiftIsland(Second);
        return world;
    }

    private static bool Coast(Hex hex) =>
        Map.IslandAt(hex) is null && Enumerable.Range(0, Hex.Directions).Any(d => Map.Contains(hex.Neighbour(d)) && Map.IslandAt(hex.Neighbour(d)) is Home or First or Second);
}
