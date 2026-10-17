namespace Eirora.Simulation.Commands
{
    /// <summary>Whether a command changes the world and is therefore stopped by the rest lock (INV-T3).</summary>
    public enum CommandCategory
    {
        /// <summary>Changes the world, such as building or sending an expedition (GLOSSARY: Spelpåverkande kommando).</summary>
        Gameplay,

        /// <summary>
        /// Allowed during rest: account, privacy, accessibility, help, deletion, rest schedule and recording
        /// self-care (GLOSSARY: Administrativt kommando, RST-07, D-54).
        /// </summary>
        Administrative,
    }
}
