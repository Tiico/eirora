using System.Globalization;
using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 7 → 8 (TASK-050, D-106): every villager, building and good is still there and belongs to a settlement. The
/// villagers who staff an island's buildings become its settlement's, the rest stay home, and what no longer fits in an
/// island's store goes home or waits at its depots (economy-v2.md, section 12; ADR-0006).
/// </summary>
public sealed class SettlementsPerIslandTests
{
    private static readonly Resource[] s_resources = Enum.GetValues<Resource>();

    // Acceptance, on a world a player could have saved: the golden save from rules 7, with stores on three islands.
    [Fact]
    public void TheGoldenSaveFromRulesSevenKeepsEveryVillagerBuildingAndGoodInASettlement()
    {
        byte[] bytes = GoldenSaveTests.Bytes("save-3-rules-7");
        var before = SaveSections.Text(Encoding.UTF8.GetString(bytes), "latest").Split('\n').SkipWhile(l => !l.StartsWith("map ", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, before.Select(l => l.Split(' ')).Where(f => f[0] == "stock").Select(f => f[1]).Distinct().Count());

        var world = Save.Load(bytes).World;

        AssertKeptEverything(string.Join('\n', before), world);
        Assert.Contains(world.Settlements, s => s.Island != world.HomeIsland && s.Villagers > 0);
    }

    // The same for worlds built out on the prototype's map and on the archipelago, as rules 7 wrote them.
    [Property(MaxTest = 24)]
    public void EveryVillagerBuildingAndGoodBelongsToASettlementAfterMigrating(int seed)
    {
        var built = BuiltOutWorld.Generate(seed);
        string rulesSeven = RulesSeven(built.Fingerprint());

        var reader = new StateReader(new SettlementsPerIsland().Migrate(rulesSeven, built.Map));
        var world = World.Read(reader, built.Map);
        reader.End();

        AssertKeptEverything(rulesSeven, world);
        Assert.Equal(built.Buildings.Select(b => b.Staffed), world.Buildings.Select(b => b.Staffed));
    }

    // Rules 7 let a jetty off the home island hold 150 of each resource, rules 8 only 50. Without a jetty at home the
    // island has no sea route, so what does not fit waits at its depots until there is room (review of #164); what goes
    // home over a sea route is in AWorldFromRulesSixKeepsNyonsGoodsAtItsJettyAndBuildsTheJettyAnIslandLacks.
    [Fact]
    public void GoodsThatNoLongerFitGoHomeOrWaitAtTheDepotAndMoveInWhenThereIsRoom()
    {
        var map = Archipelago.Map;
        var landing = map.Features.LandOf(Archipelago.Second).First(map.Features.IsHarborSpot);
        var mill = map.Features.LandOf(Archipelago.Second).First(h => h != landing && (map.Features.UsesOf(h) & TileUse.Land) != 0);
        var world = Archipelago.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        world.AdvanceTo(TestClock.At("2026-10-15 09:00", 2));
        string state = world.Fingerprint().Replace(
            "\nstock home Wadmal 0",
            $"\nstock home Wadmal 0\n{string.Concat(s_resources.Select(r => $"stock second {r} {(r == Resource.Timber ? 120 : r == Resource.Planks ? 40 : 0) * Quantity.PerUnit}\n")).TrimEnd('\n')}",
            StringComparison.Ordinal);
        state = Edit(state, "stock home Timber ", $"stock home Timber {140 * Quantity.PerUnit}");
        int last = state.LastIndexOf("\nbuilding ", StringComparison.Ordinal);
        state = state.Insert(state.IndexOf('\n', last + 1), $"\nbuilding Jetty {landing.Q} {landing.R} 1 1 0 0 0 0\nbuilding Sawmill {mill.Q} {mill.R} 1 1 0 0 0 0");
        string rulesSeven = RulesSeven(state);

        string migrated = new SettlementsPerIsland().Migrate(rulesSeven, map);

        // The jetty's and the sawmill's villagers moved with them, and the store on the second island keeps 50 of each.
        // The other 70 timber wait, and the planks fit.
        Assert.Contains($"settlement home {world.Villagers - 3}\nsettlement second 3\n", migrated, StringComparison.Ordinal);
        Assert.Contains($"stock second Timber {50 * Quantity.PerUnit}", migrated, StringComparison.Ordinal);
        Assert.Contains($"stock home Timber {140 * Quantity.PerUnit}", migrated, StringComparison.Ordinal);
        Assert.Contains($"stock second Planks {40 * Quantity.PerUnit}", migrated, StringComparison.Ordinal);
        Assert.Single(migrated.Split('\n'), l => l.StartsWith("at-depot ", StringComparison.Ordinal));
        Assert.Contains($"at-depot second Timber {70 * Quantity.PerUnit}", migrated, StringComparison.Ordinal);
        var reader = new StateReader(migrated);
        var after = World.Read(reader, map);
        reader.End();
        Assert.Equal(migrated.TrimEnd('\n'), after.Fingerprint().TrimEnd('\n'));
        AssertKeptEverything(rulesSeven, after);

        // The sawmill saws the timber in the store, and what waits moves in as room is made, never above capacity.
        after.AdvanceTo(TestClock.At("2026-10-15 13:00", 2));
        long sawn = after.Settlement.Stock(Archipelago.Second, Resource.Planks) - Quantity.Units(40);
        Assert.True(sawn > 0, "The sawmill on the second island sawed nothing.");
        long timber = after.Settlement.Stock(Archipelago.Second, Resource.Timber);
        Assert.Equal(Quantity.Units(50 + 70) - (2 * sawn), timber + after.Settlement.AtDepot(Archipelago.Second, Resource.Timber));
        Assert.InRange(timber, Quantity.Units(49), Quantity.Units(50));
    }

    // Under rules 7 an island's buildings could need more villagers than its settlement has homes: they all move there
    // and stay, and it grows only once there is room (economy-v2.md, section 12; review of #164).
    [Fact]
    public void AnIslandWhoseBuildingsNeedMoreVillagersThanItHasHomesKeepsThemAllAndDoesNotGrow()
    {
        var map = Archipelago.Map;
        var world = Archipelago.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        world.AdvanceTo(TestClock.At("2026-10-15 09:00", 2));
        var landing = map.Features.LandOf(Archipelago.Second).First(map.Features.IsHarborSpot);
        var land = map.Features.LandOf(Archipelago.Second).Where(h => h != landing && (map.Features.UsesOf(h) & TileUse.Land) != 0).ToList();
        string state = world.Fingerprint().Replace(
            "\nstock home Wadmal 0",
            $"\nstock home Wadmal 0\n{string.Concat(s_resources.Select(r => $"stock second {r} {(r == Resource.Fish ? 30 : 0) * Quantity.PerUnit}\n")).TrimEnd('\n')}",
            StringComparison.Ordinal);
        int last = state.LastIndexOf("\nbuilding ", StringComparison.Ordinal);
        state = state.Insert(state.IndexOf('\n', last + 1), $"\nbuilding Jetty {landing.Q} {landing.R} 1 1 0 0 0 0\nbuilding Sawmill {land[0].Q} {land[0].R} 1 1 0 0 0 0\nbuilding Weavery {land[1].Q} {land[1].R} 1 1 0 0 0 0");

        var reader = new StateReader(new SettlementsPerIsland().Migrate(RulesSeven(state), map));
        var migrated = World.Read(reader, map);
        reader.End();

        var second = migrated.SettlementOn(Archipelago.Second)!;
        Assert.Equal((5, IslandSettlement.SettlerHousing, 0), (second.Villagers, second.Housing, second.FreeVillagers));
        var diagnosis = migrated.Diagnose().SettlementOn(Archipelago.Second)!;
        Assert.Equal((Growth.Full, false, true), (diagnosis.Growth, diagnosis.HasRoom, diagnosis.StandsStill));
        var events = migrated.AdvanceTo(TestClock.At("2026-10-16 09:00", 2));
        Assert.DoesNotContain(events.OfType<Events.VillagersMovedIn>(), e => e.Island == Archipelago.Second);
        Assert.Equal(5, second.Villagers);
        Assert.All(migrated.Buildings.Where(b => b.Island == Archipelago.Second), b => Assert.True(b.Staffed, $"{b.Kind} lost its villagers."));
    }

    // A world from rules 6 that built beside a second island without a jetty, before the jetty on the island the goods
    // waited at: Nyon's goods go to the island with the jetty, what no longer fits goes home over its sea route, and the
    // home island's villagers build the second island's jetty (review of #164).
    [Fact]
    public void AWorldFromRulesSixKeepsNyonsGoodsAtItsJettyAndBuildsTheJettyAnIslandLacks()
    {
        var map = Archipelago.Map;
        var world = Archipelago.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        world.Settlement.Fog.LiftIsland(Archipelago.First);
        world.AdvanceTo(TestClock.At("2026-10-15 09:00", 2));
        Hex Landing(string island) => map.Features.LandOf(island).First(h => map.Features.IsHarborSpot(h) && world.BuildingAt(h) is null);
        var wood = map.Features.LandOf(Archipelago.Second).First(h => (map.Features.UsesOf(h) & TileUse.Forest) != 0 && h != Landing(Archipelago.Second));
        var lines = world.Fingerprint().Split('\n').ToList();
        lines.InsertRange(
            lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1,
            [
                $"building Jetty {Landing(Archipelago.Home).Q} {Landing(Archipelago.Home).R} 1 1 0 0 0 0",
                $"building Woodcutter {wood.Q} {wood.R} 1 1 0 0 0 0",
                $"building Jetty {Landing(Archipelago.First).Q} {Landing(Archipelago.First).R} 1 1 0 0 0 0",
            ]);
        string rulesSix = IslandsFromTheMapTests.RulesSix(string.Join('\n', lines), world)
            .Replace("stock Nyon Stone 0\n", $"stock Nyon Stone {120 * Quantity.PerUnit}\n", StringComparison.Ordinal);
        long homeStone = world.Settlement.Stock(Resource.Stone);

        string migrated = new SettlementsPerIsland().Migrate(new IslandsFromTheMap().Migrate(rulesSix, map), map);

        var reader = new StateReader(migrated);
        var after = World.Read(reader, map);
        reader.End();
        Assert.Equal(Quantity.Units(50), after.Settlement.Stock(Archipelago.First, Resource.Stone));
        Assert.Equal(homeStone + Quantity.Units(70), after.Settlement.Stock(Resource.Stone));
        Assert.All(s_resources, r => Assert.Equal(0, after.Settlement.Stock(Archipelago.Second, r) + after.Settlement.AtDepot(Archipelago.Second, r)));
        Assert.Equal(2, after.SettlementOn(Archipelago.Second)!.Villagers);
        Assert.DoesNotContain(after.SeaRoutes, r => r.From == Archipelago.Second);

        Assert.True(after.Apply("workouts", new RecordSelfCare(after.GameDay, Habit.Workouts, ContributionSource.Verified, 3)).Accepted);
        Assert.True(after.Apply("jetty", new Build(BuildingKind.Jetty, Landing(Archipelago.Second))).Accepted);
        long work = after.Projects.Single().WorkLeft;
        after.AdvanceTo(TestClock.At("2026-10-15 11:00", 2));
        Assert.True(after.Projects.Count == 0 || after.Projects[0].WorkLeft < work, "No one builds the second island's jetty.");
    }

    [Fact]
    public void AStateThatIsNotOfRulesSevenIsRefused()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        string rulesSeven = RulesSeven(world.Fingerprint());

        Assert.Throws<FormatException>(() => new SettlementsPerIsland().Migrate(world.Fingerprint(), PrototypeHome.Map));
        Assert.Throws<FormatException>(() => new SettlementsPerIsland().Migrate(rulesSeven, Archipelago.Map));
        Assert.Throws<FormatException>(() => new SettlementsPerIsland().Migrate(rulesSeven.Replace("settlement 12 ", "settlement 6 ", StringComparison.Ordinal), PrototypeHome.Map));
        Assert.Throws<FormatException>(() => new SettlementsPerIsland().Migrate(Without(rulesSeven, "stock home Wadmal "), PrototypeHome.Map));
    }

