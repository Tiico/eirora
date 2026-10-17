using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Eirora.Editor
{
    /// <summary>
    /// Player builds for CI and local use. The output path comes from -customBuildPath (GameCI) or -buildOutput.
    /// </summary>
    /// <remarks>
    /// Run: Unity -batchmode -quit -projectPath client -executeMethod Eirora.Editor.Builds.Android -buildOutput Builds/Android/Eirora.apk
    /// </remarks>
    public static class Builds
    {
        public static void Android() => Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/Eirora.apk");

        public static void Ios() => Build(BuildTarget.iOS, BuildTargetGroup.iOS, "Builds/iOS");

        private static void Build(BuildTarget target, BuildTargetGroup group, string defaultOutput)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = ArgumentValue("-customBuildPath") ?? ArgumentValue("-buildOutput") ?? defaultOutput,
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

        private static string ArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
