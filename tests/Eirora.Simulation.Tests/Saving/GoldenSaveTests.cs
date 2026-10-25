using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using static Eirora.Simulation.Tests.Tiles;

namespace Eirora.Simulation.Tests.Saving;

/// <summary>
/// Golden saves (ADR-0006): one per rules version and save format, made by a headless scenario with synthetic
/// self-care and checked in with the fingerprint that loading it with current code must give. An expected fingerprint
/// changes only in the change that bumps the rules or alters a migration, so the review sees it.
/// </summary>
public sealed class GoldenSaveTests
{
    private const string Prototype = "prototype-journal-rules-1";

    private static readonly string s_folder = Repository.PathTo("tests", "Eirora.Simulation.Tests", "Saving", "Golden");

    public static TheoryData<string> Saves =>
        Directory.Exists(s_folder)
            ? [.. Directory.GetFiles(s_folder, "*.save").Select(f => Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal)]
            : [];

    // The current version's golden save is written when it is missing, to be reviewed and committed; older ones cannot
    // be made any more, so a missing one fails.
    [Fact]
    public void EveryRulesVersionAndFormatHasAGoldenSave()
    {
        string current = Name(Save.FormatVersion, RulesVersion.Current);
        if (!File.Exists(Path.Combine(s_folder, current + ".save")))
        {
            Write(current);
            Assert.Fail($"Wrote the golden save {current}; review it and commit it.");
        }

        var names = Directory.GetFiles(s_folder, "*.save").Select(Path.GetFileNameWithoutExtension).ToList();
        for (int rules = 1; rules <= RulesVersion.Current; rules++)
        {
            Assert.Contains(names, n => n!.EndsWith($"-rules-{rules}", StringComparison.Ordinal));
        }

        for (int format = 1; format <= Save.FormatVersion; format++)
        {
            Assert.Contains(names, n => n!.StartsWith($"save-{format}-rules-", StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(Saves))]
    public void AGoldenSaveLoadsToItsExpectedWorldAndPlaysOn(string name)
    {
        var save = Save.Load(File.ReadAllBytes(Path.Combine(s_folder, name + ".save")));

        Assert.Equal(Expected(name), save.World.Fingerprint());
        PlayOn(save);
    }

    // Rules 4 made every land tile a building site, so the prototype's journal from M1 would replay to another world
    // than the one its player built; it is refused instead of shown (ADR-0006), and the client says so (TASK-014).
    [Fact]
    public void TheGoldenPrototypeJournalIsRefusedOnceItsRulesNoLongerReplay()
    {
        string path = Path.Combine(s_folder, Prototype + ".journal");
        Assert.True(File.Exists(path), $"The golden prototype journal {Prototype} is missing.");
        Assert.True(RulesVersion.ReplaysJournalsFrom > 1);

        var refused = Assert.Throws<JournalFromOlderRulesException>(() => Save.FromPrototypeJournal(File.ReadAllText(path, Encoding.UTF8)));

        Assert.Contains("rules version 1", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>Continues a loaded world for a few days under the current rules and writes and loads it again.</summary>
    private static void PlayOn(Save save)
    {
        var now = save.World.Now;
        for (int day = 1; day <= 3; day++)
        {
            save.AdvanceTo(new TimeReading(now.UtcSeconds + (day * 24 * 3600), now.ZoneId, now.OffsetSeconds));
            save.Apply($"golden-steps-{day}", new RecordSelfCare(save.World.GameDay, Habit.Steps, ContributionSource.Verified, 8_000));
            save.Apply($"golden-sleep-{day}", new RecordSelfCare(save.World.GameDay, Habit.Sleep, ContributionSource.SelfReported, 420));
            save.Apply($"golden-cottage-{day}", new Build(BuildingKind.Cottage, L1));
        }

        var again = Save.Load(save.ToBytes());
        Assert.Equal(save.World.Fingerprint(), again.World.Fingerprint());
        Assert.Equal(save.World.Fingerprint(), again.Replay().Fingerprint());
    }

    /// <summary>
    /// Makes the golden save of the current version from a fixed scenario: from rules 7 on, a world built out on the
    /// archipelago with a store on each of three islands, two sea routes home with goods waiting, and paused buildings
    /// (TASK-044, TASK-045); from rules 8 on also with roads, goods waiting for the carriers, raw materials at the
    /// processing, a depot with more than one carrier (until rules 12) and one with a lower cap, and a building carried first (TASK-051);
    /// from rules 9 on with villagers in each island's settlement and materials owed on a sea route (TASK-050); from
    /// rules 10 on also with a storehouse demolished (TASK-047); from rules 11 on also with a minimum stock of the
    /// player's, reservations and an import order (TASK-046); from rules 12 on also with step 1 unlocked, farmers, and a
    /// farmstead, a hunting lodge and a tannery with game in store (TASK-061).
    /// </summary>
    private static void Write(string name)
    {
        var save = BuiltOutWorld.GenerateSave(55);
        var world = save.World;
        WithFarmers(save);

        // A cottage on the second island, which its store cannot pay, so the ships owe the home island's materials.
        while (world.Phase == Phase.Resting)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 3600, world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        var site = world.Map.Features.LandOf(Archipelago.Second).First(h => world.CheckBuild(BuildingKind.Cottage, h) == Rejection.None);
        Assert.True(save.Apply("golden-owed", new Build(BuildingKind.Cottage, site)).Accepted);

        // A storehouse demolished, so that the corpus holds a depot's demolition and the buildings it served (TASK-047).
        var depot = world.Buildings.First(b => b.Kind == BuildingKind.Storehouse && b.Carriers <= 1 && b.CarrierCap == Logistics.MostCarriers
            && world.Demolition(b.Tile)!.Rejection == Rejection.None);
        Assert.True(save.Apply("golden-demolish", new Demolish(depot.Tile)).Accepted);
        WithStockRules(save);
        Assert.Equal(2, world.SeaRoutes.Count);
        Assert.Contains(world.Buildings, b => b.Paused);
        Assert.NotEmpty(world.Roads);
        Assert.Contains(world.Buildings, b => b.Waiting > 0);
        Assert.Contains(world.Buildings, b => b.Held?.Any(h => h > 0) == true);
        // From rules 12 the villagers left free at home are the farmers who rose, and depots hire only free crofters
        // (economy-v2.md, section 2), so the depot with more than one carrier is in the golden saves of rules 8–11.
        Assert.Contains(world.Buildings, b => Logistics.IsDepot(b.Kind) && b.CarrierCap < Logistics.MostCarriers);
        Assert.Contains(world.Buildings, b => b.CarriedFirst);
        Assert.All(world.Settlements, s => Assert.True(s.Villagers > 0, $"No villagers on {s.Island}."));
        Assert.Contains(world.Islands, i => world.Settlement.Owed(i) > 0);
        Assert.Contains(world.Buildings, b => b.Held is not null && b.Reserve != StockRules.StandardReserve);
        Assert.Contains(world.Islands, i => Enum.GetValues<Resource>().Any(r => world.ReservationOf(i, r) > 0));
        Assert.Contains(world.Islands, i => !world.ImportOrder(i).SequenceEqual(Enum.GetValues<Resource>()));
        Assert.Equal(1, world.Step);
        Assert.Contains(world.Settlements, s => s.VillagersOf(PopulationTier.Farmers) > 0);
        Assert.All([BuildingKind.Farmstead, BuildingKind.HuntingLodge, BuildingKind.Tannery], kind => Assert.Contains(world.Buildings, b => b.Kind == kind));
        Assert.Contains(world.Islands, i => world.Stock(i, Resource.Game) > 0);

        Directory.CreateDirectory(s_folder);
        File.WriteAllBytes(Path.Combine(s_folder, name + ".save"), save.ToBytes());
        File.WriteAllText(Path.Combine(s_folder, name + ".expected"), Save.Load(save.ToBytes()).World.Fingerprint(), new UTF8Encoding(false));
    }

    /// <summary>
    /// The farmers of step 1 (TASK-061): a farmstead, a hunting lodge and a tannery at home, played until crofters have
    /// risen to farmers and the store holds game and hide, so that the golden save keeps the tiers and the new goods.
    /// </summary>
    private static void WithFarmers(Save save)
    {
        var world = save.World;
        int key = 0;
        foreach (var kind in new[] { BuildingKind.Farmstead, BuildingKind.HuntingLodge, BuildingKind.Tannery })
        {
            // While the villagers are awake and the home island's store can pay for it.
            while (world.Phase != Phase.Active || !world.CanAfford(world.Content.For(kind).Cost, Hex.Origin))
            {
                save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 900, world.Now.ZoneId, world.Now.OffsetSeconds));
            }

            var site = Scenario.SitesOf(world).First(h => world.IslandOf(h) == world.HomeIsland && world.CheckBuild(kind, h) == Rejection.None);
            var built = save.Apply($"golden-farmers-{key++}", new Build(kind, site));
            Assert.True(built.Accepted, $"{kind} was refused: {built.Rejection}.");
        }

        // Once the lodge and the tannery are built, there is game for hide and hide for clothes.
        while (world.Projects.Any(p => p.Building is BuildingKind.HuntingLodge or BuildingKind.Tannery))
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 3600, world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        while (world.Phase != Phase.Active)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 900, world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        // The home island's sawmills and quarries pause, so that their villagers hunt and tan, and the food keeps coming.
        foreach (var paused in world.Buildings.Where(b => b.Island == world.HomeIsland && b.Kind is BuildingKind.Sawmill or BuildingKind.Quarry && !b.Paused).ToList())
        {
            Assert.True(save.Apply($"golden-farmers-{key++}", new PauseBuilding(paused.Tile)).Accepted, $"The {paused.Kind} was not paused.");
        }

        // The tannery leaves only 5 of the game the lodge brings home, so that it is not all eaten first.
        var tannery = world.Buildings.Single(b => b.Kind == BuildingKind.Tannery).Tile;
        Assert.True(save.Apply($"golden-farmers-{key++}", new SetReserve(tannery, 5)).Accepted, "The tannery's minimum stock was not lowered.");

        for (int hour = 0; hour < 21 * 24 && !(world.Settlements.Any(s => s.VillagersOf(PopulationTier.Farmers) > 0) && world.Stock(Resource.Hide) > 0 && world.Stock(Resource.Game) > 0); hour++)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 3600, world.Now.ZoneId, world.Now.OffsetSeconds));
        }
    }

    /// <summary>
    /// The player's stock rules, set while the villagers are awake (TASK-046): the first processing building's minimum
    /// stock lowered, stone reserved at home and wool on the island of the first sea route, and stone shipped first from
    /// it, so that the golden save keeps all three in its state and its journal.
    /// </summary>
    private static void WithStockRules(Save save)
    {
        while (save.World.Phase != Phase.Active)
        {
            var now = save.World.Now;
            save.AdvanceTo(new TimeReading(now.UtcSeconds + 900, now.ZoneId, now.OffsetSeconds));
        }

        var world = save.World;
        string away = world.SeaRoutes[0].From;
        var processing = world.Buildings.First(b => b.Held is not null);
        foreach (var (key, command) in new (string, GameCommand)[]
        {
            ("golden-reserve", new SetReserve(processing.Tile, 15)),
            ("golden-stone-home", new SetReservation(world.HomeIsland, Resource.Stone, 30)),
            ("golden-wool", new SetReservation(away, Resource.Wool, 10)),
            ("golden-stone", new MoveImport(away, Resource.Stone, 0)),
        })
        {
            Assert.True(save.Apply(key, command).Accepted, $"{key} was refused.");
        }
    }

    /// <summary>The bytes of a checked-in golden save.</summary>
    internal static byte[] Bytes(string name) => File.ReadAllBytes(Path.Combine(s_folder, name + ".save"));

    private static string Expected(string name) => File.ReadAllText(Path.Combine(s_folder, name + ".expected"), Encoding.UTF8);

    private static string Name(int format, int rules) => $"save-{format}-rules-{rules}";
}

