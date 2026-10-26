using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;

namespace Eirora.Balance;

/// <summary>
/// What every thoughtful strategy does from week 2, once its own plan has had its week (economy-v2.md, section 10;
/// economy_v2.py, keep_growing): housing when it runs out, food, timber and stone when they do not suffice, clothes and
/// farmsteads after step 1, drink and craftsmen's houses after step 2, a tar kiln and a shipyard after step 3, a jetty,
/// the expedition and a settlement on the island it finds, and the forces left over on upgrades and workdays. It always
/// leaves two free villagers to build, and upgrades only with materials to spare, so that more self-care never takes
/// materials from growth (D-34).
/// </summary>
/// <remarks>
/// Without <see cref="Power"/> it spends no Build Power, as a player who has deselected training (D-43); the jetty and
/// the weavery, which can be built by hand (D-65), it builds by hand when <see cref="ByHand"/> is set.
/// </remarks>
public sealed class KeepGrowing : DailyMove
{
    /// <summary>The first game day of week 2, when every strategy has had its own plan for a week.</summary>
    public const int FromDay = 8;

    private const int Longhouse = 120;
    private const int Comfort = 2;
    private static readonly Dictionary<Resource, int> s_surplus = new() { [Resource.Planks] = 40, [Resource.Stone] = 40 };
    private static readonly Resource[] s_food = [Resource.Fish, Resource.Game, Resource.Grain];
    private static readonly Resource[] s_clothes = [Resource.Wadmal, Resource.Hide];
    private static readonly Resource[] s_drink = [Resource.Mead, Resource.Ale];

    public KeepGrowing(bool power = true, bool byHand = false)
    {
        Power = power;
        ByHand = byHand;
    }

    public bool Power { get; }

    public bool ByHand { get; }

    public override string Name => Power ? "fortsätt växa" : ByHand ? "fortsätt växa utan Styrka, med handkraft" : "fortsätt växa utan Styrka";

    internal override void Make(Turn turn)
    {
        if (turn.Day < FromDay)
        {
            return;
        }

        var world = turn.World;
        string home = turn.Sites.Home;
        var settlement = world.SettlementOn(home)!;
        int farmers = settlement.VillagersOf(PopulationTier.Farmers);
        int craftsmen = settlement.VillagersOf(PopulationTier.Craftsmen);

        if (settlement.CroftersRoom < 3 && !Pending(world, BuildingKind.Cottage, BuildingKind.Longhouse))
        {
            bool longhouse = Power && world.Available(Force.BuildPower) >= Longhouse && world.Stock(home, Resource.Planks) >= 15 + s_surplus[Resource.Planks];
            Build(turn, longhouse ? BuildingKind.Longhouse : BuildingKind.Cottage, home);
        }

        if (Supply(turn, home, s_food) < Units(settlement.Villagers + 6) && !Pending(world, BuildingKind.FishingHut, BuildingKind.HuntingLodge, BuildingKind.Field))
        {
            _ = Build(turn, BuildingKind.Field, home) || Build(turn, BuildingKind.FishingHut, home) || Build(turn, BuildingKind.HuntingLodge, home);
        }

        // Building materials: timber for everything, and planks and stone for what comes after.
        int sawmills = world.Buildings.Count(b => b.Kind == BuildingKind.Sawmill && b.Island == home);
        if (world.Stock(home, Resource.Timber) < 60 && Supply(turn, home, [Resource.Timber]) < Units(20 + (32 * sawmills)) && !Pending(world, BuildingKind.Woodcutter))
        {
            Build(turn, BuildingKind.Woodcutter, home);
        }

        if (Power && sawmills == 0 && !Pending(world, BuildingKind.Sawmill))
        {
            Build(turn, BuildingKind.Sawmill, home);
        }

        if (world.Stock(home, Resource.Stone) < 40 && Supply(turn, home, [Resource.Stone]) < Units(12) && !Pending(world, BuildingKind.Quarry))
        {
            Build(turn, BuildingKind.Quarry, home);
        }

        if (world.Step >= 1)
        {
            Clothe(turn, home, settlement, farmers, craftsmen);
        }

        if (world.Step >= 2)
        {
            GiveDrink(turn, home, settlement, craftsmen);
        }

        if (world.Step >= 3 && Power)
        {
            foreach (var kind in new[] { BuildingKind.TarKiln, BuildingKind.Shipyard })
            {
                if (!Has(world, kind) && !Pending(world, kind))
                {
                    Build(turn, kind, home);
                }
            }
        }

        Seafare(turn, home);
        Spend(turn, home);
    }

