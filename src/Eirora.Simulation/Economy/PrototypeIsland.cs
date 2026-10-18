using System.Collections.Generic;

namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The hand-made home island of the prototype (Hemön in economy-v1.md and its model), until world generation
    /// arrives in M3.
    /// </summary>
    public static class PrototypeIsland
    {
        public const string Settlement = "S";

        public static IReadOnlyList<Site> Sites { get; } = new[]
        {
            new Site(Settlement, 0, 0, SiteKind.Land),
            new Site("L1", -1, 0, SiteKind.Land),
            new Site("L2", 0, -1, SiteKind.Land),
            new Site("L3", -1, 1, SiteKind.Land),
            new Site("E1", 2, -1, SiteKind.Land),
            new Site("E2", 2, -2, SiteKind.Land),
            new Site("K1", 1, 0, SiteKind.FishingWater),
            new Site("K2", -3, 3, SiteKind.FishingWater, hidden: true, richnessPercent: 150),
            new Site("H1", 1, 1, SiteKind.HarborSpot),
            new Site("O1", 0, 1, SiteKind.Farmland),
            new Site("O2", -1, 2, SiteKind.Farmland, hidden: true),
            new Site("F1", 1, -1, SiteKind.Forest, forestTiles: 1),
            new Site("F2", 3, -2, SiteKind.Forest, forestTiles: 3),
            new Site("F3", -2, 0, SiteKind.Forest, hidden: true, forestTiles: 4, richnessPercent: 125),
            new Site("M1", -4, 0, SiteKind.Mountain),
            new Site("M2", 3, -1, SiteKind.Mountain, hidden: true, richnessPercent: 200),
            new Site("P1", -2, -1, SiteKind.Pasture),

            // Nyön lies across the sea to the east; its stone vein and pasture are twice as rich.
            new Site("N0", 10, 0, SiteKind.HarborSpot | SiteKind.Land, hidden: true, island: IslandId.Nyon),
            new Site("N1", 10, 1, SiteKind.Pasture, hidden: true, richnessPercent: 200, island: IslandId.Nyon),
            new Site("N2", 11, 0, SiteKind.Mountain, hidden: true, richnessPercent: 200, island: IslandId.Nyon),
        };

        /// <summary>The Exploration an expedition costs before a lookout's discount (economy-v1.md, section 6).</summary>
        public const int ExpeditionCost = 250;

        /// <summary>Hours of travel with the villagers awake; the crew rests at night like everyone else (INV-T4).</summary>
        public const int ExpeditionHours = 32;

        /// <summary>What the first expedition finds.</summary>
        public static IReadOnlyList<string> ExpeditionReveals { get; } = new[] { "N0", "N1", "N2" };

        /// <summary>What each survey reveals, in order, and what it costs in Exploration (economy-v1.md, section 6).</summary>
        public static IReadOnlyList<(int Cost, IReadOnlyList<string> Reveals)> Surveys { get; } = new (int, IReadOnlyList<string>)[]
        {
            (60, new[] { "M2", "F3" }),
            (120, new[] { "K2", "O2" }),
        };
    }
}
