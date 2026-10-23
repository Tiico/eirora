using System;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 5 → 6 (TASK-044): the player can pause a building and order the staffing and the queue, and villagers are
    /// given out again in the staffing order whenever it can change. Every building is running, the staffing order is
    /// the order they were built in, which the state already keeps, and who is staffed stays as it was until the
    /// villagers are next given out, so only <c>building</c> lines change, for example
    /// <c>building Quarry 2 -1 1 1 0 0 0</c> → <c>building Quarry 2 -1 1 1 0 0 0 0</c>.
    /// </summary>
    internal sealed class StaffingIsOrdered : StateMigration
    {
        private const int BuildingFields = 9;

        public override int FromRules => 5;

        public override string Migrate(string state)
        {
            var lines = state.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var fields = lines[i].Split(' ');
                if (fields[0] != "building")
                {
                    continue;
                }

                lines[i] = fields.Length == BuildingFields
                    ? lines[i] + " 0"
                    : throw new FormatException($"'{lines[i]}' is not a building of rules 5.");
            }

            return string.Join("\n", lines);
        }
    }
}
