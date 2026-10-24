namespace Eirora.Simulation
{
    /// <summary>
    /// The version of the simulation rules.
    /// Bumped whenever a rule change can make the same commands produce a different state (INV-D1).
    /// </summary>
    public static class RulesVersion
    {
        public const int Current = 8;

        /// <summary>
        /// The oldest rules version whose journals the current rules replay to the same world (ADR-0006). Rules 2 only
        /// added the map to the state (TASK-029), and rules 3 turned revealed sites into fog per tile without changing
        /// what a journal's commands do (TASK-032). Rules 4 made every land tile a building site (TASK-037): woodcutters
        /// fell real forest tiles and distance is counted over land, so a journal from before then would replay to
        /// another world and is refused instead (ADR-0006). Rules 5 let distance, level 2 and a workday scale how fast
        /// fields and sheep farms grow (TASK-038), so a journal from rules 4 would replay to another world too. Rules 6
        /// give villagers out again in the staffing order whenever it can change (TASK-044), where a staffed building
        /// stayed staffed before, so a journal from rules 5 would replay to another world as well. Rules 7 gave every
        /// island its own store and sea route (TASK-045): a journal from rules 6 that built beside a second island off
        /// the home island, which shared Nyön's store and jetty then, would replay to another world. Rules 8 let carriers
        /// carry the goods on land and lay roads tile by tile (TASK-051): distance costs the carriers' time instead of a
        /// share of the production, so a journal from rules 7 would replay to another world too.
        /// </summary>
        public const int ReplaysJournalsFrom = 8;
    }
}
