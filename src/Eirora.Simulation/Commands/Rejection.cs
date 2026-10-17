namespace Eirora.Simulation.Commands
{
    /// <summary>Why a command was rejected; a rejected command changes nothing (INV-D7).</summary>
    public enum Rejection
    {
        /// <summary>The command was accepted.</summary>
        None,

        /// <summary>The villagers are resting and the rest lock holds (INV-T3).</summary>
        Resting,

        /// <summary>The rest schedule is outside 6–10 hours or starts outside the day (INV-T6).</summary>
        InvalidRestSchedule,

        /// <summary>The game day is in the future or more than seven game days back (D-53).</summary>
        GameDayOutOfRange,

        /// <summary>The amount is negative or beyond what the source can plausibly report.</summary>
        AmountOutOfRange,
    }
}
