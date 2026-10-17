using UnityEngine;

namespace Eirora.Client
{
    /// <summary>Flat-shaded low-poly meshes for hex tiles (pointy-top, axial layout).</summary>
    public static class HexMesh
    {
        private const int Corners = 6;

        /// <summary>
        /// A hexagonal prism with its top face at y = 0 and no bottom face (the camera never sees it).
        /// Each face has its own vertices so normals stay flat.
        /// </summary>
        public static Mesh Prism(float radius, float height)
        {
            var vertices = new Vector3[Corners + (Corners * 4)];
            var triangles = new int[(Corners - 2) * 3 + (Corners * 6)];
            int v = 0, t = 0;

            // Top face as a fan.
            for (int i = 0; i < Corners; i++)
            {
                vertices[v++] = Corner(i, radius, 0f);
            }

            for (int i = 1; i < Corners - 1; i++)
            {
                triangles[t++] = 0;
                triangles[t++] = i + 1;
                triangles[t++] = i;
            }

            // Sides: one quad per edge with its own vertices.
            for (int i = 0; i < Corners; i++)
            {
                int start = v;
                vertices[v++] = Corner(i, radius, 0f);
                vertices[v++] = Corner(i + 1, radius, 0f);
                vertices[v++] = Corner(i + 1, radius, -height);
                vertices[v++] = Corner(i, radius, -height);
                triangles[t++] = start;
                triangles[t++] = start + 1;
                triangles[t++] = start + 2;
                triangles[t++] = start;
                triangles[t++] = start + 2;
                triangles[t++] = start + 3;
            }

            var mesh = new Mesh { name = "HexPrism", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Corner(int index, float radius, float y)
        {
            float angle = Mathf.Deg2Rad * (60f * index - 30f);
            return new Vector3(radius * Mathf.Cos(angle), y, radius * Mathf.Sin(angle));
        }
    }
}
