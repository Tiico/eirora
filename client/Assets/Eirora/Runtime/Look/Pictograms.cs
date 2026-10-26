using System;
using System.Collections.Generic;
using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace Eirora.Client.Look
{
    /// <summary>
    /// A building's picture for the interface: its own look, drawn on the ground it needs, from the game camera's angle and
    /// in the game's light, rendered once into a texture when first asked for (ui-prototypes.html, direction C). The
    /// pictures follow the looks, so a new or changed building needs no drawing of its own.
    /// </summary>
    public sealed class Pictograms : IDisposable
    {
        private const int Size = 256;
        private const float Distance = 3.6f;
        // Far below the island, where neither camera sees the other's scene.
        private static readonly Vector3 s_offset = new(0f, -500f, 0f);

        private readonly Looks _looks;
        private readonly Material _flat;
        private readonly Material _glow;
        private readonly Dictionary<BuildingKind, RenderTexture?> _pictures = new();
        private readonly List<UnityEngine.Object> _owned = new();
        private Camera? _camera;
        private MeshFilter? _surfaces;
        private MeshFilter? _glows;

        public Pictograms(Looks looks, Material flat, Material glow)
        {
            _looks = looks;
            _flat = flat;
            _glow = glow;
        }

        /// <summary>The building's picture, or null where nothing can be rendered, as in a test without graphics.</summary>
        public Texture? Of(BuildingKind kind)
        {
            if (!_pictures.TryGetValue(kind, out var picture))
            {
                picture = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ? null : Render(kind);
                _pictures[kind] = picture;
            }

            return picture;
        }

        /// <summary>Forgets every picture, as when a look was edited while playing in the editor.</summary>
        public void Clear()
        {
            foreach (var picture in _pictures.Values)
            {
                if (picture != null)
                {
                    picture.Release();
                    Destroy(picture);
                }
            }

            _pictures.Clear();
        }

        public void Dispose()
        {
            Clear();
            foreach (var thing in _owned)
            {
                Destroy(thing);
            }

            _owned.Clear();
        }

        private RenderTexture Render(BuildingKind kind)
        {
            var camera = PictureCamera();
            var spec = ContentCatalog.Current.For(kind);
            var ground = IslandMap.GroundFor(spec.Requirement);
            var surfaces = new MeshBuilder();
            var glows = new MeshBuilder();
            var hex = new Hex(0, 0);
            var at = hex.Center() + (Vector3.up * Models.Height(ground));
            Models.Tile(surfaces, hex, ground);
            if (ground == Terrain.Forest)
            {
                _looks.Nature(surfaces, glows, NatureKind.Forest, IslandView.CrowdedForest, false, at, seed: 0);
            }

            _looks.Building(surfaces, glows, kind, level: 1, at, seed: 0);
            surfaces.WriteTo(_surfaces!.sharedMesh);
            glows.WriteTo(_glows!.sharedMesh);

            var picture = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { name = $"Pictogram {kind}" };
            picture.Create();
            camera.targetTexture = picture;
            var request = new RenderPipeline.StandardRequest { destination = picture };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.Render();
            }

            camera.targetTexture = null;
            return picture;
        }

        private Camera PictureCamera()
        {
            if (_camera != null)
            {
                return _camera;
            }

            var rig = new GameObject("Pictograms") { hideFlags = HideFlags.HideAndDontSave };
            rig.transform.position = s_offset;
            _owned.Add(rig);
            _surfaces = Mesh("Surfaces", rig.transform, _flat);
            _glows = Mesh("Lights", rig.transform, _glow);

            // As the look gallery frames a look: the game camera's pitch, close enough for the building to fill the picture.
            var camera = new GameObject("Pictogram Camera").AddComponent<UnityEngine.Camera>();
            camera.transform.SetParent(rig.transform, false);
            camera.enabled = false;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            var rotation = Quaternion.Euler(App.CameraRig.Pitch, 0f, 0f);
            var target = new Vector3(0f, 0.55f, 0f);
            camera.transform.SetLocalPositionAndRotation(target - (rotation * Vector3.forward * Distance), rotation);
            _camera = camera;
            return camera;
        }

        private MeshFilter Mesh(string name, Transform parent, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var filter = go.GetComponent<MeshFilter>();
            filter.sharedMesh = new Mesh { name = name };
            _owned.Add(filter.sharedMesh);
            return filter;
        }

        private static void Destroy(UnityEngine.Object thing)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(thing);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(thing);
            }
        }
    }
}
