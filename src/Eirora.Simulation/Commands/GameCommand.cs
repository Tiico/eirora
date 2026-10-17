namespace Eirora.Simulation.Commands
{
    /// <summary>A command the player asks the world to carry out (GLOSSARY: Kommando).</summary>
    public abstract class GameCommand
    {
        /// <summary>Whether the rest lock stops this command.</summary>
        public abstract CommandCategory Category { get; }

        /// <summary>Validates and applies the command; must change nothing when it returns a rejection (INV-D7).</summary>
        internal abstract Rejection Execute(World world);
    }
}
