// UnityEditor.Android exists only where Unity's Android support is installed, which the test runner's image lacks;
// Android builds always compile with UNITY_ANDROID, so the check still guards every one of them.
#if UNITY_ANDROID
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
    /// Fails every Android build, APK or app bundle, local or in CI, whose Health Connect adapter or sign-in module is
    /// missing or whose permissions break ADR-0007 and D-70: AGP 9 skips the Kotlin sources without any error unless
    /// gradle.properties turns built-in Kotlin on, and the game reads steps, exercise and sleep only, never in the
    /// background, never history or writes.
    /// </summary>
    public sealed class AndroidHealthCheck : IPostprocessBuildWithReport
    {
        public const string BridgeClass = "com.eirora.health.HealthBridge";

        /// <summary>Sign in with Apple and Google and the keystore (ADR-0008), in SignIn.androidlib.</summary>
        public const string SignInBridgeClass = "com.eirora.signin.SignInBridge";

        public static readonly string[] HealthPermissions =
        {
            "android.permission.health.READ_EXERCISE",
            "android.permission.health.READ_SLEEP",
            "android.permission.health.READ_STEPS",
        };

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            string package = report.summary.outputPath;
            if (report.summary.platform != BuildTarget.Android || !(IsApk(package) || IsBundle(package)) || !File.Exists(package))
            {
                return;
            }

            var problems = new List<string>();
            if (!PackageContainsClass(package, BridgeClass))
            {
                problems.Add($"{BridgeClass} is not in {Path.GetFileName(package)}: the Kotlin sources in HealthConnect.androidlib were not compiled");
            }

            if (!PackageContainsClass(package, SignInBridgeClass))
            {
                problems.Add($"{SignInBridgeClass} is not in {Path.GetFileName(package)}: the Kotlin sources in SignIn.androidlib were not compiled");
            }

            problems.AddRange(PermissionProblems(PackagePermissions(package)));
            if (problems.Count > 0)
            {
                throw new BuildFailedException($"{Path.GetFileName(package)} breaks Eirora's Health Connect guarantees:\n" + string.Join("\n", problems));
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

        /// <summary>
        /// Whether any classes*.dex in the APK, or in any module of the app bundle, defines <paramref name="className"/>,
        /// by its type descriptor.
        /// </summary>
        public static bool PackageContainsClass(string package, string className)
        {
            var descriptor = System.Text.Encoding.ASCII.GetBytes("L" + className.Replace('.', '/') + ";");
            using var archive = ZipFile.OpenRead(package);
            foreach (var entry in archive.Entries.Where(e => e.Name.StartsWith("classes", StringComparison.Ordinal) && e.Name.EndsWith(".dex", StringComparison.Ordinal)))
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

        /// <summary>
        /// The uses-permission names in an APK, read with aapt2 from Unity's Android SDK, or in an app bundle's base
        /// manifest, read with the bundletool that Unity's Android support ships.
        /// </summary>
        public static IReadOnlyList<string> PackagePermissions(string package)
        {
            IEnumerable<string> permissions;
            if (IsBundle(package))
            {
                // Prints one permission name per line.
                var tools = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "Tools");
                var bundletool = Directory.GetFiles(tools, "bundletool-all-*.jar").OrderByDescending(f => f, StringComparer.Ordinal).First();
                var java = Path.Combine(AndroidExternalToolsSettings.jdkRootPath, "bin", Application.platform == RuntimePlatform.WindowsEditor ? "java.exe" : "java");
                permissions = Run(java, $"-jar \"{bundletool}\" dump manifest --bundle \"{package}\" --xpath /manifest/uses-permission/@android:name")
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0);
            }
            else
            {
                // Lines look like: uses-permission: name='android.permission.health.READ_STEPS'
                var buildTools = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "build-tools");
                var aapt2 = Directory.GetDirectories(buildTools).OrderByDescending(d => d, StringComparer.Ordinal)
                    .Select(d => Path.Combine(d, Application.platform == RuntimePlatform.WindowsEditor ? "aapt2.exe" : "aapt2"))
                    .First(File.Exists);
                permissions = Run(aapt2, $"dump permissions \"{package}\"")
                    .Where(line => line.StartsWith("uses-permission", StringComparison.Ordinal))
                    .Select(line => line.Substring(line.IndexOf('\'') + 1).TrimEnd('\r', '\'', ' '));
            }

            return permissions.OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        private static bool IsApk(string path) => path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase);

        private static bool IsBundle(string path) => path.EndsWith(".aab", StringComparison.OrdinalIgnoreCase);

        private static string[] Run(string tool, string arguments)
        {
            var start = new ProcessStartInfo(tool, arguments) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {tool}");
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{Path.GetFileName(tool)} exited with {process.ExitCode}");
            }

            return output.Split('\n');
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
#endif
