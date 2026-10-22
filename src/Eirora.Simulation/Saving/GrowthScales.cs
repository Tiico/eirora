namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 4 → 5 (TASK-038): distance, level 2 and a workday scale how fast a field or a sheep farm grows, as they
    /// scale every other building's production. The state keeps its shape and what has grown stays grown, so this is
    /// the identity; only the time after the snapshot grows by the new rule (ADR-0006).
    /// </summary>
    internal sealed class GrowthScales : StateMigration
    {
        public override int FromRules => 4;

        public override string Migrate(string state) => state;
    }
}
