using Eirora.Simulation.Forces;
using UnityEngine;

namespace Eirora.Client
{
    /// <summary>
    /// Builds the world in code when the game starts (NFR-14); the scene file itself stays empty.
    /// For now: a tilted camera, dawn light and a single hex tile, enough to verify the build chain on a device.
    /// </summary>
    internal static class Bootstrap
    {
        private static readonly Color DawnSky = new Color(0.96f, 0.73f, 0.61f);
        private static readonly Color DawnSun = new Color(1f, 0.85f, 0.68f);
        private static readonly Color Meadow = new Color(0.62f, 0.71f, 0.54f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 6f, -5f), Quaternion.Euler(50f, 0f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DawnSky;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = DawnSun;
            sun.transform.rotation = Quaternion.Euler(20f, -60f, 0f);

            var tile = new GameObject("Hex", typeof(MeshFilter), typeof(MeshRenderer));
            tile.GetComponent<MeshFilter>().sharedMesh = HexMesh.Prism(radius: 2f, height: 0.4f);
            var material = new Material(Resources.Load<Material>("Island")) { color = Meadow };
            tile.GetComponent<MeshRenderer>().sharedMaterial = material;

            Debug.Log($"Eirora started. 7 500 steps give {ForceRules.Exploration(7_500)} Exploration.");
        }
    }
}
