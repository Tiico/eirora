using System;
using System.Collections.Generic;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// What a producing building makes and from what (economy-v1.md, section 3): its raw materials, its product and its
    /// rate. Distance, level 2 and a workday scale every recipe; a full store stops it and nothing is lost (D-17).
    /// </summary>
    /// <remarks>
    /// A recipe is one of four kinds. It gathers from its own tile; it processes raw materials and leaves the reserve
    /// (LOG-06); it is a slow process that grows day and night and is harvested in the active hours (INV-T5); or it
    /// fells the forest around it, which grows back (the forest is a rule of its own).
    /// </remarks>
    public sealed class Recipe
    {
        /// <summary>Quarter-hour ticks in an hour, the step a rate is divided into.</summary>
        internal const long TicksPerHour = 3600 / Settlement.TickSeconds;

        internal Recipe(
            Resource product,
            long perHour,
            bool richness = false,
            IReadOnlyList<Ingredient>? inputs = null,
            long growsPerHour = 0,
            int forestTiles = 0)
        {
            Product = product;
            PerHour = perHour;
            Richness = richness;
            Inputs = inputs ?? Array.Empty<Ingredient>();
            GrowsPerHour = growsPerHour;
            ForestTiles = forestTiles;

            if (perHour <= 0 || perHour % TicksPerHour != 0 || growsPerHour < 0 || growsPerHour % TicksPerHour != 0 || forestTiles < 0)
            {
                throw new ArgumentException("A rate is positive millionths an hour that split evenly into quarter hours.");
            }

            int kinds = (Inputs.Count > 0 ? 1 : 0) + (growsPerHour > 0 ? 1 : 0) + (forestTiles > 0 ? 1 : 0);
            if (kinds > 1)
            {
                throw new ArgumentException("A recipe processes, grows or fells, never more than one of them.");
            }

            // Processing checks the reserve of each raw material on its own and before the product is added, so a raw
            // material listed twice, or one that is also the product, would take the store below the reserve (LOG-06).
            for (int i = 0; i < Inputs.Count; i++)
            {
                for (int j = i + 1; j < Inputs.Count; j++)
                {
                    if (Inputs[i].Resource == Inputs[j].Resource)
                    {
                        throw new ArgumentException($"{Inputs[i].Resource} is a raw material of the recipe twice.", nameof(inputs));
                    }
                }

                if (Inputs[i].Resource == product)
                {
                    throw new ArgumentException($"{product} is both the product and a raw material of the recipe.", nameof(inputs));
                }
            }
        }

        public Resource Product { get; }

        /// <summary>
        /// Millionths of a unit of the product an active hour at level 1, beside a depot and without a workday: what a
        /// slow process harvests at most, and what a woodcutter fells from each forest tile.
        /// </summary>
        public long PerHour { get; }

        /// <summary>
        /// Whether richness scales the recipe: its own tile's, or how fast a slow process grows, or each forest tile's
        /// for a woodcutter.
        /// </summary>
        public bool Richness { get; }

        /// <summary>The raw materials used for each unit of the product; none for a recipe that gathers.</summary>
        public IReadOnlyList<Ingredient> Inputs { get; }

        /// <summary>Millionths of a unit that a slow process grows an hour, day and night; 0 for any other recipe.</summary>
        public long GrowsPerHour { get; }

        /// <summary>How many forest tiles it fells at once, the richest it reaches with timber left; 0 for any other recipe.</summary>
        public int ForestTiles { get; }

        public bool IsSlowProcess => GrowsPerHour > 0;

        internal long PerTick => PerHour / TicksPerHour;

        internal long GrowsPerTick => GrowsPerHour / TicksPerHour;
    }

    /// <summary>A raw material of a recipe: how many units of it one unit of the product takes.</summary>
    public readonly struct Ingredient
    {
        internal Ingredient(Resource resource, int perProduct)
        {
            if (perProduct <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(perProduct), perProduct, "An ingredient is at least one unit.");
            }

            Resource = resource;
            PerProduct = perProduct;
        }

        public Resource Resource { get; }

        public int PerProduct { get; }
    }
}
