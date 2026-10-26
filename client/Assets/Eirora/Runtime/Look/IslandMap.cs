using System.Collections.Generic;
using Eirora.Simulation.Map;
using UnityEngine;
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
    /// What the client draws of a world's map (ADR-0010, TASK-033), read from the map so distances in the picture match
    /// distances in the economy. A generated map is drawn whole. The prototype's map is drawn as far as the prototype
    /// always drew it, the sea around Hemön and Nyön, so its worlds look as they did.
    /// </summary>
    public sealed class IslandMap
    {
        private readonly Dictionary<Hex, Terrain> _terrain = new();

        public IslandMap(WorldMap map)
        {
            WorldMap = map;
            HomeIsland = map.IslandAt(map.Start);
            bool prototype = map.Hash == PrototypeHome.Map.Hash;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < map.Disc.Count; i++)
            {
                var hex = map.Disc[i];
                if (prototype && !PrototypeHome.WasDrawn(hex))
                {
                    continue;
                }

                _terrain[hex] = Look(map.TerrainAt(hex));
                if (map.TerrainAt(hex).IsLand())
                {
                    var centre = hex.Center();
                    min = Vector2.Min(min, new Vector2(centre.x, centre.z));
                    max = Vector2.Max(max, new Vector2(centre.x, centre.z));
                }
            }

            LandMin = min;
            LandMax = max;
        }

        /// <summary>The map drawn.</summary>
        public WorldMap WorldMap { get; }

        /// <summary>The island the world is founded on, which is known from the start.</summary>
        public string? HomeIsland { get; }

        public IReadOnlyDictionary<Hex, Terrain> Tiles => _terrain;

        /// <summary>The south-west corner of the land on the ground plane, as x and z.</summary>
        public Vector2 LandMin { get; }

        /// <summary>The north-east corner of the land on the ground plane, as x and z.</summary>
        public Vector2 LandMax { get; }

        public bool IsLand(Hex hex) => _terrain.TryGetValue(hex, out var terrain) && terrain >= Terrain.Beach;

        /// <summary>The island a land tile belongs to, other than the home island; null for the sea and the home island.</summary>
        public string? AwayIslandAt(Hex hex) =>
            IsLand(hex) && WorldMap.IslandAt(hex) is string island && island != HomeIsland ? island : null;

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

        /// <summary>The terrain the client draws for the map's.</summary>
        public static Terrain Look(MapTerrain terrain) => terrain switch
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
