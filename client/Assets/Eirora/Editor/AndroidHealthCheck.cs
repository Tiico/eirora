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
using UnityEngine;

namespace Eirora.Editor
{
    /// <summary>
    /// Fails every Android APK build, local or in CI, whose Health Connect adapter is missing or whose permissions break
    /// ADR-0007 and D-70: AGP 9 skips the Kotlin sources without any error unless gradle.properties turns built-in
    /// Kotlin on, and the game reads steps, exercise and sleep only, never in the background, never history or writes.
    /// </summary>
    public sealed class AndroidHealthCheck : IPostprocessBuildWithReport
    {
        public const string BridgeClass = "com.eirora.health.HealthBridge";

        public static readonly string[] HealthPermissions =
        {
            "android.permission.health.READ_EXERCISE",
            "android.permission.health.READ_SLEEP",
            "android.permission.health.READ_STEPS",
        };

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            string apk = report.summary.outputPath;
            if (report.summary.platform != BuildTarget.Android || !apk.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) || !File.Exists(apk))
            {
                return;
            }

            var problems = new List<string>();
            if (!ApkContainsClass(apk, BridgeClass))
            {
                problems.Add($"{BridgeClass} is not in the APK: the Kotlin sources in HealthConnect.androidlib were not compiled");
            }

            problems.AddRange(PermissionProblems(ApkPermissions(apk)));
            if (problems.Count > 0)
            {
                throw new BuildFailedException("The APK breaks Eirora's Health Connect guarantees:\n" + string.Join("\n", problems));
            }
        }

        /// <summary>Why a set of APK permissions is unacceptable.</summary>
        public static IEnumerable<string> PermissionProblems(IReadOnlyCollection<string> permissions)
        {
            foreach (var permission in permissions.Where(p => p.StartsWith("android.permission.health.", StringComparison.Ordinal) && !HealthPermissions.Contains(p)))
            {
                yield return $"{permission}: the game only reads steps, exercise and sleep, when it is opened (D-70)";
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
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
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
    }
}
