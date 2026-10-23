using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// The sign above a building that stands still (TASK-043): two bars on a light disc with a dark rim, turned to the
    /// camera. Its shape says it, not its colour (UX-05), and the building's sheet shows the same sign.
    /// </summary>
    public static class StandingStill
    {
        /// <summary>The light face's radius, as a share of the whole sign's.</summary>
        public const float Face = 0.8f;

        /// <summary>How far each bar's centre lies from the middle, as a share of the radius.</summary>
        public const float BarGap = 0.24f;

        /// <summary>Half a bar's width and height, as shares of the radius.</summary>
        public const float BarWidth = 0.11f;

        public const float BarHeight = 0.4f;

        /// <summary>How high above the building's tile the sign floats, clear of the tallest roof.</summary>
        public const float Height = 1.05f;

        private const float Radius = 0.19f;
        private const int Sides = 16;

        /// <summary>Draws the sign above a building standing on a tile whose top is at <paramref name="ground"/>.</summary>
        public static void Draw(MeshBuilder mesh, Vector3 ground, float pitch)
        {
            // Square to the camera, which looks down at the pitch and never turns (CameraRig).
            var toward = Quaternion.Euler(pitch, 0f, 0f) * Vector3.back;
            var right = Vector3.right;
            var up = Vector3.Cross(right, toward);
            var centre = ground + (Vector3.up * Height);
            Disc(mesh, centre, right, up, Radius, Palette.Text);
            // The panels' white rather than birch, which the sun shades grey on a face turned from it.
            Disc(mesh, centre + (toward * 0.004f), right, up, Radius * Face, Palette.Panel);
            foreach (float side in new[] { -1f, 1f })
            {
                var bar = centre + (toward * 0.008f) + (right * (side * Radius * BarGap));
                var w = right * (Radius * BarWidth);
                var h = up * (Radius * BarHeight);
                mesh.Polygon(Palette.Text, bar - w + h, bar + w + h, bar + w - h, bar - w - h);
            }
        }

        // Clockwise as the camera sees it, so the face is turned to it.
        private static void Disc(MeshBuilder mesh, Vector3 centre, Vector3 right, Vector3 up, float radius, Color colour)
        {
            var corners = new Vector3[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float angle = (Mathf.PI / 2f) - (i * 2f * Mathf.PI / Sides);
                corners[i] = centre + (radius * ((Mathf.Cos(angle) * right) + (Mathf.Sin(angle) * up)));
            }

            mesh.Polygon(colour, corners);
        }
    }
}
