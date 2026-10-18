using System.Collections.Generic;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// Builds flat-shaded low-poly meshes: every face has its own vertices, so normals stay flat and facets read
    /// clearly (art-direction.md, section 3). Colours are given in sRGB and stored linear for the Flat shader.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _triangles = new();

        public int TriangleCount => _triangles.Count / 3;

        /// <summary>A convex polygon, wound clockwise when seen from the side it faces.</summary>
        public void Polygon(Color color, params Vector3[] corners)
        {
            int start = _vertices.Count;
            var linear = color.linear;
            foreach (var corner in corners)
            {
                _vertices.Add(corner);
                _colors.Add(linear);
            }

            for (int i = 1; i < corners.Length - 1; i++)
            {
                _triangles.Add(start);
                _triangles.Add(start + i);
                _triangles.Add(start + i + 1);
            }
        }

        /// <summary>A vertical prism: top face and sides, no bottom (the camera never sees it).</summary>
        public void Prism(Vector3[] outline, float bottom, float top, Color topColor, Color sideColor)
        {
            var lid = new Vector3[outline.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                lid[i] = new Vector3(outline[i].x, top, outline[i].z);
            }

            Polygon(topColor, lid);
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                Polygon(sideColor, new Vector3(a.x, top, a.z), new Vector3(a.x, bottom, a.z), new Vector3(b.x, bottom, b.z), new Vector3(b.x, top, b.z));
            }
        }

        /// <summary>An axis-aligned box centred on <paramref name="center"/> at ground level, rotated about Y.</summary>
        public void Box(Vector3 center, Vector3 size, float yaw, Color top, Color side)
        {
            var outline = Rectangle(center, size.x, size.z, yaw);
            Prism(outline, center.y, center.y + size.y, top, side);
        }

        /// <summary>A gabled house: walls, a pitched roof and gable ends (Townscaper-like silhouette, art-direction.md).</summary>
        public void House(Vector3 center, float width, float depth, float wallHeight, float roofHeight, float yaw, Color wall, Color roof)
        {
            Box(center, new Vector3(width, wallHeight, depth), yaw, wall, wall);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            float eave = center.y + wallHeight, ridge = eave + roofHeight;
            float hw = width / 2f * 1.08f, hd = depth / 2f * 1.1f;
            Vector3 P(float x, float y, float z) => center + (rotation * new Vector3(x, 0f, z)) + new Vector3(0f, y - center.y, 0f);

            // Ridge along the width; roof planes facing ±depth.
            Polygon(roof, P(-hw, eave, -hd), P(-hw, ridge, 0f), P(hw, ridge, 0f), P(hw, eave, -hd));
            Polygon(roof, P(hw, eave, hd), P(hw, ridge, 0f), P(-hw, ridge, 0f), P(-hw, eave, hd));
            float gw = width / 2f;
            Polygon(wall, P(-gw, eave, hd / 1.1f), P(-gw, ridge, 0f), P(-gw, eave, -hd / 1.1f));
            Polygon(wall, P(gw, eave, -hd / 1.1f), P(gw, ridge, 0f), P(gw, eave, hd / 1.1f));
        }

        /// <summary>A cone with a few facets: trees, villagers, spires.</summary>
        public void Cone(Vector3 baseCenter, float radius, float height, int sides, Color color, float twist = 0f)
        {
            var apex = baseCenter + (Vector3.up * height);
            for (int i = 0; i < sides; i++)
            {
                float a0 = twist + (i * Mathf.PI * 2f / sides), a1 = twist + ((i + 1) * Mathf.PI * 2f / sides);
                var p0 = baseCenter + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var p1 = baseCenter + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Polygon(color, p0, apex, p1);
            }
        }

        /// <summary>A row of windows on the front (−depth side) of a house standing at <paramref name="center"/>.</summary>
        public void Windows(Vector3 center, float yaw, float depth, int count, Color color)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < count; i++)
            {
                float x = (i - ((count - 1) / 2f)) * 0.26f;
                var w = center + (rotation * new Vector3(x, 0f, (-depth / 2f) - 0.005f)) + (Vector3.up * 0.12f);
                var right = rotation * new Vector3(0.05f, 0f, 0f);
                Polygon(color, w - right, w - right + (Vector3.up * 0.1f), w + right + (Vector3.up * 0.1f), w + right);
            }
        }

        /// <summary>Another builder's faces, scaled, turned and moved: a kit piece placed in a look.</summary>
        public void Append(MeshBuilder other, Vector3 at, Quaternion rotation, Vector3 scale)
        {
            int start = _vertices.Count;
            foreach (var vertex in other._vertices)
            {
                _vertices.Add(at + (rotation * Vector3.Scale(vertex, scale)));
            }

            _colors.AddRange(other._colors);
            foreach (int index in other._triangles)
            {
                _triangles.Add(start + index);
            }
        }

        /// <summary>
        /// A model made by hand, split so every face stays flat. Its vertex colours are sRGB, as Blender paints them;
        /// a model without them takes <paramref name="fallback"/>. The model must be readable (Art/Models sees to it).
        /// </summary>
        public void Add(Mesh model, Vector3 at, Quaternion rotation, Vector3 scale, Color fallback)
        {
            var vertices = model.vertices;
            var colors = model.colors;
            bool painted = colors.Length == vertices.Length;
            var plain = fallback.linear;
            foreach (int index in model.triangles)
            {
                _triangles.Add(_vertices.Count);
                _vertices.Add(at + (rotation * Vector3.Scale(vertices[index], scale)));
                _colors.Add(painted ? colors[index].linear : plain);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            WriteTo(mesh);
            return mesh;
        }

        /// <summary>Replaces what <paramref name="mesh"/> holds with these faces: a mesh redrawn often is reused, not leaked.</summary>
        public void WriteTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        public static Vector3[] Rectangle(Vector3 center, float width, float depth, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            float hw = width / 2f, hd = depth / 2f;
            return new[]
            {
                center + (rotation * new Vector3(-hw, 0f, -hd)),
                center + (rotation * new Vector3(-hw, 0f, hd)),
                center + (rotation * new Vector3(hw, 0f, hd)),
                center + (rotation * new Vector3(hw, 0f, -hd)),
            };
        }
    }
}
