using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Balance;

/// <summary>How a strategy chooses among the tiles where a building can go (economy-v1.md, section 3.1).</summary>
public enum Placement
{
    /// <summary>
    /// Where it yields the most that reaches a store, nearer the store among equals: rich ground near a depot; woods
    /// another woodcutter reaches count half, and a site whose depot must take one more carrier yields per villager it
    /// takes.
    /// </summary>
    Richest,

    /// <summary>A depot where it lifts the yield of the buildings around it the most, near the start when none gains.</summary>
    Depot,

    /// <summary>Plain land next to a depot near the start, for housing and processing.</summary>
    Village,

    /// <summary>A jetty near the island's rich ground, so that what is built there reaches it.</summary>
    RichGround,

    /// <summary>The first tile that takes it, nearest the start, without reading the map (the naive strategy).</summary>
    FirstFit,
}

/// <summary>The rules behind <see cref="Placement"/>; every candidate already passes the world's build check.</summary>
internal static class Placements
{
    public static Hex Choose(Placement placement, BuildingKind kind, IReadOnlyList<Hex> candidates, Sites sites, Hex start) => placement switch
    {
        Placement.Richest => Best(candidates, Richest(kind, sites)),
        Placement.Depot => Best(candidates, Depot(sites, start)),
        Placement.Village => Best(candidates, Village(sites, start)),
        Placement.RichGround => Best(candidates, RichGround(sites)),
        _ => Best(candidates, tile => [-tile.DistanceTo(start)]),
    };

    /// <summary>The candidate with the highest scores, compared in order; the first in map order among equals.</summary>
    private static Hex Best(IReadOnlyList<Hex> candidates, Func<Hex, long[]> score)
    {
        var best = candidates[0];
        long[] top = score(best);
        for (int i = 1; i < candidates.Count; i++)
        {
            long[] next = score(candidates[i]);
            if (Compare(next, top) > 0 || (Compare(next, top) == 0 && candidates[i] < best))
            {
                (best, top) = (candidates[i], next);
            }
        }

        return best;
    }

    private static int Compare(long[] a, long[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            int order = a[i].CompareTo(b[i]);
            if (order != 0)
            {
                return order;
            }
        }

        return 0;
    }

    /// <remarks>
    /// A site whose goods make its depot take one more carrier takes that villager from those who build (D-109), so it
    /// yields per villager it takes, staff and carriers; otherwise more Exploration, which shows richer ground further
    /// away, would lure the bot to a site whose carrier takes the last builder and holds up the building queue (TASK-097).
    /// </remarks>
    private static Func<Hex, long[]> Richest(BuildingKind kind, Sites sites)
    {
        var depots = sites.PlannedDepots();
        int[] steps = sites.StepsFrom(depots);
        int[][] each = [.. depots.Select(depot => sites.StepsFrom([depot]))];
        var fells = sites.RecipeOf(kind) is { ForestTiles: > 0 } recipe ? recipe : null;
        var felled = fells is not null ? FelledByOthers(sites) : [];
        int staff = Math.Max(1, sites.StaffOf(kind));
        return tile =>
        {
            long yield = fells is not null ? SharedWoodYield(fells, tile, sites, felled) : sites.DailyYield(kind, tile, planning: true);
            int reaches = Sites.DistancePercent(sites.StepsTo(tile, steps), road: false);
            int carriers = sites.MoreCarriers(kind, tile, depots, each);

            // As much from nearer the store is better: more of what it makes later, such as at level 2, reaches it.
            return [yield * reaches / 100 * staff / (staff + carriers), reaches];
        };
    }

    /// <summary>
    /// Forest tiles that standing or queued buildings that fell reach. A woodcutter fells the fullest of its woods, so
    /// over the days it takes from all of them, not only from the ones it fells now.
    /// </summary>
    private static HashSet<Hex> FelledByOthers(Sites sites)
    {
        var felled = new HashSet<Hex>();
        foreach (var (kind, tile) in sites.Producers())
        {
            if (sites.RecipeOf(kind) is { ForestTiles: > 0 })
            {
                felled.UnionWith(sites.Woods(tile, planning: true));
            }
        }

        return felled;
    }

    /// <summary>A woodcutter's daily timber when the woods it shares with another woodcutter give it half.</summary>
    private static long SharedWoodYield(Recipe recipe, Hex tile, Sites sites, HashSet<Hex> felled)
    {
        long yield = 0;
        foreach (var wood in sites.Woods(tile, planning: true).Take(recipe.ForestTiles))
        {
            long timber = sites.DailyYieldOfWood(recipe, wood);
            yield += felled.Contains(wood) ? timber / 2 : timber;
        }

        return yield;
    }

    private static Func<Hex, long[]> Depot(Sites sites, Hex start)
    {
        int[] now = sites.StepsFrom(sites.PlannedDepots());
        var producers = sites.Producers();
        return tile =>
        {
            int[] with = sites.StepsFrom([tile]);
            long gain = 0;
            foreach (var (kind, at) in producers)
            {
                int before = sites.StepsTo(at, now);
                int after = Math.Min(before, sites.StepsTo(at, with));
                long yield = sites.DailyYield(kind, at, planning: true) * sites.LevelPercent(at) / 100;
                gain += yield * (Sites.DistancePercent(after, sites.HasRoad(at)) - Sites.DistancePercent(before, sites.HasRoad(at))) / 100;
            }

            return [gain, -sites.TerrainRank(tile), -tile.DistanceTo(start)];
        };
    }

    private static Func<Hex, long[]> Village(Sites sites, Hex start)
    {
        int[] steps = sites.StepsFrom(sites.PlannedDepots());
        return tile => [Sites.DistancePercent(sites.StepsTo(tile, steps), road: false), -sites.TerrainRank(tile), -tile.DistanceTo(start)];
    }

    private static Func<Hex, long[]> RichGround(Sites sites) => tile =>
    {
        int[] steps = sites.StepsFrom([tile]);
        string? island = sites.IslandOf(tile);
        long score = 0;
        foreach (var rich in sites.RichTiles(island))
        {
            score += (long)sites.Map.RichnessAt(rich) * Sites.DistancePercent(sites.StepsTo(rich, steps), road: false);
        }

        return [score];
    };
}
