using System;
using System.Collections.Generic;

namespace Eirora.Simulation.Map
{
    /// <summary>
    /// Puts a map together, tile by tile, for a generator or a handmade map; every tile starts as deep sea of height 0
    /// and normal richness. <see cref="Build"/> checks the rules every map keeps.
    /// </summary>
    public sealed class WorldMapBuilder
    {
        private readonly HexDisc _disc;
        private readonly Terrain[] _terrain;
        private readonly byte[] _height;
        private readonly int[] _richness;
        private readonly List<MapIsland> _islands = new();

        public WorldMapBuilder(int radius, MapSource source, string generatorId, int generatorVersion)
        {
            if (string.IsNullOrEmpty(generatorId) || generatorId.Contains(' '))
            {
                throw new ArgumentException("A generator has a name without spaces.", nameof(generatorId));
            }

            if (generatorVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(generatorVersion), generatorVersion, "Generator versions start at 1.");
            }

            _disc = new HexDisc(radius);
            Source = source;
            GeneratorId = generatorId;
            GeneratorVersion = generatorVersion;
            _terrain = new Terrain[_disc.Count];
            _height = new byte[_disc.Count];
            _richness = new int[_disc.Count];
            for (int i = 0; i < _richness.Length; i++)
            {
                _richness[i] = 100;
            }
        }

        public int Radius => _disc.Radius;

        public MapSource Source { get; }

        public string GeneratorId { get; }

        public int GeneratorVersion { get; }

        public ulong? Seed { get; set; }

        public MapProvenance? Provenance { get; set; }

        public Hex Start { get; set; }

        public bool Contains(Hex hex) => _disc.Contains(hex);

        public Terrain TerrainAt(Hex hex) => _terrain[Index(hex)];

        public void Set(Hex hex, Terrain terrain, int height, int richness = 100)
        {
            if (height < 0 || height > WorldMap.MaximumHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"Heights run from 0 to {WorldMap.MaximumHeight}.");
            }

            if (richness < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(richness), richness, "A tile's richness is at least 1 percent.");
            }

            int index = Index(hex);
            _terrain[index] = terrain;
            _height[index] = (byte)height;
            _richness[index] = richness;
        }

        /// <summary>Adds an island: the land connected to <paramref name="anchor"/>.</summary>
        public void AddIsland(string id, Hex anchor)
        {
            if (string.IsNullOrEmpty(id) || id.Contains(' '))
            {
                throw new ArgumentException("An island has an id without spaces.", nameof(id));
            }

            foreach (var island in _islands)
            {
                if (island.Id == id)
                {
                    throw new ArgumentException($"The map already has an island {id}.", nameof(id));
                }
            }

            _islands.Add(new MapIsland(id, anchor));
        }

        /// <exception cref="ArgumentException">The map breaks a rule every map keeps.</exception>
        public WorldMap Build() => new(
            _disc,
            Source,
            GeneratorId,
            GeneratorVersion,
            Seed,
            Provenance,
            Start,
            _islands.ToArray(),
            (Terrain[])_terrain.Clone(),
            (byte[])_height.Clone(),
            (int[])_richness.Clone());

        private int Index(Hex hex)
        {
            int index = _disc.IndexOf(hex);
            return index >= 0 ? index : throw new ArgumentOutOfRangeException(nameof(hex), hex, "The hex lies outside the map.");
        }
    }
}
