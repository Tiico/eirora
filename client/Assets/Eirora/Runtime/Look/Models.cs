using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// The ground and the low-poly models that are not yet looks (NFR-14, art-direction.md, section 3). Buildings and
    /// nature are looks, put together as data in Resources/Looks (ADR-0009).
    /// </summary>
    public static class Models
    {
        public const float LandHeight = 0.3f;

        public static float Height(Terrain terrain) => terrain switch
        {
            Terrain.DeepSea => -0.25f,
            Terrain.ShallowSea => -0.08f,
            Terrain.Beach => 0.16f,
            Terrain.Mountain => 0.75f,
            _ => LandHeight,
        };

        public static Color Colour(Terrain terrain) => terrain switch
        {
            Terrain.DeepSea => Palette.DeepSea,
            Terrain.ShallowSea => Palette.ShallowSea,
            Terrain.Beach => Palette.Sand,
            Terrain.Forest => Color.Lerp(Palette.Grass, Palette.Forest, 0.45f),
            Terrain.Farmland => Palette.Farmland,
            Terrain.Pasture => Palette.Pasture,
            Terrain.Mountain => Palette.Mountain,
            _ => Palette.Grass,
        };

        public static void Tile(MeshBuilder mesh, Hex hex, Terrain terrain)
        {
            float top = Height(terrain);
            var colour = Colour(terrain);
            var side = Color.Lerp(colour, Color.black, 0.18f);
            // Land tiles keep a hairline gap so the board reads as hexes; the sea is one continuous surface.
            float scale = terrain is Terrain.DeepSea or Terrain.ShallowSea ? 1.002f : 0.985f;
            mesh.Prism(hex.Corners(0f, scale), -0.6f, top, colour, side);
        }

        /// <summary>A small square-sailed boat on the water.</summary>
        public static void Boat(MeshBuilder mesh, Vector3 c, float yaw)
        {
            var hull = Color.Lerp(Palette.Wood, Color.black, 0.15f);
            mesh.Box(c + new Vector3(0f, -0.12f, 0f), new Vector3(0.7f, 0.12f, 0.22f), yaw, Palette.Wood, hull);
            mesh.Box(c, new Vector3(0.04f, 0.6f, 0.04f), yaw, Palette.Wood, Palette.Wood);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var sailCentre = c + (Vector3.up * 0.38f) + (rotation * new Vector3(0f, 0f, -0.03f));
            var right = rotation * new Vector3(0.22f, 0f, 0f);
            mesh.Polygon(Palette.Birch, sailCentre - right + (Vector3.down * 0.18f), sailCentre - right + (Vector3.up * 0.18f), sailCentre + right + (Vector3.up * 0.18f), sailCentre + right + (Vector3.down * 0.18f));
            mesh.Polygon(Palette.FaluRed, sailCentre + right + (Vector3.down * 0.18f), sailCentre + right + (Vector3.up * 0.18f), sailCentre - right + (Vector3.up * 0.18f), sailCentre - right + (Vector3.down * 0.18f));
        }

        /// <summary>Poles and a half-built frame where work is queued.</summary>
        public static void Scaffold(MeshBuilder mesh, Vector3 c)
        {
            foreach (var corner in MeshBuilder.Rectangle(c, 0.7f, 0.5f, 10f))
            {
                mesh.Box(corner, new Vector3(0.05f, 0.5f, 0.05f), 10f, Palette.Wood, Palette.Wood);
            }

            mesh.Box(c + (Vector3.up * 0.45f), new Vector3(0.75f, 0.05f, 0.05f), 10f, Palette.Wood, Palette.Wood);
            mesh.Box(c, new Vector3(0.6f, 0.08f, 0.4f), 10f, Palette.Sand, Palette.Wood);
        }

        /// <summary>A fog cloud over an unexplored site (MAP-04).</summary>
        public static void Fog(MeshBuilder mesh, Vector3 c)
        {
            mesh.Cone(c + new Vector3(0f, 0f, 0f), 0.75f, 0.55f, 6, Palette.Fog, 0.2f);
            mesh.Cone(c + new Vector3(0.45f, 0f, 0.25f), 0.5f, 0.4f, 6, Color.Lerp(Palette.Fog, Color.white, 0.25f), 0.7f);
            mesh.Cone(c + new Vector3(-0.4f, 0f, -0.2f), 0.45f, 0.35f, 6, Color.Lerp(Palette.Fog, Color.white, 0.15f), 1.3f);
        }

        /// <summary>A small cone figure; villagers are work groups, shown as a few figures by day.</summary>
        public static void Villager(MeshBuilder mesh, Vector3 p, int index)
        {
            var coat = index % 3 == 0 ? Palette.Exploration : index % 3 == 1 ? Palette.FaluRed : Palette.DawnPower;
            mesh.Cone(p, 0.06f, 0.16f, 5, coat);
            mesh.Cone(p + (Vector3.up * 0.15f), 0.035f, 0.07f, 4, Palette.Birch);
        }
    }
}
