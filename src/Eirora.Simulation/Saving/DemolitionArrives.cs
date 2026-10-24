using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 8 → 9 (TASK-047): a building can be demolished, which a state of rules 8 has never done; it keeps its shape,
    /// so this is the identity. The rules version is bumped so that an older build refuses a save or journal that holds a
    /// demolition instead of failing to read it (ADR-0006).
    /// </summary>
    internal sealed class DemolitionArrives : StateMigration
    {
        public override int FromRules => 8;

        public override string Migrate(string state, WorldMap map) => state;
    }
}
