using System.Globalization;
using System.Text;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 7 → 8 (TASK-051): a building's road becomes road tiles along its way to the nearest storehouse or jetty, a road
/// in the queue becomes road projects on those tiles, every staffed depot gets one carrier, and nothing is lost
/// (economy-v2.md, section 12; ADR-0006, INV-R2, INV-V1).
/// </summary>
public sealed class RoadsAreTilesTests
{
    private const long Unit = Quantity.PerUnit;

    private static readonly Resource[] s_resources = PopulationTiersArriveTests.RulesElevenResources; // The goods of rules 7.

    // Acceptance, on a world a player could have saved: the golden save from rules 7, five of whose buildings had roads.
    [Fact]
    public void EveryRoadBecomesRoadTilesTheWholeWayAndNoGoodsAreLost()
    {
        byte[] bytes = GoldenSaveTests.Bytes("save-3-rules-7");
        var before = State(bytes);
        Assert.Contains("rules 7", SaveSections.Text(Encoding.UTF8.GetString(bytes), "latest").Split('\n'));
        var roads = Buildings(before).Where(b => b.Road).ToList();
        Assert.True(roads.Count >= 3, $"The golden save has only {roads.Count} roads.");

        var world = Save.Load(bytes).World;

        // The buildings stand as they stood, in the same order, levels and staffing, with nothing waiting yet.
        Assert.Equal(
            Buildings(before).Select(b => (b.Kind, b.Tile, b.Level, b.Staffed)),
            world.Buildings.Select(b => (b.Kind.ToString(), b.Tile, b.Level, b.Staffed)));
        Assert.All(world.Buildings, b => Assert.Equal(0, b.Waiting));
        Assert.All(world.Buildings.Where(b => Logistics.IsDepot(b.Kind)), d => Assert.Equal((d.Staffed ? 1 : 0, Logistics.MostCarriers), (d.Carriers, d.CarrierCap)));

        // Every road leads the whole way: each step on a road, only the last into the depot at a walker's pace.
        foreach (var road in roads)
        {
            var trip = world.TripFrom(road.Tile);
            Assert.True(trip.Reaches, $"{road.Kind} on {road.Tile} reaches no depot.");
            Assert.Equal(((trip.Steps - 1) * Logistics.RoadStepTime) + Logistics.StepTime, trip.Time);
            Assert.Equal(StepsToDepot(world, road.Tile), trip.Steps);
        }

        // No goods are lost: every store is as it was, and the home island has back the 2 stone paid for each step that
        // got no road tile of its own: the last into the depot, and the tiles two roads share.
        Assert.Contains(world.Roads, t => roads.Count(r => world.WayToDepot(r.Tile).Contains(t)) > 1);
        long unused = 2 * Unit * (roads.Sum(r => StepsToDepot(world, r.Tile)) - world.Roads.Count);
        foreach (var island in world.Islands)
        {
            foreach (var resource in s_resources)
            {
                long was = Stock(before, island, resource);
                long back = island == world.HomeIsland && resource == Resource.Stone ? Math.Min(unused, Capacity(world, island) - was) : 0;
                Assert.Equal(was + back, world.Settlement.Stock(island, resource));
            }
        }
    }

    [Fact]
    public void ARoadInTheQueueBecomesARoadProjectOnEachTileWithTheWorkItHadLeft()
    {
        // A quarry on the stone vein, three steps from the storehouse, with its road half built.
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var vein = Tiles.M2;
        string state = RulesSeven(world.Fingerprint().TrimEnd('\n'));
        var lines = state.Split('\n').ToList();
        lines.Insert(lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1, $"building Quarry {vein.Q} {vein.R} 1 0 0 0 0 0");
        lines.Insert(lines.FindIndex(l => l.StartsWith("fog ", StringComparison.Ordinal)), $"project Road Quarry {vein.Q} {vein.R} 7001");
        lines.Add("command old 1");
        lines.Add($"project-completed 1 Road Quarry {vein.Q} {vein.R}");

        string migrated = new RoadsAreTiles().Migrate(string.Join('\n', lines), PrototypeHome.Map);

        var projects = migrated.Split('\n').Where(l => l.StartsWith("project ", StringComparison.Ordinal)).Select(l => l.Split(' ')).ToList();
        Assert.Equal(2, projects.Count); // Three steps: two tiles between the quarry and the storehouse.
        Assert.All(projects, p => Assert.Equal("Road", p[1]));
        Assert.Equal(7001, projects.Sum(p => long.Parse(p[4], CultureInfo.InvariantCulture)));
        Assert.Equal([3501, 3500], projects.Select(p => long.Parse(p[4], CultureInfo.InvariantCulture)));
        var tiles = projects.Select(p => new Hex(int.Parse(p[2], CultureInfo.InvariantCulture), int.Parse(p[3], CultureInfo.InvariantCulture))).ToList();
        Assert.Equal(1, tiles[0].DistanceTo(Tiles.S)); // Nearest the storehouse first.
        Assert.Contains($"project-completed 1 Road {vein.Q} {vein.R}", migrated.Split('\n'));
        Assert.Contains($"stock {PrototypeHome.Home} Stone {20 * Unit + (2 * Unit)}", migrated.Split('\n'));

        // Once built, the quarry's carriers walk the whole way on the road.
        var read = World.Read(new Serialization.StateReader(Migrated(migrated, 8, RulesVersion.Current, PrototypeHome.Map) + "\n"), PrototypeHome.Map);
        Assert.Equal(3, read.TripFrom(vein).Steps);
        read.AdvanceTo(TestClock.At("2026-10-15 12:00", 2));
        Assert.Equal(tiles.Order(), read.Roads);
        Assert.Equal((2 * Logistics.RoadStepTime) + Logistics.StepTime, read.TripFrom(vein).Time);
    }

