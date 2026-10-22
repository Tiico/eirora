using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Eirora.Simulation;
using Eirora.Simulation.Saving;
using Eirora.WorldGeneration;
using UnityEngine;

namespace Eirora.Client.App
{
    /// <summary>
    /// Plays the determinism corpus and reports a state hash for each entry, so that the IL2CPP build on Android can be
    /// compared with the server's .NET (TASK-021, NFR-02): each replay, each golden save loaded (and migrated) and saved
    /// again, and each golden map made again by its generator. ReplayCorpusTests in
    /// Eirora.Simulation.Tests reports the same entries under the same names. Only the determinism build runs it
    /// (DeterminismBootstrap), and only that build carries the corpus under Resources/Determinism
    /// (Builds.AndroidDeterminism).
    /// </summary>
    public static class DeterminismRun
    {
        /// <summary>Starts every line in the log that scripts/check-determinism.sh reads.</summary>
        public const string Marker = "EIRORA-DETERMINISM";

        /// <summary>Where the determinism build keeps the corpus under Resources.</summary>
        public const string ResourceFolder = "Determinism";

#if UNITY_EDITOR
        /// <summary>The replay corpus in the repository, which the editor copies into the build.</summary>
        public static string ReplayFolder => Repository("tests", "Replays");

        /// <summary>The golden saves (GoldenSaveTests), which the editor copies into the build.</summary>
        public static string GoldenSaveFolder => Repository("tests", "Eirora.Simulation.Tests", "Saving", "Golden");

        /// <summary>The golden maps' generators and seeds (GoldenMapTests), which the editor copies into the build.</summary>
        public static string GoldenMapList => Repository("tests", "Eirora.Simulation.Tests", "WorldGeneration", "Golden", "maps.txt");

        private static string Repository(params string[] path) =>
            Path.GetFullPath(Path.Combine(new[] { Application.dataPath, "..", ".." }.Concat(path).ToArray()));
#endif

        /// <summary>The state hash of each entry in the corpus, by name, in ordinal order.</summary>
        /// <param name="replays">Each replay's name and text.</param>
        /// <param name="saves">Each golden save's name and bytes.</param>
        /// <param name="goldenMaps">The golden maps' list: a generator, its version and a seed in hex per line.</param>
        public static IReadOnlyList<(string Name, string Hash)> Run(
            IEnumerable<(string Name, string Text)> replays,
            IEnumerable<(string Name, byte[] Bytes)> saves,
            string goldenMaps)
        {
            var maps = goldenMaps.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).Select(line => line.Split(' '));
            return replays.Select(r => ("replay/" + r.Name, Replay.StateHash(r.Text)))
                .Concat(saves.Select(s => ("save/" + s.Name, Replay.StateHashAfterSaving(Save.Load(s.Bytes)))))
                .Concat(maps.Select(f => ($"map/{f[0]}-{f[1]}-{f[2]}", MapHash(f[0], f[1], f[2]))))
                .OrderBy(e => e.Item1, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Runs the corpus in Resources, logs and writes the hashes, and quits.</summary>
        public static void RunFromResources()
        {
            var report = new StringBuilder();
            try
            {
                var hashes = Run(
                    Texts("Replays"),
                    Resources.LoadAll<TextAsset>(ResourceFolder + "/Saves").Select(asset => (asset.name, asset.bytes)).ToList(),
                    Resources.Load<TextAsset>(ResourceFolder + "/maps").text);
                foreach (var (name, hash) in hashes)
                {
                    Debug.Log($"{Marker} {name} {hash}");
                    report.Append(name).Append(' ').Append(hash).Append('\n');
                }

                File.WriteAllText(Path.Combine(Application.persistentDataPath, "determinism.txt"), report.ToString());
                Debug.Log($"{Marker}-DONE {hashes.Count.ToString(CultureInfo.InvariantCulture)}");
            }
            catch (Exception problem)
            {
                Debug.Log($"{Marker}-FAILED {problem.GetType().Name}: {problem.Message}");
            }

            Application.Quit();
        }

        private static List<(string Name, string Text)> Texts(string folder) =>
            Resources.LoadAll<TextAsset>(ResourceFolder + "/" + folder).Select(asset => (asset.name, asset.text)).ToList();

        private static string MapHash(string generator, string version, string seed) =>
            WorldGenerators.Get(generator, int.Parse(version, NumberStyles.None, CultureInfo.InvariantCulture))
                .Generate(ulong.Parse(seed, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture))
                .Hash;
    }
}
