using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>One key light of the day cycle (art-direction.md, section 3 and section 11 for the night).</summary>
    public readonly struct LightMood
    {
        public LightMood(Color zenith, Color horizon, Color sun, float elevation, Color skyLight, float darkness)
        {
            Zenith = zenith;
            Horizon = horizon;
            Sun = sun;
            Elevation = elevation;
            SkyLight = skyLight;
            Darkness = darkness;
        }

        public static LightMood Dawn { get; } = new(Palette.Hex(0x8FA2D4), Palette.Hex(0xF6C7A8), Palette.Hex(0xFFBE94), 14f, Palette.Hex(0xE2D2EC), 0f);

        public static LightMood Day { get; } = new(Palette.Hex(0x66AEE6), Palette.Hex(0xD3ECF7), Palette.Hex(0xFFF6E8), 55f, Palette.Hex(0xDDF0FB), 0f);

        public static LightMood Dusk { get; } = new(Palette.Hex(0x4A4A8C), Palette.Hex(0xF2A07E), Palette.Hex(0xFF9466), 12f, Palette.Hex(0xA898CC), 0.25f);

        /// <summary>The rest-lock night of direction C (D-61): moonlight, stars and a dimmed aurora.</summary>
        public static LightMood Night { get; } = new(Palette.Hex(0x060A1A), Palette.Hex(0x10274A), Palette.Hex(0x86AEE0), 38f, Palette.Hex(0x2E4A78), 1f);

        public Color Zenith { get; }

        public Color Horizon { get; }

        /// <summary>The sun's or, at night, the moon's colour.</summary>
        public Color Sun { get; }

        /// <summary>Height of the sun or moon above the horizon, in degrees.</summary>
        public float Elevation { get; }

        /// <summary>Light from the sky that fills the shadows.</summary>
        public Color SkyLight { get; }

        /// <summary>0 by day, 1 at night: drives stars, aurora and lit windows.</summary>
        public float Darkness { get; }

        public static LightMood Lerp(LightMood a, LightMood b, float t) => new(
            Color.Lerp(a.Zenith, b.Zenith, t),
            Color.Lerp(a.Horizon, b.Horizon, t),
            Color.Lerp(a.Sun, b.Sun, t),
            Mathf.Lerp(a.Elevation, b.Elevation, t),
            Color.Lerp(a.SkyLight, b.SkyLight, t),
            Mathf.Lerp(a.Darkness, b.Darkness, t));
    }
}
