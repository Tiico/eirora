namespace Eirora.Simulation.SelfCare
{
    /// <summary>Where a contribution came from (GLOSSARY: Verifierat bidrag, Egenrapporterat bidrag).</summary>
    public enum ContributionSource
    {
        /// <summary>Recorded automatically by the operating system's health store.</summary>
        Verified,

        /// <summary>Entered by the player, or marked as manually entered by the health store.</summary>
        SelfReported,
    }
}