    /// <remarks>
    /// The clothes are counted from the home island's own wool and game, and a sheep farm on another island does not
    /// stand in for one at home: before the knarrs, the sea route's 40 a day go first to the materials and food that
    /// island needs, so its wool may never reach the weavery. With more Exploration the expedition goes earlier and that
    /// island grows larger, and the farmers at home would wait for clothes all month (TASK-097).
    /// </remarks>
    private void Clothe(Turn turn, string home, IslandSettlement settlement, int farmers, int craftsmen)
    {
        var world = turn.World;
        long clothes = Units(((farmers + craftsmen + 1) / 2) + Comfort + 2);
        if (Supply(turn, home, s_clothes) < clothes
            && !Pending(world, BuildingKind.Weavery, BuildingKind.Tannery, BuildingKind.SheepFarm, BuildingKind.HuntingLodge))
        {
            if ((Power || ByHand) && Supply(turn, home, [Resource.Wool]) >= Units(8) && !Has(world, BuildingKind.Weavery))
            {
                Build(turn, BuildingKind.Weavery, home);
            }
            else if (Power && !Has(world, BuildingKind.SheepFarm, home) && turn.Sites.Free(BuildingKind.SheepFarm, home).Count > 0)
            {
                Build(turn, BuildingKind.SheepFarm, home);
            }
            else if (Supply(turn, home, [Resource.Game]) < (2 * Supply(turn, home, [Resource.Hide])) + Units(10))
            {
                Build(turn, BuildingKind.HuntingLodge, home);
            }
            else
            {
                Build(turn, BuildingKind.Tannery, home);
            }
        }

        if (settlement.HousingOf(PopulationTier.Farmers) - farmers < 2 && Supply(turn, home, s_clothes) >= clothes - Units(2)
            && !Pending(world, BuildingKind.Farmstead))
        {
            Build(turn, BuildingKind.Farmstead, home);
        }
    }

    private void GiveDrink(Turn turn, string home, IslandSettlement settlement, int craftsmen)
    {
        var world = turn.World;
        long drink = Units(((craftsmen + 1) / 2) + 2);
        if (Supply(turn, home, s_drink) < drink && !Pending(world, BuildingKind.Brewery, BuildingKind.Apiary, BuildingKind.Meadery, BuildingKind.Field))
        {
            if (Supply(turn, home, [Resource.Honey]) >= Units(8) && !Has(world, BuildingKind.Meadery))
            {
                Build(turn, BuildingKind.Meadery, home);
            }
            else if (!Has(world, BuildingKind.Apiary))
            {
                Build(turn, BuildingKind.Apiary, home);
            }
            else if (!Has(world, BuildingKind.Brewery))
            {
                Build(turn, BuildingKind.Brewery, home);
            }
            else if (!Build(turn, BuildingKind.Field, home))
            {
                Build(turn, BuildingKind.Apiary, home);
            }
        }

        if (settlement.HousingOf(PopulationTier.Craftsmen) - craftsmen < 2 && Supply(turn, home, s_drink) >= drink - Units(1)
            && !Pending(world, BuildingKind.CraftsmensHouse))
        {
            Build(turn, BuildingKind.CraftsmensHouse, home);
        }
    }

