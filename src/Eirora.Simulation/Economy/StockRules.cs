namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The player's rules for the stores (LOG-06, D-44; TASK-046): the minimum stock processing leaves, the order the
    /// ships take the goods home in, and the reservations on each island. All are free planning (D-23), and they only
    /// keep goods where they are or change their order: they never make or lose any (INV-R1, INV-R2).
    /// </summary>
    public static class StockRules
    {
        /// <summary>
        /// Whole units of its raw material that a new processing building leaves in the store (economy-v1.md, section 5),
        /// unless its recipe says otherwise: the tannery leaves 5 game, which is also food (TASK-061).
        /// </summary>
        public const int StandardReserve = 20;
    }

    /// <summary>Which of the player's rules keeps a good in the store.</summary>
    public enum KeptBy
    {
        /// <summary>The processing building's minimum stock (GLOSSARY: Minimilager).</summary>
        Reserve,

        /// <summary>The island's reservation, which neither the ships nor processing take (GLOSSARY: Reservering).</summary>
        Reservation,
    }
}
