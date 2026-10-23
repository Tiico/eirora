using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Balance;

/// <summary>
/// The balance measures of economy v1, section 7 (economy_v1.py, daily_output and value), with what the carriers bring to
/// the store as economy v2 counts it (economy_v2.py, daily_output). They exist only for balance tests and are never shown
/// to the player. Amounts are whole millionths (<see cref="Quantity.PerUnit"/>).
/// </summary>
internal static class Measures
{
    private const int VillagerValue = 15;

    /// <summary>
    /// Weighted production capacity a game day: what each staffed building makes at its level, without workdays, whether
    /// or not its inputs last, times the share of it that its depot's carriers bring to the store.
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

    /// <summary>
    /// What a standing building on <paramref name="tile"/> delivers a game day at its level: all it makes when its depot's
    /// carriers keep up, and their share of it when they do not (economy-v2.md, section 7.1).
    /// </summary>
    public static long Delivered(World world, Sites sites, BuildingKind kind, Hex tile)
    {
        var trip = world.TripFrom(tile);
        if (!trip.Reaches)
        {
            return 0;
        }

        int level = world.BuildingAt(tile) is { Level: 2 } ? 150 : 100;
        return sites.DailyYield(kind, tile, planning: false) * level / 100 * CarriedShare(world, trip.Depot) / Quantity.PerUnit;
    }

    /// <summary>
    /// The share of what a depot's buildings make that its carriers bring to the store, in millionths: its carriers'
    /// hours against the carrier-hours its buildings need, without a workday, as the depot plans them.
    /// </summary>
    public static long CarriedShare(World world, Hex depot)
    {
        long need = world.CarrierNeed(depot);
        long hours = Quantity.Units(world.BuildingAt(depot)?.Carriers ?? 0);
        return need <= hours ? Quantity.PerUnit : hours * Quantity.PerUnit / need;
    }

    /// <summary>
    /// The carrier-hours the depots' buildings need an hour against the carriers' hours, as far as the carriers have them,
    /// in millionths: what share of the carriers' time goes to carrying (economy-v2.md, section 7.1).
    /// </summary>
    public static (long Used, long Hours) CarrierUse(World world)
    {
        long used = 0, hours = 0;
        foreach (var depot in world.Buildings)
        {
            if (Logistics.IsDepot(depot.Kind) && depot.Carriers > 0)
            {
                long have = Quantity.Units(depot.Carriers);
                used += Math.Min(world.CarrierNeed(depot.Tile), have);
                hours += have;
            }
        }

        return (used, hours);
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
