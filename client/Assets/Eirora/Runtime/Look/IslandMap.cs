using System.Collections.Generic;
using Eirora.Simulation.Map;
using MapTerrain = Eirora.Simulation.Map.Terrain;

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
    /// What the client draws of the world's map: the land and sea the prototype has always shown, read from the map
    /// (ADR-0010), so distances in the picture match distances in the economy. Generated worlds are drawn in TASK-033.
    /// </summary>
    public sealed class IslandMap
    {
        /// <summary>Nyön's centre; Nyön and its sea stay hidden until an expedition finds it.</summary>
        public static readonly Hex NyonCentre = PrototypeHome.NyonCentre;
        private const int NyonSeaRadius = 4;

        private readonly Dictionary<Hex, Terrain> _terrain = new();

        public IslandMap(WorldMap map)
        {
            for (int i = 0; i < map.Disc.Count; i++)
            {
                var hex = map.Disc[i];
                if (PrototypeHome.WasDrawn(hex))
                {
                    _terrain[hex] = Look(map.TerrainAt(hex));
                }
            }
        }

        public IReadOnlyDictionary<Hex, Terrain> Tiles => _terrain;

        /// <summary>Whether a hex belongs to Nyön, which stays hidden until an expedition finds it.</summary>
        public static bool OnNyon(Hex hex) => hex.DistanceTo(NyonCentre) <= NyonSeaRadius;

        public bool IsLand(Hex hex) => _terrain.TryGetValue(hex, out var terrain) && terrain >= Terrain.Beach;

        /// <summary>The ground a building's site usually has, to show the building on in the look gallery.</summary>
        public static Terrain GroundFor(TileUse requirement) => requirement switch
        {
            TileUse.FishingWater => Terrain.ShallowSea,
            TileUse.Forest => Terrain.Forest,
            TileUse.Farmland => Terrain.Farmland,
            TileUse.Pasture => Terrain.Pasture,
            TileUse.Mountain or TileUse.Crest => Terrain.Mountain,
            TileUse.HarborSpot => Terrain.Beach,
            _ => Terrain.Grass,
        };

        private static Terrain Look(MapTerrain terrain) => terrain switch
        {
            MapTerrain.DeepSea => Terrain.DeepSea,
            MapTerrain.ShallowSea => Terrain.ShallowSea,
            MapTerrain.Beach => Terrain.Beach,
            MapTerrain.Forest => Terrain.Forest,
            MapTerrain.Farmland => Terrain.Farmland,
            MapTerrain.Pasture => Terrain.Pasture,
            MapTerrain.Mountain => Terrain.Mountain,
            _ => Terrain.Grass,
        };
    }
}
