using System;
using System.Collections.Generic;
using System.Globalization;

namespace Eirora.Simulation.Map
{
    /// <summary>
    /// A hex on the map in axial coordinates (q, r), with s = -q - r as the third cube coordinate. Pointy-top: r grows
    /// towards the south. Integer only (INV-D3), so the client, the server and every platform agree (ADR-0010).
    /// </summary>
    /// <remarks>
    /// Hexes are ordered by r and then q, the order of the tiles on the map; every walk over neighbours, rings and
    /// areas has a fixed order, so nothing built on them depends on a hash or a platform.
    /// </remarks>
    public readonly struct Hex : IEquatable<Hex>, IComparable<Hex>
    {
        /// <summary>The number of neighbours, and of directions.</summary>
        public const int Directions = 6;

        public static readonly Hex Origin = new(0, 0);

        // Counter-clockwise from east: east, north-east, north-west, west, south-west, south-east.
        private static readonly Hex[] s_directions = { new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1) };

        public Hex(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int Q { get; }

        public int R { get; }

        /// <summary>The third cube coordinate.</summary>
        public int S => -Q - R;

        public static Hex operator +(Hex a, Hex b) => new(a.Q + b.Q, a.R + b.R);

        public static Hex operator -(Hex a, Hex b) => new(a.Q - b.Q, a.R - b.R);

        public static Hex operator *(Hex hex, int factor) => new(hex.Q * factor, hex.R * factor);

        public static bool operator ==(Hex a, Hex b) => a.Equals(b);

        public static bool operator !=(Hex a, Hex b) => !a.Equals(b);

        public static bool operator <(Hex a, Hex b) => a.CompareTo(b) < 0;

        public static bool operator >(Hex a, Hex b) => a.CompareTo(b) > 0;

        public static bool operator <=(Hex a, Hex b) => a.CompareTo(b) <= 0;

        public static bool operator >=(Hex a, Hex b) => a.CompareTo(b) >= 0;

        /// <summary>The hex with cube coordinates (q, r, s), which must add up to zero.</summary>
        public static Hex FromCube(int q, int r, int s) =>
            q + r + s == 0 ? new Hex(q, r) : throw new ArgumentException($"Cube coordinates ({q}, {r}, {s}) do not add up to zero.");

        /// <summary>One step in direction 0–5, counter-clockwise from east.</summary>
        public static Hex Direction(int direction) => s_directions[direction];

        public Hex Neighbour(int direction) => this + s_directions[direction];

        /// <summary>The number of steps between two hexes on an open map.</summary>
        public int DistanceTo(Hex other)
        {
            int dq = Abs(Q - other.Q), dr = Abs(R - other.R), ds = Abs(S - other.S);
            return dq > dr ? (dq > ds ? dq : ds) : (dr > ds ? dr : ds);
        }

        /// <summary>The hexes exactly <paramref name="radius"/> steps away, counter-clockwise from the south-west corner.</summary>
        public IEnumerable<Hex> Ring(int radius)
        {
            if (radius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "A ring has a radius of zero or more.");
            }

            return RingIterator(this, radius);
        }

        /// <summary>The hexes at most <paramref name="radius"/> steps away, in map order (r, then q).</summary>
        public IEnumerable<Hex> Within(int radius)
        {
            if (radius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "An area has a radius of zero or more.");
            }

            return WithinIterator(this, radius);
        }

        /// <summary>
        /// The hexes on the straight line to <paramref name="other"/>, both ends included: one per step, each a neighbour
        /// of the one before. Where the line runs exactly along an edge it always takes the same side.
        /// </summary>
        public Hex[] LineTo(Hex other)
        {
            int steps = DistanceTo(other);
            var line = new Hex[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                line[i] = Interpolate(this, other, i, steps);
            }

            return line;
        }

        public bool Equals(Hex other) => Q == other.Q && R == other.R;

        public override bool Equals(object? obj) => obj is Hex other && Equals(other);

        public override int GetHashCode() => unchecked((Q * 397) ^ R);

        public int CompareTo(Hex other) => R != other.R ? R.CompareTo(other.R) : Q.CompareTo(other.Q);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0}, {1})", Q, R);

        private static IEnumerable<Hex> RingIterator(Hex center, int radius)
        {
            if (radius == 0)
            {
                yield return center;
                yield break;
            }

            var hex = center + (s_directions[4] * radius);
            for (int side = 0; side < Directions; side++)
            {
                for (int step = 0; step < radius; step++)
                {
                    yield return hex;
                    hex = hex.Neighbour(side);
                }
            }
        }

        private static IEnumerable<Hex> WithinIterator(Hex center, int radius)
        {
            for (int dr = -radius; dr <= radius; dr++)
            {
                int from = -radius > -dr - radius ? -radius : -dr - radius;
                int to = radius < -dr + radius ? radius : -dr + radius;
                for (int dq = from; dq <= to; dq++)
                {
                    yield return new Hex(center.Q + dq, center.R + dr);
                }
            }
        }

        /// <summary>
        /// The hex nearest to the point <paramref name="step"/>/<paramref name="steps"/> of the way from a to b, rounded in
        /// exact integer arithmetic. The point is nudged by (1, 2, -3) of a unit far smaller than the distance between
        /// points, so it never lies exactly between two hexes and the rounding never depends on a tie.
        /// </summary>
        private static Hex Interpolate(Hex a, Hex b, int step, int steps)
        {
            if (steps == 0)
            {
                return a;
            }

            const long Fine = 8; // The nudge, at most 3, stays below half of this.
            long scale = steps * Fine;
            long q = (((long)a.Q * (steps - step)) + ((long)b.Q * step)) * Fine + 1;
            long r = (((long)a.R * (steps - step)) + ((long)b.R * step)) * Fine + 2;
            long s = (((long)a.S * (steps - step)) + ((long)b.S * step)) * Fine - 3;

            long rq = RoundDivide(q, scale), rr = RoundDivide(r, scale), rs = RoundDivide(s, scale);
            long dq = AbsLong((rq * scale) - q), dr = AbsLong((rr * scale) - r), ds = AbsLong((rs * scale) - s);
            if (dq > dr && dq > ds)
            {
                rq = -rr - rs;
            }
            else if (dr > ds)
            {
                rr = -rq - rs;
            }

            return new Hex((int)rq, (int)rr);
        }

        /// <summary>The integer nearest to value / divisor for a positive divisor; never a tie here.</summary>
        private static long RoundDivide(long value, long divisor) => FloorDivide((2 * value) + divisor, 2 * divisor);

        private static long FloorDivide(long value, long divisor)
        {
            long quotient = value / divisor;
            return (value % divisor != 0) && ((value < 0) != (divisor < 0)) ? quotient - 1 : quotient;
        }

        private static int Abs(int value) => value < 0 ? -value : value;

        private static long AbsLong(long value) => value < 0 ? -value : value;
    }
}
