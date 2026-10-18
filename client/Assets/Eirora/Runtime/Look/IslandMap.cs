using System.Collections.Generic;
using System.Linq;
using Eirora.Simulation.Economy;

namespace Eirora.Client.Look
{
    /// <summary>What a hex looks like.</summary>
    public enum Terrain
    {
        DeepSea,
        ShallowSea,
        Beach,
        Grass,
        Forest,
        Farmland,
        Pasture,
        Mountain,
    }

    /// <summary>
    /// The geography drawn around the simulation's sites (PrototypeIsland): the sites keep their coordinates, so
    /// distances in the picture match distances in the economy; the rest is scenery until world generation (M3).
    /// </summary>
    public sealed class IslandMap
    {
        public const int Radius = 4;
        public const int SeaRadius = 9;

        // Fishing water and the channels that join it to the open sea.
        private static readonly Hex[] s_water =
        {
            new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(4, -1),
            new(-3, 3), new(-3, 4), new(-4, 4), new(-2, 3),
            new(0, -4), new(1, -4), new(4, -4), new(-4, 1), new(2, 2), new(3, 1),
        };

        private readonly Dictionary<Hex, Terrain> _terrain = new();
        private readonly Dictionary<Hex, Site> _sites = new();

        public IslandMap()
        {
            foreach (var site in PrototypeIsland.Sites)
            {
                _sites[new Hex(site.Q, site.R)] = site;
            }

            var origin = new Hex(0, 0);
            for (int q = -SeaRadius; q <= SeaRadius; q++)
            {
                for (int r = -SeaRadius; r <= SeaRadius; r++)
                {
                    var hex = new Hex(q, r);
                    int distance = hex.DistanceTo(origin);
                    if (distance > SeaRadius)
                    {
                        continue;
                    }

                    _terrain[hex] = distance <= Radius && !s_water.Contains(hex) ? LandTerrain(hex, distance) : Terrain.DeepSea;
                }
            }

            // Sea next to land is shallow and turquoise (art-direction.md: shallow water marks coasts and fairways).
            foreach (var hex in _terrain.Keys.ToList())
            {
                if (_terrain[hex] == Terrain.DeepSea && Neighbours(hex).Any(IsLand))
                {
                    _terrain[hex] = Terrain.ShallowSea;
                }
            }
        }

        public IReadOnlyDictionary<Hex, Terrain> Tiles => _terrain;

        public Site? SiteAt(Hex hex) => _sites.TryGetValue(hex, out var site) ? site : null;

        public static Hex HexOf(Site site) => new(site.Q, site.R);

        public bool IsLand(Hex hex) => _terrain.TryGetValue(hex, out var terrain) && terrain >= Terrain.Beach;

        public static IEnumerable<Hex> Neighbours(Hex hex)
        {
            yield return new Hex(hex.Q + 1, hex.R);
            yield return new Hex(hex.Q + 1, hex.R - 1);
            yield return new Hex(hex.Q, hex.R - 1);
            yield return new Hex(hex.Q - 1, hex.R);
            yield return new Hex(hex.Q - 1, hex.R + 1);
            yield return new Hex(hex.Q, hex.R + 1);
        }

        private Terrain LandTerrain(Hex hex, int distance)
        {
            if (_sites.TryGetValue(hex, out var site))
            {
                var kinds = site.Kinds;
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

                return (kinds & SiteKind.HarborSpot) != 0 ? Terrain.Beach : Terrain.Grass;
            }

            // Scenery: woods near the forest sites, a beach on the outer ring, meadow in between.
            int noise = ((hex.Q * 73) + (hex.R * 151) + (hex.Q * hex.R * 17)) & 7;
            if (distance == Radius)
            {
                return noise < 5 ? Terrain.Beach : Terrain.Grass;
            }

            return noise < 2 ? Terrain.Forest : Terrain.Grass;
        }
    }
}
