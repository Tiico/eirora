using Eirora.Simulation.Economy;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// Low-poly models built in code (NFR-14). Each building kind has its own silhouette, readable at 20 px
    /// (art-direction.md, section 3): steep gables, towers and chimneys.
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

            if (terrain == Terrain.Mountain)
            {
                var c = hex.Center + (Vector3.up * top);
                mesh.Cone(c, 0.8f, 0.9f, 5, Color.Lerp(Palette.Mountain, Color.black, 0.05f), 0.3f);
                mesh.Cone(c + new Vector3(0.38f, 0f, 0.3f), 0.5f, 0.55f, 5, Palette.Mountain, 1.1f);
                mesh.Cone(c + (Vector3.up * 0.58f), 0.27f, 0.32f, 5, Palette.Snow, 0.3f);
            }
            else if (terrain == Terrain.Farmland)
            {
                var c = hex.Center + (Vector3.up * (top + 0.01f));
                for (int row = -1; row <= 1; row++)
                {
                    var stripe = MeshBuilder.Rectangle(c + new Vector3(0f, 0f, row * 0.42f), 1.25f, 0.24f, 0f);
                    mesh.Polygon(Color.Lerp(Palette.Farmland, Palette.DawnPower, 0.25f), stripe);
                }
            }
        }

        public static void Trees(MeshBuilder mesh, Hex hex, int count, int seed)
        {
            var c = hex.Center;
            for (int i = 0; i < count; i++)
            {
                float a = (seed * 2.399f) + (i * 2.399f);
                float d = 0.18f + (0.55f * (((i * 7) + seed) % 5) / 5f);
                var p = c + new Vector3(Mathf.Cos(a) * d, LandHeight, Mathf.Sin(a) * d);
                float h = 0.55f + (0.12f * ((i + seed) % 3));
                mesh.Cone(p, 0.07f, 0.15f, 4, Palette.Wood);
                mesh.Cone(p + (Vector3.up * 0.1f), 0.24f, h, 5, Palette.Forest, a);
            }
        }

        /// <summary>A building; windows go to <paramref name="glow"/> so they can light up at dusk.</summary>
        public static void Building(MeshBuilder mesh, MeshBuilder glow, BuildingKind kind, Vector3 c, int level)
        {
            float lift = level == 2 ? 1.15f : 1f;
            switch (kind)
            {
                case BuildingKind.Longhouse:
                    mesh.House(c, 1.15f, 0.55f, 0.32f, 0.42f * lift, 15f, Palette.Birch, Palette.FaluRed);
                    Windows(glow, c, 15f, 0.55f, 3);
                    break;
                case BuildingKind.Cottage:
                    mesh.House(c, 0.6f, 0.48f, 0.3f, 0.36f, -10f, Palette.FaluRed, Color.Lerp(Palette.FaluRed, Color.black, 0.35f));
                    Windows(glow, c, -10f, 0.48f, 1);
                    break;
                case BuildingKind.Storehouse:
                    mesh.Box(c, new Vector3(0.7f, 0.42f, 0.7f), 0f, Palette.Wood, Color.Lerp(Palette.Wood, Palette.FaluRed, 0.4f));
                    mesh.Cone(c + (Vector3.up * 0.42f), 0.55f, 0.38f, 4, Palette.FaluRed, Mathf.PI / 4f);
                    break;
                case BuildingKind.FishingHut:
                    mesh.Box(c + new Vector3(0.2f, 0f, -0.25f), new Vector3(0.8f, 0.05f, 0.25f), 0f, Palette.Wood, Palette.Wood);
                    mesh.House(c + new Vector3(-0.15f, 0f, 0.1f), 0.5f, 0.42f, 0.26f, 0.3f, 0f, Palette.FaluRed, Palette.Birch);
                    break;
                case BuildingKind.Field:
                    mesh.House(c + new Vector3(0.45f, 0f, 0.4f), 0.32f, 0.28f, 0.18f, 0.2f, 30f, Palette.Wood, Palette.FaluRed);
                    break;
                case BuildingKind.Woodcutter:
                    mesh.House(c + new Vector3(-0.1f, 0f, -0.25f), 0.45f, 0.36f, 0.22f, 0.26f, 20f, Palette.Wood, Palette.FaluRed);
                    for (int i = 0; i < 3; i++)
                    {
                        mesh.Box(c + new Vector3(0.3f, i * 0.07f, -0.35f), new Vector3(0.35f, 0.07f, 0.08f), 20f, Palette.Sand, Palette.Wood);
                    }

                    break;
                case BuildingKind.SheepFarm:
                    mesh.House(c + new Vector3(-0.3f, 0f, 0.2f), 0.4f, 0.32f, 0.2f, 0.24f, 0f, Palette.FaluRed, Palette.Birch);
                    for (int i = 0; i < 4; i++)
                    {
                        mesh.Box(c + new Vector3(0.1f + (0.2f * (i % 2)), 0f, -0.2f - (0.22f * (i / 2))), new Vector3(0.14f, 0.1f, 0.1f), i * 25f, Palette.Wool, Palette.Wool);
                    }

                    break;
                case BuildingKind.Quarry:
                    mesh.Box(c + new Vector3(0.25f, 0f, -0.3f), new Vector3(0.5f, 0.15f, 0.4f), 10f, Palette.Stone, Color.Lerp(Palette.Stone, Color.black, 0.25f));
                    mesh.Box(c + new Vector3(-0.2f, 0f, -0.4f), new Vector3(0.3f, 0.32f, 0.3f), 10f, Palette.Stone, Color.Lerp(Palette.Stone, Color.black, 0.25f));
                    break;
                case BuildingKind.Sawmill:
                    mesh.House(c, 0.85f, 0.5f, 0.3f, 0.34f, 0f, Palette.Wood, Palette.FaluRed);
                    mesh.Box(c + new Vector3(0.55f, 0f, 0f), new Vector3(0.12f, 0.45f, 0.12f), 0f, Palette.Stone, Palette.Stone);
                    Windows(glow, c, 0f, 0.5f, 2);
                    break;
                case BuildingKind.Weavery:
                    mesh.House(c, 0.6f, 0.55f, 0.5f, 0.4f, 0f, Palette.Birch, Palette.FaluRed);
                    mesh.Box(c + new Vector3(0.2f, 0.5f, 0.12f), new Vector3(0.1f, 0.45f, 0.1f), 0f, Palette.Stone, Palette.Stone);
                    Windows(glow, c, 0f, 0.55f, 2);
                    break;
                case BuildingKind.Jetty:
                    mesh.Box(c + new Vector3(0.45f, -0.12f, -0.1f), new Vector3(1.1f, 0.06f, 0.28f), -20f, Palette.Wood, Palette.Wood);
                    mesh.House(c + new Vector3(-0.3f, 0f, 0.2f), 0.4f, 0.4f, 0.26f, 0.3f, 0f, Palette.Birch, Palette.FaluRed);
                    break;
                case BuildingKind.Lookout:
                    mesh.Box(c, new Vector3(0.3f, 0.95f, 0.3f), 0f, Palette.Stone, Color.Lerp(Palette.Stone, Color.black, 0.2f));
                    mesh.Cone(c + (Vector3.up * 0.95f), 0.3f, 0.35f, 4, Palette.FaluRed, Mathf.PI / 4f);
                    glow.Cone(c + (Vector3.up * 0.75f), 0.17f, 0.12f, 4, Palette.RuneGlow);
                    break;
            }
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

        private static void Windows(MeshBuilder glow, Vector3 c, float yaw, float depth, int count)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < count; i++)
            {
                float x = (i - ((count - 1) / 2f)) * 0.26f;
                var w = c + (rotation * new Vector3(x, 0f, (-depth / 2f) - 0.005f)) + (Vector3.up * 0.12f);
                var right = rotation * new Vector3(0.05f, 0f, 0f);
                glow.Polygon(Palette.Window, w - right, w - right + (Vector3.up * 0.1f), w + right + (Vector3.up * 0.1f), w + right);
            }
        }
    }
}
