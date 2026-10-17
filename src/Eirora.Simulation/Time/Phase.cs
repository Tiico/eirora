namespace Eirora.Simulation.Time
{
    /// <summary>
    /// The world's phase in the day cycle (INV-T2). Dawn is the instant a rest period ends; it is reported as a
    /// <see cref="Events.DawnBroke"/> event rather than a phase with a duration.
    /// </summary>
    public enum Phase
    {
        /// <summary>The villagers are awake and gameplay commands are accepted.</summary>
        Active,

        /// <summary>The rest lock holds: villagers sleep and gameplay commands are rejected (INV-T3).</summary>
        Resting,
    }
}
