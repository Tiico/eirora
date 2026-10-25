using System;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 9 → 10 (TASK-046): the player sets the minimum stock of each processing building, the order the ships take
    /// each island's goods home in, and reservations on each island (LOG-06). Every world keeps what it had: the sawmills
    /// and weaveries leave the 20 that all processing left, the ships keep the resources' fixed order and nothing is
    /// reserved, which the state writes by leaving it out. Only <c>building</c> lines change, for example
    /// <c>building Sawmill 1 0 1 1 0 0 0 0 0 0 0 0 1 0 0</c> → <c>building Sawmill 1 0 1 1 0 0 0 0 0 0 0 0 1 0 0 20</c>.
    /// </summary>
    internal sealed class StockRulesArrive : StateMigration
    {
        private const int BuildingFields = 16;
        private const int Reserve = 20; // What rules 9 left of a raw material for building (economy-v1.md, section 5).

        public override int FromRules => 9;

        public override string Migrate(string state, WorldMap map)
        {
            var lines = state.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var fields = lines[i].Split(' ');
                if (fields[0] != "building")
                {
                    continue;
                }

                // The processing of rules 9: the buildings with raw materials.
                bool processing = fields[1] is "Sawmill" or "Weavery";
                lines[i] = fields.Length == BuildingFields
                    ? $"{lines[i]} {(processing ? Reserve : 0)}"
                    : throw new FormatException($"'{lines[i]}' is not a building of rules 9.");
            }

            return string.Join("\n", lines);
        }
    }
}
