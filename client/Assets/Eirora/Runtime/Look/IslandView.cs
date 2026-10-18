using System.Collections.Generic;
using System.Linq;
using Eirora.Simulation;
using Eirora.Simulation.Time;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>Draws the island and the settlement from the world's state; rebuilt whenever the world changes.</summary>
    public sealed class IslandView
    {
        private readonly IslandMap _map = new();
        private readonly MeshFilter _settlement;
        private readonly MeshFilter _lights;
        private readonly MeshFilter _selection;

        public IslandView(Transform parent, Material flat, Material glow)
        {
            Create("Terrain", parent, flat).sharedMesh = BuildTerrain();
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

            foreach (var tile in _map.Tiles)
            {
                var site = _map.SiteAt(tile.Key);
                if (site is not null && !world.IsRevealed(site.Id))
                {
                    Models.Fog(mesh, tile.Key.Center + (Vector3.up * Models.Height(tile.Value)));
                    continue;
                }

                if (tile.Value == Terrain.Forest)
                {
                    Models.Trees(mesh, tile.Key, occupied.Contains(tile.Key) ? 2 : 6, (tile.Key.Q * 31) + tile.Key.R);
                }
            }

            int villager = 0;
            foreach (var building in world.Buildings)
            {
                var hex = IslandMap.HexOf(building.Site);
                var c = hex.Center + (Vector3.up * Models.Height(_map.Tiles[hex]));
                Models.Building(mesh, lights, building.Kind, c, building.Level);
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
                    Models.Scaffold(mesh, hex.Center + (Vector3.up * Models.Height(_map.Tiles[hex])));
                }
            }

            _settlement.sharedMesh = mesh.ToMesh("Settlement");
            _lights.sharedMesh = lights.ToMesh("Lights");
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

            _selection.sharedMesh = mesh.ToMesh("Selection");
        }

        private Mesh BuildTerrain()
        {
            var mesh = new MeshBuilder();
            foreach (var tile in _map.Tiles)
            {
                Models.Tile(mesh, tile.Key, tile.Value);
            }

            return mesh.ToMesh("Terrain");
        }

        private static MeshFilter Create(string name, Transform parent, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.GetComponent<MeshFilter>();
        }
    }
}
