using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>The colours of art direction A, Gryningsdiorama, with the night of C (D-61, art-direction.md).</summary>
    public static class Palette
    {
        public static readonly Color DeepSea = Hex(0x2F7FA3);
        public static readonly Color ShallowSea = Hex(0x6CC4C0);
        public static readonly Color Sand = Hex(0xF0D9A8);
        public static readonly Color Grass = Hex(0x9BCB7A);
        public static readonly Color Forest = Hex(0x3F7F55);
        public static readonly Color Mountain = Hex(0xA3A0A8);
        public static readonly Color Snow = Hex(0xF5F3F8);
        public static readonly Color Fog = Hex(0xC9C2DD);
        public static readonly Color FaluRed = Hex(0xB4473A);
        public static readonly Color Birch = Hex(0xF3E9DA);
        public static readonly Color Farmland = Hex(0xD9C878);
        public static readonly Color Pasture = Hex(0xB5D98E);
        public static readonly Color Wood = Hex(0x8A6A4A);
        public static readonly Color Stone = Hex(0x8E8B93);
        public static readonly Color Wool = Hex(0xF7F3EA);

        /// <summary>A road of trodden earth, darker than the sand so it reads on the beach too (D-112).</summary>
        public static readonly Color Road = Hex(0xC9A97C);

        /// <summary>Rune glow: a light colour, never a surface colour.</summary>
        public static readonly Color RuneGlow = Hex(0xFFD27A);

        /// <summary>Lit windows at night.</summary>
        public static readonly Color Window = Hex(0xFFC27A);

        public static readonly Color AuroraGreen = Hex(0x6EF0B4);
        public static readonly Color AuroraViolet = Hex(0x9B7BF0);

        // Forces have their own colours and their own shapes (art-direction.md, section 2, principle 4).
        public static readonly Color Exploration = Hex(0x2A7FB0);
        public static readonly Color BuildPower = Hex(0xB4473A);
        public static readonly Color DawnPower = Hex(0xC27A12);

        public static readonly Color Panel = Hex(0xFBF7EF);
        public static readonly Color Text = Hex(0x2B2A33);

        /// <summary>Marks a building or nature kind that has no look yet, so it cannot be missed.</summary>
        public static readonly Color Missing = Hex(0xFF00FF);

        public static Color Of(Swatch swatch) => swatch switch
        {
            Swatch.DeepSea => DeepSea,
            Swatch.ShallowSea => ShallowSea,
            Swatch.Sand => Sand,
            Swatch.Grass => Grass,
            Swatch.Forest => Forest,
            Swatch.Mountain => Mountain,
            Swatch.Snow => Snow,
            Swatch.Fog => Fog,
            Swatch.FaluRed => FaluRed,
            Swatch.Birch => Birch,
            Swatch.Farmland => Farmland,
            Swatch.Pasture => Pasture,
            Swatch.Wood => Wood,
            Swatch.Stone => Stone,
            Swatch.Wool => Wool,
            Swatch.RuneGlow => RuneGlow,
            Swatch.Window => Window,
            Swatch.Exploration => Exploration,
            Swatch.DawnPower => DawnPower,
            Swatch.White => Color.white,
            _ => Color.black,
        };

        public static Color Hex(int rgb) =>
            new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
