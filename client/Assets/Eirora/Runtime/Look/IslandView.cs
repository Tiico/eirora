using System.Collections.Generic;
using System.Linq;
using Eirora.Simulation;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// Draws the island and the settlement from the world's state; rebuilt whenever the world changes, into the same
    /// meshes each time. Disposing it frees them.
    /// </summary>
    public sealed class IslandView : System.IDisposable
    {
        /// <summary>How full a forest is drawn where a building or work leaves room for less, until the economy shows its stock.</summary>
        public const float CrowdedForest = 0.25f;

        // Every world is on the prototype's map until generated worlds are drawn (TASK-033).
        private readonly IslandMap _map = new(PrototypeHome.Map);
        private readonly Looks _looks;
        private readonly GameObject _nyon;
        private readonly MeshFilter _settlement;
        private readonly MeshFilter _lights;
        private readonly MeshFilter _selection;
        private readonly List<Mesh> _meshes = new();

        public IslandView(Transform parent, Material flat, Material glow, Looks looks)
        {
            _looks = looks;
            BuildTerrain(nyon: false).WriteTo(Create("Terrain", parent, flat).sharedMesh);
            var nyon = Create("Nyön", parent, flat);
            BuildTerrain(nyon: true).WriteTo(nyon.sharedMesh);
            _nyon = nyon.gameObject;
            _settlement = Create("Settlement", parent, flat);
            _lights = Create("Lights", parent, glow);
            _lights.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _selection = Create("Selection", parent, flat);
        }

        public IslandMap Map => _map;

        public void Refresh(World world)
        {
            var mesh = new MeshBuilder();
            var lights = new MeshBuilder();
            var occupied = new HashSet<Hex>(world.Buildings.Select(b => IslandMap.HexOf(b.Site)));
            occupied.UnionWith(world.Projects.Select(p => IslandMap.HexOf(p.Site)));

            // Fog per tile from the world's state (TASK-032); Nyön's land is its own mesh, shown once it is found.
            bool nyonFound = world.IsKnown(IslandMap.NyonCentre);
            _nyon.SetActive(nyonFound);
            foreach (var tile in _map.Tiles)
            {
                bool fogged = !world.IsKnown(tile.Key);
                bool thin = fogged && world.CanLiftFog(tile.Key);
                if (!nyonFound && IslandMap.OnNyon(tile.Key) && _map.IsLand(tile.Key))
                {
                    Models.Fog(mesh, tile.Key.Center(), thin);
                    continue;
                }

                var ground = tile.Key.Center() + (Vector3.up * Mathf.Max(Models.Height(tile.Value), 0f));
                if (fogged)
                {
                    Models.Fog(mesh, ground, thin, sea: !_map.IsLand(tile.Key));
                }

                if (NatureOf(tile.Value) is NatureKind nature)
                {
                    float fullness = nature == NatureKind.Forest && occupied.Contains(tile.Key) ? CrowdedForest : 1f;
                    _looks.Nature(mesh, lights, nature, fullness, fogged, ground, Seed(tile.Key));
                }
            }

            int villager = 0;
            foreach (var building in world.Buildings)
            {
                var hex = IslandMap.HexOf(building.Site);
                var c = hex.Center() + (Vector3.up * Models.Height(_map.Tiles[hex]));
                _looks.Building(mesh, lights, building.Kind, building.Level, c, Seed(hex));
                if (building.Staffed && world.Phase == Phase.Active)
                {
                    Models.Villager(mesh, c + new Vector3(-0.42f, 0f, -0.45f), villager++);
                    Models.Villager(mesh, c + new Vector3(-0.25f, 0f, -0.55f), villager++);
                }
            }

            foreach (var project in world.Projects)
            {
                var hex = IslandMap.HexOf(project.Site);
                if (project.Kind == Simulation.Economy.ProjectKind.Build)
                {
                    Models.Scaffold(mesh, hex.Center() + (Vector3.up * Models.Height(_map.Tiles[hex])));
                }
            }

            SeaRoute(mesh, world);
            mesh.WriteTo(_settlement.sharedMesh);
            lights.WriteTo(_lights.sharedMesh);
        }

        /// <summary>Marks the selected hex with a glowing rim, or clears the mark.</summary>
        public void Select(Hex? hex)
        {
            var mesh = new MeshBuilder();
            if (hex is Hex selected && _map.Tiles.TryGetValue(selected, out var terrain))
            {
                float y = Mathf.Max(Models.Height(terrain), 0f) + 0.02f;
                var outer = selected.Corners(y, 1.0f);
                var inner = selected.Corners(y, 0.86f);
                for (int i = 0; i < 6; i++)
                {
                    int j = (i + 1) % 6;
                    mesh.Polygon(Palette.RuneGlow, inner[i], outer[i], outer[j], inner[j]);
                }
            }

            mesh.WriteTo(_selection.sharedMesh);
        }

        public void Dispose()
        {
            foreach (var mesh in _meshes)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(mesh);
                }
                else
                {
                    Object.DestroyImmediate(mesh);
                }
            }

            _meshes.Clear();
        }

        private MeshBuilder BuildTerrain(bool nyon)
        {
            var mesh = new MeshBuilder();
            foreach (var tile in _map.Tiles)
            {
                // Nyön's land is its own mesh, shown once an expedition has found it; the sea is always there.
                bool partOfNyon = IslandMap.OnNyon(tile.Key) && _map.IsLand(tile.Key);
                if (partOfNyon == nyon)
                {
                    Models.Tile(mesh, tile.Key, tile.Value);
                }
            }

            return mesh;
        }

        /// <summary>How far a hex's side lies from its centre (half the width of a pointy-top hex), plus a sliver.</summary>
        private const float Shore = 0.9f;

        /// <summary>The fairway between the jetties once ships sail, and the expedition's boat while it is out.</summary>
        private static void SeaRoute(MeshBuilder mesh, World world)
        {
            // From shore to shore: Hemön's harbour and Nyön's, across the bay that faces Hemön.
            var shore = new Vector3(Shore, 0f, 0f);
            var from = new Hex(1, 1).Center() + shore;
            var to = IslandMap.NyonCentre.Center() - shore;
            if (world.SeaRouteOpen)
            {
                const int Dashes = 15; // Odd, so the line starts and ends with a dash at the shores.
                for (int i = 0; i < Dashes; i += 2)
                {
                    var a = Vector3.Lerp(from, to, i / (float)Dashes) + (Vector3.up * 0.01f);
                    var b = Vector3.Lerp(from, to, (i + 1) / (float)Dashes) + (Vector3.up * 0.01f);
                    var side = Vector3.Cross(b - a, Vector3.up).normalized * 0.05f;
                    mesh.Polygon(Palette.Birch, a - side, a + side, b + side, b - side);
                }

                Models.Boat(mesh, Vector3.Lerp(from, to, 0.55f), 0f);
            }
            else if (world.ExpeditionHoursLeft > 0)
            {
                float sailed = 1f - (world.ExpeditionHoursLeft / (float)Simulation.Economy.PrototypeIsland.ExpeditionHours);
                Models.Boat(mesh, Vector3.Lerp(from, to, Mathf.Lerp(0.05f, 0.9f, sailed)), 0f);
            }
        }

        public static NatureKind? NatureOf(Terrain terrain) => terrain switch
        {
            Terrain.Forest => NatureKind.Forest,
            Terrain.Farmland => NatureKind.Farmland,
            Terrain.Mountain => NatureKind.Mountain,
            _ => null,
        };

        private static int Seed(Hex hex) => (hex.Q * 31) + hex.R;

        private MeshFilter Create(string name, Transform parent, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var filter = go.GetComponent<MeshFilter>();
            filter.sharedMesh = new Mesh { name = name };
            _meshes.Add(filter.sharedMesh);
            return filter;
        }
    }
}
