using System.Collections.Generic;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// What the prototype's world holds besides its map (economy-v1.md): the start village on Hemön, the two surveys and
    /// the expedition to Nyön, until world generation gives every world its own (TASK-033).
    /// </summary>
    public static class PrototypeIsland
    {
        /// <summary>The start village's storehouse, on the map's start.</summary>
        public static readonly Hex Settlement = Hex.Origin;

        /// <summary>The buildings a new world starts with (economy-v1.md, section 4).</summary>
        public static IReadOnlyList<(BuildingKind Kind, Hex Tile)> StartBuildings { get; } = new[]
        {
            (BuildingKind.Storehouse, Settlement),
            (BuildingKind.FishingHut, new Hex(1, 0)),
            (BuildingKind.Field, new Hex(0, 1)),
            (BuildingKind.Woodcutter, new Hex(1, -1)),
        };

        /// <summary>The Exploration an expedition costs before a lookout's discount (economy-v1.md, section 6).</summary>
        public const int ExpeditionCost = 250;

        /// <summary>Hours of travel with the villagers awake; the crew rests at night like everyone else (INV-T4).</summary>
        public const int ExpeditionHours = 32;

        /// <summary>A tile of the island the first expedition finds: Nyön's beach.</summary>
        public static readonly Hex ExpeditionFinds = PrototypeHome.NyonCentre;

        /// <summary>
        /// What each survey lifts the fog from, in order, and what it costs in Exploration (economy-v1.md, section 6):
        /// first the stone vein and the old-growth forest's two tiles, then the rich fishing water and more farmland.
        /// </summary>
        public static IReadOnlyList<(int Cost, IReadOnlyList<Hex> Reveals)> Surveys { get; } = new (int, IReadOnlyList<Hex>)[]
        {
            (60, new[] { new Hex(3, -1), new Hex(-2, 0), new Hex(-3, 1) }),
            (120, new[] { new Hex(-3, 3), new Hex(-1, 2) }),
        };
    }
}
