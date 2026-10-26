using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;

namespace Eirora.Balance;

/// <summary>
/// The systems a player meets, as the economy model counts them for the first week (economy-v2.md, section 5, H4): the
/// tutorial and building and staffing meet everyone and are not counted.
/// </summary>
internal static class Systems
{
    public const string Fog = "Dimman";
    public const string Expedition = "Expedition";
    public const string Processing = "Förädling och minimilager";
    public const string SeaRoute = "Sjöväg och ny bosättning";
    public const string CarriersBehind = "Bärarna hinner inte";
    public const string Farmers = "Bönder";
    public const string Craftsmen = "Hantverkare";
    public const string Seafaring = "Sjöfart";

    /// <summary>
    /// Notes each system the world shows the player, on the first game day it does; read after the dawn's commands and
    /// at the end of the day, since what it shows only grows within a day.
    /// </summary>
    public static void Note(World world, int day, Tally tally)
    {
        var met = tally.SystemsMet;
        Meet(Fog, tally.Decisions.Any(d => d.Command is Survey or LiftFog));
        Meet(Expedition, world.Buildings.Any(b => b.Kind == BuildingKind.Jetty && b.Island == world.HomeIsland));
        Meet(Processing, world.Buildings.Any(b => world.Content.For(b.Kind).Recipe is { Inputs.Count: > 0 }));
        Meet(SeaRoute, world.SeaRoutes.Count > 0 && world.Settlements.Any(s => s.Island != world.HomeIsland));
        Meet(CarriersBehind, world.Diagnose().Depots.Any(d => d.SharePercent < 100));
        Meet(Farmers, world.Step >= 1);
        Meet(Craftsmen, world.Step >= 2);
        Meet(Seafaring, world.Step >= 3);

        void Meet(string system, bool now)
        {
            if (now)
            {
                met.TryAdd(system, day);
            }
        }
    }
}
