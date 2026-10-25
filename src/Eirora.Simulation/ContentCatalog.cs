using System;
using System.Collections.Generic;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation
{
    /// <summary>
    /// The game's content at <see cref="ContentVersion"/>: what each building costs, needs and produces
    /// (economy-v1.md, section 3), the population tiers and their needs, and the steps that unlock them (economy-v2.md,
    /// sections 2–5). The settlement reads it instead of knowing any building's numbers, and the client and the server
    /// compile the same catalog from the same source (ADR-0004).
    /// </summary>
    /// <remarks>
    /// Content is only added: a new building bumps <see cref="ContentVersion"/>, and changing how existing content works
    /// bumps <see cref="RulesVersion"/> (ADR-0006). Rates are whole millionths of a unit an hour (INV-D3). The catalog's
    /// canonical text is checked in beside its tests, so that every change to it shows in review.
    /// </remarks>
    public sealed class ContentCatalog
    {
        private readonly Dictionary<BuildingKind, BuildingSpec> _specs = new();
        private readonly List<BuildingKind> _buildings = new();
        private readonly BuildingSpec?[] _byKind; // The specs by kind, which the simulation looks up for every building every quarter hour (#173).

        private ContentCatalog(
            int version,
            BuildingSpec upgrade,
            IEnumerable<(BuildingKind Kind, BuildingSpec Spec)> buildings,
            IReadOnlyList<NeedSpec> needs,
            IReadOnlyList<TierSpec> tiers,
            IReadOnlyList<UnlockSpec> steps)
        {
            Version = version;
            Upgrade = upgrade;
            Needs = needs;
            Tiers = tiers;
            Steps = steps;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].Tier != (PopulationTier)i || (i > 0 && tiers[i].Step <= tiers[i - 1].Step) || tiers[i].Step > steps.Count)
                {
                    throw new ArgumentException("The tiers come in order, each at a later step that the catalog has.", nameof(tiers));
                }
            }

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].Step != i + 1 || (int)steps[i].Tier >= tiers.Count)
                {
                    throw new ArgumentException("The steps are numbered from 1 in order, each asking for a tier the catalog has.", nameof(steps));
                }
            }

            for (int i = 0; i < needs.Count; i++)
            {
                if (needs[i].Need != (Need)i)
                {
                    throw new ArgumentException("The needs come in order, each once.", nameof(needs));
                }
            }

            foreach (var (kind, spec) in buildings)
            {
                if (_specs.ContainsKey(kind))
                {
                    throw new ArgumentException($"{kind} is in the catalog twice.", nameof(buildings));
                }

                if (spec.Step > steps.Count || (int)spec.Tier >= tiers.Count || (spec.Housing > 0 && (int)spec.Houses >= tiers.Count))
                {
                    throw new ArgumentException($"{kind} is unlocked by a step, or staffed or housing a tier, that the catalog lacks.", nameof(buildings));
                }

                _specs[kind] = spec;
                _buildings.Add(kind);
            }

            int kinds = 0;
            foreach (var kind in _buildings)
            {
                kinds = (int)kind >= kinds ? (int)kind + 1 : kinds;
            }

            _byKind = new BuildingSpec?[kinds];
            foreach (var kind in _buildings)
            {
                _byKind[(int)kind] = _specs[kind];
            }
        }

        /// <summary>The content this build ships.</summary>
        public static ContentCatalog Current { get; } = new(
            ContentVersion.Current,
            upgrade: new(Materials(planks: 10, stone: 5), workHours: 16, buildPower: 50, staff: 0, TileUse.None),
            new (BuildingKind, BuildingSpec)[]
            {
                (BuildingKind.Longhouse, new(Materials(timber: 10, planks: 15, stone: 10), workHours: 40, buildPower: 60, staff: 0, TileUse.Land, housing: 8)),
                (BuildingKind.Cottage, new(Materials(timber: 15, stone: 5), workHours: 32, buildPower: 0, staff: 0, TileUse.Land, housing: 4)),
                (BuildingKind.Storehouse, new(Materials(timber: 10, stone: 5), workHours: 16, buildPower: 0, staff: 1, TileUse.Land)),
                (BuildingKind.FishingHut, new(
                    Materials(timber: 8), workHours: 12, buildPower: 0, staff: 2, TileUse.FishingWater, maxLevel: 2,
                    recipe: new(Resource.Fish, perHour: 1_250_000, richness: true))),
                (BuildingKind.Field, new(
                    Materials(timber: 4), workHours: 8, buildPower: 0, staff: 2, TileUse.Farmland, maxLevel: 2,
                    recipe: new(Resource.Grain, perHour: 2_000_000, richness: true, growsPerHour: 500_000))),
                (BuildingKind.Woodcutter, new(
                    Materials(timber: 6), workHours: 8, buildPower: 0, staff: 2, TileUse.Forest, maxLevel: 2,
                    recipe: new(Resource.Timber, perHour: 500_000, richness: true, forestTiles: 2))),
                (BuildingKind.SheepFarm, new(
                    Materials(timber: 10, planks: 5), workHours: 12, buildPower: 0, staff: 1, TileUse.Pasture, maxLevel: 2,
                    recipe: new(Resource.Wool, perHour: 2_000_000, richness: true, growsPerHour: 400_000))),
                (BuildingKind.Quarry, new(
                    Materials(timber: 10, planks: 4), workHours: 16, buildPower: 0, staff: 2, TileUse.Mountain, maxLevel: 2,
                    recipe: new(Resource.Stone, perHour: 750_000, richness: true))),
                (BuildingKind.Sawmill, new(
                    Materials(timber: 15, stone: 5), workHours: 24, buildPower: 30, staff: 2, TileUse.Land, maxLevel: 2,
                    recipe: new(Resource.Planks, perHour: 1_000_000, inputs: new[] { new Ingredient(Resource.Timber, 2) }))),
                (BuildingKind.Weavery, new(
                    Materials(planks: 10, stone: 5), workHours: 24, buildPower: 60, staff: 2, TileUse.Land, maxLevel: 2,
                    recipe: new(Resource.Wadmal, perHour: 500_000, inputs: new[] { new Ingredient(Resource.Wool, 2) }), handBuild: true)),
                (BuildingKind.Jetty, new(Materials(timber: 15, planks: 10), workHours: 24, buildPower: 60, staff: 1, TileUse.HarborSpot, handBuild: true)),
                (BuildingKind.Lookout, new(Materials(stone: 10), workHours: 12, buildPower: 40, staff: 0, TileUse.Land)),

                // Step 1, Farmers (economy-v2.md, section 4).
                (BuildingKind.Farmstead, new(
                    Materials(timber: 20, stone: 10), workHours: 32, buildPower: 0, staff: 0, TileUse.Land, housing: 6, step: 1,
                    houses: PopulationTier.Farmers)),
                (BuildingKind.HuntingLodge, new(
                    Materials(timber: 8), workHours: 8, buildPower: 0, staff: 2, TileUse.Forest, maxLevel: 2, step: 1,
                    recipe: new(Resource.Game, perHour: 250_000, richness: true, huntTiles: 4))),
                (BuildingKind.Tannery, new(
                    Materials(timber: 10, stone: 5), workHours: 16, buildPower: 0, staff: 2, TileUse.Land, maxLevel: 2, step: 1,
                    recipe: new(Resource.Hide, perHour: 500_000, inputs: new[] { new Ingredient(Resource.Game, 2) }, reserve: 5))),

                // Step 2, Craftsmen (economy-v2.md, section 4).
                (BuildingKind.CraftsmensHouse, new(
                    Materials(timber: 20, stone: 20), workHours: 40, buildPower: 0, staff: 0, TileUse.Land, housing: 8, step: 2,
                    houses: PopulationTier.Craftsmen)),
                (BuildingKind.Apiary, new(
                    Materials(timber: 6), workHours: 8, buildPower: 0, staff: 1, TileUse.Meadow, maxLevel: 2, step: 2,
                    recipe: new(Resource.Honey, perHour: 2_000_000, richness: true, growsPerHour: 400_000))),
                (BuildingKind.Brewery, new(
                    Materials(timber: 15, stone: 5), workHours: 16, buildPower: 0, staff: 2, TileUse.Land, maxLevel: 2, step: 2,
                    tier: PopulationTier.Farmers, recipe: new(Resource.Ale, perHour: 500_000, inputs: new[] { new Ingredient(Resource.Grain, 2) }))),
                (BuildingKind.Meadery, new(
                    Materials(timber: 15, stone: 5), workHours: 24, buildPower: 0, staff: 2, TileUse.Land, maxLevel: 2, step: 2,
                    tier: PopulationTier.Farmers, recipe: new(Resource.Mead, perHour: 500_000, inputs: new[] { new Ingredient(Resource.Honey, 1) }))),
            },
            needs: new[]
            {
                new NeedSpec(Need.Food, Resource.Fish, Resource.Game, Resource.Grain),
                new NeedSpec(Need.Clothes, Resource.Wadmal, Resource.Hide),
                new NeedSpec(Need.Drink, Resource.Mead, Resource.Ale),
            },
            tiers: new[]
            {
                new TierSpec(PopulationTier.Crofters, step: 0, builds: 1, (Need.Food, 1000)),
                new TierSpec(PopulationTier.Farmers, step: 1, builds: 1, (Need.Food, 1000), (Need.Clothes, 500)),
                new TierSpec(PopulationTier.Craftsmen, step: 2, builds: 2, (Need.Food, 1000), (Need.Clothes, 500), (Need.Drink, 500)),
            },
            steps: new[]
            {
                new UnlockSpec(1, villagers: 20),
                new UnlockSpec(2, villagers: 30, PopulationTier.Farmers, ofTier: 6),
            });

        public int Version { get; }

        /// <summary>Every building in the catalog, in the order it lists them.</summary>
        public IReadOnlyList<BuildingKind> Buildings => _buildings;

        /// <summary>Upgrading any producing building to level 2: +50 % production.</summary>
        public BuildingSpec Upgrade { get; }

        /// <summary>Every need, in the order of <see cref="Need"/>, with the goods that meet it.</summary>
        public IReadOnlyList<NeedSpec> Needs { get; }

        /// <summary>Every population tier, in the order of <see cref="PopulationTier"/>, each at a later step.</summary>
        public IReadOnlyList<TierSpec> Tiers { get; }

        /// <summary>The steps after the start, in order, which open one after the other (D-105).</summary>
        public IReadOnlyList<UnlockSpec> Steps { get; }

        public NeedSpec For(Need need) => Needs[(int)need];

        public TierSpec For(PopulationTier tier) => Tiers[(int)tier];

        public BuildingSpec For(BuildingKind kind) =>
            (uint)kind < (uint)_byKind.Length && _byKind[(int)kind] is BuildingSpec spec
                ? spec
                : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building.");

        /// <summary>This catalog with other needs, as the content validator's tests give it (INV-R4).</summary>
        internal ContentCatalog WithNeeds(IReadOnlyList<NeedSpec> needs)
        {
            var buildings = new List<(BuildingKind, BuildingSpec)>();
            foreach (var known in _buildings)
            {
                buildings.Add((known, _specs[known]));
            }

            return new ContentCatalog(Version, Upgrade, buildings, needs, Tiers, Steps);
        }

        /// <summary>This catalog with a building's spec replaced, as the content validator's tests give it (INV-R4).</summary>
        internal ContentCatalog Replacing(BuildingKind kind, BuildingSpec spec)
        {
            var buildings = new List<(BuildingKind, BuildingSpec)>();
            foreach (var known in _buildings)
            {
                buildings.Add((known, known == kind ? spec : _specs[known]));
            }

            return new ContentCatalog(Version, Upgrade, buildings, Needs, Tiers, Steps);
        }

        /// <summary>This catalog with one building more, as content is added (ADR-0006).</summary>
        internal ContentCatalog With(BuildingKind kind, BuildingSpec spec)
        {
            var buildings = new List<(BuildingKind, BuildingSpec)>();
            foreach (var known in _buildings)
            {
                buildings.Add((known, _specs[known]));
            }

            buildings.Add((kind, spec));
            return new ContentCatalog(Version, Upgrade, buildings, Needs, Tiers, Steps);
        }

        /// <summary>The catalog in canonical text, one record per line, so that a change to it is plain to see.</summary>
        internal string ToText()
        {
            var writer = new StateWriter();
            writer.Line("content").Long(Version);
            Write(writer.Line("upgrade"), Upgrade);
            foreach (var kind in _buildings)
            {
                Write(writer.Line("building").Enum(kind), _specs[kind]);
            }

            foreach (var need in Needs)
            {
                var line = writer.Line("need").Enum(need.Need);
                foreach (var good in need.Goods)
                {
                    line.Enum(good);
                }
            }

            foreach (var tier in Tiers)
            {
                writer.Line("tier").Enum(tier.Tier).Text("step").Long(tier.Step).Text("builds").Long(tier.Builds);
                foreach (var need in tier.Needs)
                {
                    writer.Line("uses").Enum(need.Need).Text("per-thousand").Long(need.PerThousand);
                }
            }

            foreach (var step in Steps)
            {
                writer.Line("step").Long(step.Step).Text("villagers").Long(step.Villagers).Text("of").Enum(step.Tier).Long(step.OfTier);
            }

            return writer.ToString();
        }

        private static void Write(StateWriter writer, BuildingSpec spec)
        {
            // Each number is named, so that the checked-in text reads like the table in economy-v1.md.
            writer.Text("work").Long(spec.WorkHours).Text("power").Long(spec.BuildPower).Text("staff").Long(spec.Staff)
                .Text("site").Enum(spec.Requirement).Text("housing").Long(spec.Housing).Text("levels").Long(spec.MaxLevel)
                .Text("step").Long(spec.Step).Text("tier").Enum(spec.Tier).Text("houses").Enum(spec.Houses).Text("hand-build").Bool(spec.HandBuild);
            foreach (Resource resource in Enum.GetValues(typeof(Resource)))
            {
                if (spec.Cost.TryGetValue(resource, out int amount))
                {
                    writer.Line("cost").Enum(resource).Long(amount);
                }
            }

            if (spec.Recipe is Recipe recipe)
            {
                writer.Line("recipe").Enum(recipe.Product).Text("per-hour").Long(recipe.PerHour).Text("richness").Bool(recipe.Richness)
                    .Text("grows-per-hour").Long(recipe.GrowsPerHour).Text("forest-tiles").Long(recipe.ForestTiles)
                    .Text("hunt-tiles").Long(recipe.HuntTiles).Text("reserve").Long(recipe.Reserve);
                foreach (var input in recipe.Inputs)
                {
                    writer.Line("input").Enum(input.Resource).Long(input.PerProduct);
                }
            }
        }

        private static Dictionary<Resource, int> Materials(int timber = 0, int planks = 0, int stone = 0)
        {
            var cost = new Dictionary<Resource, int>();
            if (timber > 0)
            {
                cost[Resource.Timber] = timber;
            }

            if (planks > 0)
            {
                cost[Resource.Planks] = planks;
            }

            if (stone > 0)
            {
                cost[Resource.Stone] = stone;
            }

            return cost;
        }
    }
}
