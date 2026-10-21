using System;
using System.Collections.Generic;

namespace Eirora.Simulation.Map
{
    /// <summary>
    /// The twenty named sites of the M1 prototype, from before every land tile became a building site (TASK-037). They
    /// are kept only to make the prototype's map, which never changes, and to migrate states that name them.
    /// </summary>
    internal static class PrototypeSites
    {
        public static IReadOnlyList<Named> All { get; } = new Named[]
        {
            new("S", 0, 0, Terrain.Grassland),
            new("L1", -1, 0, Terrain.Grassland),
            new("L2", 0, -1, Terrain.Grassland),
            new("L3", -1, 1, Terrain.Grassland),
            new("E1", 2, -1, Terrain.Grassland),
            new("E2", 2, -2, Terrain.Grassland),
            new("K1", 1, 0, Terrain.ShallowSea),
            new("K2", -3, 3, Terrain.ShallowSea, hidden: true, richness: 150),
            new("H1", 1, 1, Terrain.Beach),
            new("O1", 0, 1, Terrain.Farmland),
            new("O2", -1, 2, Terrain.Farmland, hidden: true),
            new("F1", 1, -1, Terrain.Forest, forestTiles: 1),
            new("F2", 3, -2, Terrain.Forest, forestTiles: 3),
            new("F3", -2, 0, Terrain.Forest, hidden: true, forestTiles: 4, richness: 125),
            new("M1", -4, 0, Terrain.Mountain),
            new("M2", 3, -1, Terrain.Mountain, hidden: true, richness: 200),
            new("P1", -2, -1, Terrain.Pasture),
            new("N0", 10, 0, Terrain.Beach, hidden: true),
            new("N1", 10, 1, Terrain.Pasture, hidden: true, richness: 200),
            new("N2", 11, 0, Terrain.Mountain, hidden: true, richness: 200),
        };

        /// <summary>The sites the expedition found, all on Nyön.</summary>
        public static IReadOnlyList<string> ExpeditionFound { get; } = new[] { "N0", "N1", "N2" };

        /// <summary>The hex of a named site.</summary>
        /// <exception cref="FormatException">There is no site with that id.</exception>
        public static Hex HexOf(string id)
        {
            foreach (var site in All)
            {
                if (site.Id == id)
                {
                    return site.Hex;
                }
            }

            throw new FormatException($"There is no site '{id}'.");
        }

        public readonly struct Named
        {
            public Named(string id, int q, int r, Terrain terrain, bool hidden = false, int forestTiles = 0, int richness = 100)
            {
                Id = id;
                Hex = new Hex(q, r);
                Terrain = terrain;
                Hidden = hidden;
                ForestTiles = forestTiles;
                Richness = richness;
            }

            public string Id { get; }

            public Hex Hex { get; }

            public Terrain Terrain { get; }

            /// <summary>Under the fog when a world began, until a survey or the expedition found it.</summary>
            public bool Hidden { get; }

            /// <summary>The forest tiles the prototype counted at the site; its timber was kept for all of them.</summary>
            public int ForestTiles { get; }

            public int Richness { get; }
        }
    }
}
