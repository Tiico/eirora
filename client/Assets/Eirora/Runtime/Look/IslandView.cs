using System.Collections.Generic;
using System.Linq;
using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using UnityEngine;

namespace Eirora.Client.Look
{
    /// <summary>
    /// Draws the world's map and the settlement from the world's state (TASK-033); rebuilt whenever the world changes,
    /// into the same meshes each time. The map's ground is built once per map: the sea and the home island in one mesh,
    /// and every other island in its own, shown once an expedition has found it. Disposing it frees the meshes.
    /// </summary>
    public sealed class IslandView : System.IDisposable
    {
        /// <summary>How full a forest is drawn where a building stands in it, and at the least when it has been felled bare.</summary>
        public const float CrowdedForest = 0.25f;

        private readonly Transform _parent;
        private readonly Material _flat;
        private readonly Looks _looks;
        private readonly MeshFilter _terrain;
        private readonly Dictionary<string, GameObject> _islands = new(System.StringComparer.Ordinal);
        private readonly MeshFilter _settlement;
        private readonly MeshFilter _lights;
        private readonly MeshFilter _selection;
        private readonly MeshFilter _standingStill;
        private readonly List<Hex> _markedStandingStill = new();
        private readonly List<Mesh> _meshes = new();

        public IslandView(Transform parent, Material flat, Material glow, Looks looks)
        {
            _parent = parent;
            _flat = flat;
            _looks = looks;
            _terrain = Create("Terrain", parent, flat);
            _settlement = Create("Settlement", parent, flat);
            _lights = Create("Lights", parent, glow);
            _lights.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _selection = Create("Selection", parent, flat);

            // The signs float above the roofs; a shadow on the ground below would read as something standing there.
            _standingStill = Create("Standing still", parent, flat);
            _standingStill.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>The map drawn, or null before the first world is shown.</summary>
        public IslandMap? Map { get; private set; }

        /// <summary>The buildings marked as standing still, by tile (TASK-043).</summary>
        public IReadOnlyList<Hex> MarkedStandingStill => _markedStandingStill;

        /// <summary>
        /// The world's diagnosis while the villagers are awake, and null while they rest, when the village marks nothing
        /// as standing still (art-direction.md, section 2, principle 3). The island and the sheets read it alike.
        /// </summary>
        public static Diagnostics? DiagnoseWhileAwake(World world) => world.Phase == Phase.Active ? world.Diagnose() : null;

        /// <param name="diagnostics">The diagnosis to mark from, shared with the sheets; taken here when not given.</param>
        public void Refresh(World world, Diagnostics? diagnostics = null)
        {
            if (Map?.WorldMap.Hash != world.Map.Hash)
            {
                Show(world.Map);
            }

            var map = Map!;
            var mesh = new MeshBuilder();
            var lights = new MeshBuilder();
            var occupied = new HashSet<Hex>(world.Buildings.Select(b => b.Tile));
            occupied.UnionWith(world.Projects.Select(p => p.Tile));

            // Fog per tile from the world's state (TASK-032). An island an expedition has yet to find is not drawn at all,
            // so the fog never shows its outline; it is found whole, so one known tile shows it.
            var found = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var tile in map.Tiles)
            {
                if (map.AwayIslandAt(tile.Key) is string island && world.IsKnown(tile.Key))
                {
                    found.Add(island);
                }
            }

            foreach (var island in _islands)
            {
                island.Value.SetActive(found.Contains(island.Key));
            }

            foreach (var tile in map.Tiles)
            {
                bool fogged = !world.IsKnown(tile.Key);
                bool thin = fogged && world.CanLiftFog(tile.Key);
                if (map.AwayIslandAt(tile.Key) is string away && !found.Contains(away))
                {
                    Models.Fog(mesh, tile.Key.Center(), thin);
                    continue;
                }

                var ground = tile.Key.Center() + (Vector3.up * Mathf.Max(Models.Height(tile.Value), 0f));
                if (fogged)
                {
                    Models.Fog(mesh, ground, thin, sea: !map.IsLand(tile.Key));
                }

                if (NatureOf(tile.Value) is NatureKind nature)
                {
                    // Felled forest thins out as the woodcutters take its timber and fills again as it grows back.
                    float fullness = nature != NatureKind.Forest ? 1f
                        : occupied.Contains(tile.Key) ? CrowdedForest
                        : Mathf.Max(CrowdedForest, world.TimberLeft(tile.Key) / (float)World.TimberPerForest);
                    _looks.Nature(mesh, lights, nature, fullness, fogged, ground, Seed(tile.Key));
                }
            }

            // Buildings that stand still are marked while the villagers are awake; at night the whole village rests.
            var signs = new MeshBuilder();
            diagnostics ??= DiagnoseWhileAwake(world);
            _markedStandingStill.Clear();
            int villager = 0;
            foreach (var building in world.Buildings)
            {
                var hex = building.Tile;
                var c = hex.Center() + (Vector3.up * Models.Height(map.Tiles[hex]));
                _looks.Building(mesh, lights, building.Kind, building.Level, c, Seed(hex));
                if (diagnostics?.At(hex)?.StandsStill == true)
                {
                    StandingStill.Draw(signs, hex.Center() + (Vector3.up * Mathf.Max(Models.Height(map.Tiles[hex]), 0f)), App.CameraRig.Pitch);
                    _markedStandingStill.Add(hex);
                }

                if (building.Staffed && world.Phase == Phase.Active)
                {
                    Models.Villager(mesh, c + new Vector3(-0.42f, 0f, -0.45f), villager++);
                    Models.Villager(mesh, c + new Vector3(-0.25f, 0f, -0.55f), villager++);
                }
            }

            foreach (var project in world.Projects)
            {
                var hex = project.Tile;
                if (project.Kind == Simulation.Economy.ProjectKind.Build)
                {
                    Models.Scaffold(mesh, hex.Center() + (Vector3.up * Models.Height(map.Tiles[hex])));
                }
            }

            SeaRoute(mesh, world);
            mesh.WriteTo(_settlement.sharedMesh);
            lights.WriteTo(_lights.sharedMesh);
            signs.WriteTo(_standingStill.sharedMesh);
        }

