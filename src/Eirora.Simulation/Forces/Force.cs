namespace Eirora.Simulation.Forces
{
    /// <summary>
    /// A force: the game resource that a habit is converted into and that the player directs (GLOSSARY: Kraft).
    /// </summary>
    public enum Force
    {
        /// <summary>From steps; drives expeditions and lifts the fog (Utforskning).</summary>
        Exploration,

        /// <summary>From workouts; drives large builds and upgrades (Styrka).</summary>
        BuildPower,

        /// <summary>From sleep duration; drives the morning's planning moves (Ork).</summary>
        DawnPower,
    }
}
