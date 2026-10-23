using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Balance;

/// <summary>
/// The balance measures of economy v1, section 7 (economy_v1.py, daily_output and value). They exist only for balance
/// tests and are never shown to the player. Amounts are whole millionths (<see cref="Quantity.PerUnit"/>).
/// </summary>
internal static class Measures
{
    private const int VillagerValue = 15;

    /// <summary>
    /// Weighted production capacity a game day: what each staffed building makes at its level and distance, without
    /// workdays, whether or not its inputs last.
    /// </summary>
    public static long DailyOutput(World world)
    {
        var sites = new Sites(world);
        long total = 0;
        foreach (var building in world.Buildings)
        {
            if (building.Staffed)
            {
                total += Delivered(world, sites, building.Kind, building.Tile);
            }
        }

        return total;
    }

    /// <summary>What a standing building on <paramref name="tile"/> delivers a game day at its level and distance.</summary>
    public static long Delivered(World world, Sites sites, BuildingKind kind, Hex tile)
    {
        var building = world.BuildingAt(tile);
        int level = building is { Level: 2 } ? 150 : 100;
        int distance = Sites.DistancePercent(world.DepotDistance(tile), building?.HasRoad ?? false);
        return sites.DailyYield(kind, tile, planning: false) * level / 100 * distance / 100;
    }

    /// <summary>
    /// The development value: villagers × 15, half the weighted stock at home and three days of production
    /// (economy-v1.md, section 7).
    /// </summary>
    public static long Value(World world)
    {
        long stock = 0;
        foreach (var resource in Enum.GetValues<Resource>())
        {
            stock += Sites.Weights[(int)resource] * Quantity.Units(world.Stock(resource));
        }

        return Quantity.Units(world.Villagers * VillagerValue) + (stock / 2) + (3 * DailyOutput(world));
    }
}
