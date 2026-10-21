namespace Eirora.Simulation
{
    /// <summary>
    /// The version of the simulation rules.
    /// Bumped whenever a rule change can make the same commands produce a different state (INV-D1).
    /// </summary>
    public static class RulesVersion
    {
        public const int Current = 4;

        /// <summary>
        /// The oldest rules version whose journals the current rules replay to the same world (ADR-0006). Rules 2 only
        /// added the map to the state (TASK-029), and rules 3 turned revealed sites into fog per tile without changing
        /// what a journal's commands do (TASK-032). Rules 4 made every land tile a building site (TASK-037): woodcutters
        /// fell real forest tiles and distance is counted over land, so a journal from before then would replay to
        /// another world and is refused instead (ADR-0006).
        /// </summary>
        public const int ReplaysJournalsFrom = 4;
    }
}