    /// <summary>A jetty, the expedition and a settlement on the island it finds, as epoch 1 asks (economy-v2.md, section 10).</summary>
    private void Seafare(Turn turn, string home)
    {
        var world = turn.World;
        bool homeJetty = world.Buildings.Any(b => b.Kind == BuildingKind.Jetty && b.Island == home);
        if (!homeJetty && !Pending(world, BuildingKind.Jetty) && (Power ? world.Available(Force.BuildPower) >= Longhouse : ByHand))
        {
            Build(turn, BuildingKind.Jetty, home);
        }

        if (homeJetty && world.ExpeditionHoursLeft == 0 && turn.Sites.ExpeditionIsland is not null && !turn.Sites.ExpeditionIslandKnown)
        {
            turn.Apply(new SendExpedition());
        }

        if (turn.Sites.ExpeditionIsland is not string island || !turn.Sites.ExpeditionIslandKnown)
        {
            return;
        }

        if (!world.Buildings.Any(b => b.Kind == BuildingKind.Jetty && b.Island == island))
        {
            if (!Pending(world, BuildingKind.Jetty))
            {
                Build(turn, BuildingKind.Jetty, island, Placement.RichGround);
            }
        }
        else if (world.SettlementOn(island) is { } settlement)
        {
            GrowIsland(turn, settlement);
        }
    }

    /// <summary>A settlement on another island: the raw materials it is rich in, housing, food and farmers (D-106).</summary>
    private void GrowIsland(Turn turn, IslandSettlement settlement)
    {
        var world = turn.World;
        string island = settlement.Island;
        foreach (var kind in new[] { BuildingKind.SheepFarm, BuildingKind.Quarry })
        {
            if (!world.Buildings.Any(b => b.Kind == kind && b.Island == island) && !Pending(world, kind))
            {
                Build(turn, kind, island);
            }
        }

        if (settlement.CroftersRoom < 2 && !Pending(world, BuildingKind.Cottage))
        {
            Build(turn, BuildingKind.Cottage, island);
        }

        if (Supply(turn, island, s_food) < Units(settlement.Villagers + 2) && !Pending(world, BuildingKind.FishingHut, BuildingKind.HuntingLodge))
        {
            _ = Build(turn, BuildingKind.FishingHut, island) || Build(turn, BuildingKind.HuntingLodge, island);
        }

        if (world.Step >= 1 && settlement.VillagersOf(PopulationTier.Crofters) >= 4
            && settlement.HousingOf(PopulationTier.Farmers) - settlement.VillagersOf(PopulationTier.Farmers) < 2 && !Pending(world, BuildingKind.Farmstead))
        {
            Build(turn, BuildingKind.Farmstead, island);
        }
    }

    /// <summary>
    /// The forces left over: Dawn Power on the depot whose carriers fall furthest behind, upgrades only of materials
    /// to spare and one at a time, and workdays on what makes most while Dawn Power lasts for a rush too.
    /// </summary>
    /// <remarks>
    /// An upgrade goes last in the queue, so it takes only the work the builds before it leave over (economy-v2.md,
    /// section 2). Waiting for an empty queue instead left more Build Power unused the more there was of it, since more
    /// of it keeps the queue busy, and so more self-care ended in a smaller world (TASK-097).
    /// </remarks>
    private void Spend(Turn turn, string home)
    {
        var world = turn.World;
        var behind = world.Buildings
            .Where(b => b.Island == home && Logistics.IsDepot(b.Kind) && b.Staffed && b.Carriers > 0 && world.CarrierNeed(b.Tile) > Quantity.Units(b.Carriers))
            .OrderByDescending(b => world.CarrierNeed(b.Tile) - Quantity.Units(b.Carriers)).ThenBy(b => b.Tile)
            .FirstOrDefault();
        if (behind is not null && !behind.WorkdayToday)
        {
            turn.Apply(new Workday(behind.Tile));
        }

        var working = world.Buildings.Where(b => b.Staffed && turn.Sites.Produces(b.Kind))
            .OrderByDescending(b => Measures.Delivered(world, turn.Sites, b.Kind, b.Tile)).ThenByDescending(b => b.Tile)
            .ToList();
        var upgrade = world.Content.Upgrade;
        bool surplus = s_surplus.All(s => world.Stock(home, s.Key) >= s.Value + upgrade.Cost.GetValueOrDefault(s.Key));
        if (Power && surplus && !world.Projects.Any(p => p.Kind == ProjectKind.Upgrade))
        {
            foreach (var building in working)
            {
                if (building.Level < world.Content.For(building.Kind).MaxLevel && turn.Apply(new Upgrade(building.Tile)).Accepted)
                {
                    break;
                }
            }
        }

        foreach (var building in working)
        {
            if (world.Available(Force.DawnPower) < Workday.Cost + Rush.Cost)
            {
                break;
            }

            if (!building.WorkdayToday)
            {
                turn.Apply(new Workday(building.Tile));
            }
        }
    }

