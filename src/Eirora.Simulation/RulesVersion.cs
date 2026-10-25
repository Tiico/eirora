namespace Eirora.Simulation
{
    /// <summary>
    /// The version of the simulation rules.
    /// Bumped whenever a rule change can make the same commands produce a different state (INV-D1).
    /// </summary>
    public static class RulesVersion
    {
        public const int Current = 12;

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
        /// share of the production, so a journal from rules 7 would replay to another world too. Rules 9 gave every
        /// island its own settlement (TASK-050, D-106): its villagers staff only its buildings, carry only for its depots
        /// and eat from its store, so a journal from rules 8 that staffed Nyön's buildings from the home island would
        /// replay to another world as well. Rules 10 added demolition (TASK-047), a command and an event that older rules
        /// cannot read, and changed nothing that a journal from the rules before can hold, so such a journal still replays
        /// to the same world.
        /// Rules 11 let the player set the minimum stock, the import order and reservations (TASK-046); a journal from
        /// rules 10 has none of their commands and keeps the minimum stock of 20 and the fixed order, so it replays to the
        /// same world.
        /// Rules 12 gave the settlements population tiers and the world unlocked steps (TASK-061): the villagers eat from
        /// the good there is most of instead of fish first, and wadmal for comfort is used up only when a second villager
        /// fits, so a journal from rules 11 would replay to another world and is refused instead.
        /// </summary>
        public const int ReplaysJournalsFrom = 12;
    }
}
