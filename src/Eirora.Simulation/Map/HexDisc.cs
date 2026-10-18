using System;

namespace Eirora.Simulation.Map
{
    /// <summary>
    /// The hexes at most <see cref="Radius"/> steps from the origin, numbered 0 to <see cref="Count"/> - 1 in map order
    /// (r, then q), so that per-tile data can live in plain arrays. A map is such a disc (ADR-0010).
    /// </summary>
    public sealed class HexDisc
    {
        private readonly Hex[] _hexes;
        private readonly int[] _rowStart;

        public HexDisc(int radius)
        {
            if (radius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "A disc has a radius of zero or more.");
            }

            Radius = radius;
            Count = CountWithin(radius);
            _hexes = new Hex[Count];
            _rowStart = new int[(2 * radius) + 1];
            int index = 0;
            for (int r = -radius; r <= radius; r++)
            {
                _rowStart[r + radius] = index - FirstQ(r);
                for (int q = FirstQ(r); q <= LastQ(r); q++)
                {
                    _hexes[index++] = new Hex(q, r);
                }
            }
        }

        public int Radius { get; }

        public int Count { get; }

        public Hex this[int index] => _hexes[index];

        /// <summary>The number of hexes at most <paramref name="radius"/> steps from a hex: 3r(r + 1) + 1.</summary>
        public static int CountWithin(int radius) => (3 * radius * (radius + 1)) + 1;

        public bool Contains(Hex hex) => hex.DistanceTo(Hex.Origin) <= Radius;

        /// <summary>The hex's number in this disc, or -1 when it lies outside.</summary>
        public int IndexOf(Hex hex) => Contains(hex) ? _rowStart[hex.R + Radius] + hex.Q : -1;

        private int FirstQ(int r) => -Radius > -r - Radius ? -Radius : -r - Radius;

        private int LastQ(int r) => Radius < -r + Radius ? Radius : -r + Radius;
    }
}
