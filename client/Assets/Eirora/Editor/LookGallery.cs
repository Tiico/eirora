using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eirora.Client.Look;
using Eirora.Simulation.Economy;
using UnityEditor;
using UnityEngine;
using Terrain = Eirora.Client.Look.Terrain;

namespace Eirora.Editor
{
    /// <summary>
    /// Every building and nature look side by side, one column per level or stage, in the game's light and redrawn as
    /// soon as a look or piece changes. Click a stage to edit its look in the inspector (ADR-0009, docs/design/looks.md).
    /// </summary>
    /// <remarks>
    /// Export every stage as PNG for review without the editor open:
    /// Unity -batchmode -quit -projectPath client -executeMethod Eirora.Editor.LookGallery.Export [-looksOutput dir].
    /// It renders, so it cannot run with -nographics.
    /// </remarks>
    public sealed class LookGallery : EditorWindow
    {
        private const string LooksFolder = "Assets/Eirora/Resources/" + Looks.Folder;
        private const int ExportSize = 512;
        private const float TitleWidth = 110f;

        private static readonly (string Name, LightMood Mood, float Azimuth)[] s_moods =
        {
            ("Dawn", LightMood.Dawn, Atmosphere.East),
            ("Day", LightMood.Day, Atmosphere.South),
            ("Dusk", LightMood.Dusk, Atmosphere.West),
            ("Night", LightMood.Night, Atmosphere.Moon),
        };

        private readonly Dictionary<(int Look, int Stage), Texture2D> _renders = new();
        private LookPreview? _preview;
        private Looks _looks = Looks.Load();
        private List<string> _problems = new();
        private Vector2 _scroll;
        private int _mood = 1;
        private float _cellSize = 150f;
        private GUIStyle? _caption;
        private GUIStyle? _problem;

        [MenuItem("Eirora/Look Gallery")]
        public static void Open() => GetWindow<LookGallery>("Looks");

        /// <summary>Renders every stage of every look in day and dusk light to PNG files, for review outside the editor.</summary>
        public static void Export() => Export(Argument("-looksOutput") ?? Path.Combine("..", "artifacts", "looks"));

        private static void Export(string folder)
        {
            Directory.CreateDirectory(folder);
            var looks = Looks.Load();
            using var preview = new LookPreview();
            int count = 0;
            foreach (var row in Rows(looks))
            {
                if (row.Look == null)
                {
                    continue;
                }

                for (int stage = 0; stage < row.Look.Stages.Count; stage++)
                {
                    foreach (var (name, mood, azimuth) in s_moods.Where(m => m.Name is "Day" or "Dusk"))
                    {
                        var texture = preview.Render(new Rect(0f, 0f, ExportSize, ExportSize), row.Look, stage, row.Ground, row.Nature, row.Fullness, mood, azimuth);
                        File.WriteAllBytes(Path.Combine(folder, $"{row.Title}-{stage + 1}-{name.ToLowerInvariant()}.png"), texture.EncodeToPNG());
                        DestroyImmediate(texture);
                        count++;
                    }
                }
            }

            Debug.Log($"Exported {count} look renders to {Path.GetFullPath(folder)}.");
        }

        private void OnEnable()
        {
            StagedLook.Edited += Redraw;
            EditorApplication.projectChanged += Reload;
            Reload();
        }

        private void OnDisable()
        {
            StagedLook.Edited -= Redraw;
            EditorApplication.projectChanged -= Reload;
            ClearRenders();
            _preview?.Dispose();
            _preview = null;
        }

        private void Reload()
        {
            _looks = Looks.Load();
            _problems = Problems();
            Redraw();
        }

        private void Redraw()
        {
            ClearRenders();
            Repaint();
        }

        private void ClearRenders()
        {
            foreach (var texture in _renders.Values)
            {
                DestroyImmediate(texture);
            }

            _renders.Clear();
        }

        private void OnGUI()
        {
            _caption ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
            _problem ??= new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };

