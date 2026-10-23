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

        /// <summary>The tile is under the fog, already taken or does not meet the building's requirement (D-79).</summary>
        SiteUnavailable,

        /// <summary>The store does not hold the materials the work costs.</summary>
        InsufficientResources,

        /// <summary>The force the command costs is not available (INV-F5).</summary>
        InsufficientForces,

        /// <summary>There is no suitable building on the site.</summary>
        NoBuildingThere,

        /// <summary>It is already done, queued or not needed, such as a second upgrade or a road to a building next to the store.</summary>
        AlreadyDone,

        /// <summary>Every survey area has been surveyed.</summary>
        NothingLeftToSurvey,

        /// <summary>The building queue is empty.</summary>
        NothingToRush,

        /// <summary>The fog lifts only from a tile next to what is known (TASK-032).</summary>
        NotNextToTheKnown,

        /// <summary>A survey reveals this tile; its fog is not lifted tile by tile.</summary>
        FoundBySurvey,

        /// <summary>This island is found by an expedition; its fog is not lifted tile by tile.</summary>
        FoundByExpedition,

        /// <summary>The staffing order has no such place.</summary>
        NoSuchPlace,

        /// <summary>The building queue holds no such project.</summary>
        NotQueued,
    }
}