    /// <summary>
    /// Builds on the best free site of the island, producers where they yield most and the rest in the village, but
    /// always leaves two free villagers to build (TASK-044); with <see cref="ByHand"/>, the jetty and the weavery by hand when
    /// the Build Power does not suffice (D-65).
    /// </summary>
    private bool Build(Turn turn, BuildingKind kind, string island, Placement? placement = null)
    {
        var world = turn.World;
        var spec = world.Content.For(kind);
        var settlement = world.SettlementOn(island) ?? world.SettlementOn(turn.Sites.Home)!;
        if (spec.Staff > 0 && settlement.FreeVillagers < spec.Staff + 2)
        {
            return false;
        }

        var ready = turn.Sites.Free(kind, island).Where(tile => world.CheckBuild(kind, tile) == Rejection.None).ToList();
        if (ready.Count == 0)
        {
            return false;
        }

        var chosen = placement ?? (spec.Recipe is { Inputs.Count: 0 } ? Placement.Richest : Placement.Village);
        var site = Placements.Choose(chosen, kind, ready, turn.Sites, world.Map.Start);
        bool byHand = ByHand && spec.HandBuild && world.Available(Force.BuildPower) < spec.BuildPower;
        return turn.Apply(byHand ? new BuildByHand(kind, site) : new Build(kind, site)).Accepted;
    }

    /// <summary>
    /// What the island's staffed buildings make of the goods a game day, in millionths; processing no more than the raw
    /// materials the island's own buildings make for it, since the sea route may carry none of another island's home.
    /// </summary>
    private static long Supply(Turn turn, string island, Resource[] goods)
    {
        long total = 0;
        foreach (var building in turn.World.Buildings)
        {
            if (building.Staffed && building.Island == island && turn.Sites.RecipeOf(building.Kind) is Recipe recipe && goods.Contains(recipe.Product))
            {
                int level = building.Level == 2 ? 150 : 100;
                long made = turn.Sites.DailyYield(building.Kind, building.Tile, planning: false) * level / 100 / Sites.Weights[(int)recipe.Product];
                foreach (var input in recipe.Inputs)
                {
                    made = Math.Min(made, Supply(turn, island, [input.Resource]) / input.PerProduct);
                }

                total += made;
            }
        }

        return total;
    }

    private static bool Pending(World world, params BuildingKind[] kinds) =>
        world.Projects.Any(p => p.Kind == ProjectKind.Build && p.Building is BuildingKind kind && kinds.Contains(kind));

    private static bool Has(World world, BuildingKind kind) => world.Buildings.Any(b => b.Kind == kind);

    private static bool Has(World world, BuildingKind kind, string island) => world.Buildings.Any(b => b.Kind == kind && b.Island == island);

    private static long Units(int whole) => Quantity.Units(whole);
}
