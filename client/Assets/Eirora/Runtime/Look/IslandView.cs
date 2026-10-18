using Eirora.Simulation.Map;
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
        private readonly GameObject _nyon;
        private readonly MeshFilter _settlement;
        private readonly MeshFilter _lights;
        private readonly MeshFilter _selection;

        public IslandView(Transform parent, Material flat, Material glow)
        {
            Create("Terrain", parent, flat).sharedMesh = BuildTerrain(nyon: false);
            var nyon = Create("Nyön", parent, flat);
            nyon.sharedMesh = BuildTerrain(nyon: true);
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

            bool nyonFound = world.IsRevealed("N0");
            _nyon.SetActive(nyonFound);
            foreach (var tile in _map.Tiles)
            {
                var site = _map.SiteAt(tile.Key);
                if (!nyonFound && IslandMap.OnNyon(tile.Key))
                {
                    if (_map.IsLand(tile.Key))
                    {
                        Models.Fog(mesh, tile.Key.Center());
                    }

                    continue;
                }

                if (site is not null && !world.IsRevealed(site.Id))
                {
                    Models.Fog(mesh, tile.Key.Center() + (Vector3.up * Models.Height(tile.Value)));
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
                var c = hex.Center() + (Vector3.up * Models.Height(_map.Tiles[hex]));
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
                    Models.Scaffold(mesh, hex.Center() + (Vector3.up * Models.Height(_map.Tiles[hex])));
                }
            }

            SeaRoute(mesh, world);
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

        private Mesh BuildTerrain(bool nyon)
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

            return mesh.ToMesh(nyon ? "Nyön" : "Terrain");
        }

        /// <summary>The fairway between the jetties once ships sail, and the expedition's boat while it is out.</summary>
        private static void SeaRoute(MeshBuilder mesh, World world)
        {
            var from = new Hex(1, 1).Center() + new Vector3(1.2f, 0f, 0f);
            var to = IslandMap.NyonCentre.Center() - new Vector3(1.1f, 0f, 0f);
            if (world.SeaRouteOpen)
            {
                const int Dashes = 14;
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

        private static MeshFilter Create(string name, Transform parent, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.GetComponent<MeshFilter>();
        }
    }
}
