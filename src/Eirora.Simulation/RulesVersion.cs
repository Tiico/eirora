namespace Eirora.Simulation
{
    /// <summary>
    /// The version of the simulation rules.
    /// Bumped whenever a rule change can make the same commands produce a different state (INV-D1).
    /// </summary>
    public static class RulesVersion
    {
        public const int Current = 3;

        /// <summary>
        /// The oldest rules version whose journals the current rules replay to the same world, so that a prototype
        /// journal from M1 can still be taken over (ADR-0006). Rules 2 only added the map to the state (TASK-029), and
        /// rules 3 turned revealed sites into fog per tile without changing what a journal's commands do (TASK-032).
        /// </summary>
        public const int ReplaysJournalsFrom = 1;
    }
}
