using System;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>Axial hex coordinates (q, r) laid out pointy-top on the XZ plane.</summary>
    public readonly struct Hex : IEquatable<Hex>
    {
        public const float Size = 1f;
        private static readonly float s_sqrt3 = Mathf.Sqrt(3f);

        public Hex(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int Q { get; }

        public int R { get; }

        public Vector3 Center => new(Size * s_sqrt3 * (Q + (R / 2f)), 0f, -Size * 1.5f * R);

        public int DistanceTo(Hex other)
        {
            int dq = Q - other.Q, dr = R - other.R;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
        }

        /// <summary>The six corners at the given height, clockwise seen from above.</summary>
        public Vector3[] Corners(float y, float scale = 1f)
        {
            var corners = new Vector3[6];
            var center = Center;
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (90f - (60f * i));
                corners[i] = new Vector3(center.x + (Mathf.Cos(angle) * Size * scale), y, center.z + (Mathf.Sin(angle) * Size * scale));
            }

            return corners;
        }

        /// <summary>The hex containing a point on the ground plane.</summary>
        public static Hex FromWorld(Vector3 point)
        {
            float q = ((s_sqrt3 / 3f * point.x) + (point.z / 3f)) / Size;
            float r = -(2f / 3f * point.z) / Size;
            return Round(q, r);
        }

        public bool Equals(Hex other) => Q == other.Q && R == other.R;

        public override bool Equals(object? obj) => obj is Hex other && Equals(other);

        public override int GetHashCode() => (Q * 397) ^ R;

        private static Hex Round(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
            float dq = Mathf.Abs(rq - q), dr = Mathf.Abs(rr - r), ds = Mathf.Abs(rs - s);
            if (dq > dr && dq > ds)
            {
                rq = -rr - rs;
            }
            else if (dr > ds)
            {
                rr = -rq - rs;
            }

            return new Hex(rq, rr);
        }
    }
}
