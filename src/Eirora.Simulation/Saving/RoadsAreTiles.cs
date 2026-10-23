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
    /// <item>A building's road becomes road tiles on every tile between it and the nearest staffed storehouse or jetty, or
    /// the nearest one when no staffed one is reached, along the shortest way over land, so its carriers walk the whole
    /// way on the road. Rules 7 charged 2 stone for each step to the nearest depot; the stone for each step that gets no
    /// new road tile, the last into the depot and those another road already lies on, goes back to the home island's
    /// store as far as it has room.</item>
    /// <item>A road in the queue becomes a road project on each of those tiles that has no road, laid or queued, with the
    /// work it had left shared between them, at most a tile's 4 hours each, and gives back its stone the same way.</item>
    /// <item>A staffed storehouse or jetty gets one carrier, its villager, and takes more at the next dawn; every depot may
    /// take four. No building's outbox holds anything yet, and processing starts with what the carriers bring it.</item>
    /// <item>Each building with a recipe belongs to the staffed depot on its island whose carriers reach it fastest on the
    /// roads, as the settlement of rules 8 gives it one at dawn, or to none when no staffed depot reaches it.</item>
    /// </list>
    /// For example <c>building Quarry 3 2 1 1 1 0 0 0</c> → <c>building Quarry 3 2 1 1 0 0 0 0 0 0 0 1 0 0</c>, served by
    /// the storehouse on 0 0, with the road's
    /// tiles as <c>road</c> lines after the queue, and a used command's <c>project-completed … Road Quarry 3 2</c> →
    /// <c>project-completed … Road 3 2</c>, since a road no longer names a building.
    /// </summary>
    internal sealed class RoadsAreTiles : StateMigration
    {
        private const int BuildingFields = 10;
        private const long StonePerStep = 2_000_000;
        private const long StorePerStorehouse = 150_000_000;
        private const long WorkPerTile = 4000;
        private const long StepTime = 250_000;
        private const long RoadStepTime = 125_000;

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
            var staffed = new HashSet<Hex>();
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
                        if (fields[5] == "1")
                        {
                            staffed.Add(tile);
                        }
                    }
                }
            }

            // The tiles a road leads over: to the nearest staffed depot, where the carriers will go, or else the nearest
            // depot. Rules 7 charged 2 stone for each step to the nearest depot of any kind.
            List<Hex> RoadOf(Hex from) => Way(map, from, staffed) is { Found: true } way ? way.Tiles : Way(map, from, depots).Tiles;
            long Paid(Hex from) => Way(map, from, depots) is { Found: true } way ? StonePerStep * (way.Tiles.Count + 1) : 0;

            int storehouses = buildings.Count(b => b.Kind == "Storehouse" && (IslandOf(map, b.Tile) ?? home) == home);
            var roads = new SortedSet<Hex>();
            long refund = 0;
            int afterQueue = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string[] fields = lines[i].Split(' ');
                if (fields[0] == "building")
                {
                    // building Kind q r level staffed road workday ripe paused → … level staffed workday ripe paused carriers cap first waiting depot
                    bool depot = IsDepot(fields[1]);
                    if (fields[6] == "1")
                    {
                        // Each step paid for that no new road tile is laid on goes back: the last step into the depot,
                        // and the tiles another road already lies on.
                        var tiles = RoadOf(TileAt(fields, 2)).Where(t => !roads.Contains(t)).ToList();
                        roads.UnionWith(tiles);
                        refund += Unused(Paid(TileAt(fields, 2)), tiles.Count);
                    }

                    string carriers = depot && fields[5] == "1" ? "1" : "0";
                    // Which depot serves it is known once all the roads are.
                    lines[i] = string.Join(" ", fields.Take(6).Concat(fields.Skip(7)).Concat(new[] { carriers, depot ? "4" : "0", "0", "0", "0", "0", "0" }));
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

                // The work left is shared between the tiles, at most a tile's 4 hours each; the stone for each step that gets
                // no new tile goes back.
                var tiles = RoadOf(TileAt(fields, 3)).Where(t => !roads.Contains(t) && !Queued(lines, t)).ToList();
                refund += Unused(Paid(TileAt(fields, 3)), tiles.Count);
                var projects = tiles.Select((t, k) => $"project Road {Text(t.Q)} {Text(t.R)} {Text(Share(work, tiles.Count, k))}").ToList();
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
            var walks = new Dictionary<string, Dictionary<Hex, (long Time, Hex Depot)>>();
            for (int i = 0; i < lines.Count; i++)
            {
                string[] fields = lines[i].Split(' ');
                if (fields[0] == "building" && HasRecipe(fields[1]) && IslandOf(map, TileAt(fields, 2)) is string island)
                {
                    if (!walks.TryGetValue(island, out var walk))
                    {
                        walk = Walk(map, island, staffed, roads);
                        walks.Add(island, walk);
                    }

                    if (DepotOf(map, walk, TileAt(fields, 2), island, roads) is Hex served)
                    {
                        lines[i] = string.Join(" ", fields.Take(fields.Length - 3).Concat(new[] { "1", Text(served.Q), Text(served.R) }));
                    }
                }
            }

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
        /// The tiles between a building and the nearest of <paramref name="depots"/> on its island, nearest the depot first,
        /// on the way rules 7 counted its distance by: breadth first over the island's land, neighbours counter-clockwise
        /// from east; and whether a depot was found.
        /// </summary>
        private static (bool Found, List<Hex> Tiles) Way(WorldMap map, Hex from, HashSet<Hex> depots)
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

            return (found is not null, way);
        }

        /// <summary>
        /// How fast the carriers of the staffed depots on an island reach each tile of it and from which depot, as the
        /// settlement of rules 8 walks it: back from the depots, a step onto a tile taking 15 minutes and 7.5 with a road,
        /// and of two depots as fast the one with the smaller tile (R, then Q).
        /// </summary>
        private static Dictionary<Hex, (long Time, Hex Depot)> Walk(WorldMap map, string island, HashSet<Hex> depots, SortedSet<Hex> roads)
        {
            var best = new Dictionary<Hex, (long Time, Hex Depot)>();
            var open = new SortedSet<(long Time, Hex Depot, Hex Tile)>();
            foreach (var depot in depots.Where(d => IslandOf(map, d) == island))
            {
                best[depot] = (0, depot);
                open.Add((0, depot, depot));
            }

            while (open.Count > 0)
            {
                var at = open.Min;
                open.Remove(at);
                long time = at.Time + (roads.Contains(at.Tile) ? RoadStepTime : StepTime);
                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    var near = at.Tile.Neighbour(direction);
                    if (!map.Contains(near) || map.IslandAt(near) != island)
                    {
                        continue;
                    }

                    if (best.TryGetValue(near, out var known))
                    {
                        if (known.Time < time || (known.Time == time && known.Depot <= at.Depot))
                        {
                            continue;
                        }

                        open.Remove((known.Time, known.Depot, near));
                    }

                    best[near] = (time, at.Depot);
                    open.Add((time, at.Depot, near));
                }
            }

            return best;
        }

        /// <summary>The depot that reaches a tile fastest: its own for land, and for a fishing hut on its stilts by the best first step ashore.</summary>
        private static Hex? DepotOf(WorldMap map, Dictionary<Hex, (long Time, Hex Depot)> walk, Hex tile, string island, SortedSet<Hex> roads)
        {
            if (map.IslandAt(tile) is not null)
            {
                return walk.TryGetValue(tile, out var trip) && trip.Time > 0 ? trip.Depot : null;
            }

            (long Time, Hex Depot)? ashore = null;
            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var near = tile.Neighbour(direction);
                if (!map.Contains(near) || map.IslandAt(near) != island || !walk.TryGetValue(near, out var land))
                {
                    continue;
                }

                long time = land.Time + (roads.Contains(near) ? RoadStepTime : StepTime);
                if (ashore is not { } so || time < so.Time || (time == so.Time && land.Depot < so.Depot))
                {
                    ashore = (time, land.Depot);
                }
            }

            return ashore?.Depot;
        }

        /// <summary>The stone paid for a road that no new road tile takes.</summary>
        private static long Unused(long paid, int tiles) => paid > StonePerStep * tiles ? paid - (StonePerStep * tiles) : 0;

        /// <summary>The <paramref name="tile"/>th tile's share of the work a road had left, the remainder on the first, at most a tile's work.</summary>
        private static long Share(long work, int tiles, int tile)
        {
            long share = (work / tiles) + (tile == 0 ? work % tiles : 0);
            return share < WorkPerTile ? share : WorkPerTile;
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

        private static bool HasRecipe(string kind) => kind is "FishingHut" or "Field" or "Woodcutter" or "SheepFarm" or "Quarry" or "Sawmill" or "Weavery";

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
