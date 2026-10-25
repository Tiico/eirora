using System;
using System.Collections.Generic;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation
{
    /// <summary>
    /// The game's content at <see cref="ContentVersion"/>: what each building costs, needs and produces
    /// (economy-v1.md, section 3). The settlement reads it instead of knowing any building's numbers, and the client
    /// and the server compile the same catalog from the same source (ADR-0004).
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

        private ContentCatalog(int version, BuildingSpec upgrade, IEnumerable<(BuildingKind Kind, BuildingSpec Spec)> buildings)
        {
            Version = version;
            Upgrade = upgrade;
            foreach (var (kind, spec) in buildings)
            {
                if (_specs.ContainsKey(kind))
                {
                    throw new ArgumentException($"{kind} is in the catalog twice.", nameof(buildings));
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
                    recipe: new(Resource.Wadmal, perHour: 500_000, inputs: new[] { new Ingredient(Resource.Wool, 2) }))),
                (BuildingKind.Jetty, new(Materials(timber: 15, planks: 10), workHours: 24, buildPower: 60, staff: 1, TileUse.HarborSpot)),
                (BuildingKind.Lookout, new(Materials(stone: 10), workHours: 12, buildPower: 40, staff: 0, TileUse.Land)),
            });

        public int Version { get; }

        /// <summary>Every building in the catalog, in the order it lists them.</summary>
        public IReadOnlyList<BuildingKind> Buildings => _buildings;

        /// <summary>Upgrading any producing building to level 2: +50 % production.</summary>
        public BuildingSpec Upgrade { get; }

        public BuildingSpec For(BuildingKind kind) =>
            (uint)kind < (uint)_byKind.Length && _byKind[(int)kind] is BuildingSpec spec
                ? spec
                : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building.");

        /// <summary>This catalog with one building more, as content is added (ADR-0006).</summary>
        internal ContentCatalog With(BuildingKind kind, BuildingSpec spec)
        {
            var buildings = new List<(BuildingKind, BuildingSpec)>();
            foreach (var known in _buildings)
            {
                buildings.Add((known, _specs[known]));
            }

            buildings.Add((kind, spec));
            return new ContentCatalog(Version, Upgrade, buildings);
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

            return writer.ToString();
        }

        private static void Write(StateWriter writer, BuildingSpec spec)
        {
            // Each number is named, so that the checked-in text reads like the table in economy-v1.md.
            writer.Text("work").Long(spec.WorkHours).Text("power").Long(spec.BuildPower).Text("staff").Long(spec.Staff)
                .Text("site").Enum(spec.Requirement).Text("housing").Long(spec.Housing).Text("levels").Long(spec.MaxLevel);
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
                    .Text("grows-per-hour").Long(recipe.GrowsPerHour).Text("forest-tiles").Long(recipe.ForestTiles);
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
