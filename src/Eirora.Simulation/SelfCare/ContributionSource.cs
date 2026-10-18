namespace Eirora.Simulation.SelfCare
{
    /// <summary>Where a contribution came from (GLOSSARY: Verifierat bidrag, Egenrapporterat bidrag).</summary>
    public enum ContributionSource
    {
        /// <summary>Recorded automatically by the operating system's health store, including an unknown recording method (D-68).</summary>
        Verified,

        /// <summary>Entered by the player in the game.</summary>
        SelfReported,

        /// <summary>
        /// The part of the health store's total that the store marks as manually entered (`MANUAL_ENTRY`, D-68).
        /// Self-reported, but counted together with the verified part, since the store has already de-duplicated both.
        /// </summary>
        HealthStoreManualEntry,
    }
}
