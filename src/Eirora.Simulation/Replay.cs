using System;
using System.Collections.Generic;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Saving;

namespace Eirora.Simulation
{
    /// <summary>
    /// A replay from the determinism corpus (GLOSSARY: Replay, TASK-021): a world's create line and its inputs in journal
    /// form, without a rules version, so that it runs under whatever rules the build has. The same replay must lead to
    /// the same state on the server and in the client on every device (INV-D1, NFR-02).
    /// </summary>
    public static class Replay
    {
        /// <summary>The replay of a journal: its create line and inputs.</summary>
        public static string Of(Journal journal)
        {
            if (journal is null)
            {
                throw new ArgumentNullException(nameof(journal));
            }

            string text = journal.ToText();
            return text.Substring(text.IndexOf('\n') + 1);
        }

        /// <summary>
        /// The SHA-256 hash of the state the replay leads to under the current rules, after the world has been taken over
        /// as a save and been through <see cref="StateHashAfterSaving"/>.
        /// </summary>
        /// <exception cref="FormatException">The text is not a replay.</exception>
        /// <exception cref="InvalidOperationException">The restored world is not the world that was saved.</exception>
        public static string StateHash(string replay) => StateHashAfterSaving(Save.FromPrototypeJournal(AsJournal(replay)));

        /// <summary>
        /// The SHA-256 hash of a save's world after the save has been written to bytes and restored from them, as the
        /// client keeps a world, so the determinism corpus covers the save and its restore as well as the simulation.
        /// </summary>
        /// <exception cref="InvalidOperationException">The restored world is not the world that was saved.</exception>
        public static string StateHashAfterSaving(Save save)
        {
            if (save is null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            string saved = SimulationSnapshot.Take(save.World).StateHash;
            string restored = SimulationSnapshot.Take(Save.Load(save.ToBytes()).World).StateHash;
            if (!string.Equals(saved, restored, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The save restored the state {restored}, not the {saved} it saved.");
            }

            return restored;
        }

        /// <summary>The key and reason of each command in the replay that the world rejects, in replay order.</summary>
        /// <exception cref="FormatException">The text is not a replay.</exception>
        internal static IReadOnlyList<(string Key, Rejection Rejection)> Rejections(string replay)
        {
            var rejections = new List<(string Key, Rejection Rejection)>();
            Journal.Load(AsJournal(replay), out _, (key, result) =>
            {
                if (!result.Accepted)
                {
                    rejections.Add((key, result.Rejection));
                }
            });
            return rejections;
        }

        private static string AsJournal(string replay) =>
            replay is null ? throw new ArgumentNullException(nameof(replay)) : Journal.CurrentHeader + "\n" + replay;
    }
}