            Toolbar();
            foreach (var problem in _problems)
            {
                EditorGUILayout.HelpBox(problem, MessageType.Error);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string? section = null;
            foreach (var row in Rows(_looks))
            {
                if (row.Section != section)
                {
                    section = row.Section;
                    EditorGUILayout.Space(8f);
                    GUILayout.Label(section, EditorStyles.largeLabel);
                }

                Row(row);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Toolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int mood = EditorGUILayout.Popup(_mood, s_moods.Select(m => m.Name).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(70f));
                GUILayout.Space(8f);
                float size = GUILayout.HorizontalSlider(_cellSize, 90f, 300f, GUILayout.Width(120f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Export PNGs…", EditorStyles.toolbarButton))
                {
                    string folder = EditorUtility.SaveFolderPanel("Export look renders", Path.Combine("..", "artifacts"), "looks");
                    if (folder.Length > 0)
                    {
                        Export(folder);
                    }
                }

                if (mood != _mood || !Mathf.Approximately(size, _cellSize))
                {
                    _mood = mood;
                    _cellSize = Mathf.Round(size);
                    ClearRenders();
                }
            }
        }

        private void Row(GalleryRow row)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(row.Title, EditorStyles.boldLabel, GUILayout.Width(TitleWidth));
                if (row.Look == null)
                {
                    GUILayout.Label("<color=#E0457B>No look yet: the game draws a magenta box.</color>", _problem, GUILayout.ExpandWidth(false));
                    if (GUILayout.Button("Create", GUILayout.Width(70f)))
                    {
                        row.Create();
                    }

                    GUILayout.FlexibleSpace();
                    return;
                }

                int columns = Math.Max(row.Look.Stages.Count, row.Reachable);
                for (int stage = 0; stage < columns; stage++)
                {
                    Cell(row, row.Look, stage);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void Cell(GalleryRow row, StagedLook look, int stage)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(_cellSize)))
            {
                var rect = GUILayoutUtility.GetRect(_cellSize, _cellSize, GUILayout.Width(_cellSize), GUILayout.Height(_cellSize));
                bool defined = stage < look.Stages.Count;
                if (Event.current.type == EventType.Repaint)
                {
                    if (defined)
                    {
                        GUI.DrawTexture(rect, Render(row, look, stage, rect), ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        EditorGUI.DrawRect(rect, new Color(0.88f, 0.27f, 0.48f, 0.25f));
                    }
                }

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    Selection.activeObject = look;
                    EditorGUIUtility.PingObject(look);
                    Event.current.Use();
                }

                GUILayout.Label(Caption(row, look, stage, defined), _caption, GUILayout.Width(_cellSize));
            }
        }

        private Texture2D Render(GalleryRow row, StagedLook look, int stage, Rect rect)
        {
            var key = (look.GetInstanceID(), stage);
            if (!_renders.TryGetValue(key, out var texture) || texture == null)
            {
                _preview ??= new LookPreview();
                var (_, mood, azimuth) = s_moods[_mood];
                texture = _preview.Render(new Rect(0f, 0f, rect.width, rect.height), look, stage, row.Ground, row.Nature, row.Fullness, mood, azimuth);
                _renders[key] = texture;
            }

            return texture;
        }

        /// <summary>The stage's name, its triangle count against the budget, and anything a designer should know.</summary>
        private static string Caption(GalleryRow row, StagedLook look, int stage, bool defined)
        {
            string name = row.StageName(stage);
            if (!defined)
            {
                return $"{name}\n<color=#E0457B>Missing: shows the last stage.</color>";
            }

            var current = look.Stages[stage];
            if (current.name.Length > 0 && current.name != name)
            {
                name += $" · {current.name}";
            }

            var surfaces = new MeshBuilder();
            var glows = new MeshBuilder();
            look.Draw(surfaces, glows, stage, Vector3.zero, seed: 0);
            int triangles = surfaces.TriangleCount + glows.TriangleCount;
            string count = triangles > look.triangleBudget
                ? $"<color=#E0457B>{triangles} triangles, over the budget of {look.triangleBudget}</color>"
                : $"{triangles} / {look.triangleBudget} triangles";

            string note = "";
            if (stage >= row.Reachable)
            {
                note = "\n<color=#888888>Not reachable in the game.</color>";
            }
            else if (stage > 0 && current.extendsPrevious && current.parts.Count == 0 && current.removes.Count == 0)
            {
                note = $"\n<color=#C27A12>Same as {row.StageName(stage - 1)}: nothing added yet.</color>";
            }

            return $"{name}\n{count}{note}";
        }

