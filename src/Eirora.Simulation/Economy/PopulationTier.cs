using System;
using System.Collections.Generic;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The step a villager has reached (GLOSSARY: Befolkningsnivå; D-105): its housing, its needs and the buildings it
    /// can staff (economy-v2.md, section 2). A villager rises one tier at a time and never goes back (D-39, D-115).
    /// The craftsmen come with step 2 (TASK-062).
    /// </summary>
    public enum PopulationTier
    {
        /// <summary>Every villager starts as a crofter, lives in cottages and longhouses and needs only food (torpare).</summary>
        Crofters,

        /// <summary>Crofters rise to farmers with clothes; they live in farmsteads (bönder; step 1).</summary>
        Farmers,
    }

    /// <summary>What a tier uses up every dawn (GLOSSARY: Behov), each met by more than one good (INV-R4).</summary>
    public enum Need
    {
        /// <summary>The basic need of every tier, one a villager (GLOSSARY: Basbehov).</summary>
        Food,

        /// <summary>Wadmal or hide, for farmers (kläder).</summary>
        Clothes,
    }

    /// <summary>A need and the goods that meet it, which are taken from the one the store holds most of (economy-v2.md, section 2).</summary>
    public sealed class NeedSpec
    {
        private readonly Resource[] _goods;

        internal NeedSpec(Need need, params Resource[] goods)
        {
            if (goods.Length == 0)
            {
                throw new ArgumentException("A need is met by at least one good.", nameof(goods));
            }

            Need = need;
            _goods = (Resource[])goods.Clone();
            Goods = Array.AsReadOnly(_goods);
        }

        public Need Need { get; }

        /// <summary>The goods that meet it, in the order a tie between them is broken.</summary>
        public IReadOnlyList<Resource> Goods { get; }

        /// <summary><see cref="Goods"/> for the simulation's every dawn and quarter hour, without allocating.</summary>
        internal ReadOnlySpan<Resource> GoodSpan => _goods;
    }

    /// <summary>
    /// A population tier's needs and the unlocked step that makes it reachable (economy-v2.md, sections 2 and 5): what each
    /// villager of the tier uses up a dawn, in thousandths, rounded up for the settlement.
    /// </summary>
    public sealed class TierSpec
    {
        internal TierSpec(PopulationTier tier, int step, params (Need Need, int PerThousand)[] needs)
        {
            Tier = tier;
            Step = step;
            Needs = Array.AsReadOnly((TierNeed[])Array.ConvertAll(needs, n => new TierNeed(n.Need, n.PerThousand)));
            if (step < 0)
            {
                throw new ArgumentException("A tier is reached from the start or at an unlocked step.", nameof(step));
            }
        }

        public PopulationTier Tier { get; }

        /// <summary>The unlocked step from which villagers can rise to it; 0 for the crofters, who are there from the start.</summary>
        public int Step { get; }

        /// <summary>What each villager of the tier uses up at dawn.</summary>
        public IReadOnlyList<TierNeed> Needs { get; }

        /// <summary>Thousandths of a need each villager of the tier uses up a dawn; 0 when the tier does not have it.</summary>
        public int PerThousand(Need need)
        {
            foreach (var tierNeed in Needs)
            {
                if (tierNeed.Need == need)
                {
                    return tierNeed.PerThousand;
                }
            }

            return 0;
        }

        /// <summary>Whole units of a need that <paramref name="villagers"/> of the tier use up a dawn, rounded up.</summary>
        public int Uses(Need need, int villagers) => (int)(((long)villagers * PerThousand(need) + 999) / 1000);
    }

    /// <summary>One of a tier's needs: thousandths of it that each villager uses up a dawn.</summary>
    public readonly struct TierNeed
    {
        internal TierNeed(Need need, int perThousand)
        {
            if (perThousand < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(perThousand), perThousand, "A need is more than nothing.");
            }

            Need = need;
            PerThousand = perThousand;
        }

        public Need Need { get; }

        public int PerThousand { get; }
    }

    /// <summary>
    /// An unlocked step (GLOSSARY: Upplåsning; D-105, D-114): it opens to the whole world at the dawn when any settlement
    /// has <see cref="Villagers"/>, and is never locked again (INV-V1).
    /// </summary>
    public sealed class UnlockSpec
    {
        internal UnlockSpec(int step, int villagers)
        {
            if (step < 1 || villagers < 1)
            {
                throw new ArgumentException("A step after the start opens at a settlement of some size.");
            }

            Step = step;
            Villagers = villagers;
        }

        /// <summary>Its number, from 1; step 0 is what a world has from the start.</summary>
        public int Step { get; }

        /// <summary>The villagers one settlement must have.</summary>
        public int Villagers { get; }
    }
}
