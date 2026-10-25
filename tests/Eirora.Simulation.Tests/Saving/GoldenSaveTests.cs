using System.Text;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
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

    // A world on the archipelago (BuiltOutWorld) whose scenario reaches every state the golden save keeps; seed 55 no
    // longer leaves a building paused or a depot with more than one carrier since TASK-064 moved the start woodcutter.
    private const int GoldenSeed = 35;

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
    /// processing, a depot with more than one carrier and one with a lower cap, and a building carried first (TASK-051);
    /// from rules 9 on with villagers in each island's settlement and materials owed on a sea route (TASK-050); from
    /// rules 10 on also with a storehouse demolished (TASK-047); from rules 11 on also with a minimum stock of the
    /// player's, reservations and an import order (TASK-046); from rules 12 on also with step 1 unlocked, farmers, and a
    /// farmstead, a hunting lodge and a tannery with game in store (TASK-061); from rules 13 on also with a weavery built
    /// by hand (TASK-065); from rules 14 on also with step 2 unlocked, craftsmen, and a craftsmen's house, an apiary, a
    /// brewery and a meadery with honey, mead and ale in store (TASK-062); from rules 15 on also with step 3 unlocked, a
    /// tar kiln and a shipyard, knarrs and one being built (TASK-063).
    /// </summary>
    private static void Write(string name)
    {
        var save = Golden();
        Directory.CreateDirectory(s_folder);
        File.WriteAllBytes(Path.Combine(s_folder, name + ".save"), save.ToBytes());
        File.WriteAllText(Path.Combine(s_folder, name + ".expected"), Save.Load(save.ToBytes()).World.Fingerprint(), new UTF8Encoding(false));
    }

    private static Save Golden()
    {
        // Built out of steps 0 and 1 only, as the golden save of rules 13 was, so that each step's buildings come in order.
        var save = BuiltOutWorld.GenerateSave(GoldenSeed, (BuiltOutWorld.SelfCare)(GoldenSeed % 3), archipelago: true, lastStep: 1);
        var world = save.World;
        WithFarmers(save);
        WithCraftsmen(save);

        // A storehouse demolished, so that the corpus holds a depot's demolition and the buildings it served (TASK-047),
        // before the village grows so large that every depot carries with more than one.
        var depot = world.Buildings.First(b => b.Kind == BuildingKind.Storehouse && b.Carriers <= 1 && b.CarrierCap == Logistics.MostCarriers
            && world.Demolition(b.Tile)!.Rejection == Rejection.None);
        Assert.True(save.Apply("golden-demolish", new Demolish(depot.Tile)).Accepted);
        WithSeafaring(save);

        // A cottage on the second island, which its store cannot pay, so the ships owe the home island's materials.
        while (world.Phase == Phase.Resting)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 3600, world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        var site = world.Map.Features.LandOf(Archipelago.Second).First(h => world.CheckBuild(BuildingKind.Cottage, h) == Rejection.None);
        Assert.True(save.Apply("golden-owed", new Build(BuildingKind.Cottage, site)).Accepted);

        WithStockRules(save);
        var weavery = Scenario.SitesOf(world).First(h => world.CheckBuild(BuildingKind.Weavery, h) == Rejection.None && world.CanAfford(world.Content.For(BuildingKind.Weavery).Cost, h));
        Assert.True(save.Apply("golden-by-hand", new BuildByHand(BuildingKind.Weavery, weavery)).Accepted);
        Assert.Equal(2, world.SeaRoutes.Count);
        Assert.Contains(world.Buildings, b => b.Paused);
        Assert.NotEmpty(world.Roads);
        Assert.Contains(world.Buildings, b => b.Waiting > 0);
        Assert.Contains(world.Buildings, b => b.Held?.Any(h => h > 0) == true);
        Assert.Contains(world.Buildings, b => b.Carriers > 1);
        Assert.Contains(world.Buildings, b => Logistics.IsDepot(b.Kind) && b.CarrierCap < Logistics.MostCarriers);
        Assert.Contains(world.Buildings, b => b.CarriedFirst);
        Assert.All(world.Settlements, s => Assert.True(s.Villagers > 0, $"No villagers on {s.Island}."));
        Assert.Contains(world.Islands, i => world.Settlement.Owed(i) > 0);
        Assert.Contains(world.Buildings, b => b.Held is not null && b.Reserve != StockRules.StandardReserve);
        Assert.Contains(world.Islands, i => Enum.GetValues<Resource>().Any(r => world.ReservationOf(i, r) > 0));
        Assert.Contains(world.Islands, i => !world.ImportOrder(i).SequenceEqual(Enum.GetValues<Resource>()));
        Assert.Contains(world.Settlements, s => s.VillagersOf(PopulationTier.Farmers) > 0);
        Assert.All([BuildingKind.Farmstead, BuildingKind.HuntingLodge, BuildingKind.Tannery], kind => Assert.Contains(world.Buildings, b => b.Kind == kind));
        Assert.Contains(world.Islands, i => world.Stock(i, Resource.Game) > 0);
        Assert.Contains(world.Projects, p => p.ByHand);
        Assert.Contains(world.Settlements, s => s.VillagersOf(PopulationTier.Craftsmen) > 0);
        Assert.All([BuildingKind.CraftsmensHouse, BuildingKind.Apiary, BuildingKind.Brewery, BuildingKind.Meadery], kind => Assert.Contains(world.Buildings, b => b.Kind == kind));
        Assert.All([Resource.Honey, Resource.Mead, Resource.Ale], good => Assert.Contains(world.Islands, i => world.Stock(i, good) > 0));
        Assert.Equal(3, world.Step);
        Assert.All([BuildingKind.TarKiln, BuildingKind.Shipyard], kind => Assert.Contains(world.Buildings, b => b.Kind == kind));
        Assert.Contains(world.Islands, i => world.Knarrs(i) > 0);
        Assert.Contains(world.Buildings, b => b.KnarrHoursLeft > 0);
        return save;
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
        var pausing = world.Buildings.Where(b => b.Island == world.HomeIsland && b.Kind is BuildingKind.Sawmill or BuildingKind.Quarry && !b.Paused).ToList();
        foreach (var paused in pausing)
        {
            Assert.True(save.Apply($"golden-farmers-{key++}", new PauseBuilding(paused.Tile)).Accepted, $"The {paused.Kind} was not paused.");
        }

        for (int hour = 0; hour < 21 * 24 && !(world.Settlements.Any(s => s.VillagersOf(PopulationTier.Farmers) > 0) && world.Stock(Resource.Hide) > 0 && world.Stock(Resource.Game) > 0); hour++)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 3600, world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        // Then they work again, and at the next dawn the depots hire carriers of every tier for their goods (D-109).
        while (world.Phase != Phase.Active)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 900, world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        foreach (var paused in pausing)
        {
            Assert.True(save.Apply($"golden-farmers-{key++}", new ResumeBuilding(paused.Tile)).Accepted, $"The {paused.Kind} was not resumed.");
        }

        save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + (24 * 3600), world.Now.ZoneId, world.Now.OffsetSeconds));
    }

    /// <summary>
    /// The craftsmen of step 2 (TASK-062): once a settlement has 30 villagers, of them 6 farmers, a craftsmen's house, an
    /// apiary on a meadow, a brewery and a meadery at home, played until farmers have risen to craftsmen and the store
    /// holds honey, mead and ale, so that the golden save keeps the third tier and the new goods.
    /// </summary>
    private static void WithCraftsmen(Save save)
    {
        var world = save.World;
        int key = 0;

        // A cottage at home makes room for the crofters, so that the village soon has 30 villagers.
        Build(save, BuildingKind.Cottage, $"golden-craftsmen-{key++}");
        for (int day = 0; day < 42 && world.Step < 2; day++)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + (24 * 3600), world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        Assert.Equal(2, world.Step);
        foreach (var kind in new[] { BuildingKind.Apiary, BuildingKind.Meadery, BuildingKind.Brewery, BuildingKind.CraftsmensHouse })
        {
            Build(save, kind, $"golden-craftsmen-{key++}");
        }

        // The home island's buildings other than the fishing huts and the chains of hide and drink pause while the new ones
        // are built and drink is made, so that their villagers build and carry, and they work again once craftsmen have risen.
        BuildingKind[] working = [BuildingKind.FishingHut, BuildingKind.HuntingLodge, BuildingKind.Tannery, BuildingKind.Apiary, BuildingKind.Meadery, BuildingKind.Brewery];
        var pausing = world.Buildings.Where(b => b.Island == world.HomeIsland && !b.Paused && world.Content.For(b.Kind).Produces && !working.Contains(b.Kind)).ToList();
        foreach (var paused in pausing)
        {
            Assert.True(save.Apply($"golden-craftsmen-{key++}", new PauseBuilding(paused.Tile)).Accepted, $"The {paused.Kind} was not paused.");
        }

        // The home island's depots may take as many carriers as the goods need.
        foreach (var depot in world.Buildings.Where(b => b.Island == world.HomeIsland && Logistics.IsDepot(b.Kind) && b.CarrierCap < Logistics.MostCarriers).ToList())
        {
            Assert.True(save.Apply($"golden-craftsmen-{key++}", new SetCarrierCap(depot.Tile, Logistics.MostCarriers)).Accepted, "The cap was not raised.");
        }

        for (int hour = 0; hour < 28 * 24 && !(world.Settlements.Any(s => s.VillagersOf(PopulationTier.Craftsmen) > 0)
            && world.Stock(Resource.Honey) > 0 && world.Stock(Resource.Mead) > 0 && world.Stock(Resource.Ale) > 0); hour++)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 3600, world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        // It ends a few hours into a day, while goods wait at the buildings for the carriers.
        while (world.Phase != Phase.Active)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 900, world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        // All but the first work again, so that the golden save keeps a paused building (TASK-044), and a depot away from
        // home gets a cap of the player's below the most, as the home island's had (TASK-051).
        foreach (var paused in pausing.Skip(1))
        {
            Assert.True(save.Apply($"golden-craftsmen-{key++}", new ResumeBuilding(paused.Tile)).Accepted, $"The {paused.Kind} was not resumed.");
        }

        var away = world.Buildings.First(b => b.Island != world.HomeIsland && Logistics.IsDepot(b.Kind));
        Assert.True(save.Apply($"golden-craftsmen-{key++}", new SetCarrierCap(away.Tile, 2)).Accepted, "The cap was not lowered.");

        save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + (3 * 3600), world.Now.ZoneId, world.Now.OffsetSeconds));
    }

    /// <summary>
    /// Seafaring, step 3 (TASK-063): a cottage and a farmstead at home make room until the village has 45 villagers, of them
    /// 6 craftsmen, and the sea routes are there already; then a tar kiln and a shipyard at home, played until a knarr is
    /// launched and the next is being built, so that the golden save keeps the knarrs and the shipyard's work.
    /// </summary>
    private static void WithSeafaring(Save save)
    {
        var world = save.World;
        int key = 0;
        Build(save, BuildingKind.Cottage, $"golden-seafaring-{key++}");
        Build(save, BuildingKind.Farmstead, $"golden-seafaring-{key++}");
        for (int day = 0; day < 56 && world.Step < 3; day++)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + (24 * 3600), world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        Assert.Equal(3, world.Step);
        Build(save, BuildingKind.TarKiln, $"golden-seafaring-{key++}");

        // The shipyard takes Build Power, which workouts give (D-117).
        for (int day = 0; day < 14 && world.Available(Force.BuildPower) < world.Content.For(BuildingKind.Shipyard).BuildPower; day++)
        {
            Assert.True(save.Apply($"golden-seafaring-{key++}", new RecordSelfCare(world.GameDay, Habit.Workouts, ContributionSource.Verified, 3)).Accepted);
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + (24 * 3600), world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        Build(save, BuildingKind.Shipyard, $"golden-seafaring-{key++}");
        for (int hour = 0; hour < 28 * 24 && !(world.Islands.Any(i => world.Knarrs(i) > 0) && world.Buildings.Any(b => b.KnarrHoursLeft > 0)); hour++)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 3600, world.Now.ZoneId, world.Now.OffsetSeconds));
        }
    }

    /// <summary>Builds on the home island once the villagers are awake and its store can pay.</summary>
    private static void Build(Save save, BuildingKind kind, string key)
    {
        var world = save.World;
        for (int quarter = 0; quarter < 7 * 96 && (world.Phase != Phase.Active || !world.CanAfford(world.Content.For(kind).Cost, Hex.Origin)); quarter++)
        {
            save.AdvanceTo(new TimeReading(world.Now.UtcSeconds + 900, world.Now.ZoneId, world.Now.OffsetSeconds));
        }

        var site = Scenario.SitesOf(world).First(h => world.IslandOf(h) == world.HomeIsland && world.CheckBuild(kind, h) == Rejection.None);
        var built = save.Apply(key, new Build(kind, site));
        Assert.True(built.Accepted, $"{kind} was refused: {built.Rejection}.");
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

