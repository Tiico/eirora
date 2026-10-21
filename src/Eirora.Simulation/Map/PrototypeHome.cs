using System;
using System.Collections.Generic;
using Eirora.Simulation.Economy;

namespace Eirora.Simulation.Map
{
    /// <summary>
    /// The prototype's handmade map, <c>prototype-home</c> version 1: Hemön with its sites and Nyön across the sea to the
    /// east, exactly as the client drew them in M1, so every world from the prototype keeps what it has seen (D-81,
    /// ADR-0010). What was scenery around the sites, such as woods and beaches, is now real terrain. Beyond the sea the
    /// prototype drew, the map is deep sea until generator v1 fills it once (TASK-031). Three things differ from M1's
    /// picture, so the map passes the validator: a bay where ships land at Nyön's harbour, a single crest, and a second
    /// mountain to quarry beside it.
    /// </summary>
    /// <remarks>Never change this map: every world made before world generation is on it, and its hash is in their state.</remarks>
    public static class PrototypeHome
    {
        public const string GeneratorId = "prototype-home";
        public const int GeneratorVersion = 1;
        public const int Radius = 24;

        public const string Home = "home";
        public const string Nyon = "nyon";

        /// <summary>Hemön's land reaches this far from the settlement.</summary>
        public const int HomeRadius = 4;

        /// <summary>The sea the prototype drew around Hemön.</summary>
        public const int HomeSeaRadius = 9;

        public static readonly Hex NyonCentre = new(10, 0);

        /// <summary>Nyön's land: one ring around its harbour, except the bay west of it where ships land.</summary>
        public const int NyonRadius = 1;

        /// <summary>The bay facing Hemön that makes Nyön's harbour site a harbour spot (map-validation.md).</summary>
        public static readonly Hex NyonBay = new(9, 0);

        /// <summary>Hemön's crest, where the Vårdkase stands: the western mountain, a step above the stone vein.</summary>
        public const string CrestSite = "M1";
        public const int CrestHeight = 6;

        /// <summary>
        /// A plain mountain beside the crest, on what M1 drew as a beach, so that Hemön has a mountain to quarry besides
        /// the stone vein and the rich vein stays a choice (D-95, map-validation.md).
        /// </summary>
        public static readonly Hex WesternQuarry = new(-3, -1);

        /// <summary>The sea the prototype drew around Nyön; it overlaps Hemön's, so one sea joins the islands.</summary>
        public const int NyonSeaRadius = 7;

        // Fishing water and the channels that join it to the open sea.
        private static readonly HashSet<Hex> s_water = new()
        {
            new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(4, -1),
            new(-3, 3), new(-3, 4), new(-4, 4), new(-2, 3),
            new(0, -4), new(1, -4), new(4, -4), new(-4, 1), new(2, 2), new(3, 1),
        };

        private static readonly Lazy<WorldMap> s_map = new(Create);

        public static WorldMap Map => s_map.Value;

        /// <summary>Whether a hex was drawn as part of the prototype's sea and land.</summary>
        public static bool WasDrawn(Hex hex) => hex.DistanceTo(Hex.Origin) <= HomeSeaRadius || hex.DistanceTo(NyonCentre) <= NyonSeaRadius;

        /// <summary>The ground of a site: what it offers decides what it is.</summary>
        public static Terrain TerrainOf(SiteKind kinds)
        {
            if ((kinds & SiteKind.FishingWater) != 0)
            {
                return Terrain.ShallowSea;
            }

            if ((kinds & SiteKind.Forest) != 0)
            {
                return Terrain.Forest;
            }

            if ((kinds & SiteKind.Mountain) != 0)
            {
                return Terrain.Mountain;
            }

            if ((kinds & SiteKind.Farmland) != 0)
            {
                return Terrain.Farmland;
            }

            if ((kinds & SiteKind.Pasture) != 0)
            {
                return Terrain.Pasture;
            }

            return (kinds & SiteKind.HarborSpot) != 0 ? Terrain.Beach : Terrain.Grassland;
        }

        /// <summary>The map's island id for an island of the economy.</summary>
        public static string IslandOf(IslandId island) => island == IslandId.Nyon ? Nyon : Home;

        private static WorldMap Create()
        {
            var sites = new Dictionary<Hex, Site>();
            foreach (var site in PrototypeIsland.Sites)
            {
                sites[site.Hex] = site;
            }

            var map = new WorldMapBuilder(Radius, MapSource.Handmade, GeneratorId, GeneratorVersion) { Start = Hex.Origin };
            var land = new HashSet<Hex>();
            foreach (var hex in Hex.Origin.Within(Radius))
            {
                int distance = hex.DistanceTo(Hex.Origin);
                if (distance <= HomeRadius && !s_water.Contains(hex))
                {
                    Set(map, hex, LandTerrain(hex, distance, sites));
                    land.Add(hex);
                }
                else if (hex.DistanceTo(NyonCentre) <= NyonRadius && hex != NyonBay)
                {
                    Set(map, hex, LandTerrain(hex, 0, sites)); // A small green island, not a sandbank.
                    land.Add(hex);
                }
            }

            // Sea next to land is shallow (art-direction.md: shallow water marks coasts and fairways).
            foreach (var hex in Hex.Origin.Within(Radius))
            {
                if (!land.Contains(hex) && WasDrawn(hex) && NextToLand(hex, land))
                {
                    Set(map, hex, Terrain.ShallowSea);
                }
            }

            foreach (var site in PrototypeIsland.Sites)
            {
                var hex = site.Hex;
                int height = site.Id == CrestSite ? CrestHeight : Height(map.TerrainAt(hex));
                map.Set(hex, map.TerrainAt(hex), height, site.RichnessPercent);
            }

            Set(map, WesternQuarry, Terrain.Mountain);

            map.AddIsland(Home, Hex.Origin);
            map.AddIsland(Nyon, NyonCentre);
            return map.Build();
        }

        private static Terrain LandTerrain(Hex hex, int distance, Dictionary<Hex, Site> sites)
        {
            if (sites.TryGetValue(hex, out var site))
            {
                return TerrainOf(site.Kinds);
            }

            // Woods near the forest sites, a beach on the outer ring, meadow in between.
            int noise = ((hex.Q * 73) + (hex.R * 151) + (hex.Q * hex.R * 17)) & 7;
            if (distance == HomeRadius)
            {
                return noise < 5 ? Terrain.Beach : Terrain.Grassland;
            }

            return noise < 2 ? Terrain.Forest : Terrain.Grassland;
        }

        private static bool NextToLand(Hex hex, HashSet<Hex> land)
        {
            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                if (land.Contains(hex.Neighbour(direction)))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Set(WorldMapBuilder map, Hex hex, Terrain terrain) => map.Set(hex, terrain, Height(terrain));

        /// <summary>The height steps the client draws for each terrain.</summary>
        private static int Height(Terrain terrain) => terrain switch
        {
            Terrain.DeepSea => 0,
            Terrain.ShallowSea => 1,
            Terrain.Beach => 2,
            Terrain.Mountain => 5,
            _ => 3,
        };
    }
}
