using System;

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

        /// <summary>The SHA-256 hash of the state the replay leads to under the current rules.</summary>
        /// <exception cref="FormatException">The text is not a replay.</exception>
        public static string StateHash(string replay)
        {
            if (replay is null)
            {
                throw new ArgumentNullException(nameof(replay));
            }

            var world = Journal.Load(Journal.CurrentHeader + "\n" + replay).World;
            return SimulationSnapshot.Take(world).StateHash;
        }
    }
}
