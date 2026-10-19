using System;
using Eirora.Client.App;
using Eirora.Client.Look;
using Eirora.Simulation.Map;
using UnityEditor;
using UnityEngine;
using Terrain = Eirora.Client.Look.Terrain;

namespace Eirora.Editor
{
    /// <summary>
    /// Renders one stage of a look on its own hex, from the game camera's angle and in the game's light, so the
    /// gallery shows what the player will see.
    /// </summary>
    internal sealed class LookPreview : IDisposable
    {
        private const float Distance = 5f;

        private readonly PreviewRenderUtility _utility = new();
        private readonly Material _flat = new(Shader.Find("Eirora/Flat"));
        private readonly Material _glow = new(Shader.Find("Eirora/Glow"));

        public LookPreview()
        {
            var camera = _utility.camera;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            var rotation = Quaternion.Euler(CameraRig.Pitch, 0f, 0f);
            var target = new Vector3(0f, 0.4f, 0f);
            camera.transform.SetPositionAndRotation(target - (rotation * Vector3.forward * Distance), rotation);
            _utility.lights[1].intensity = 0f; // The game has one light; the Flat shader fills the shadows from the sky.
        }

        /// <summary>A stage of a look standing on <paramref name="ground"/>, with <paramref name="nature"/> drawn on the hex as in the game.</summary>
        public Texture2D Render(Rect rect, StagedLook look, int stage, Terrain ground, NatureLook? nature, float fullness, LightMood mood, float azimuth)
        {
            var surfaces = new MeshBuilder();
            var glows = new MeshBuilder();
            var hex = new Hex(0, 0);
            var at = hex.Center() + (Vector3.up * Models.Height(ground));
            Models.Tile(surfaces, hex, ground);
            if (nature != null && nature != look)
            {
                nature.Draw(surfaces, glows, nature.StageOf(fullness), at, seed: 0);
            }

            look.Draw(surfaces, glows, stage, at, seed: 0);
            var surfaceMesh = surfaces.ToMesh("Preview");
            var glowMesh = glows.ToMesh("Preview Lights");

            Atmosphere.Light(_utility.lights[0], mood, azimuth);
            _utility.camera.backgroundColor = mood.Horizon;
            _utility.BeginStaticPreview(rect);
            _utility.DrawMesh(surfaceMesh, Matrix4x4.identity, _flat, 0);
            _utility.DrawMesh(glowMesh, Matrix4x4.identity, _glow, 0);
            _utility.Render(allowScriptableRenderPipeline: true);
            var texture = _utility.EndStaticPreview();

            UnityEngine.Object.DestroyImmediate(surfaceMesh);
            UnityEngine.Object.DestroyImmediate(glowMesh);
            return texture;
        }

        public void Dispose()
        {
            _utility.Cleanup();
            UnityEngine.Object.DestroyImmediate(_flat);
            UnityEngine.Object.DestroyImmediate(_glow);
        }
    }
}
