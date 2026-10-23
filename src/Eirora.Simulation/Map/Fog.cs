using System;
using System.Collections.Generic;
using System.Text;

namespace Eirora.Simulation.Map
{
    /// <summary>
    /// What of the map is known (GLOSSARY: Dimma, TASK-032): a tile is either under the fog or known, and once known it
    /// stays known (MAP-04, INV-V1). The fog is the world's state, not the map's, so the map never changes when it lifts.
    /// </summary>
    /// <remarks>
    /// In the state it is one line: a bit per tile of the map in map order, four tiles to a hex digit, so the same fog
    /// always gives the same text.
    /// </remarks>
    public sealed class Fog
    {
        private readonly WorldMap _map;
        private readonly bool[] _known;

        private Fog(WorldMap map, bool[] known)
        {
            _map = map;
            _known = known;
        }

        /// <summary>
        /// How many times tiles have been lifted since this fog was made or read; it only tells whether the fog has
        /// changed, without a pass over the map, and is not part of the state.
        /// </summary>
        public int Lifts { get; private set; }

        /// <summary>The tiles lifted so far.</summary>
        public int KnownCount
        {
            get
            {
                int count = 0;
                foreach (bool known in _known)
                {
                    count += known ? 1 : 0;
                }

                return count;
            }
        }

        /// <summary>
        /// What a new world knows (TASK-032): the start island's land and the sea along its coast, except the tiles
        /// in <paramref name="hidden"/>, which wait for a survey.
        /// </summary>
        public static Fog AtStart(WorldMap map, IEnumerable<Hex> hidden)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var fog = new Fog(map, new bool[map.Disc.Count]);
            fog.LiftIsland(map.IslandAt(map.Start) ?? throw new ArgumentException("A map starts on land.", nameof(map)));
            foreach (var hex in hidden)
            {
                int index = map.Disc.IndexOf(hex);
                if (index >= 0)
                {
                    fog._known[index] = false;
                }
            }

            return fog;
        }

        /// <summary>A map wholly under the fog; a state's fog line replaces it when the world is read.</summary>
        internal static Fog Unknown(WorldMap map) => new(map, new bool[map.Disc.Count]);

        public bool IsKnown(Hex hex)
        {
            int index = _map.Disc.IndexOf(hex);
            return index >= 0 && _known[index];
        }

        /// <summary>Whether a tile can be lifted next: under the fog, on the map and next to a known tile.</summary>
        public bool CanLift(Hex hex)
        {
            if (!_map.Contains(hex) || IsKnown(hex))
            {
                return false;
            }

            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                if (IsKnown(hex.Neighbour(direction)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Lifts the fog from one tile; returns whether it was under the fog.</summary>
        public bool Lift(Hex hex)
        {
            int index = _map.Disc.IndexOf(hex);
            if (index < 0 || _known[index])
            {
                return false;
            }

            _known[index] = true;
            Lifts++;
            return true;
        }

        /// <summary>Lifts the fog from an island's land and the sea along its coast, as an expedition finds it.</summary>
        public void LiftIsland(string island)
        {
            Lifts++;
            for (int index = 0; index < _map.Disc.Count; index++)
            {
                var hex = _map.Disc[index];
                if (_map.IslandAt(hex) == island)
                {
                    _known[index] = true;
                    for (int direction = 0; direction < Hex.Directions; direction++)
                    {
                        var next = hex.Neighbour(direction);
                        if (_map.Contains(next) && !_map.TerrainAt(next).IsLand())
                        {
                            _known[_map.Disc.IndexOf(next)] = true;
                        }
                    }
                }
            }
        }

        /// <summary>The fog as hex digits, four tiles to a digit in map order.</summary>
        public string ToText()
        {
            var text = new StringBuilder((_known.Length + 3) / 4);
            for (int start = 0; start < _known.Length; start += 4)
            {
                int digit = 0;
                for (int bit = 0; bit < 4 && start + bit < _known.Length; bit++)
                {
                    digit |= _known[start + bit] ? 8 >> bit : 0;
                }

                text.Append("0123456789abcdef"[digit]);
            }

            return text.ToString();
        }

        /// <exception cref="FormatException">The text is not the fog of this map.</exception>
        public static Fog Parse(WorldMap map, string text)
        {
            if (map is null || text is null)
            {
                throw new ArgumentNullException(map is null ? nameof(map) : nameof(text));
            }

            if (text.Length != (map.Disc.Count + 3) / 4)
            {
                throw new FormatException("The fog does not fit the map.");
            }

            var known = new bool[map.Disc.Count];
            for (int index = 0; index < known.Length; index++)
            {
                int digit = "0123456789abcdef".IndexOf(text[index / 4]);
                if (digit < 0)
                {
                    throw new FormatException($"'{text[index / 4]}' is not a hex digit.");
                }

                known[index] = (digit & (8 >> (index % 4))) != 0;
            }

            var fog = new Fog(map, known);
            if (!string.Equals(fog.ToText(), text, StringComparison.Ordinal))
            {
                throw new FormatException("The fog's text is not canonical.");
            }

            return fog;
        }
    }
}