        /// <summary>Two looks of the same kind: the game uses only one of them.</summary>
        private static List<string> Problems()
        {
            var problems = new List<string>();
            foreach (var group in Resources.LoadAll<BuildingLook>(Looks.Folder).GroupBy(l => l.kind).Where(g => g.Count() > 1))
            {
                problems.Add($"{group.Count()} looks for {group.Key} ({string.Join(", ", group.Select(l => l.name))}); the game uses only one.");
            }

            foreach (var group in Resources.LoadAll<NatureLook>(Looks.Folder).GroupBy(l => l.kind).Where(g => g.Count() > 1))
            {
                problems.Add($"{group.Count()} looks for {group.Key} ({string.Join(", ", group.Select(l => l.name))}); the game uses only one.");
            }

            return problems;
        }

        private static IEnumerable<GalleryRow> Rows(Looks looks)
        {
            foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
            {
                var spec = BuildingSpec.For(kind);
                var ground = IslandMap.TerrainOf(spec.Site);
                var nature = IslandView.NatureOf(ground) is NatureKind natureKind ? looks.For(natureKind) : null;
                yield return new GalleryRow(
                    "Buildings",
                    kind.ToString(),
                    looks.For(kind),
                    spec.MaxLevel,
                    stage => $"Level {stage + 1}",
                    ground,
                    nature,
                    IslandView.CrowdedForest,
                    () => Create<BuildingLook>("Buildings", kind.ToString(), look =>
                    {
                        look.kind = kind;
                        for (int level = 1; level <= spec.MaxLevel; level++)
                        {
                            look.levels.Add(new LookStage());
                        }
                    }));
            }

            foreach (NatureKind kind in Enum.GetValues(typeof(NatureKind)))
            {
                var look = looks.For(kind);
                yield return new GalleryRow(
                    "Nature",
                    kind.ToString(),
                    look,
                    look == null ? 1 : look.stages.Count,
                    stage => look != null && stage < look.stages.Count ? $"From {look.stages[stage].from:P0}" : $"Stage {stage + 1}",
                    GroundOf(kind),
                    null,
                    1f,
                    () => Create<NatureLook>("Nature", kind.ToString(), created =>
                    {
                        created.kind = kind;
                        created.stages.Add(new NatureStage());
                    }));
            }
        }

        private static Terrain GroundOf(NatureKind kind) => kind switch
        {
            NatureKind.Forest => Terrain.Forest,
            NatureKind.Farmland => Terrain.Farmland,
            NatureKind.Mountain => Terrain.Mountain,
            _ => Terrain.Grass,
        };

        private static void Create<T>(string folder, string name, Action<T> setUp)
            where T : StagedLook
        {
            var look = CreateInstance<T>();
            setUp(look);
            Directory.CreateDirectory($"{LooksFolder}/{folder}");
            AssetDatabase.CreateAsset(look, AssetDatabase.GenerateUniqueAssetPath($"{LooksFolder}/{folder}/{name}.asset"));
            AssetDatabase.SaveAssets();
            Selection.activeObject = look;
        }

        private static string? Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        /// <summary>One row of the gallery: a building or nature kind, its look if it has one, and the ground it stands on.</summary>
        private sealed class GalleryRow
        {
            public GalleryRow(string section, string title, StagedLook? look, int reachable, Func<int, string> stageName, Terrain ground, NatureLook? nature, float fullness, Action create)
            {
                Section = section;
                Title = title;
                Look = look;
                Reachable = reachable;
                StageName = stageName;
                Ground = ground;
                Nature = nature;
                Fullness = fullness;
                Create = create;
            }

            public string Section { get; }

            public string Title { get; }

            public StagedLook? Look { get; }

            /// <summary>How many stages the game can show: a building's highest level.</summary>
            public int Reachable { get; }

            public Func<int, string> StageName { get; }

            public Terrain Ground { get; }

            /// <summary>Nature on the same hex, as in the game: a woodcutter stands among trees.</summary>
            public NatureLook? Nature { get; }

            public float Fullness { get; }

            public Action Create { get; }
        }
    }
}
