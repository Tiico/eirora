using System;
using System.Collections.Generic;
using System.Globalization;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 14 → 15 (TASK-063): the stores hold tar too, and the islands have knarrs (economy-v2.md, sections 4–5). The
    /// stores of rules 14 have no tar and the islands no knarrs, which are written only where there are any, an import
    /// order takes tar home last, and the unlocking goes on where the world stands: step 3 when step 2 is unlocked, a
    /// settlement already has 45 villagers, of them 6 craftsmen, and a sea route joins the home island to another
    /// settlement, a jetty standing on each. For example <c>economy 2 0 2</c> → <c>economy 2 0 3</c>, and after
    /// <c>stock hemon Ale 0</c> comes <c>stock hemon Tar 0</c>.
    /// </summary>
    internal sealed class SeafaringArrives : StateMigration
    {
        private const int SeafaringStep = 45; // Villagers of one settlement that unlock step 3, Seafaring (economy-v2.md, section 5).
        private const int SeafaringStepCraftsmen = 6; // The craftsmen among them.
        private const string NewGood = "Tar"; // The resource of rules 15 after Ale, rules 14's last.

        public override int FromRules => 14;

        public override string Migrate(string state, WorldMap map)
        {
            var lines = new List<string>(state.Split('\n'));
            string home = map.IslandAt(map.Start) ?? throw new FormatException("A map starts on land.");
            var settlements = new List<string>();
            var jetties = new HashSet<string>(StringComparer.Ordinal);
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
                        throw new FormatException($"'{lines[i]}' is not the economy of rules 14.");
                    case "settlement" when fields.Length == 5 && Count(fields[2]) is int crofters && Count(fields[3]) is int farmers && Count(fields[4]) is int craftsmen:
                        met |= crofters + farmers + craftsmen >= SeafaringStep && craftsmen >= SeafaringStepCraftsmen;
                        settlements.Add(fields[1]);
                        break;
                    case "settlement":
                        throw new FormatException($"'{lines[i]}' is not a settlement of rules 14.");
                    case "stock" when fields.Length == 4 && fields[2] == "Ale":
                        lines.Insert(++i, $"stock {fields[1]} {NewGood} 0");
                        break;
                    case "imports":
                        lines[i] = $"{lines[i]} {NewGood}";
                        break;
                    case "building" when fields.Length > 3 && fields[1] == "Jetty" && Count(fields[2], NumberStyles.AllowLeadingSign) is int q && Count(fields[3], NumberStyles.AllowLeadingSign) is int r:
                        if (map.IslandAt(new Hex(q, r)) is string island)
                        {
                            jetties.Add(island);
                        }

                        break;
                }
            }

            if (economy < 0)
            {
                throw new FormatException("A state of rules 14 has an economy.");
            }

            // A sea route joins the home island to another island's settlement when a jetty stands on each (economy-v2.md, section 6).
            bool routed = false;
            foreach (string island in settlements)
            {
                routed |= island != home && jetties.Contains(island) && jetties.Contains(home);
            }

            var economyFields = lines[economy].Split(' ');
            if (economyFields[3] == "2" && met && routed)
            {
                economyFields[3] = "3";
                lines[economy] = string.Join(" ", economyFields);
            }

            return string.Join("\n", lines);
        }

        private static int? Count(string field, NumberStyles styles = NumberStyles.None) =>
            int.TryParse(field, styles, CultureInfo.InvariantCulture, out int count) ? count : null;
    }
}
