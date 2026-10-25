using System;
using System.Collections.Generic;
using System.Globalization;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 11 → 12 (TASK-061): every settlement has villagers of each population tier, the world has an unlocked step,
    /// and the stores hold game and hide (economy-v2.md, sections 2–5). Every villager of rules 11 is a crofter, the
    /// stores have neither game nor hide, an import order takes them home last, and the unlocking starts where the world
    /// stands: step 1 when a settlement already has 20 villagers. For example <c>economy 2 0</c> →
    /// <c>economy 2 0 1</c>, <c>settlement hemon 20</c> → <c>settlement hemon 20 0</c>, and after
    /// <c>stock hemon Wadmal 0</c> come <c>stock hemon Game 0</c> and <c>stock hemon Hide 0</c>.
    /// </summary>
    internal sealed class PopulationTiersArrive : StateMigration
    {
        private const int FarmersStep = 20; // Villagers of one settlement that unlock step 1, Farmers (economy-v2.md, section 5).
        private const string NewGoods = "Game Hide"; // The resources of rules 12 after Wadmal, rules 11's last.

        public override int FromRules => 11;

        public override string Migrate(string state, WorldMap map)
        {
            var lines = new List<string>(state.Split('\n'));
            int economy = -1;
            int largest = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var fields = lines[i].Split(' ');
                switch (fields[0])
                {
                    case "economy" when fields.Length == 3:
                        economy = i;
                        break;
                    case "economy":
                        throw new FormatException($"'{lines[i]}' is not the economy of rules 11.");
                    case "settlement" when fields.Length == 3 && int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int villagers):
                        largest = villagers > largest ? villagers : largest;
                        lines[i] = $"{lines[i]} 0";
                        break;
                    case "settlement":
                        throw new FormatException($"'{lines[i]}' is not a settlement of rules 11.");
                    case "stock" when fields.Length == 4 && fields[2] == "Wadmal":
                        lines.Insert(++i, $"stock {fields[1]} Game 0");
                        lines.Insert(++i, $"stock {fields[1]} Hide 0");
                        break;
                    case "imports":
                        lines[i] = $"{lines[i]} {NewGoods}";
                        break;
                }
            }

            if (economy < 0)
            {
                throw new FormatException("A state of rules 11 has an economy.");
            }

            lines[economy] = $"{lines[economy]} {(largest >= FarmersStep ? 1 : 0)}";
            return string.Join("\n", lines);
        }
    }
}
