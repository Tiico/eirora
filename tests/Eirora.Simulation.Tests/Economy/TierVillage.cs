using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// Villages for the population tiers' tests (TASK-061, TASK-062): most worlds are written as state text on the
/// prototype's map half an hour before a dawn, so that each settlement has exactly the villagers, buildings and goods the
/// test needs; a bare village has only its storehouse, so that nothing produces and the dawn's goods are all that change.
/// </summary>
internal static class TierVillage
{
    /// <summary>
    /// The prototype's world half an hour before its second dawn, with Nyön known and its settlement of <paramref name="nyon"/>
    /// villagers (none when null), the home island's crofters, farmers and craftsmen, the step, the buildings after the founding's
    /// (only the storehouse when <paramref name="bare"/>) and the home island's stock in whole units; read back as a save
    /// would be.
    /// </summary>
    public static World Village(
        int crofters,
        int farmers = 0,
        int step = 0,
        int craftsmen = 0,
        string[]? buildings = null,
        (Resource Resource, int Units)[]? stock = null,
        bool bare = false,
        int? nyon = null)
    {
        var world = World.Create(At("2026-10-14 14:00", 2), Night);
        world.AdvanceTo(At("2026-10-16 06:30", 2));
        world.Settlement.Fog.LiftIsland(PrototypeHome.Nyon);
        var lines = world.Fingerprint().TrimEnd('\n').Split('\n').ToList();
        int economy = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
        lines[economy] = $"{lines[economy][..lines[economy].LastIndexOf(' ')]} {step}";
        lines[lines.FindIndex(l => l.StartsWith("settlement home ", StringComparison.Ordinal))] = $"settlement home {crofters} {farmers} {craftsmen}";
        if (nyon is int moved)
        {
            lines.Insert(lines.FindLastIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal)) + 1, $"settlement {PrototypeHome.Nyon} {moved} 0 0");
            lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("stock ", StringComparison.Ordinal)) + 1, Enum.GetValues<Resource>().Select(r => $"stock {PrototypeHome.Nyon} {r} 0"));
        }

        foreach (var (resource, units) in stock ?? [])
        {
            lines[lines.FindIndex(l => l.StartsWith($"stock home {resource} ", StringComparison.Ordinal))] = $"stock home {resource} {Quantity.Units(units)}";
        }

        if (bare)
        {
            lines.RemoveAll(l => l.StartsWith("building ", StringComparison.Ordinal) && !l.StartsWith("building Storehouse ", StringComparison.Ordinal));
            lines.RemoveAll(l => l.StartsWith("forest ", StringComparison.Ordinal));
        }

        lines.InsertRange(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, buildings ?? []);
        var reader = new StateReader(string.Join('\n', lines) + "\n");
        var read = World.Read(reader, world.Map);
        reader.End();
        return read;
    }

    /// <summary>Lets the world reach the dawn of the <paramref name="day"/>th day after the village was written and returns its events.</summary>
    public static List<GameEvent> Dawn(World world, int day = 1)
    {
        var events = world.AdvanceTo(At($"2026-10-{15 + day} 07:00", 2)).ToList();
        Assert.Contains(events, e => e is DawnBroke);
        return events;
    }

    /// <summary>A building as the state writes it, a storehouse or a jetty with its one carrier when staffed, no other with a depot yet.</summary>
    public static string Line(BuildingKind kind, Hex tile, bool staffed = false) => Logistics.IsDepot(kind)
        ? $"building {kind} {tile.Q} {tile.R} 1 {(staffed ? 1 : 0)} 0 0 0 {(staffed ? 1 : 0)} 4 0 0 0 0 0 0"
        : $"building {kind} {tile.Q} {tile.R} 1 {(staffed ? 1 : 0)} 0 0 0 0 0 0 0 0 0 0 {ContentCatalog.Current.For(kind).Reserve}";
}
