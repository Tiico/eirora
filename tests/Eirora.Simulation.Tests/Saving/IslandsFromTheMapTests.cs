using System.Text;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Rules 5 → 6 (TASK-045): the islands come from the map, so the stores on Home and Nyon become the stores of the map's
/// islands, and a world with Nyön goes on as it did under rules 5 apart from the islands' ids (ADR-0006).
/// </summary>
public sealed class IslandsFromTheMapTests
{
    private const string SeaRoute = "sea-route-3-rules-5";

    private static readonly string s_folder = Repository.PathTo("tests", "Eirora.Simulation.Tests", "Saving", "Golden");

    /// <summary>
    /// The golden save <c>sea-route-3-rules-5</c> is a built-out world with the sea route from Nyön, written by the rules 5
    /// code; its <c>.week</c> file is that code's world a week later, with the stores named by this migration. A later
    /// rules version that changes the week writes the file again in the change that bumps the rules, so the review sees
    /// what changed (ADR-0006).
    /// </summary>
    [Fact]
    public void AWorldWithNyonGivesTheSameWeekAsUnderRulesFiveApartFromTheIslandIds()
    {
        var save = Save.Load(GoldenSaveTests.Bytes(SeaRoute));
        Assert.True(save.ChangedOnLoad);
        Assert.Equal(PrototypeHome.Nyon, Assert.Single(save.World.SeaRoutes).From);
        Assert.True(Enum.GetValues<Resource>().Sum(r => save.World.Stock(PrototypeHome.Nyon, r)) > 0, "Nothing waits on Nyön.");

        var now = save.World.Now;
        for (int day = 1; day <= 7; day++)
        {
            save.AdvanceTo(new TimeReading(now.UtcSeconds + (day * 24 * 3600), now.ZoneId, now.OffsetSeconds));
        }

        string week = Path.Combine(s_folder, SeaRoute + ".week");
        if (!File.Exists(week))
        {
            File.WriteAllText(week, save.World.Fingerprint(), new UTF8Encoding(false));
            Assert.Fail($"Wrote {week}; review it and commit it.");
        }

        Assert.Equal(File.ReadAllText(week, Encoding.UTF8), save.World.Fingerprint());
    }

    // A world built under rules 6 on the prototype's map, written as rules 5 wrote it, migrates back to itself.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void ARulesFiveStateOnThePrototypesMapGetsTheStoresOfHemonAndNyon(int seed)
    {
        var world = BuiltOutWorld.GenerateSave(seed, (BuiltOutWorld.SelfCare)(seed % 3), archipelago: false).World;
        string state = world.Fingerprint();

        Assert.Equal(state, new IslandsFromTheMap().Migrate(AsRulesFive(state, world), PrototypeHome.Map));
    }

    // Under rules 5 every island but the home island shared Nyon's store; it goes to the island of the first building off
    // the home island, which was the island the expedition found, and every other island built on gets an empty store.
    [Fact]
    public void NyonsStoreGoesToTheIslandOfTheFirstBuildingOffTheHomeIsland()
    {
        var map = Archipelago.Map;
        var world = Archipelago.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        var lines = world.Fingerprint().Split('\n').ToList();
        lines.InsertRange(
            lines.FindLastIndex(l => l.StartsWith("building ", StringComparison.Ordinal)) + 1,
            [Jetty(Archipelago.Second), Jetty(Archipelago.First)]);
        string state = string.Join('\n', lines);
        string nyon = string.Concat(Enum.GetValues<Resource>().Select(r => $"stock Nyon {r} {(int)r + 1}\n"));
        string rulesFive = AsRulesFive(state, world).Replace(string.Concat(Enum.GetValues<Resource>().Select(r => $"stock Nyon {r} 0\n")), nyon, StringComparison.Ordinal);

        string migrated = new IslandsFromTheMap().Migrate(rulesFive, map);

        var stores = migrated.Split('\n').Where(l => l.StartsWith("stock ", StringComparison.Ordinal)).Select(l => l.Split(' ')).ToList();
        Assert.Equal(map.Islands.Select(i => i.Id).Where(i => i is Archipelago.Home or Archipelago.First or Archipelago.Second), stores.Select(s => s[1]).Distinct());
        Assert.All(stores.Where(s => s[1] == Archipelago.First), s => Assert.Equal("0", s[3]));
        Assert.Equal(Enum.GetValues<Resource>().Select(r => ((int)r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)), stores.Where(s => s[1] == Archipelago.Second).Select(s => s[3]));
        var reader = new StateReader(migrated);
        var read = World.Read(reader, map);
        reader.End();
        Assert.Equal(new[] { Archipelago.Home, Archipelago.First, Archipelago.Second }, read.Islands);

        string Jetty(string island)
        {
            var landing = map.Features.LandOf(island).First(map.Features.IsHarborSpot);
            return $"building Jetty {landing.Q} {landing.R} 1 0 0 0 0";
        }
    }

    [Fact]
    public void GoodsOnNyonWithNothingBuiltOffTheHomeIslandAreRefused()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        string rulesFive = AsRulesFive(world.Fingerprint(), world);
        Assert.DoesNotContain("stock nyon", new IslandsFromTheMap().Migrate(rulesFive, PrototypeHome.Map), StringComparison.Ordinal);

        Assert.Throws<FormatException>(() => new IslandsFromTheMap().Migrate(rulesFive.Replace("stock Nyon Stone 0", "stock Nyon Stone 1", StringComparison.Ordinal), PrototypeHome.Map));
    }

    [Fact]
    public void AStateOfAnotherMapOrWithoutRulesFivesStoresIsRefused()
    {
        var world = World.Create(TestClock.At("2026-10-14 14:00", 2), TestClock.Night);
        string rulesFive = AsRulesFive(world.Fingerprint(), world);

        Assert.Throws<FormatException>(() => new IslandsFromTheMap().Migrate(rulesFive, Archipelago.Map));
        Assert.Throws<FormatException>(() => new IslandsFromTheMap().Migrate(world.Fingerprint(), PrototypeHome.Map));
        Assert.Throws<FormatException>(() => new IslandsFromTheMap().Migrate(rulesFive.Replace("stock Nyon Stone", "stock Nyon Wool", StringComparison.Ordinal), PrototypeHome.Map));
    }

    /// <summary>
    /// A state of rules 6 as rules 5 wrote it: every resource on Home, then on Nyon, which holds the store of the one
    /// island off the home island built on, if any.
    /// </summary>
    private static string AsRulesFive(string state, World world)
    {
        var off = world.Islands.Where(i => i != world.HomeIsland).ToList();
        Assert.True(off.Count <= 1, "Rules 5 had one store for every island off the home island.");
        var lines = state.Split('\n').ToList();
        int at = lines.FindIndex(l => l.StartsWith("stock ", StringComparison.Ordinal));
        string Store(string name, string? island) =>
            string.Join('\n', Enum.GetValues<Resource>().Select(r => $"stock {name} {r} {(island is null ? 0 : world.Settlement.Stock(island, r))}"));
        lines.RemoveAll(l => l.StartsWith("stock ", StringComparison.Ordinal));
        lines.Insert(at, Store("Home", world.HomeIsland) + "\n" + Store("Nyon", off.Count == 1 ? off[0] : null));
        return string.Join('\n', lines);
    }
}
