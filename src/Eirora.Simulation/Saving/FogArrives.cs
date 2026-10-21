using System;
using System.Collections.Generic;
using System.Linq;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 2 → 3 (TASK-032): the revealed sites become fog per tile. Every world under rules 2 lives on the prototype's
    /// map, so it knows what a new world knows (Hemön and its coast, without the hidden sites), the hidden sites it has
    /// surveyed, and Nyön with its coast once the expedition has found it; nothing it has seen goes back under the fog.
    /// </summary>
    internal sealed class FogArrives : StateMigration
    {
        public override int FromRules => 2;

        public override string Migrate(string state)
        {
            var lines = state.Split('\n').ToList();
            var map = PrototypeHome.Map;
            if (lines.Count == 0 || lines[0] != "map " + map.Hash)
            {
                throw new FormatException("A world under rules 2 lives on the prototype's map.");
            }

            int first = lines.FindIndex(l => l.StartsWith("revealed ", StringComparison.Ordinal));
            if (first < 0)
            {
                throw new FormatException("A world under rules 2 has revealed sites.");
            }

            var revealed = new HashSet<string>(
                lines.Where(l => l.StartsWith("revealed ", StringComparison.Ordinal)).Select(l => l.Substring("revealed ".Length)),
                StringComparer.Ordinal);
            var fog = Fog.AtStart(map, PrototypeIsland.Sites.Where(s => s.Hidden).Select(s => s.Hex));
            foreach (var site in PrototypeIsland.Sites.Where(s => s.Hidden && revealed.Contains(s.Id)))
            {
                if (PrototypeIsland.ExpeditionReveals.Contains(site.Id) && map.IslandAt(site.Hex) is string island)
                {
                    fog.LiftIsland(island);
                }
                else
                {
                    fog.Lift(site.Hex);
                }
            }

            lines.RemoveAll(l => l.StartsWith("revealed ", StringComparison.Ordinal));
            lines.Insert(first, "fog " + fog.ToText());
            return string.Join("\n", lines);
        }
    }
}
