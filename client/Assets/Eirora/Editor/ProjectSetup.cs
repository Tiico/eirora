using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Eirora.Editor
{
    /// <summary>
    /// Project settings the client depends on, applied from code so they can be reviewed in a diff and re-applied
    /// (NFR-14). Idempotent.
    /// </summary>
    /// <remarks>
    /// Run: Unity -batchmode -quit -projectPath client -executeMethod Eirora.Editor.ProjectSetup.Apply
    /// </remarks>
    public static class ProjectSetup
    {
        public const string PipelinePath = "Assets/Eirora/Settings/EiroraPipeline.asset";
        public const string RendererPath = "Assets/Eirora/Settings/EiroraRenderer.asset";

        [MenuItem("Eirora/Apply Project Setup")]
        public static void Apply()
        {
            ApplyPlayerSettings();
            ApplyRenderPipeline();
            AssetDatabase.SaveAssets();
            Debug.Log("Eirora project setup applied.");
        }

        /// <summary>Why Eirora wants the camera: only to read the new phone's QR code when a key is sent (TASK-023b).</summary>
        internal const string CameraUsageDescription = "Eirora läser QR-koden på din nya telefon för att skicka nyckeln till din egenvård dit. Bilden stannar i telefonen.";

        private static void ApplyPlayerSettings()
        {
            // Lighting is authored for linear colour (art-direction.md); the game is held in one hand (UX-01).
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Health Connect's client library needs Android 8 (API 26) or later (ADR-0007). Unity 6.3 picks up
            // Assets/Plugins/Android/gradleTemplate.properties, which turns Kotlin on, by its presence alone;
            // AndroidHealthCheck fails any build where the Kotlin adapter is missing from the APK.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            // iOS asks for the camera with this text the first time the old phone scans the new phone's QR code (TASK-023b).
            PlayerSettings.iOS.cameraUsageDescription = CameraUsageDescription;

            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").Single());
            settings.FindProperty("activeInputHandler").intValue = 1; // Input System only.
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyRenderPipeline()
        {
            EnsureFolder("Assets/Eirora/Settings");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            // Budget from art-direction.md, section 7: one light, one short shadow cascade, no HDR effects.
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1f;
            pipeline.shadowDistance = 40f;
            pipeline.shadowCascadeCount = 1;
            pipeline.shadowDepthBias = 2f; // Low dawn and dusk sun: avoid shadow acne on the flat hexes.
            pipeline.shadowNormalBias = 1.5f;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(pipeline);

            // URP writes the light settings every frame and the active quality level's MSAA when it starts, so an editor
            // with graphics would rewrite any other committed value. Linear intensity follows the linear colour space.
            GraphicsSettings.defaultRenderPipeline = pipeline;
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.antiAliasing = pipeline.msaaSampleCount;
            }

            QualitySettings.SetQualityLevel(current, applyExpensiveChanges: false);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                int slash = path.LastIndexOf('/');
                AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
            }
        }
    }
}
