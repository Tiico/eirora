using System;
using System.Collections.Generic;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 15 → 16 (TASK-056, D-135): the default import order ships wool before stone (<see cref="StockRules.DefaultImportOrder"/>).
    /// An island without an order of the player's takes the new default from the next quarter hour, and an order the
    /// player chose stays as it is. Only an order the player chose that is now the default changes, since the default is
    /// never written: for example <c>imports first Fish Grain Timber Planks Wool Stone Wadmal Game Hide Honey Mead Ale Tar</c>
    /// is left out. The balances, buildings and villagers are the same.
    /// </summary>
    internal sealed class WoolShipsBeforeStone : StateMigration
    {
        public override int FromRules => 15;

        public override string Migrate(string state, WorldMap map)
        {
            string order = string.Join(" ", StockRules.DefaultImportOrder);
            var lines = new List<string>(state.Split('\n'));
            lines.RemoveAll(line => line.StartsWith("imports ", StringComparison.Ordinal) && Order(line) == order);
            return string.Join("\n", lines);
        }

        /// <summary>The goods of an <c>imports</c> line, after its island.</summary>
        private static string Order(string line)
        {
            int island = line.IndexOf(' ', "imports ".Length);
            return island < 0 ? string.Empty : line.Substring(island + 1);
        }
    }
}