        /// <summary>Marks the selected hex with a glowing rim, or clears the mark.</summary>
        public void Select(Hex? hex)
        {
            var mesh = new MeshBuilder();
            if (hex is Hex selected && Map is { } map && map.Tiles.TryGetValue(selected, out var terrain))
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
            RemoveIslands();
            foreach (var mesh in _meshes)
            {
                Destroy(mesh);
            }

            _meshes.Clear();
        }

        /// <summary>Builds the ground of a map: the sea and the home island, and each other island on its own.</summary>
        private void Show(WorldMap source)
        {
            RemoveIslands();
            var map = new IslandMap(source);
            var home = new MeshBuilder();
            var away = new SortedDictionary<string, MeshBuilder>(System.StringComparer.Ordinal);
            foreach (var tile in map.Tiles)
            {
                var mesh = home;
                if (map.AwayIslandAt(tile.Key) is string island && !away.TryGetValue(island, out mesh))
                {
                    away[island] = mesh = new MeshBuilder();
                }

                Models.Tile(mesh, tile.Key, tile.Value);
            }

            home.WriteTo(_terrain.sharedMesh);
            foreach (var island in away)
            {
                var filter = Create(island.Key, _parent, _flat);
                island.Value.WriteTo(filter.sharedMesh);
                _islands[island.Key] = filter.gameObject;
            }

            Map = map;
        }

        private void RemoveIslands()
        {
            foreach (var island in _islands.Values)
            {
                var mesh = island.GetComponent<MeshFilter>().sharedMesh;
                _meshes.Remove(mesh);
                Destroy(mesh);
                Destroy(island);
            }

            _islands.Clear();
        }

        private static void Destroy(Object thing)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(thing);
            }
            else
            {
                Object.DestroyImmediate(thing);
            }
        }

        /// <summary>
        /// The fairway between the jetties once ships sail, and the expedition's boat while it is out: along the shortest
        /// way by sea, from shore to shore, wherever the jetties stand (D-89).
        /// </summary>
        private static void SeaRoute(MeshBuilder mesh, World world)
        {
            var home = JettyOn(world, IslandId.Home);
            if (home is null)
            {
                return;
            }

            var destination = world.SeaRouteOpen ? JettyOn(world, IslandId.Nyon) : world.ExpeditionFinds;
            if (destination is null)
            {
                return;
            }

            var way = Fairway(world.Map.Features.SeaRoute(new[] { home.Value }, new[] { destination.Value }));
            if (way.Count < 2)
            {
                return;
            }

            if (world.SeaRouteOpen)
            {
                float length = Length(way);
                int dashes = Mathf.Max(3, Mathf.RoundToInt(length / 0.6f) | 1); // Odd, so the line starts and ends with a dash.
                for (int i = 0; i < dashes; i += 2)
                {
                    var a = Along(way, length * i / dashes) + (Vector3.up * 0.01f);
                    var b = Along(way, length * (i + 1) / dashes) + (Vector3.up * 0.01f);
                    var side = Vector3.Cross(b - a, Vector3.up).normalized * 0.05f;
                    mesh.Polygon(Palette.Birch, a - side, a + side, b + side, b - side);
                }

                Models.Boat(mesh, Along(way, length * 0.55f), 0f);
            }
            else if (world.ExpeditionHoursLeft > 0)
            {
                float sailed = 1f - (world.ExpeditionHoursLeft / (float)PrototypeIsland.ExpeditionHours);
                Models.Boat(mesh, Along(way, Length(way) * Mathf.Lerp(0.05f, 0.9f, sailed)), 0f);
            }
        }

        private static Hex? JettyOn(World world, IslandId island) =>
            world.Buildings.Where(b => b.Kind == BuildingKind.Jetty && b.Island == island).Select(b => (Hex?)b.Tile).FirstOrDefault();

        /// <summary>The way through the sea tiles' centres, from where it leaves one shore to where it meets the other.</summary>
        private static List<Vector3> Fairway(IReadOnlyList<Hex> route)
        {
            var way = new List<Vector3>();
            if (route.Count < 3)
            {
                return way;
            }

            way.Add(Vector3.Lerp(route[0].Center(), route[1].Center(), 0.5f));
            for (int i = 1; i < route.Count - 1; i++)
            {
                way.Add(route[i].Center());
            }

            way.Add(Vector3.Lerp(route[route.Count - 2].Center(), route[route.Count - 1].Center(), 0.5f));
            return way;
        }

        private static float Length(List<Vector3> way)
        {
            float length = 0f;
            for (int i = 1; i < way.Count; i++)
            {
                length += Vector3.Distance(way[i - 1], way[i]);
            }

            return length;
        }

        private static Vector3 Along(List<Vector3> way, float distance)
        {
            for (int i = 1; i < way.Count; i++)
            {
                float step = Vector3.Distance(way[i - 1], way[i]);
                if (distance <= step)
                {
                    return Vector3.Lerp(way[i - 1], way[i], step > 0f ? distance / step : 0f);
                }

                distance -= step;
            }

            return way[way.Count - 1];
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
