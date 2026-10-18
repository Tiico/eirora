using System;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>A colour of the palette by name, so every look follows the art direction (D-61) and a palette change reaches them all.</summary>
    /// <remarks>Looks store the number, so new colours go last and none is ever removed or reordered.</remarks>
    public enum Swatch
    {
        DeepSea,
        ShallowSea,
        Sand,
        Grass,
        Forest,
        Mountain,
        Snow,
        Fog,
        FaluRed,
        Birch,
        Farmland,
        Pasture,
        Wood,
        Stone,
        Wool,
        RuneGlow,
        Window,
        Exploration,
        DawnPower,
        Black,
        White,
    }

    /// <summary>A palette colour, optionally mixed towards another: a shaded side, a darker roof, ripening grain.</summary>
    [Serializable]
    public struct Paint
    {
        public Swatch swatch;

        [Tooltip("The colour to mix towards; only used when Mix is above 0.")]
        public Swatch mixWith;

        [Range(0f, 1f)]
        public float mix;

        public Paint(Swatch swatch, Swatch mixWith = Swatch.Black, float mix = 0f)
        {
            this.swatch = swatch;
            this.mixWith = mixWith;
            this.mix = mix;
        }

        public Color Colour => Color.Lerp(Palette.Of(swatch), Palette.Of(mixWith), mix);
    }
}
