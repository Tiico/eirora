using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Eirora.Spike.HealthConnect.Editor
{
    /// <summary>
    /// Configures and builds the spike APK from code, so that nothing depends on hand-edited settings (NFR-14).
    /// Run from the repository root:
    /// Unity.exe -batchmode -quit -projectPath spikes/health-connect -executeMethod Eirora.Spike.HealthConnect.Editor.SpikeBuild.BuildApk
    /// </summary>
    public static class SpikeBuild
    {
        public const string ApplicationId = "com.eirora.spike.healthconnect";
        public const string ScenePath = "Assets/Spike/Scenes/Spike.unity";
        public const string ManifestPath = "Assets/Spike/Plugins/Android/HealthConnectBridge.androidlib/AndroidManifest.xml";
        public const int TargetSdk = 36;
        public const string BridgeClass = "com.eirora.spike.healthconnect.bridge.HealthConnectBridge";

        /// <summary>The only health permissions the spike may hold: read-only, one per habit plus background and history.</summary>
        public static readonly string[] HealthPermissions =
        {
            "android.permission.health.READ_STEPS",
            "android.permission.health.READ_EXERCISE",
            "android.permission.health.READ_SLEEP",
            "android.permission.health.READ_HEALTH_DATA_IN_BACKGROUND",
            "android.permission.health.READ_HEALTH_DATA_HISTORY",
        };

        private static string OutputDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/spike-health-connect"));

        public static string ApkPath => Path.Combine(OutputDirectory, "eirora-hc-spike.apk");

        [MenuItem("Eirora/Spike/Configure project")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Eirora";
            PlayerSettings.productName = "Eirora HC-spike";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;

            // ARM64 for phones, x86_64 so the same APK runs natively on an emulator.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // A tool, not a game: keep the status bar so the clock and notifications stay visible.
            PlayerSettings.Android.startInFullscreen = false;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;

            EnsureScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Eirora/Spike/Build APK")]
        public static void BuildApk()
        {
            try
            {
                Configure();
                Directory.CreateDirectory(OutputDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    locationPathName = ApkPath,
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException($"Build {report.summary.result} with {report.summary.totalErrors} errors");
                }

                if (!ApkContainsClass(ApkPath, BridgeClass))
                {
                    throw new BuildFailedException($"{BridgeClass} is not in the APK: the Kotlin sources in the .androidlib were not compiled");
                }

                var permissions = ApkPermissions(ApkPath);
                File.WriteAllLines(Path.Combine(OutputDirectory, "permissions.txt"), permissions);
                var problems = PermissionProblems(permissions).ToList();
                if (problems.Count > 0)
                {
                    throw new BuildFailedException("The APK breaks the spike's privacy guarantees:\n" + string.Join("\n", problems));
                }

                Debug.Log($"Built {ApkPath} ({new FileInfo(ApkPath).Length / (1024 * 1024)} MB) with permissions:\n{string.Join("\n", permissions)}");
                ExitIfBatch(0);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                ExitIfBatch(1);
            }
        }

        /// <summary>Why a set of APK permissions is unacceptable: network access, writing health data or a missing read.</summary>
        public static IEnumerable<string> PermissionProblems(IReadOnlyCollection<string> permissions)
        {
            if (permissions.Contains("android.permission.INTERNET"))
            {
                yield return "android.permission.INTERNET: the spike must not be able to send health data anywhere (HLT-10)";
            }

            foreach (var permission in permissions.Where(p => p.StartsWith("android.permission.health.", StringComparison.Ordinal) && !HealthPermissions.Contains(p)))
            {
                yield return $"{permission}: the spike only reads steps, exercise and sleep";
            }

            foreach (var missing in HealthPermissions.Where(p => !permissions.Contains(p)))
            {
                yield return $"{missing} is missing";
            }
        }

        /// <summary>Whether any classes*.dex in the APK defines <paramref name="className"/>, by its type descriptor.</summary>
        public static bool ApkContainsClass(string apk, string className)
        {
            var descriptor = System.Text.Encoding.ASCII.GetBytes("L" + className.Replace('.', '/') + ";");
            using var archive = ZipFile.OpenRead(apk);
            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("classes", StringComparison.Ordinal) && e.FullName.EndsWith(".dex", StringComparison.Ordinal)))
            {
                using var stream = entry.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                if (IndexOf(memory.ToArray(), descriptor) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The uses-permission names in an APK, read with aapt2 from Unity's Android SDK.</summary>
        public static IReadOnlyList<string> ApkPermissions(string apk)
        {
            var buildTools = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "build-tools");
            var aapt2 = Directory.GetDirectories(buildTools).OrderByDescending(d => d, StringComparer.Ordinal)
                .Select(d => Path.Combine(d, Application.platform == RuntimePlatform.WindowsEditor ? "aapt2.exe" : "aapt2"))
                .First(File.Exists);
            var start = new ProcessStartInfo(aapt2, $"dump permissions \"{apk}\"") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start aapt2");
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"aapt2 exited with {process.ExitCode}");
            }

            // Lines look like: uses-permission: name='android.permission.health.READ_STEPS'
            return output.Split('\n')
                .Where(line => line.StartsWith("uses-permission", StringComparison.Ordinal))
                .Select(line => line.Substring(line.IndexOf('\'') + 1).TrimEnd('\r', '\'', ' '))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                var j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j])
                {
                    j++;
                }

                if (j == needle.Length)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SpikeStyles.Background;
            camera.cullingMask = 0;
            new GameObject(nameof(SpikeScreen)).AddComponent<SpikeScreen>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void ExitIfBatch(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
