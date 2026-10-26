using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.Economy.TierVillage;

namespace Eirora.Simulation.Tests;

/// <summary>
/// The prototype's world as the guided tour leaves it in epoch 1's fourth week (economy-v2.md, section 10), written as
/// state text so that a scenario can start from it: step 3 unlocked, the home island surveyed and its 45 villagers of
/// all three tiers in their houses, every chain of food, clothes and drink, a tar kiln and a shipyard with a knarr
/// launched and the next under way, and Nyön's settlement on a sea route home (TASK-052).
/// </summary>
/// <remarks>
/// A world made by playing would take weeks of simulated time for each seed. The buildings are placed once, on the first
/// tiles that take them, and every world from here is the new world of its install with them written in, so that the
/// reader checks it as it checks a save.
/// </remarks>
internal static class GrownVillage
{
    // What the village has built, in its order: the home island's buildings beyond the start village's, then Nyön's.
    private static readonly (string Island, BuildingKind Kind)[] s_plan =
    [
        (PrototypeHome.Home, BuildingKind.Jetty),
        (PrototypeHome.Home, BuildingKind.Longhouse),
        (PrototypeHome.Home, BuildingKind.Cottage),
        (PrototypeHome.Home, BuildingKind.Cottage),
        (PrototypeHome.Home, BuildingKind.Farmstead),
        (PrototypeHome.Home, BuildingKind.Farmstead),
        (PrototypeHome.Home, BuildingKind.CraftsmensHouse),
        (PrototypeHome.Home, BuildingKind.Sawmill),
        (PrototypeHome.Home, BuildingKind.Quarry),
        (PrototypeHome.Home, BuildingKind.SheepFarm),
        (PrototypeHome.Home, BuildingKind.Weavery),
        (PrototypeHome.Home, BuildingKind.HuntingLodge),
        (PrototypeHome.Home, BuildingKind.Tannery),
        (PrototypeHome.Home, BuildingKind.Apiary),
        (PrototypeHome.Home, BuildingKind.Brewery),
        (PrototypeHome.Home, BuildingKind.Meadery),
        (PrototypeHome.Home, BuildingKind.TarKiln),
        (PrototypeHome.Home, BuildingKind.Shipyard),
        (PrototypeHome.Nyon, BuildingKind.Jetty),
        (PrototypeHome.Nyon, BuildingKind.FishingHut),
    ];

    // The home island's goods in whole units, within what its storehouse and jetty hold; the rest of each store is empty.
    private static readonly (Resource Resource, int Units)[] s_home =
    [
        (Resource.Fish, 100), (Resource.Grain, 20), (Resource.Timber, 100), (Resource.Planks, 60), (Resource.Stone, 60), (Resource.Wool, 10),
        (Resource.Wadmal, 20), (Resource.Game, 10), (Resource.Hide, 10), (Resource.Honey, 10), (Resource.Mead, 20), (Resource.Ale, 10), (Resource.Tar, 15),
    ];

    private static readonly Lazy<Hex[]> s_tiles = new(Place);

    /// <summary>
    /// The grown village in the world <paramref name="install"/> creates, read back as a save would be, with the shipyard
    /// paused by the player when <paramref name="shipyardPaused"/>.
    /// </summary>
    public static World Create(TimeReading install, RestSchedule schedule, bool shipyardPaused = false)
    {
        var world = World.Create(install, schedule);
        LiftFog(world);
        var lines = Grown(world.Fingerprint(), s_tiles.Value);
        lines[lines.FindIndex(l => l.StartsWith("settlement home ", StringComparison.Ordinal))] = "settlement home 28 10 7";
        lines[lines.FindIndex(l => l.StartsWith($"settlement {PrototypeHome.Nyon} ", StringComparison.Ordinal))] = $"settlement {PrototypeHome.Nyon} 4 0 0";
        foreach (var (resource, units) in s_home)
        {
            lines[lines.FindIndex(l => l.StartsWith($"stock home {resource} ", StringComparison.Ordinal))] = $"stock home {resource} {Quantity.Units(units)}";
        }

        lines[lines.FindIndex(l => l.StartsWith($"stock {PrototypeHome.Nyon} {Resource.Fish} ", StringComparison.Ordinal))] = $"stock {PrototypeHome.Nyon} {Resource.Fish} {Quantity.Units(20)}";

        // A knarr launched from the home island and the next a third built, paid for when it was begun.
        lines.Insert(lines.FindIndex(l => l.StartsWith("building ", StringComparison.Ordinal)), "knarrs home 1");
        var knarrs = ContentCatalog.Current.For(BuildingKind.Shipyard).Knarrs!;
        int shipyard = lines.FindIndex(l => l.StartsWith("building Shipyard ", StringComparison.Ordinal));
        lines.Insert(shipyard + 1, $"knarr {knarrs.WorkHours * 1000L * 2 / 3}");
        if (shipyardPaused)
        {
            // The paused field follows the level, the staffing, the workday and what has ripened.
            var fields = lines[shipyard].Split(' ');
            fields[8] = "1";
            lines[shipyard] = string.Join(' ', fields);
        }

        return Read(lines, world.Map);
    }

    /// <summary>
    /// The state of a new world with step 3 unlocked and the plan's buildings on <paramref name="tiles"/>, none of them
    /// staffed yet, and Nyön's settlement and store once a building stands there.
    /// </summary>
    private static List<string> Grown(string state, IReadOnlyList<Hex> tiles)
    {
        var lines = state.TrimEnd('\n').Split('\n').ToList();
        int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
        lines[economy] = $"{lines[economy][..lines[economy].LastIndexOf(' ')]} 3";
        if (Enumerable.Range(0, tiles.Count).Any(i => s_plan[i].Island == PrototypeHome.Nyon))
        {
            lines.Insert(lines.FindLastIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal)) + 1, $"settlement {PrototypeHome.Nyon} 0 0 0");
            lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("stock ", StringComparison.Ordinal)) + 1, Enum.GetValues<Resource>().Select(r => $"stock {PrototypeHome.Nyon} {r} 0"));
        }

        lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, tiles.Select((tile, i) => Line(s_plan[i].Kind, tile)));
        return lines;
    }

    /// <summary>The tile of each building in the plan: the first on its island that takes it beside those placed before it.</summary>
    private static Hex[] Place()
    {
        var map = PrototypeHome.Map;
        var template = World.Create(TestClock.At("2026-03-02 10:00", 1), TestClock.Night);
        LiftFog(template);
        string state = template.Fingerprint();
        Hex[] sites = [.. Hex.Origin.Within(6), .. PrototypeHome.NyonCentre.Within(2)];
        var tiles = new List<Hex>();
        foreach (var (island, kind) in s_plan)
        {
            var world = Read(Grown(state, tiles), map);
            tiles.Add(sites.First(h => world.Settlement.IslandOf(h) == island && world.CheckBuild(kind, h) == Rejection.None));
        }

        return [.. tiles];
    }

    /// <summary>The home island surveyed and Nyön found, as a player has them by the fourth week.</summary>
    private static void LiftFog(World world)
    {
        world.Settlement.Fog.LiftIsland(PrototypeHome.Home);
        world.Settlement.Fog.LiftIsland(PrototypeHome.Nyon);
    }

    private static World Read(List<string> lines, WorldMap map)
    {
        var reader = new StateReader(string.Join('\n', lines) + "\n");
        var world = World.Read(reader, map);
        reader.End();
        return world;
    }
}