    // Review of #166: a depot without its villager under rules 7 has no carriers under rules 8, so its buildings go to the
    // nearest staffed depot, and a road leads there, where the carriers walk.
    [Fact]
    public void AnUnstaffedDepotIsPassedForTheNearestStaffedOneAndTheRoadLeadsThere()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesSeven(world.Fingerprint().TrimEnd('\n')).Split('\n').ToList();
        int last = lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal));
        lines.Insert(last + 1, $"building Quarry {Tiles.M2.Q} {Tiles.M2.R} 1 1 1 0 0 0");
        lines.Insert(last + 2, $"building Storehouse {Tiles.E2.Q} {Tiles.E2.R} 1 0 0 0 0 0");

        var read = World.Read(new Serialization.StateReader(Migrated(string.Join('\n', lines), 7, RulesVersion.Current, PrototypeHome.Map) + "\n"), PrototypeHome.Map);

        Assert.Equal(Tiles.S, read.TripFrom(Tiles.M2).Depot);
        Assert.Equal(read.TripFrom(Tiles.M2).Steps - 1, read.Roads.Count);
        Assert.All(read.WayToDepot(Tiles.M2).SkipLast(1), t => Assert.True(read.HasRoad(t) || read.BuildingAt(t) is not null, $"{t} has no road."));
    }

    // Two roads that share tiles paid twice for them; the second gives back the stone for every tile it gets no new road on.
    [Fact]
    public void RoadsThatShareTilesGiveBackTheStoneOfTheTilesTheyShare()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesSeven(world.Fingerprint().TrimEnd('\n')).Split('\n').ToList();
        int last = lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal));
        lines.Insert(last + 1, $"building Quarry {Tiles.M2.Q} {Tiles.M2.R} 1 1 1 0 0 0");
        lines.Insert(last + 2, $"building Woodcutter {Tiles.F2.Q} {Tiles.F2.R} 1 1 1 0 0 0");

        var migrated = new RoadsAreTiles().Migrate(string.Join('\n', lines), PrototypeHome.Map).Split('\n');

        // Each paid 2 stone a step to the storehouse; the tiles laid are paid once and the rest comes back.
        int laid = migrated.Count(l => l.StartsWith("road ", StringComparison.Ordinal));
        long paid = 2 * Unit * (Tiles.M2.DistanceTo(Tiles.S) + Tiles.F2.DistanceTo(Tiles.S));
        Assert.Contains($"stock {PrototypeHome.Home} Stone {(20 * Unit) + paid - (2 * Unit * laid)}", migrated);
    }

    // The stone the store has no room for waits for rules 9's migration, which puts it at the home island's depots, so
    // none is lost (review of #166).
    [Fact]
    public void TheStoneTheStoreHasNoRoomForWaitsAtTheDepot()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = RulesSeven(world.Fingerprint()).Split('\n').ToList();
        int stone = lines.FindIndex(l => l.StartsWith($"stock {PrototypeHome.Home} Stone ", StringComparison.Ordinal));
        lines[stone] = $"stock {PrototypeHome.Home} Stone {149 * Unit}";
        int woodcutter = lines.FindIndex(l => l.StartsWith("building Woodcutter ", StringComparison.Ordinal));
        var fields = lines[woodcutter].Split(' ');
        fields[6] = "1";
        lines[woodcutter] = string.Join(' ', fields);

        string migrated = new RoadsAreTiles().Migrate(string.Join('\n', lines), PrototypeHome.Map);
        Assert.Contains($"stock {PrototypeHome.Home} Stone {151 * Unit}", migrated.Split('\n'));

        var settled = new SettlementsPerIsland().Migrate(migrated, PrototypeHome.Map).Split('\n');
        Assert.Contains($"stock {PrototypeHome.Home} Stone {150 * Unit}", settled);
        Assert.Contains($"at-depot {PrototypeHome.Home} Stone {1 * Unit}", settled);
    }

    // A world of rules 8 that rules 7 could have written, with no roads, one carrier a depot and nothing waiting, comes back as it was.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AWorldWithoutRoadsMigratesToItself(bool archipelago)
    {
        var world = archipelago ? Archipelago.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night) : World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        string state = SettlementsPerIslandTests.RulesEight(world.Fingerprint());

        Assert.Equal(state, new RoadsAreTiles().Migrate(RulesSeven(state), world.Map));
    }

    [Fact]
    public void AStateOfAnotherMapOrNotOfRulesSevenIsRefused()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        string rulesSeven = RulesSeven(world.Fingerprint());

        Assert.Throws<FormatException>(() => new RoadsAreTiles().Migrate(rulesSeven, Archipelago.Map));
        Assert.Throws<FormatException>(() => new RoadsAreTiles().Migrate(SettlementsPerIslandTests.RulesEight(world.Fingerprint()), PrototypeHome.Map));
    }

    /// <summary>
    /// A state of rules 8 (or of rules 9, through <see cref="SettlementsPerIslandTests.RulesEight"/>) as rules 7 wrote it,
    /// for a world with no roads, one carrier a depot and nothing at its buildings: no road on any building, no carriers,
    /// cap, priority or outbox, and no road or held lines.
    /// </summary>
    internal static string RulesSeven(string state) => string.Join(
        "\n",
        (state.Contains("\neconomy ", StringComparison.Ordinal) ? SettlementsPerIslandTests.RulesEight(state) : state).Split('\n')
            .Where(line => !line.StartsWith("road ", StringComparison.Ordinal) && !line.StartsWith("held ", StringComparison.Ordinal))
            .Select(line =>
            {
                if (!line.StartsWith("building ", StringComparison.Ordinal))
                {
                    return line;
                }

                var f = line.Split(' ');
                return string.Join(' ', f.Take(6).Append("0").Concat(f.Skip(6).Take(3)));
            }));

    /// <summary>A state migrated from <paramref name="fromRules"/> as far as <paramref name="toRules"/>, by the chain's own steps.</summary>
    internal static string Migrated(string state, int fromRules, int toRules, WorldMap map)
    {
        for (int rules = fromRules; rules < toRules; rules++)
        {
            state = Migrations.All.Single(m => m.FromRules == rules).Migrate(state, map);
        }

        return state;
    }

    private static string[] State(byte[] bytes) =>
        [.. SaveSections.Text(Encoding.UTF8.GetString(bytes), "latest").Split('\n').SkipWhile(l => !l.StartsWith("map ", StringComparison.Ordinal))];

    private static List<(string Kind, Hex Tile, int Level, bool Staffed, bool Road)> Buildings(string[] state) =>
        [.. state.Where(l => l.StartsWith("building ", StringComparison.Ordinal)).Select(l => l.Split(' '))
            .Select(f => (f[1], new Hex(int.Parse(f[2], CultureInfo.InvariantCulture), int.Parse(f[3], CultureInfo.InvariantCulture)), int.Parse(f[4], CultureInfo.InvariantCulture), f[5] == "1", f[6] == "1"))];

    private static long Stock(string[] state, string island, Resource resource) =>
        long.Parse(state.Single(l => l.StartsWith($"stock {island} {resource} ", StringComparison.Ordinal)).Split(' ')[3], CultureInfo.InvariantCulture);

    private static long Capacity(World world, string island) =>
        Quantity.Units(150 * world.Buildings.Count(b => b.Island == island && (b.Kind == BuildingKind.Storehouse || (b.Kind == BuildingKind.Jetty && island != world.HomeIsland))));

    /// <summary>Steps over the land of a tile's island to the nearest storehouse or jetty, as rules 7 counted them.</summary>
    private static int StepsToDepot(World world, Hex from)
    {
        var map = world.Map;
        string island = map.IslandAt(from) ?? Enumerable.Range(0, Hex.Directions).Select(from.Neighbour).Where(map.Contains).Select(map.IslandAt).First(i => i is not null)!;
        var depots = world.Buildings.Where(b => Logistics.IsDepot(b.Kind)).Select(b => b.Tile).ToHashSet();
        var seen = new HashSet<Hex> { from };
        var frontier = new List<Hex> { from };
        for (int steps = 0; frontier.Count > 0; steps++)
        {
            if (frontier.Any(depots.Contains))
            {
                return steps;
            }

            frontier = [.. frontier.SelectMany(h => Enumerable.Range(0, Hex.Directions).Select(h.Neighbour)).Where(n => map.Contains(n) && map.IslandAt(n) == island && seen.Add(n))];
        }

        return int.MaxValue;
    }
}
