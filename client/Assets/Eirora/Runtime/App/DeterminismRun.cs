using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Eirora.Simulation;
using UnityEngine;

namespace Eirora.Client.App
{
    /// <summary>
    /// Plays the replay corpus and reports the state hash of each replay, so that the IL2CPP build on Android can be
    /// compared with the server's .NET (TASK-021, NFR-02). Only the determinism build runs it (DeterminismBootstrap),
    /// and only that build carries the corpus under Resources/Replays (Builds.AndroidDeterminism).
    /// </summary>
    public static class DeterminismRun
    {
        /// <summary>Starts every line in the log that scripts/check-determinism.sh reads.</summary>
        public const string Marker = "EIRORA-DETERMINISM";

#if UNITY_EDITOR
        /// <summary>The replay corpus in the repository (tests/Replays), which the editor copies into the build.</summary>
        public static string CorpusFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tests", "Replays"));
#endif

        /// <summary>The state hash of each replay, by name, in ordinal order.</summary>
        public static IReadOnlyList<(string Name, string Hash)> Run(IEnumerable<(string Name, string Replay)> replays) =>
            replays.OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => (r.Name, Replay.StateHash(r.Replay))).ToList();

        /// <summary>Runs the corpus in Resources, logs and writes the hashes, and quits.</summary>
        public static void RunFromResources()
        {
            var replays = Resources.LoadAll<TextAsset>("Replays").Select(asset => (asset.name, asset.text)).ToList();
            var report = new StringBuilder();
            try
            {
                foreach (var (name, hash) in Run(replays))
                {
                    Debug.Log($"{Marker} {name} {hash}");
                    report.Append(name).Append(' ').Append(hash).Append('\n');
                }

                File.WriteAllText(Path.Combine(Application.persistentDataPath, "determinism.txt"), report.ToString());
                Debug.Log($"{Marker}-DONE {replays.Count}");
            }
            catch (Exception problem)
            {
                Debug.Log($"{Marker}-FAILED {problem.GetType().Name}: {problem.Message}");
            }

            Application.Quit();
        }
    }
}
