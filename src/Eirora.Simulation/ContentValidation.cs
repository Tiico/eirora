using System.Collections.Generic;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation
{
    /// <summary>
    /// The content validator (INV-R4, D-43; economy-v2.md, section 3), which CI runs over the catalog: every need can be
    /// made without forces in at least two ways that start on different land, and no need takes its own tier to make, so
    /// no tier can lock itself out.
    /// </summary>
    /// <remarks>
    /// A way to make a good is its chain: the first building in the catalog that makes it and, for processing, the chain
    /// of each raw material. It is made without forces when every building in it costs no Build Power or can be built by
    /// hand (D-65). Without forces there is no sawmill, so planks come only from the start: a chain counts only when its
    /// planks fit in what the start leaves beside the cheapest quarry.
    /// </remarks>
    public static class ContentValidation
    {
        /// <summary>What breaks INV-R4 in <paramref name="content"/>, one line each; none when it holds.</summary>
        public static IReadOnlyList<string> Problems(ContentCatalog content)
        {
            var problems = new List<string>();
            int spare = Settlement.StartingPlanks - Cheapest(content, Resource.Stone);
            foreach (var need in content.Needs)
            {
                var lowest = LowestTier(content, need.Need);
                if (lowest is null)
                {
                    continue;
                }

                var free = new List<Chain>();
                foreach (var good in need.Goods)
                {
                    if (Of(content, good, new HashSet<Resource>()) is not Chain chain)
                    {
                        problems.Add($"{need.Need}: nothing in the catalog makes {good}.");
                        continue;
                    }

                    if ((int)chain.Tier > (lowest == PopulationTier.Crofters ? 0 : (int)lowest - 1))
                    {
                        problems.Add($"{need.Need}: {good} takes {chain.Tier} to make, a tier that needs it.");
                    }

                    if (chain.WithoutForces && chain.Planks <= spare)
                    {
                        free.Add(chain);
                    }
                }

                bool apart = false;
                for (int a = 0; a < free.Count; a++)
                {
                    for (int b = a + 1; b < free.Count; b++)
                    {
                        apart |= (free[a].Land & free[b].Land) == 0;
                    }
                }

                if (!apart)
                {
                    problems.Add($"{need.Need}: fewer than two ways to make it without forces that start on different land.");
                }
            }

            return problems;
        }

        /// <summary>The lowest tier with the need, or null when no tier has it.</summary>
        private static PopulationTier? LowestTier(ContentCatalog content, Need need)
        {
            foreach (var tier in content.Tiers)
            {
                if (tier.PerThousand(need) > 0)
                {
                    return tier.Tier;
                }
            }

            return null;
        }

        /// <summary>The planks the cheapest building that makes a good costs; the start's all when nothing makes it.</summary>
        private static int Cheapest(ContentCatalog content, Resource good)
        {
            int cheapest = Settlement.StartingPlanks;
            foreach (var kind in content.Buildings)
            {
                var spec = content.For(kind);
                if (spec.Recipe?.Product == good)
                {
                    int planks = spec.Cost.TryGetValue(Resource.Planks, out int cost) ? cost : 0;
                    cheapest = planks < cheapest ? planks : cheapest;
                }
            }

            return cheapest;
        }

        /// <summary>The chain that makes a good, or null when nothing does or it is made of itself.</summary>
        private static Chain? Of(ContentCatalog content, Resource good, HashSet<Resource> making)
        {
            if (!making.Add(good))
            {
                return null;
            }

            foreach (var kind in content.Buildings)
            {
                var spec = content.For(kind);
                if (spec.Recipe is not Recipe recipe || recipe.Product != good)
                {
                    continue;
                }

                var chain = new Chain(
                    recipe.Inputs.Count == 0 ? spec.Requirement : TileUse.None,
                    spec.BuildPower == 0 || spec.HandBuild,
                    spec.Cost.TryGetValue(Resource.Planks, out int planks) ? planks : 0,
                    spec.Tier);
                foreach (var input in recipe.Inputs)
                {
                    if (Of(content, input.Resource, making) is not Chain raw)
                    {
                        return null;
                    }

                    chain = new Chain(chain.Land | raw.Land, chain.WithoutForces && raw.WithoutForces, chain.Planks + raw.Planks, chain.Tier > raw.Tier ? chain.Tier : raw.Tier);
                }

                making.Remove(good);
                return chain;
            }

            return null;
        }

        /// <summary>A way to make a good: the land it starts on, whether forces are needed, the planks it costs and the highest tier that staffs it.</summary>
        private readonly struct Chain
        {
            public Chain(TileUse land, bool withoutForces, int planks, PopulationTier tier)
            {
                Land = land;
                WithoutForces = withoutForces;
                Planks = planks;
                Tier = tier;
            }

            public TileUse Land { get; }

            public bool WithoutForces { get; }

            public int Planks { get; }

            public PopulationTier Tier { get; }
        }
    }
}
