using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Eirora.Client.App;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Eirora.Editor
{
    /// <summary>
    /// Local player builds: an Android APK for the product owner's phone, a Windows player for the screenshot tour and the
    /// determinism build. CI builds the game with GameCI's default build, but with the same version rule and signing key,
    /// so a local APK and a CI APK can update each other without losing the world (TASK-014).
    /// </summary>
    /// <remarks>
    /// Run: Unity -batchmode -quit -projectPath client -buildTarget Android -executeMethod Eirora.Editor.Builds.Android -buildOutput Builds/Eirora.apk
    /// </remarks>
    public static class Builds
    {
        private const string DeterminismFolder = "Assets/Eirora/Determinism";

        public static void Android() => ExitOnFailure(Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/Eirora.apk"));

        public static void Windows() => ExitOnFailure(Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Windows/Eirora.exe"));

        /// <summary>
        /// The determinism build (TASK-021): IL2CPP for ARM64 that only plays the replay corpus in tests/Replays and logs
        /// each state hash (DeterminismRun). It has its own package name, so it never replaces the game on a phone.
        /// </summary>
        public static void AndroidDeterminism()
        {
            var android = NamedBuildTarget.Android;
            string identifier = PlayerSettings.GetApplicationIdentifier(android);
            string productName = PlayerSettings.productName;
            bool succeeded;
            try
            {
                CopyCorpus(Path.Combine(DeterminismFolder, "Resources", "Replays"));
                PlayerSettings.SetApplicationIdentifier(android, identifier + ".determinism");
                PlayerSettings.productName = productName + " determinism";
                succeeded = Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/EiroraDeterminism.apk", "EIRORA_DETERMINISM");
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(android, identifier);
                PlayerSettings.productName = productName;
                AssetDatabase.DeleteAsset(DeterminismFolder);
                AssetDatabase.SaveAssets();
            }

            ExitOnFailure(succeeded);
        }

        /// <summary>Copies the replays into the project as text assets, only for the determinism build.</summary>
        private static void CopyCorpus(string folder)
        {
            string corpus = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tests", "Replays"));
            var replays = Directory.GetFiles(corpus, "*.replay");
            if (replays.Length == 0)
            {
                throw new BuildFailedException($"There are no replays in {corpus}.");
            }

            Directory.CreateDirectory(folder);
            foreach (string replay in replays)
            {
                File.Copy(replay, Path.Combine(folder, Path.GetFileNameWithoutExtension(replay) + ".txt"));
            }

            AssetDatabase.Refresh();
        }

        private static void ExitOnFailure(bool succeeded)
        {
            if (!succeeded && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }

        private static bool Build(BuildTarget target, BuildTargetGroup group, string defaultOutput, params string[] defines)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Argument("-buildOutput") ?? defaultOutput,
                target = target,
                targetGroup = group,
                options = BuildOptions.StrictMode,

                // For this build only; the project's own defines stay as they are.
                extraScriptingDefines = defines,
            };

            // The settings are set for this build only, so the project files stay as they are in git.
            string bundleVersion = PlayerSettings.bundleVersion;
            int versionCode = PlayerSettings.Android.bundleVersionCode;
            string keystoreName = SerializedKeystoreName().stringValue;
            bool signed = false;
            BuildSummary summary;
            try
            {
                var version = BuildVersion.FromCommitCount(CommitCount());
                PlayerSettings.bundleVersion = version.Name;
                PlayerSettings.Android.bundleVersionCode = version.Code;
                signed = target == BuildTarget.Android && UseReleaseKeystore();
                summary = BuildPipeline.BuildPlayer(options).summary;
            }
            finally
            {
                PlayerSettings.bundleVersion = bundleVersion;
                PlayerSettings.Android.bundleVersionCode = versionCode;

                if (signed)
                {
                    PlayerSettings.Android.useCustomKeystore = false;
                    PlayerSettings.Android.keyaliasName = string.Empty;
                    PlayerSettings.Android.keystorePass = string.Empty;
                    PlayerSettings.Android.keyaliasPass = string.Empty;

                    // PlayerSettings would write an empty name back as '{inproject}: ', so the stored text is restored.
                    var name = SerializedKeystoreName();
                    name.stringValue = keystoreName;
                    name.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Debug.Log($"Build {summary.result}: {summary.outputPath} ({summary.totalSize} bytes, {summary.totalErrors} errors).");
            return summary.result == BuildResult.Succeeded;
        }

        /// <summary>
        /// Signs with the prototype's release key from the environment (docs/development.md#signering). Without it the
        /// APK gets this computer's debug key and cannot update a build from CI, nor CI's builds it.
        /// </summary>
        private static bool UseReleaseKeystore()
        {
            string? keystore = Environment.GetEnvironmentVariable("EIRORA_ANDROID_KEYSTORE");
            if (string.IsNullOrEmpty(keystore))
            {
                Debug.LogWarning("EIRORA_ANDROID_KEYSTORE is not set: the APK is signed with the debug key and cannot update a CI build.");
                return false;
            }

            if (!File.Exists(keystore))
            {
                throw new BuildFailedException($"The keystore {keystore} does not exist.");
            }

            // Everything is read before any setting changes, so a missing variable leaves the project as it was.
            string keystorePass = Required("EIRORA_ANDROID_KEYSTORE_PASS");
            string keyaliasName = Required("EIRORA_ANDROID_KEYALIAS_NAME");
            string keyaliasPass = Required("EIRORA_ANDROID_KEYALIAS_PASS");

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = keyaliasName;
            PlayerSettings.Android.keyaliasPass = keyaliasPass;
            return true;
        }

        private static SerializedProperty SerializedKeystoreName()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            return settings.FindProperty("AndroidKeystoreName");
        }

        private static string Required(string name) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
                ? value
                : throw new BuildFailedException($"{name} must be set together with EIRORA_ANDROID_KEYSTORE.");

        /// <summary>The number of commits in the checked-out history, which CI also derives the version from.</summary>
        private static int CommitCount()
        {
            var git = new ProcessStartInfo("git", "rev-list --count HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                using var process = Process.Start(git)!;
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode == 0 && int.TryParse(output, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
                {
                    return count;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Git is not installed; fall through.
            }

            throw new BuildFailedException("The version comes from 'git rev-list --count HEAD', which failed. Build from a git checkout.");
        }

        private static string? Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
