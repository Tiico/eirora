using System;
using System.Collections.Generic;
using System.Globalization;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation.Map
{
    /// <summary>
    /// A world's map (GLOSSARY: Karta, ADR-0010): a disc of tiles with terrain, height and richness, and the islands on
    /// it. It is made once, when the world is made, and never changes; what changes on the map during play, such as fog,
    /// felled forest and buildings, is the world's state. It is stored whole, never as a seed alone (MAP-06).
    /// </summary>
    /// <remarks>
    /// The text form is canonical UTF-8 text: a header, one island per line, then one line per row of the disc from
    /// north to south, each tile a terrain letter and a height digit, with ":richness" when it is not 100. The same map
    /// always gives the same text, and its SHA-256 hash is the map's identity in the world's state.
    /// </remarks>
    public sealed class WorldMap
    {
        /// <summary>The version of the map's text form; a migration may only change the representation (ADR-0010).</summary>
        public const int FormatVersion = 1;

        public const int MaximumHeight = 7;

        private const string Magic = "eirora-map";

        private readonly Terrain[] _terrain;
        private readonly byte[] _height;
        private readonly int[] _richness;
        private readonly int[] _island;
        private string? _text;
        private string? _hash;

        internal WorldMap(
            HexDisc disc,
            MapSource source,
            string generatorId,
            int generatorVersion,
            ulong? seed,
            MapProvenance? provenance,
            Hex start,
            IReadOnlyList<MapIsland> islands,
            Terrain[] terrain,
            byte[] height,
            int[] richness)
        {
            Disc = disc;
            Source = source;
            GeneratorId = generatorId;
            GeneratorVersion = generatorVersion;
            Seed = seed;
            Provenance = provenance;
            Start = start;
            Islands = islands;
            _terrain = terrain;
            _height = height;
            _richness = richness;
            _island = new int[disc.Count];
            Check();
        }

        public HexDisc Disc { get; }

        public int Radius => Disc.Radius;

        public MapSource Source { get; }

        /// <summary>The generator that made the map, or the name of a handmade map.</summary>
        public string GeneratorId { get; }

        /// <summary>The generator's version, fixed for the world; the map is never generated again (ADR-0010).</summary>
        public int GeneratorVersion { get; }

        /// <summary>The seed of a procedural map; none for other sources.</summary>
        public ulong? Seed { get; }

        /// <summary>Where a geographic map's data came from; none for other sources.</summary>
        public MapProvenance? Provenance { get; }

        /// <summary>The tile where the world is founded.</summary>
        public Hex Start { get; }

        public IReadOnlyList<MapIsland> Islands { get; }

        /// <summary>The SHA-256 hash of the map's text: its identity in the world's state.</summary>
        public string Hash => _hash ??= Sha256.Of(ToText());

        public bool Contains(Hex hex) => Disc.Contains(hex);

        public Terrain TerrainAt(Hex hex) => _terrain[Index(hex)];

        public int HeightAt(Hex hex) => _height[Index(hex)];

        /// <summary>How rich the tile is in what its terrain gives, in percent; 100 is normal.</summary>
        public int RichnessAt(Hex hex) => _richness[Index(hex)];

        /// <summary>The id of the island a tile belongs to, or null for the sea.</summary>
        public string? IslandAt(Hex hex)
        {
            int island = _island[Index(hex)];
            return island > 0 ? Islands[island - 1].Id : null;
        }

        public string ToText()
        {
            if (_text is not null)
            {
                return _text;
            }

            var writer = new StateWriter();
            writer.Line(Magic).Long(FormatVersion);
            writer.Line("radius").Long(Radius);
            writer.Line("source").Text(SourceName(Source));
            writer.Line("generator").Text(GeneratorId).Long(GeneratorVersion);
            if (Seed is ulong seed)
            {
                writer.Line("seed").Text(seed.ToString("x16", CultureInfo.InvariantCulture));
            }

            if (Provenance is MapProvenance from)
            {
                writer.Line("provenance").Text(from.Dataset).Text(from.Version).Text(from.Licence)
                    .Long(from.West).Long(from.South).Long(from.East).Long(from.North);
            }

            writer.Line("start").Long(Start.Q).Long(Start.R);
            foreach (var island in Islands)
            {
                writer.Line("island").Text(island.Id).Long(island.Anchor.Q).Long(island.Anchor.R);
            }

            int index = 0;
            for (int r = -Radius; r <= Radius; r++)
            {
                writer.Line("row").Long(r);
                int count = (2 * Radius) + 1 - (r < 0 ? -r : r);
                for (int i = 0; i < count; i++, index++)
                {
                    writer.Text(Cell(index));
                }
            }

            return _text = writer.ToString();
        }

        /// <summary>Reads a map's text and checks that it is a whole, canonical map.</summary>
        /// <exception cref="SavedByNewerVersionException">A newer app wrote the map.</exception>
        /// <exception cref="FormatException">The text is not a valid map of this format.</exception>
        public static WorldMap Parse(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            WorldMap map;
            try
            {
                map = Read(new StateReader(text));
            }
            catch (ArgumentException e)
            {
                throw new FormatException("The text is not a valid map.", e);
            }

            if (!string.Equals(map.ToText(), text, StringComparison.Ordinal))
            {
                throw new FormatException("The map's text is not in canonical form.");
            }

            return map;
        }

        internal static string SourceName(MapSource source) => source switch
        {
            MapSource.Procedural => "procedural",
            MapSource.Handmade => "handmade",
            _ => "geographic",
        };

        private static WorldMap Read(StateReader reader)
        {
            reader.Expect(Magic);
            int format = reader.Int();
            if (format != FormatVersion)
            {
                throw format > FormatVersion
                    ? new SavedByNewerVersionException($"The map has format {format}; this build reads format {FormatVersion}.")
                    : new FormatException($"The map has format {format}; this build reads format {FormatVersion}.");
            }

            reader.Expect("radius");
            int radius = reader.Int();
            var source = ParseSource(reader);
            reader.Expect("generator");
            var builder = new WorldMapBuilder(radius, source, reader.Text(), reader.Int());
            if (reader.Next("seed"))
            {
                string seed = reader.Text();
                builder.Seed = seed.Length == 16 && ulong.TryParse(seed, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong value)
                    ? value
                    : throw new FormatException($"'{seed}' is not a seed.");
            }

            if (reader.Next("provenance"))
            {
                builder.Provenance = new MapProvenance(reader.Text(), reader.Text(), reader.Text(), reader.Long(), reader.Long(), reader.Long(), reader.Long());
            }

            reader.Expect("start");
            builder.Start = new Hex(reader.Int(), reader.Int());
            while (reader.Next("island"))
            {
                builder.AddIsland(reader.Text(), new Hex(reader.Int(), reader.Int()));
            }

            var disc = new HexDisc(builder.Radius);
            int index = 0;
            for (int r = -builder.Radius; r <= builder.Radius; r++)
            {
                reader.Expect("row");
                if (reader.Int() != r)
                {
                    throw new FormatException($"Expected row {r}.");
                }

                while (reader.HasField)
                {
                    if (index >= disc.Count || disc[index].R != r)
                    {
                        throw new FormatException($"Row {r} does not have the map's shape.");
                    }

                    ReadCell(reader.Text(), disc[index++], builder);
                }
            }

            reader.End();
            if (index != disc.Count)
            {
                throw new FormatException("The map has too few tiles.");
            }

            return builder.Build();
        }

        private static MapSource ParseSource(StateReader reader)
        {
            reader.Expect("source");
            string name = reader.Text();
            return name switch
            {
                "procedural" => MapSource.Procedural,
                "handmade" => MapSource.Handmade,
                "geographic" => MapSource.Geographic,
                _ => throw new FormatException($"'{name}' is not a map source."),
            };
        }

        private static void ReadCell(string cell, Hex hex, WorldMapBuilder builder)
        {
            var terrain = cell.Length >= 2 ? Terrains.FromCode(cell[0]) : null;
            int height = cell.Length >= 2 ? cell[1] - '0' : -1;
            int richness = 100;
            if (cell.Length > 2 && (cell[2] != ':' || !int.TryParse(cell.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out richness)))
            {
                throw new FormatException($"'{cell}' is not a tile.");
            }

            if (terrain is not Terrain value || height < 0 || height > MaximumHeight)
            {
                throw new FormatException($"'{cell}' is not a tile.");
            }

            builder.Set(hex, value, height, richness);
        }

        private string Cell(int index)
        {
            string cell = _terrain[index].Code().ToString() + (char)('0' + _height[index]);
            return _richness[index] == 100 ? cell : cell + ":" + _richness[index].ToString(CultureInfo.InvariantCulture);
        }

        private int Index(Hex hex)
        {
            int index = Disc.IndexOf(hex);
            return index >= 0 ? index : throw new ArgumentOutOfRangeException(nameof(hex), hex, "The hex lies outside the map.");
        }

        /// <summary>
        /// The structural rules every map keeps, whatever made it: the header fits the source, the start and every
        /// anchor lie on land, and every land tile belongs to exactly one island. Whether a map is good to play on is
        /// the validator's question (TASK-030).
        /// </summary>
        private void Check()
        {
            if ((Source == MapSource.Procedural) != Seed.HasValue)
            {
                throw new ArgumentException("A procedural map has a seed, and no other map has one.");
            }

            if ((Source == MapSource.Geographic) != (Provenance is not null))
            {
                throw new ArgumentException("A geographic map has a provenance, and no other map has one.");
            }

            if (!Contains(Start) || !TerrainAt(Start).IsLand())
            {
                throw new ArgumentException($"The start {Start} is not land on the map.");
            }

            for (int i = 0; i < Islands.Count; i++)
            {
                var anchor = Islands[i].Anchor;
                if (!Contains(anchor) || !TerrainAt(anchor).IsLand())
                {
                    throw new ArgumentException($"The island {Islands[i].Id} is anchored at {anchor}, which is not land on the map.");
                }

                if (_island[Disc.IndexOf(anchor)] != 0)
                {
                    throw new ArgumentException($"The island {Islands[i].Id} is the same land as {IslandAt(anchor)}.");
                }

                Fill(Disc.IndexOf(anchor), i + 1);
            }

            for (int index = 0; index < Disc.Count; index++)
            {
                if (_terrain[index].IsLand() && _island[index] == 0)
                {
                    throw new ArgumentException($"The land at {Disc[index]} belongs to no island.");
                }
            }
        }

        private void Fill(int from, int island)
        {
            var open = new Stack<int>();
            _island[from] = island;
            open.Push(from);
            while (open.Count > 0)
            {
                var hex = Disc[open.Pop()];
                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    int next = Disc.IndexOf(hex.Neighbour(direction));
                    if (next >= 0 && _island[next] == 0 && _terrain[next].IsLand())
                    {
                        _island[next] = island;
                        open.Push(next);
                    }
                }
            }
        }
    }
}
