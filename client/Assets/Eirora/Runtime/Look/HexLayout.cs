using Eirora.Simulation.Map;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// Lays the simulation's hexes out pointy-top on the XZ plane. The geometry itself (distance, neighbours, paths)
    /// lives in <see cref="Hex"/>, shared with the server (TASK-028); floats appear only here, for the look.
    /// </summary>
    public static class HexLayout
    {
        public const float Size = 1f;
        private static readonly float s_sqrt3 = Mathf.Sqrt(3f);

        /// <summary>The hex's centre on the ground plane.</summary>
        public static Vector3 Center(this Hex hex) => new(Size * s_sqrt3 * (hex.Q + (hex.R / 2f)), 0f, -Size * 1.5f * hex.R);

        /// <summary>The six corners at the given height, clockwise seen from above.</summary>
        public static Vector3[] Corners(this Hex hex, float y, float scale = 1f)
        {
            var corners = new Vector3[6];
            var center = hex.Center();
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
