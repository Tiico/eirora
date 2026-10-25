using System;
using System.Collections.Generic;
using System.Globalization;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 12 → 13 (TASK-062): every settlement has craftsmen too, and the stores hold honey, mead and ale
    /// (economy-v2.md, sections 2–5). No villager of rules 12 is a craftsman, the stores have none of the new goods, an
    /// import order takes them home last, and the unlocking goes on where the world stands: step 2 when step 1 is unlocked
    /// and a settlement already has 30 villagers, of them 6 farmers. For example <c>economy 2 0 1</c> →
    /// <c>economy 2 0 2</c>, <c>settlement hemon 24 6</c> → <c>settlement hemon 24 6 0</c>, and after
    /// <c>stock hemon Hide 0</c> come <c>stock hemon Honey 0</c>, <c>stock hemon Mead 0</c> and <c>stock hemon Ale 0</c>.
    /// </summary>
    internal sealed class CraftsmenArrive : StateMigration
    {
        private const int CraftsmenStep = 30; // Villagers of one settlement that unlock step 2, Craftsmen (economy-v2.md, section 5).
        private const int CraftsmenStepFarmers = 6; // The farmers among them.
        private static readonly string[] s_newGoods = { "Honey", "Mead", "Ale" }; // The resources of rules 13 after Hide, rules 12's last.

        public override int FromRules => 12;

        public override string Migrate(string state, WorldMap map)
        {
            var lines = new List<string>(state.Split('\n'));
            int economy = -1;
            bool met = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var fields = lines[i].Split(' ');
                switch (fields[0])
                {
                    case "economy" when fields.Length == 4:
                        economy = i;
                        break;
                    case "economy":
                        throw new FormatException($"'{lines[i]}' is not the economy of rules 12.");
                    case "settlement" when fields.Length == 4 && Count(fields[2]) is int crofters && Count(fields[3]) is int farmers:
                        met |= crofters + farmers >= CraftsmenStep && farmers >= CraftsmenStepFarmers;
                        lines[i] = $"{lines[i]} 0";
                        break;
                    case "settlement":
                        throw new FormatException($"'{lines[i]}' is not a settlement of rules 12.");
                    case "stock" when fields.Length == 4 && fields[2] == "Hide":
                        foreach (string good in s_newGoods)
                        {
                            lines.Insert(++i, $"stock {fields[1]} {good} 0");
                        }

                        break;
                    case "imports":
                        lines[i] = $"{lines[i]} {string.Join(" ", s_newGoods)}";
                        break;
                }
            }

            if (economy < 0)
            {
                throw new FormatException("A state of rules 12 has an economy.");
            }

            var economyFields = lines[economy].Split(' ');
            if (economyFields[3] == "1" && met)
            {
                economyFields[3] = "2";
                lines[economy] = string.Join(" ", economyFields);
            }

            return string.Join("\n", lines);
        }

        private static int? Count(string field) =>
            int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out int count) ? count : null;
    }
}
