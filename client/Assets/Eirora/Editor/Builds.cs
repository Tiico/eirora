using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Eirora.Client.App;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Eirora.Editor
{
    /// <summary>
    /// Local player builds: an Android APK for the product owner's phone, the app bundle that Google Play takes, a
    /// Windows player for the screenshot tour and the determinism build. CI builds the game with GameCI's default build,
    /// but with the same version rule and signing key, so a local APK and a CI APK can update each other without losing
    /// the world (TASK-014).
    /// </summary>
    /// <remarks>
    /// Run: Unity -batchmode -quit -projectPath client -buildTarget Android -executeMethod Eirora.Editor.Builds.Android -buildOutput Builds/Eirora.apk
    /// </remarks>
    public static class Builds
    {
        // Made for the determinism build and deleted after it, so its corpus never reaches the game (git ignores it).
        private const string DeterminismFolder = "Assets/Eirora/Determinism";

        public static void Android()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            ExitOnFailure(Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/Eirora.apk"));
        }

        /// <summary>
        /// The app bundle that CI uploads to Google Play from main (D-75), to check locally what Google Play gets.
        /// </summary>
        public static void AndroidBundle()
        {
            EditorUserBuildSettings.buildAppBundle = true;
            bool succeeded;
            try
            {
                succeeded = Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/Eirora.aab");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = false;
            }

            ExitOnFailure(succeeded);
        }

        public static void Windows() => ExitOnFailure(Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Windows/Eirora.exe"));

        /// <summary>
        /// The determinism build (TASK-021): IL2CPP for ARM64 and x86_64 that only plays the determinism corpus (the replays in
        /// tests/Replays, the golden saves and the golden maps) and logs each state hash (DeterminismRun). Its only scene holds DeterminismBootstrap instead of the game, and it has its
        /// own package name, so it never replaces the game on a phone. A phone runs its ARM64 code; an x86_64 emulator
        /// runs its x86_64 code, because Android's ARM translation lacks an instruction (SEVL) that Unity's own ARM64
        /// engine uses and kills it with SIGILL. It renders with OpenGL ES 3, which the emulator's software renderer
        /// provides. It has no version, so it needs no git history, which CI's container may refuse to read ("dubious
        /// ownership").
        /// </summary>
        public static void AndroidDeterminism()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            var android = NamedBuildTarget.Android;
            string identifier = PlayerSettings.GetApplicationIdentifier(android);
            string productName = PlayerSettings.productName;
            bool defaultGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            var graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            bool storedGraphics = AndroidGraphicsEntry() >= 0;
            var architectures = PlayerSettings.Android.targetArchitectures;
            var openScenes = EditorSceneManager.GetSceneManagerSetup();
            bool succeeded;
            try
            {
                CopyCorpus(Path.Combine(DeterminismFolder, "Resources", DeterminismRun.ResourceFolder));
                string scene = DeterminismScene();
                PlayerSettings.SetApplicationIdentifier(android, identifier + ".determinism");
                PlayerSettings.productName = productName + " determinism";
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;
                succeeded = Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/EiroraDeterminism.apk", scene, versioned: false);
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(android, identifier);
                PlayerSettings.productName = productName;
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, graphics);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, defaultGraphics);
                RemoveAndroidGraphicsEntryUnless(storedGraphics);
                PlayerSettings.Android.targetArchitectures = architectures;
                // An untitled scene cannot be reopened, so an empty one takes its place.
                if (openScenes.Length > 0 && openScenes.All(s => s.path.Length > 0))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(openScenes);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }

                RemoveDeterminismFolder();
                AssetDatabase.SaveAssets();
            }

            ExitOnFailure(succeeded);
        }

        /// <summary>Copies the determinism corpus into the project as text assets, only for the determinism build.</summary>
        private static void CopyCorpus(string folder)
        {
            var replays = Directory.GetFiles(DeterminismRun.ReplayFolder, "*.replay");
            var saves = Directory.GetFiles(DeterminismRun.GoldenSaveFolder, "*.save");
            if (replays.Length == 0 || saves.Length == 0 || !File.Exists(DeterminismRun.GoldenMapList))
            {
                throw new BuildFailedException($"The corpus is incomplete: replays in {DeterminismRun.ReplayFolder}, golden saves in {DeterminismRun.GoldenSaveFolder} and {DeterminismRun.GoldenMapList}.");
            }

            // A build that was killed leaves the folder behind, perhaps with files that are no longer in the corpus.
            RemoveDeterminismFolder();
            Copy(replays, Path.Combine(folder, "Replays"), ".txt");
            Copy(saves, Path.Combine(folder, "Saves"), ".bytes");
            File.Copy(DeterminismRun.GoldenMapList, Path.Combine(folder, "maps.txt"));
            AssetDatabase.Refresh();
        }

        /// <summary>Copies files as TextAssets with the extension that keeps them as they are: .txt for text, .bytes for bytes.</summary>
        private static void Copy(string[] files, string folder, string extension)
        {
            Directory.CreateDirectory(folder);
            foreach (string file in files)
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileNameWithoutExtension(file) + extension));
            }
        }

        /// <summary>Saves a scene with nothing but DeterminismBootstrap in it.</summary>
        private static string DeterminismScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new BuildFailedException("The open scenes have unsaved changes.");
            }

            string path = DeterminismFolder + "/Determinism.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.MoveGameObjectToScene(new GameObject(nameof(DeterminismBootstrap), typeof(DeterminismBootstrap)), scene);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new BuildFailedException($"Could not save {path}.");
            }

            return path;
        }

        private static void RemoveDeterminismFolder()
        {
            if (AssetDatabase.IsValidFolder(DeterminismFolder) && !AssetDatabase.DeleteAsset(DeterminismFolder))
            {
                throw new BuildFailedException($"Could not delete {DeterminismFolder}.");
            }
        }

        private static void ExitOnFailure(bool succeeded)
        {
            if (!succeeded && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Builds the game's scenes, or only the given scene, versioned by the number of commits unless told not to.</summary>
        private static bool Build(BuildTarget target, BuildTargetGroup group, string defaultOutput, string? scene = null, bool versioned = true)
        {
            if (scene is null)
            {
                // The game never carries the replays that a killed determinism build left behind.
                RemoveDeterminismFolder();
            }

            var options = new BuildPlayerOptions
            {
                scenes = scene is null ? EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray() : new[] { scene },
                // GameCI names its output with -customBuildPath and fails when nothing is there.
                locationPathName = Argument("-buildOutput") ?? Argument("-customBuildPath") ?? defaultOutput,
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
                if (versioned)
                {
                    var version = BuildVersion.FromCommitCount(CommitCount());
                    PlayerSettings.bundleVersion = version.Name;
                    PlayerSettings.Android.bundleVersionCode = version.Code;
                }

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
            ReportBuildResults(summary);
            return summary.result == BuildResult.Succeeded;
        }

        /// <summary>
        /// Writes the summary GameCI reads to tell whether a build succeeded (UnityBuildValidation in game-ci/cli). GameCI's
        /// own build method writes it; without it GameCI fails a custom method's build even when Unity succeeded, which
        /// is what kept the determinism build red (TASK-021).
        /// </summary>
        private static void ReportBuildResults(BuildSummary summary)
        {
            string errors = summary.result == BuildResult.Succeeded
                ? summary.totalErrors.ToString(CultureInfo.InvariantCulture)
                : Math.Max(summary.totalErrors, 1).ToString(CultureInfo.InvariantCulture); // A failed build is never reported as clean.
            Console.WriteLine(string.Join(
                Environment.NewLine,
                "###########################",
                "#      Build results      #",
                "###########################",
                string.Empty,
                $"Duration: {summary.totalTime}",
                $"Warnings: {summary.totalWarnings.ToString(CultureInfo.InvariantCulture)}",
                $"Errors: {errors}",
                $"Size: {summary.totalSize.ToString(CultureInfo.InvariantCulture)} bytes",
                "###########################"));
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

        private static SerializedProperty SerializedKeystoreName() => SerializedPlayerSetting("AndroidKeystoreName");

        private static SerializedProperty SerializedPlayerSetting(string name)
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            return settings.FindProperty(name);
        }

        /// <summary>The index of Android's entry among the stored graphics APIs, or -1 when it has none.</summary>
        private static int AndroidGraphicsEntry()
        {
            var entries = SerializedPlayerSetting("m_BuildTargetGraphicsAPIs");
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("m_BuildTarget").stringValue == "AndroidPlayer")
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// PlayerSettings stores an entry for Android even when given back the default APIs, so the project file would
        /// change; the entry is removed again when the project had none.
        /// </summary>
        private static void RemoveAndroidGraphicsEntryUnless(bool stored)
        {
            int index = AndroidGraphicsEntry();
            if (stored || index < 0)
            {
                return;
            }

            var entries = SerializedPlayerSetting("m_BuildTargetGraphicsAPIs");
            entries.DeleteArrayElementAtIndex(index);
            entries.serializedObject.ApplyModifiedPropertiesWithoutUndo();
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
            return index >= 0 && index + 1 < args.Length && args[index + 1].Length > 0 && !args[index + 1].StartsWith("-", StringComparison.Ordinal) ? args[index + 1] : null;
        }
    }
}
