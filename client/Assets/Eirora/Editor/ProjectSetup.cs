using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Eirora.Editor
{
    /// <summary>
    /// Project settings as code (NFR-14): every setting the client depends on is applied here, so it can be
    /// reviewed in a diff and re-applied in batch mode. Idempotent.
    /// </summary>
    /// <remarks>
    /// Run: Unity -batchmode -quit -projectPath client -executeMethod Eirora.Editor.ProjectSetup.Apply
    /// </remarks>
    public static class ProjectSetup
    {
        public const string ApplicationIdentifier = "com.eirora.app";
        public const string MainScenePath = "Assets/Eirora/Scenes/Main.unity";
        public const string IslandMaterialPath = "Assets/Eirora/Resources/Island.mat";

        [MenuItem("Eirora/Apply Project Setup")]
        public static void Apply()
        {
            ApplyPlayerSettings();
            EnsureMainScene();
            EnsureIslandMaterial();
            AssetDatabase.SaveAssets();
            Debug.Log("Eirora project setup applied.");
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Eirora";
            PlayerSettings.productName = "Eirora";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, ApplicationIdentifier);
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetApplicationIdentifier(target, ApplicationIdentifier);
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetApiCompatibilityLevel(target, ApiCompatibilityLevel.NET_Standard);
            }

            // Health Connect is available from Android 9 (API 28); Google Play requires 64-bit.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
        }

        private static void EnsureMainScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
            {
                // The scene stays empty: the world is built in code by Eirora.Client.Bootstrap.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureFolder("Assets/Eirora/Scenes");
                EditorSceneManager.SaveScene(scene, MainScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
        }

        private static void EnsureIslandMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(IslandMaterialPath) != null)
            {
                return;
            }

            var shader = GraphicsSettings.currentRenderPipeline.defaultMaterial.shader;
            var material = new Material(shader) { name = "Island" };
            AssetDatabase.CreateAsset(material, IslandMaterialPath);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
            }
        }
    }
}
