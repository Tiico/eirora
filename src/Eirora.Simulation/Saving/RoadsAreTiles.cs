using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 7 → 8 (TASK-051): the carriers of the storehouses and jetties carry the goods on land, and roads lie on tiles
    /// (economy-v2.md, sections 7 and 12; D-107, D-109–D-112). Nothing is lost (INV-R2, INV-V1):
    /// <list type="bullet">
    /// <item>A building's road becomes road tiles on every tile between it and the nearest storehouse or jetty, along the
    /// shortest way over land that rules 7 counted its distance by, so its carriers walk the whole way on the road. Rules 7
    /// charged 2 stone for each of its steps, the last into the depot too, so those 2 stone go back to the home island's
    /// store, as far as it has room.</item>
    /// <item>A road in the queue becomes a road project on each of those tiles that has no road, with the work it had left
    /// shared between them, and gives back its 2 stone the same way.</item>
    /// <item>A staffed storehouse or jetty gets one carrier, its villager, and takes more at the next dawn; every depot may
    /// take four. No building's outbox holds anything yet, and processing starts with what the carriers bring it.</item>
    /// </list>
    /// For example <c>building Quarry 3 2 1 1 1 0 0 0</c> → <c>building Quarry 3 2 1 1 0 0 0 0 0 0 0</c> with the road's
    /// tiles as <c>road</c> lines after the queue, and a used command's <c>project-completed … Road Quarry 3 2</c> →
    /// <c>project-completed … Road 3 2</c>, since a road no longer names a building.
    /// </summary>
    internal sealed class RoadsAreTiles : StateMigration
    {
        private const int BuildingFields = 10;
        private const long StonePerStep = 2_000_000;
        private const long StorePerStorehouse = 150_000_000;

        public override int FromRules => 7;

        public override string Migrate(string state, WorldMap map)
        {
            var lines = state.Split('\n').ToList();
            if (lines.Count == 0 || lines[0] != "map " + map.Hash)
            {
                throw new FormatException("The state belongs to another map.");
            }

            string home = map.IslandAt(map.Start) ?? throw new FormatException("A map starts on land.");
            var buildings = new List<(string Kind, Hex Tile)>();
            var depots = new HashSet<Hex>();
            foreach (string line in lines)
            {
                string[] fields = line.Split(' ');
                if (fields[0] == "building")
                {
                    var tile = fields.Length == BuildingFields ? TileAt(fields, 2) : throw new FormatException($"'{line}' is not a building of rules 7.");
                    buildings.Add((fields[1], tile));
                    if (IsDepot(fields[1]))
                    {
                        depots.Add(tile);
                    }
                }
            }

            int storehouses = buildings.Count(b => b.Kind == "Storehouse" && (IslandOf(map, b.Tile) ?? home) == home);
            var roads = new SortedSet<Hex>();
            long refund = 0;
            int afterQueue = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string[] fields = lines[i].Split(' ');
                if (fields[0] == "building")
                {
                    // building Kind q r level staffed road workday ripe paused → … level staffed workday ripe paused carriers cap first waiting
                    bool depot = IsDepot(fields[1]);
                    if (fields[6] == "1")
                    {
                        roads.UnionWith(Way(map, TileAt(fields, 2), depots));
                        refund += StonePerStep;
                    }

                    string carriers = depot && fields[5] == "1" ? "1" : "0";
                    lines[i] = string.Join(" ", fields.Take(6).Concat(fields.Skip(7)).Concat(new[] { carriers, depot ? "4" : "0", "0", "0" }));
                }
                else if (fields[0] == "project")
                {
                    afterQueue = i + 1;
                }
                else if (fields[0] is "forest" or "fog" && afterQueue < 0)
                {
                    afterQueue = i;
                }
            }

            // Roads in the queue, in their places there, once the roads already laid are known.
            for (int i = 0; i < lines.Count; i++)
            {
                string[] fields = lines[i].Split(' ');
                if (fields[0] != "project" || fields[1] != "Road")
                {
                    continue;
                }

                if (fields.Length != 6 || !long.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out long work))
                {
                    throw new FormatException($"'{lines[i]}' is not a road in the queue of rules 7.");
                }

                var tiles = Way(map, TileAt(fields, 3), depots).Where(t => !roads.Contains(t) && !Queued(lines, t)).ToList();
                refund += StonePerStep;
                var projects = tiles.Select((t, k) => $"project Road {Text(t.Q)} {Text(t.R)} {Text((work / tiles.Count) + (k == 0 ? work % tiles.Count : 0))}").ToList();
                lines.RemoveAt(i);
                lines.InsertRange(i, projects);
                afterQueue += projects.Count - 1;
                i += projects.Count - 1;
            }

            if (afterQueue < 0)
            {
                throw new FormatException("A world under rules 7 ends its settlement with the fog.");
            }

            lines.InsertRange(afterQueue, roads.Select(t => $"road {Text(t.Q)} {Text(t.R)}"));
            GiveBack(lines, home, refund, storehouses * StorePerStorehouse);

            // A road no longer names the building it was for.
            for (int i = 0; i < lines.Count; i++)
            {
                string[] fields = lines[i].Split(' ');
                if (fields[0] == "project-completed" && fields.Length == 6 && fields[2] == "Road")
                {
                    lines[i] = string.Join(" ", fields.Take(3).Concat(fields.Skip(4)));
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// The tiles between a building and the nearest storehouse or jetty on its island, nearest the depot first, on the
        /// way rules 7 counted its distance by: breadth first over the island's land, neighbours counter-clockwise from east.
        /// </summary>
        private static List<Hex> Way(WorldMap map, Hex from, HashSet<Hex> depots)
        {
            var way = new List<Hex>();
            string? island = IslandOf(map, from);
            var parent = new Dictionary<Hex, Hex> { [from] = from };
            var frontier = new List<Hex> { from };
            Hex? found = null;
            while (frontier.Count > 0 && found is null)
            {
                var next = new List<Hex>();
                foreach (var hex in frontier)
                {
                    if (depots.Contains(hex))
                    {
                        found = hex;
                        break;
                    }

                    for (int direction = 0; direction < Hex.Directions; direction++)
                    {
                        var near = hex.Neighbour(direction);
                        if (map.Contains(near) && map.IslandAt(near) == island && island is not null && parent.TryAdd(near, hex))
                        {
                            next.Add(near);
                        }
                    }
                }

                frontier = next;
            }

            for (var step = found is Hex depot ? parent[depot] : from; step != from; step = parent[step])
            {
                way.Add(step);
            }

            return way;
        }

        private static bool Queued(List<string> lines, Hex tile) =>
            lines.Any(l => l == $"project Road {Text(tile.Q)} {Text(tile.R)}" || l.StartsWith($"project Road {Text(tile.Q)} {Text(tile.R)} ", StringComparison.Ordinal));

        /// <summary>Puts the stone back in the home island's store, as far as it has room (INV-R1).</summary>
        private static void GiveBack(List<string> lines, string home, long stone, long capacity)
        {
            int at = lines.FindIndex(l => l.StartsWith($"stock {home} Stone ", StringComparison.Ordinal));
            if (at < 0)
            {
                throw new FormatException("A world under rules 7 has stone on the home island.");
            }

            long stock = long.Parse(lines[at].Split(' ')[3], NumberStyles.None, CultureInfo.InvariantCulture);
            long room = capacity - stock;
            stock += stone < room ? stone : room > 0 ? room : 0;
            lines[at] = $"stock {home} Stone {Text(stock)}";
        }

        private static bool IsDepot(string kind) => kind is "Storehouse" or "Jetty";

        private static Hex TileAt(string[] fields, int at) =>
            int.TryParse(fields[at], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int q)
            && int.TryParse(fields[at + 1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int r)
                ? new Hex(q, r)
                : throw new FormatException($"'{string.Join(" ", fields)}' has no tile.");

        /// <summary>The island of a tile as rules 7 told it: its own for land, or the first land next to it, counter-clockwise from east.</summary>
        private static string? IslandOf(WorldMap map, Hex tile)
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

        private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
