using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Eirora.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// Guards the project setup that every build depends on (TASK-010).
    /// </summary>
    public sealed class ClientProjectTests
    {
        private const string BootstrapScene = "Assets/Eirora/Scenes/Bootstrap.unity";

        [Test]
        public void SharedSimulationCompilesInsideUnity()
        {
            Assert.That(RulesVersion.Current, Is.GreaterThan(0));
        }

        [Test]
        public void BootstrapSceneIsTheOnlySceneInTheBuild()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path);

            Assert.That(scenes, Is.EqualTo(new[] { BootstrapScene }));
        }

        [Test]
        public void BootstrapSceneStartsTheClient()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BootstrapScene, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var bootstraps = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ClientBootstrap>());

                Assert.That(bootstraps.Count(), Is.EqualTo(1));
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void MobileBuildsUseIl2CppOnArm64()
        {
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android), Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64));
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS), Is.EqualTo(ScriptingImplementation.IL2CPP));
        }

        [Test]
        public void BuildsSendNoHardwareStatisticsToUnity()
        {
            // Not exposed in the PlayerSettings API, so read the serialised setting.
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").Single());

            Assert.That(playerSettings.FindProperty("submitAnalytics").boolValue, Is.False);
        }

        // Unity's splash screen held the first frame of the world back by about two seconds, more than the reference
        // tier's whole budget for it (#227).
        [Test]
        public void TheWorldIsNotHeldBackByUnitysSplashScreen()
        {
            Assert.That(PlayerSettings.SplashScreen.show, Is.False);
        }

        // iOS shows this text when the old phone first opens the camera to scan the new phone's QR code (TASK-023b).
        [Test]
        public void IosIsToldWhyTheCameraIsWanted()
        {
            Assert.That(PlayerSettings.iOS.cameraUsageDescription, Does.Contain("QR-koden").And.Contain("stannar i telefonen"));
        }

        [Test]
        public void BothPlatformsShareOneApplicationIdentifier()
        {
            var android = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            var ios = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

            Assert.That(android, Is.EqualTo("se.eirora.app"));
            Assert.That(ios, Is.EqualTo(android));
        }

        [Test]
        public void CommittedRenderSettingsAreTheOnesUrpWritesWhenItRenders()
        {
            // URP sets the active quality level's MSAA to the pipeline's when it starts, and both light flags every
            // frame, so any other committed value is rewritten whenever the editor runs with graphics. Read the files,
            // not the settings in memory, which URP may already have changed.
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
            var graphics = File.ReadAllText("ProjectSettings/GraphicsSettings.asset");
            var quality = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            var antiAliasing = Regex.Matches(quality, @"^\s+antiAliasing: (\d+)\s*$", RegexOptions.Multiline)
                .Select(match => int.Parse(match.Groups[1].Value))
                .ToList();

            Assert.That(graphics, Does.Match(@"m_LightsUseLinearIntensity: 1\s"));
            Assert.That(graphics, Does.Match(@"m_LightsUseColorTemperature: 1\s"));
            Assert.That(antiAliasing, Has.Count.EqualTo(QualitySettings.names.Length));
            Assert.That(antiAliasing, Is.All.EqualTo(pipeline.msaaSampleCount));
        }
    }
}
