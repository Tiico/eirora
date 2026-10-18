using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 1 → 2 (TASK-029): the world gets its map. Every world under rules 1 lives on the prototype's handmade map,
    /// so its state is the same with that map's hash first (ADR-0010, D-81); nothing else changes.
    /// </summary>
    internal sealed class MapArrives : StateMigration
    {
        public override int FromRules => 1;

        public override string Migrate(string state) => "map " + PrototypeHome.Map.Hash + "\n" + state;
    }
}
