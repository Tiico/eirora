using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Saving
{
    /// <summary>
    /// Rules 8 → 9 (TASK-050, D-106): each island built on has its own settlement, whose villagers staff only its
    /// buildings, carry only for its depots, eat from its store and move over a sea route, and a jetty stores 50 of each
    /// resource on every island. The villagers who staff buildings or carry for depots on an island other than the home
    /// island become its settlement's, and the rest stay home (economy-v2.md, section 12), for example
    /// <c>settlement 23 2 0</c> → <c>economy 2 0</c>, <c>settlement home 18</c> and <c>settlement second 5</c>. Who is
    /// staffed and who carries stays as it was, which is what giving each settlement's villagers out again would give. An island can then have more villagers than homes; they stay,
    /// and it does not grow until there is room (economy-v2.md, section 12). What no longer fits in an island's store is
    /// shipped home over its sea route, and what does not fit there either, or has no sea route, waits at the island's
    /// depots until there is room (INV-R1); so does the stone that rules 8's migration gave back to the home island
    /// beyond its room (<see cref="RoadsAreTiles"/>). Nothing is lost or made (INV-R2, INV-V1).
    /// </summary>
    internal sealed class SettlementsPerIsland : StateMigration
    {
        private const int BuildingFields = 16;
        private const int StoragePerStorehouse = 150;
        private const int StorageAtJetty = 50;

        // The resources of rules 8, in the order each store lists them.
        private static readonly string[] s_resources = { "Fish", "Grain", "Timber", "Planks", "Stone", "Wool", "Wadmal" };

        // The villagers each building needed under rules 8, which they still need; a depot's are its carriers.
        private static readonly Dictionary<string, int> s_staff = new(StringComparer.Ordinal)
        {
            ["Longhouse"] = 0,
            ["Cottage"] = 0,
            ["Storehouse"] = 1,
            ["FishingHut"] = 2,
            ["Field"] = 2,
            ["Woodcutter"] = 2,
            ["SheepFarm"] = 1,
            ["Quarry"] = 2,
            ["Sawmill"] = 2,
            ["Weavery"] = 2,
            ["Jetty"] = 1,
            ["Lookout"] = 0,
        };

        public override int FromRules => 8;

        public override string Migrate(string state, WorldMap map)
        {
            var lines = state.Split('\n').ToList();
            if (lines.Count == 0 || lines[0] != "map " + map.Hash)
            {
                throw new FormatException("The state belongs to another map.");
            }

            string home = map.IslandAt(map.Start) ?? throw new FormatException("A map starts on land.");
            int at = lines.FindIndex(l => l.StartsWith("settlement ", StringComparison.Ordinal));
            string[] economy = at >= 0 ? lines[at].Split(' ') : Array.Empty<string>();
            if (economy.Length != 4 || !TryNumber(economy[1], out long villagers))
            {
                throw new FormatException("A world under rules 8 has one settlement line with its villagers, surveys and expedition.");
            }

            // Each island's store, as rules 8 wrote them: every resource of an island together, the islands in map order.
            var islands = new List<string>();
            var stores = new Dictionary<string, List<(string Resource, long Amount)>>(StringComparer.Ordinal);
            int stocks = 0;
            while (at + 1 + stocks < lines.Count && lines[at + 1 + stocks].StartsWith("stock ", StringComparison.Ordinal))
            {
                string[] fields = lines[at + 1 + stocks].Split(' ');
                if (fields.Length != 4 || !TryNumber(fields[3], out long amount) || fields[2] != s_resources[stocks % s_resources.Length])
                {
                    throw new FormatException($"'{lines[at + 1 + stocks]}' is not a store of rules 8.");
                }

                if (!stores.TryGetValue(fields[1], out var store))
                {
                    store = new List<(string, long)>();
                    stores.Add(fields[1], store);
                    islands.Add(fields[1]);
                }

                store.Add((fields[2], amount));
                stocks++;
            }

            if (!stores.TryGetValue(home, out var homeStore) || islands.Any(i => stores[i].Count != s_resources.Length))
            {
                throw new FormatException("A world under rules 8 has a full store on the home island and on every island built on.");
            }

            // Who staffs the buildings on each island, and what each island's depots hold under rules 8.
            var staffing = islands.ToDictionary(i => i, _ => 0L, StringComparer.Ordinal);
            var capacity = islands.ToDictionary(i => i, _ => 0L, StringComparer.Ordinal);
            var jetties = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                string[] fields = line.Split(' ');
                if (fields[0] != "building")
                {
                    continue;
                }

                if (fields.Length != BuildingFields || !s_staff.TryGetValue(fields[1], out int staff)
                    || !int.TryParse(fields[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int q)
                    || !int.TryParse(fields[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int r))
                {
                    throw new FormatException($"'{line}' is not a building of rules 8.");
                }

                string island = IslandsFromTheMap.IslandOf(map, new Hex(q, r)) ?? home;
                if (!staffing.ContainsKey(island))
                {
                    throw new FormatException($"'{line}' stands on {island}, which has no store.");
                }

                staffing[island] += fields[1] is "Storehouse" or "Jetty" ? Carriers(fields[9], line) : fields[5] == "1" ? staff : 0;
                if (fields[1] == "Jetty")
                {
                    jetties.Add(island);
                }

                capacity[island] += Quantity.Units(fields[1] switch
                {
                    "Storehouse" => StoragePerStorehouse,
                    "Jetty" => StorageAtJetty,
                    _ => 0,
                });
            }

            var settlers = islands.ToDictionary(i => i, i => i == home ? 0 : staffing[i], StringComparer.Ordinal);
            settlers[home] = villagers - islands.Sum(i => settlers[i]);
            if (settlers[home] < staffing[home])
            {
                throw new FormatException("The buildings have more staff than the world has villagers.");
            }

            // What no longer fits on an island goes home over its sea route, and what home has no room for, or what an
            // island without a sea route holds, waits at the island's depots until there is room; so does what home holds
            // beyond its room, which only the stone rules 8 gave back can be.
            var waiting = islands.ToDictionary(i => i, _ => new List<string>(), StringComparer.Ordinal);
            for (int i = 0; i < homeStore.Count; i++)
            {
                long over = homeStore[i].Amount - capacity[home];
                if (over > 0)
                {
                    homeStore[i] = (homeStore[i].Resource, capacity[home]);
                    waiting[home].Add($"at-depot {home} {homeStore[i].Resource} {Text(over)}");
                }
            }

            foreach (string island in islands.Where(i => i != home))
            {
                var store = stores[island];
                for (int i = 0; i < store.Count; i++)
                {
                    long over = store[i].Amount - capacity[island];
                    if (over <= 0)
                    {
                        continue;
                    }

                    long room = jetties.Contains(island) && jetties.Contains(home) ? capacity[home] - homeStore[i].Amount : 0;
                    long shipped = room <= 0 ? 0 : room < over ? room : over;
                    homeStore[i] = (homeStore[i].Resource, homeStore[i].Amount + shipped);
                    store[i] = (store[i].Resource, capacity[island]);
                    if (over > shipped)
                    {
                        waiting[island].Add($"at-depot {island} {store[i].Resource} {Text(over - shipped)}");
                    }
                }
            }

            var migrated = new List<string> { $"economy {economy[2]} {economy[3]}" };
            migrated.AddRange(islands.Select(i => $"settlement {i} {Text(settlers[i])}"));
            migrated.AddRange(islands.SelectMany(i => stores[i].Select(s => $"stock {i} {s.Resource} {Text(s.Amount)}")));
            migrated.AddRange(islands.SelectMany(i => waiting[i]));
            lines.RemoveRange(at, 1 + stocks);
            lines.InsertRange(at, migrated);
            return string.Join("\n", lines);
        }

        private static int Carriers(string text, string line) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int carriers) ? carriers : throw new FormatException($"'{line}' has no carriers.");

        private static bool TryNumber(string text, out long value) =>
            long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) && value >= 0;

        private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