    /// <summary>
    /// A state of rules 8 as rules 7 wrote it: all the villagers on the economy's line, and no goods waiting at a depot
    /// or owed by a sea route, which rules 7 did not have.
    /// </summary>
    internal static string RulesSeven(string state)
    {
        var lines = state.Split('\n').ToList();
        Assert.DoesNotContain(lines, l => l.StartsWith("at-depot ", StringComparison.Ordinal));
        lines.RemoveAll(l => l.StartsWith("owed ", StringComparison.Ordinal));
        long villagers = lines.Where(l => l.StartsWith("settlement ", StringComparison.Ordinal)).Sum(l => long.Parse(l.Split(' ')[2], CultureInfo.InvariantCulture));
        int at = lines.FindIndex(l => l.StartsWith("economy ", StringComparison.Ordinal));
        lines[at] = $"settlement {villagers.ToString(CultureInfo.InvariantCulture)} {lines[at]["economy ".Length..]}";
        lines.RemoveAll(l => l.StartsWith("settlement ", StringComparison.Ordinal) && l.Split(' ').Length == 3);
        return string.Join('\n', lines);
    }

    /// <summary>
    /// The world from a state of rules 7 has every villager, building and good of it, each in a settlement: the villagers
    /// together, the buildings line for line, each good in the stores and at the depots together, and no store above what
    /// its depots hold (INV-R1, INV-R2, INV-V1). An island's settlement has the villagers its buildings need.
    /// </summary>
    private static void AssertKeptEverything(string rulesSeven, World world)
    {
        var lines = rulesSeven.Split('\n').Select(l => l.Split(' ')).ToList();
        Assert.Equal(long.Parse(lines.Single(f => f[0] == "settlement")[1], CultureInfo.InvariantCulture), world.Villagers);
        Assert.Equal(
            lines.Where(f => f[0] == "building").Select(f => string.Join(' ', f)),
            world.Fingerprint().Split('\n').Where(l => l.StartsWith("building ", StringComparison.Ordinal)));
        Assert.All(world.Buildings, b => Assert.NotNull(world.SettlementOn(b.Island)));
        Assert.All(s_resources, r => Assert.Equal(
            lines.Where(f => f[0] == "stock" && f[2] == r.ToString()).Sum(f => long.Parse(f[3], CultureInfo.InvariantCulture)),
            world.Islands.Sum(i => world.Settlement.Stock(i, r) + world.Settlement.AtDepot(i, r))));
        Assert.All(world.Settlements, s =>
        {
            long capacity = Quantity.Units(world.Buildings.Where(b => b.Island == s.Island).Sum(b => b.Kind switch
            {
                BuildingKind.Storehouse => 150,
                BuildingKind.Jetty => 50,
                _ => 0,
            }));
            Assert.All(s_resources, r => Assert.InRange(world.Settlement.Stock(s.Island, r), 0, capacity));
            Assert.InRange(s.FreeVillagers, 0, s.Island == world.HomeIsland ? s.Villagers : 0);
        });
    }

    private static string Edit(string state, string prefix, string line)
    {
        var lines = state.Split('\n').ToList();
        lines[lines.FindIndex(l => l.StartsWith(prefix, StringComparison.Ordinal))] = line;
        return string.Join('\n', lines);
    }

    private static string Without(string state, string prefix) =>
        string.Join('\n', state.Split('\n').Where(l => !l.StartsWith(prefix, StringComparison.Ordinal)));
}
