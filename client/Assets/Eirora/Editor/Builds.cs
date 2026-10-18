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
    /// Local player builds: an Android APK for the product owner's phone and a Windows player for the screenshot tour.
    /// CI builds with GameCI's default build, but with the same version rule and signing key, so a local APK and a CI
    /// APK can update each other without losing the world (TASK-014).
    /// </summary>
    /// <remarks>
    /// Run: Unity -batchmode -quit -projectPath client -buildTarget Android -executeMethod Eirora.Editor.Builds.Android -buildOutput Builds/Eirora.apk
    /// </remarks>
    public static class Builds
    {
        public static void Android() => Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/Eirora.apk");

        public static void Windows() => Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Windows/Eirora.exe");

        private static void Build(BuildTarget target, BuildTargetGroup group, string defaultOutput)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Argument("-buildOutput") ?? defaultOutput,
                target = target,
                targetGroup = group,
                options = BuildOptions.StrictMode,
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
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
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
