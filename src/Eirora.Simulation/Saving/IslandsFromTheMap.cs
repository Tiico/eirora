using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 6 → 7 (TASK-045): the islands come from the map, each island built on keeps its own store, and each island
    /// with a jetty gets its own sea route home. Under rules 6 the economy knew two islands, Home and Nyon, and every
    /// island but the home island shared Nyon's store, for example <c>stock Nyon Stone 5000000</c> →
    /// <c>stock nyon Stone 5000000</c> on the prototype's map. Home's store becomes the map's start island's, and Nyon's
    /// becomes the store of the island of the first building off the home island, which is the island the expedition
    /// found; every other island built on gets an empty store. The stores follow in map order. Nothing else changes,
    /// so no goods are lost or made (INV-R2, INV-V1).
    /// </summary>
    internal sealed class IslandsFromTheMap : StateMigration
    {
        private const int BuildingFields = 10;

        public override int FromRules => 6;

        public override string Migrate(string state, WorldMap map)
        {
            var lines = state.Split('\n').ToList();
            if (lines.Count == 0 || lines[0] != "map " + map.Hash)
            {
                throw new FormatException("The state belongs to another map.");
            }

            string home = map.IslandAt(map.Start) ?? throw new FormatException("A map starts on land.");
            int first = lines.FindIndex(l => l.StartsWith("stock ", StringComparison.Ordinal));
            int stocks = lines.Count(l => l.StartsWith("stock ", StringComparison.Ordinal));
            if (first < 0 || stocks % 2 != 0)
            {
                throw new FormatException("A world under rules 6 has a store on Home and one on Nyon.");
            }

            // Each line's resource and amount, as rules 6 wrote them: every resource on Home, then on Nyon.
            int resources = stocks / 2;
            var stored = new Dictionary<string, List<(string Resource, string Amount)>>(StringComparer.Ordinal);
            for (int i = 0; i < stocks; i++)
            {
                string[] fields = lines[first + i].Split(' ');
                string from = i < resources ? "Home" : "Nyon";
                if (fields.Length != 4 || fields[0] != "stock" || fields[1] != from || (i >= resources && fields[2] != stored["Home"][i - resources].Resource))
                {
                    throw new FormatException($"'{lines[first + i]}' is not a store of rules 6.");
                }

                if (!stored.TryGetValue(from, out var store))
                {
                    store = new List<(string, string)>();
                    stored.Add(from, store);
                }

                store.Add((fields[2], fields[3]));
            }

            // The islands built on, in the order of their buildings.
            var builtOn = new List<string>();
            foreach (string line in lines)
            {
                string[] fields = line.Split(' ');
                if (fields[0] == "building")
                {
                    var tile = fields.Length == BuildingFields && int.TryParse(fields[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int q)
                        && int.TryParse(fields[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int r)
                        ? new Hex(q, r)
                        : throw new FormatException($"'{line}' is not a building of rules 6.");
                    builtOn.Add(IslandOf(map, tile) ?? home);
                }
            }

            var stores = new Dictionary<string, List<(string Resource, string Amount)>>(StringComparer.Ordinal) { [home] = stored["Home"] };
            if (builtOn.FirstOrDefault(island => island != home) is string found)
            {
                stores[found] = stored["Nyon"];
            }
            else if (stored["Nyon"].Any(s => s.Amount != "0"))
            {
                throw new FormatException("A world under rules 6 has goods on Nyon but nothing built off the home island.");
            }

            foreach (string island in builtOn)
            {
                if (!stores.ContainsKey(island))
                {
                    stores[island] = stored["Home"].Select(s => (s.Resource, "0")).ToList();
                }
            }

            var migrated = map.Islands.Where(i => stores.ContainsKey(i.Id))
                .SelectMany(i => stores[i.Id].Select(s => $"stock {i.Id} {s.Resource} {s.Amount}"));
            lines.RemoveRange(first, stocks);
            lines.InsertRange(first, migrated);
            return string.Join("\n", lines);
        }

        /// <summary>The island of a tile as rules 6 told it: its own for land, or the first land next to it, counter-clockwise from east.</summary>
        internal static string? IslandOf(WorldMap map, Hex tile)
        {
            if (!map.Contains(tile))
            {
                throw new FormatException($"{tile} is not on the map.");
            }

            if (map.IslandAt(tile) is string island)
            {
                return island;
            }

            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var near = tile.Neighbour(direction);
                if (map.Contains(near) && map.IslandAt(near) is string coast)
                {
                    return coast;
                }
            }

            return null;
        }
    }
}
