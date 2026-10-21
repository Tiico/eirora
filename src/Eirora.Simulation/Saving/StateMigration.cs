namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Turns the state of one rules version into the state of the next (GLOSSARY: Migration, ADR-0006).
    /// </summary>
    /// <remarks>
    /// It works on the canonical state text, because the rules that wrote it are no longer in the code. It is a pure
    /// function (INV-D2) that keeps the simulation time and returns the canonical text of version
    /// <see cref="FromRules"/> + 1; a rule change that keeps the state's shape has an identity migration. From rules 3
    /// on, the state can end before the contributions when a cloud save's contribution part is lost (D-101).
    /// </remarks>
    internal abstract class StateMigration
    {
        /// <summary>The rules version the state comes from.</summary>
        public abstract int FromRules { get; }

        public abstract string Migrate(string state);
    }
}
