using System;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// A prototype journal from rules that this build no longer replays to the same world (ADR-0006,
    /// <see cref="RulesVersion.ReplaysJournalsFrom"/>). It is intact, so it is kept; the player is told it cannot be
    /// played on rather than shown another world.
    /// </summary>
    public sealed class JournalFromOlderRulesException : FormatException
    {
        public JournalFromOlderRulesException(string message)
            : base(message)
        {
        }
    }
}
