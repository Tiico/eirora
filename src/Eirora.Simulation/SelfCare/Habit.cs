namespace Eirora.Simulation.SelfCare
{
    /// <summary>A tracked self-care habit (GLOSSARY: Vana) and the unit its daily total is counted in.</summary>
    public enum Habit
    {
        /// <summary>Movement, counted in steps (active minutes are converted before recording, D-41).</summary>
        Steps,

        /// <summary>Training, counted in sessions of at least ten minutes (D-42).</summary>
        Workouts,

        /// <summary>Sleep, counted in minutes, attributed to the game day in which the sleep ended.</summary>
        Sleep,
    }
}
