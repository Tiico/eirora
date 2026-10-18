using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Eirora.Editor
{
    /// <summary>
    /// Local player builds: an Android APK for the product owner's phone and a Windows player for the screenshot tour.
    /// CI builds with GameCI's default build instead.
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

            var summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log($"Build {summary.result}: {summary.outputPath} ({summary.totalSize} bytes, {summary.totalErrors} errors).");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }

        private static string? Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
