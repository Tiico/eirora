using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 3 → 4 (TASK-037): every land tile becomes a building site, so the state names tiles by their coordinates
    /// instead of the prototype's site ids, for example <c>building Longhouse S</c> → <c>building Longhouse 0 0</c>.
    /// Every building, project and road stays where it is, and stock, forces and fog are untouched (INV-V1). The forest
    /// is kept per tile instead of per site: a site's tile keeps the share of timber the site had per forest tile, and
    /// the map's other forest tiles, which the prototype never felled, are full.
    /// </summary>
    internal sealed class TilesArrive : StateMigration
    {
        private const long ForestPerTile = 40 * Quantity.PerUnit;

        public override int FromRules => 3;

        public override string Migrate(string state, WorldMap map)
        {
            var lines = state.Split('\n').ToList();
            var prototype = PrototypeHome.Map;
            if (lines.Count == 0 || lines[0] != "map " + prototype.Hash)
            {
                // Before rules 4 every world began with the prototype's sites, which lie only on the prototype's map.
                throw new FormatException("A world under rules 3 lives on the prototype's map.");
            }

            var felled = new SortedDictionary<Hex, long>();
            int forestAt = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                var fields = lines[i].Split(' ');
                switch (fields[0])
                {
                    case "building" when fields.Length == 8:
                    case "project-completed" when fields.Length == 5:
                        lines[i] = Replace(fields, fields[0] == "building" ? 2 : 4);
                        break;
                    case "project" when fields.Length == 5:
                        lines[i] = Replace(fields, 3);
                        break;
                    case "sites-revealed":
                        lines[i] = "tiles-revealed " + fields[1] + string.Concat(fields.Skip(2).Select(id => " " + Coordinates(PrototypeSites.HexOf(id))));
                        break;
                    case "expedition-returned" when fields.Length > 2:
                        lines[i] = $"expedition-returned {fields[1]} {prototype.IslandAt(PrototypeSites.HexOf(fields[2]))}";
                        break;
                    case "forest" when fields.Length == 3:
                        forestAt = forestAt < 0 ? i : forestAt;
                        Fell(felled, fields[1], fields[2]);
                        lines[i] = string.Empty;
                        break;
                    case "building":
                    case "project":
                    case "project-completed":
                    case "forest":
                        throw new FormatException($"'{lines[i]}' is not a record of rules 3.");
                }
            }

            foreach (var forest in felled)
            {
                lines.Insert(forestAt++, "forest " + Coordinates(forest.Key) + " " + forest.Value.ToString(CultureInfo.InvariantCulture));
            }

            return string.Join("\n", lines.Where((line, i) => line.Length > 0 || i == lines.Count - 1));
        }

        /// <summary>Keeps a forest site's timber per forest tile on its own tile, unless the tile is full.</summary>
        private static void Fell(SortedDictionary<Hex, long> felled, string id, string timber)
        {
            var site = PrototypeSites.All.FirstOrDefault(s => s.Id == id);
            if (site.ForestTiles == 0 || !long.TryParse(timber, NumberStyles.None, CultureInfo.InvariantCulture, out long left))
            {
                throw new FormatException($"'forest {id} {timber}' is not a forest of rules 3.");
            }

            long perTile = left / site.ForestTiles;
            if (perTile < ForestPerTile)
            {
                felled[site.Hex] = perTile;
            }
        }

        private static string Replace(string[] fields, int site)
        {
            fields[site] = Coordinates(PrototypeSites.HexOf(fields[site]));
            return string.Join(" ", fields);
        }

        private static string Coordinates(Hex tile) =>
            tile.Q.ToString(CultureInfo.InvariantCulture) + " " + tile.R.ToString(CultureInfo.InvariantCulture);
    }
}
