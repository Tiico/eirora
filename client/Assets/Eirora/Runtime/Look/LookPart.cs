using System;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>What a part of a look is drawn as.</summary>
    /// <remarks>Looks store the number, so new shapes go last and none is ever removed or reordered.</remarks>
    public enum LookShape
    {
        /// <summary>Walls and a gabled roof (<see cref="MeshBuilder.House"/>).</summary>
        House,

        /// <summary>A block standing on its base: a jetty, a chimney, a stone.</summary>
        Box,

        /// <summary>A faceted cone: a tree, a pyramid roof, a mountain, a beacon.</summary>
        Cone,

        /// <summary>A flat rectangle lying on the ground: a furrow, a path.</summary>
        Plate,

        /// <summary>A row of windows on a house's front, lit at dusk.</summary>
        Windows,

        /// <summary>A model made by hand, such as in Blender, coloured per vertex (Art/Models).</summary>
        Mesh,

        /// <summary>A reusable piece of the kit, once or scattered several times.</summary>
        Piece,
    }

    /// <summary>
    /// One part of a look: a shape, where it stands on the hex and how it is painted. Positions are relative to the
    /// centre of the hex at ground level, in hex units (a hex is 2 wide).
    /// </summary>
    [Serializable]
    public sealed class LookPart
    {
        private const int MaxNesting = 4;

        [Tooltip("A name for the part; a later stage with a part of the same name replaces it, and Removes takes it away.")]
        public string id = "";

        public LookShape shape;

        [Tooltip("Where the part stands, from the centre of the hex at ground level.")]
        public Vector3 position;

        [Tooltip("Turn around the vertical axis, in degrees.")]
        public float yaw;

        [Tooltip("House: width, wall height, depth. Box: width, height, depth. Cone: radius (x) and height (y). Plate: width (x) and depth (z). Windows: the house's depth (z). Mesh and Piece: scale.")]
        public Vector3 size = Vector3.one;

        [Tooltip("House: the roof's height above the eaves.")]
        public float roof;

        [Tooltip("Cone: number of sides.")]
        public int sides = 5;

        [Tooltip("Windows: number of windows. Piece: number of copies.")]
        public int count = 1;

        [Tooltip("House: walls. Box: top. Cone, Plate and Windows: everything. Mesh: used where the model has no vertex colours.")]
        public Paint paint;

        [Tooltip("House: roof. Box: sides.")]
        public Paint trim;

        [Tooltip("Shines at dusk and night instead of being lit, like windows and rune glow.")]
        public bool glow;

        [Tooltip("Mesh: the model, from Assets/Eirora/Art/Models.")]
        public Mesh? mesh;

        [Tooltip("Piece: the kit piece to place.")]
        public LookPiece? piece;

        [Tooltip("Piece: copies are scattered within this distance of Position.")]
        public float spread;

        [Tooltip("Piece: scattered copies grow up to this much taller (0.4 = 40 %).")]
        public float growth;

        /// <summary>Draws the part; lights go to <paramref name="glows"/>.</summary>
        /// <param name="seed">Varies scattered pieces from hex to hex.</param>
        public void Draw(MeshBuilder surfaces, MeshBuilder glows, Vector3 at, int seed, int nesting = 0)
        {
            var target = glow ? glows : surfaces;
            var c = at + position;
            switch (shape)
            {
                case LookShape.House:
                    target.House(c, size.x, size.z, size.y, roof, yaw, paint.Colour, trim.Colour);
                    break;
                case LookShape.Box:
                    target.Box(c, size, yaw, paint.Colour, trim.Colour);
                    break;
                case LookShape.Cone:
                    // A cone's facets turn the same way as every other part.
                    target.Cone(c, size.x, size.y, sides, paint.Colour, -yaw * Mathf.Deg2Rad);
                    break;
                case LookShape.Plate:
                    target.Polygon(paint.Colour, MeshBuilder.Rectangle(c, size.x, size.z, yaw));
                    break;
                case LookShape.Windows:
                    glows.Windows(c, yaw, size.z, count, paint.Colour);
                    break;
                case LookShape.Mesh:
                    if (mesh != null)
                    {
                        target.Add(mesh, c, Quaternion.Euler(0f, yaw, 0f), size, paint.Colour);
                    }

                    break;
                case LookShape.Piece:
                    if (piece != null && nesting < MaxNesting)
                    {
                        Scatter(piece, surfaces, glows, c, seed, nesting);
                    }

                    break;
            }
        }

        /// <summary>
        /// Places copies of a piece: one at <paramref name="c"/>, or several around it on a golden-angle spiral that
        /// depends only on the seed and the copy's number, so a hex keeps its pattern and copy n is the same tree
        /// whether the forest has three trees or six.
        /// </summary>
        private void Scatter(LookPiece kit, MeshBuilder surfaces, MeshBuilder glows, Vector3 c, int seed, int nesting)
        {
            const float GoldenAngle = 2.399f;
            var pieceSurfaces = new MeshBuilder();
            var pieceGlows = new MeshBuilder();
            foreach (var part in kit.parts)
            {
                part.Draw(pieceSurfaces, pieceGlows, Vector3.zero, seed, nesting + 1);
            }

            for (int i = 0; i < count; i++)
            {
                var offset = Vector3.zero;
                float turn = 0f, taller = 0f;
                if (spread > 0f)
                {
                    float angle = (seed + i) * GoldenAngle;
                    int ring = Modulo((i * 7) + seed, 5);
                    float distance = spread * (0.25f + (0.15f * ring));
                    offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                    turn = -angle * Mathf.Rad2Deg;
                    taller = growth * Modulo(i + seed, 3) / 2f;
                }

                var rotation = Quaternion.Euler(0f, yaw + turn, 0f);
                var scale = new Vector3(size.x, size.y * (1f + taller), size.z);
                surfaces.Append(pieceSurfaces, c + offset, rotation, scale);
                glows.Append(pieceGlows, c + offset, rotation, scale);
            }
        }

        private static int Modulo(int value, int divisor) => ((value % divisor) + divisor) % divisor;
    }
}
