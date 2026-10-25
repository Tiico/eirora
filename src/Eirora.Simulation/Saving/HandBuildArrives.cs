using System;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 12 → 13 (TASK-065): a building the catalog marks can be built by hand in a fixed number of game days (D-65),
    /// and each project tells the dawns it has left, 0 for work by the free villagers, which is all a state of rules 12
    /// holds. Only <c>project</c> lines change, for example <c>project Build Cottage 1 0 8000</c> →
    /// <c>project Build Cottage 1 0 8000 0</c> and <c>project Road 2 -1 4000</c> → <c>project Road 2 -1 4000 0</c>.
    /// </summary>
    internal sealed class HandBuildArrives : StateMigration
    {
        private const int BuildingFields = 6;
        private const int RoadFields = 5; // A road names no building (D-112).

        public override int FromRules => 12;

        public override string Migrate(string state, WorldMap map)
        {
            var lines = state.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var fields = lines[i].Split(' ');
                if (fields[0] != "project")
                {
                    continue;
                }

                int expected = fields.Length > 1 && fields[1] == "Road" ? RoadFields : BuildingFields;
                lines[i] = fields.Length == expected
                    ? $"{lines[i]} 0"
                    : throw new FormatException($"'{lines[i]}' is not a project of rules 12.");
            }

            return string.Join("\n", lines);
        }
    }
}
