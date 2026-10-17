using System.Linq;
using Eirora.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

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

        [Test]
        public void BothPlatformsShareOneApplicationIdentifier()
        {
            var android = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            var ios = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

            Assert.That(android, Is.EqualTo("com.eirora.game"));
            Assert.That(ios, Is.EqualTo(android));
        }
    }
}
