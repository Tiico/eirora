using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;

namespace Eirora.Simulation.Tests;

/// <summary>
/// A world from <see cref="Scenario"/> built out over days of play and then left with work in flight: a building queue in
/// the player's order, an expedition at sea or the sea route open, workdays, paused buildings and a staffing order of the
/// player's, felled forest, ripening fields and sheep, and now and then stores filled to the brim (TASK-049, TASK-044),
/// settlements on other islands with buildings that lack villagers and materials the ships still owe (TASK-050).
/// </summary>
/// <remarks>
/// Each morning of the build-out records self-care, unless the world is built without it, and plays commands that suit
/// the world; a jetty, a sawmill, the expedition and a jetty on each island it knows come first, so that most worlds
/// with self-care reach a sea route. One world in four is built on the <see cref="Archipelago"/>, where it knows a
/// second island from the start, so that it can have three stores and two sea routes (TASK-045). Everything is drawn
/// from the seed, so the same seed gives the same world.
/// </remarks>
internal static class BuiltOutWorld
{
    private const long Hour = 3600;

    private static readonly BuildingKind[] s_buildings = Enum.GetValues<BuildingKind>();

    // What a settlement builds first at home: a jetty for the expedition, planks and stone for everything after.
    private static readonly BuildingKind[] s_first = [BuildingKind.Jetty, BuildingKind.Sawmill, BuildingKind.Quarry];

    /// <summary>How much self-care the build-out records.</summary>
    public enum SelfCare
    {
        None,
        SomeDays,
        EveryDay,
    }

    public static World Generate(int seed) => GenerateSave(seed).World;

    public static World Generate(int seed, SelfCare selfCare) => GenerateSave(seed, selfCare).World;

    /// <summary>The world as its save, with the journal of everything that built it, as the client would keep it.</summary>
    public static Save GenerateSave(int seed) => GenerateSave(seed, (SelfCare)(Math.Abs(seed) % 3));

    public static Save GenerateSave(int seed, SelfCare selfCare) => GenerateSave(seed, selfCare, archipelago: Math.Abs(seed % 4) == 3);

    public static Save GenerateSave(int seed, SelfCare selfCare, bool archipelago)
    {
        var random = new Random(~seed);
        var scenario = Scenario.Generate(seed, length: 30, archipelago);
        var save = scenario.Begin();
        var world = save.World;
        foreach (var input in scenario.Inputs)
        {
            switch (input)
            {
                case Scenario.Advance advance:
                    save.AdvanceTo(advance.To);
                    break;
                case Scenario.Command command:
                    save.Apply(command.Key, command.For(world));
                    break;
            }
        }

        int key = 0;

        // One world in three keeps its expedition home until the last visit, so it is at sea while the player is away.
        bool expeditionLast = random.Next(3) == 0;
        int days = 8 + random.Next(15);
        for (int day = 0; day < days; day++)
        {
            ToMorning(save, random);
            if (selfCare == SelfCare.EveryDay || (selfCare == SelfCare.SomeDays && random.Next(2) == 0))
            {
                Record(Habit.Steps, 15_000 + random.Next(35_000));
                Record(Habit.Workouts, 1 + random.Next(3));
                Record(Habit.Sleep, (6 * 60) + random.Next(3 * 60));
            }

            for (int move = 3 + random.Next(5); move > 0; move--)
            {
                if (Suited(world, random, expeditionLast) is { } command)
                {
                    save.Apply($"out-{key++}", command);
                }
            }
        }

        // Now and then the stores fill up for some days before the player leaves.
        if (random.Next(3) == 0)
        {
            save.AdvanceTo(Shift(world.Now, (3 + random.Next(10)) * 24 * Hour));
        }

        // The last visit, while the villagers are awake: the queue is filled and the expedition sent if it can be.
        ToMorning(save, random);
        save.AdvanceTo(Shift(world.Now, random.Next(10) * Hour));
        while (world.Phase == Phase.Resting)
        {
            save.AdvanceTo(Shift(world.Now, Hour));
        }

        if (expeditionLast || random.Next(2) == 0)
        {
            save.Apply($"out-{key++}", new SendExpedition());
        }

        for (int build = random.Next(7); build > 0; build--)
        {
            if (FreeSite(world, random.Next(int.MaxValue), _ => true) is { } free)
            {
                save.Apply($"out-{key++}", new Build(free.Kind, free.Site));
            }
        }

        // Now and then a building on another island with a settlement, which its own store or the home island's pays;
        // the ships carry the home island's materials before anything else (D-106, D-113).
        var settled = world.Islands.Where(i => i != world.HomeIsland && world.Settlement.HasBuilt(BuildingKind.Jetty, i)).ToList();
        if (random.Next(2) == 0 && settled.Count > 0
            && FreeSite(world, random.Next(int.MaxValue), c => world.Settlement.IslandOf(c.Site) == settled[key % settled.Count]) is { } away)
        {
            save.Apply($"out-{key++}", new Build(away.Kind, away.Site));
        }

        if (BuiltSite(world, random.Next(int.MaxValue), b => b.Staffed && !b.WorkdayToday) is Hex worked)
        {
            save.Apply($"out-{key++}", new Workday(worked));
        }

        // Before leaving, the player may pause a building, give another villagers first or put a project first.
        if (random.Next(2) == 0 && BuiltSite(world, random.Next(int.MaxValue), b => !b.Paused) is Hex paused)
        {
            save.Apply($"out-{key++}", new PauseBuilding(paused));
        }

        if (random.Next(2) == 0 && StaffingMove(world, random.Next(int.MaxValue), random.Next(int.MaxValue)) is { } prioritize)
        {
            save.Apply($"out-{key++}", prioritize);
        }

        if (random.Next(2) == 0 && QueueMove(world, random.Next(int.MaxValue)) is { } first)
        {
            save.Apply($"out-{key++}", first);
        }

        // The player may leave at any moment of the day, also just before or during the rest.
        save.AdvanceTo(Shift(world.Now, random.Next((int)(20 * Hour))));
        return save;

        void Record(Habit habit, int total) =>
            save.Apply($"out-{key++}", new RecordSelfCare(world.GameDay, habit, ContributionSource.Verified, total));
    }

    /// <summary>Advances to the next dawn and on into the morning.</summary>
    private static void ToMorning(Save save, Random random)
    {
        int day = save.World.GameDay;
        while (save.World.GameDay == day)
        {
            save.AdvanceTo(Shift(save.World.Now, Hour));
        }

        save.AdvanceTo(Shift(save.World.Now, random.Next((int)(4 * Hour))));
    }

    /// <summary>
    /// A command that suits the world, or none: a jetty, a sawmill and a quarry, the expedition and a jetty on each island
    /// it knows first, then a little of everything. A few projects at a time, and a building that needs staff only while
    /// villagers are left to build, as a player builds while the villagers keep up.
    /// </summary>
    private static GameCommand? Suited(World world, Random random, bool expeditionLast)
    {
        int pick = random.Next(int.MaxValue);
        var settlement = world.Settlement;
        bool building = world.Projects.Count < 4;
        int staffAfterQueue = world.FreeVillagers - world.Projects.Where(p => p.Kind == ProjectKind.Build).Sum(p => world.Content.For(p.Building).Staff);
        bool Staffable((BuildingKind Kind, Hex Site) c) => world.Content.For(c.Kind).Staff == 0 || staffAfterQueue - world.Content.For(c.Kind).Staff >= 2;
        bool Wanting(BuildingKind kind, string island) =>
            building && !settlement.HasBuilt(kind, island) && !world.Projects.Any(p => p.Building == kind && settlement.IslandOf(p.Tile) == island);

        foreach (var first in s_first)
        {
            if (Wanting(first, world.HomeIsland) && FreeSite(world, pick, c => c.Kind == first && settlement.IslandOf(c.Site) == world.HomeIsland) is { } site)
            {
                return new Build(site.Kind, site.Site);
            }
        }

        if (!expeditionLast && settlement.CanSendExpedition() == Rejection.None && world.Available(Force.Exploration) >= SendExpedition.Cost(world))
        {
            return new SendExpedition();
        }

        var known = world.Map.Islands.Select(i => i.Id).Where(i => i != world.HomeIsland && world.Map.Features.LandOf(i).Any(world.IsKnown)).ToList();
        foreach (var island in known)
        {
            if (Wanting(BuildingKind.Jetty, island) && FreeSite(world, pick, c => c.Kind == BuildingKind.Jetty && settlement.IslandOf(c.Site) == island) is { } landing)
            {
                return new Build(landing.Kind, landing.Site);
            }
        }

        var landed = known.Where(i => settlement.HasBuilt(BuildingKind.Jetty, i)).ToList();
        if (building && random.Next(2) == 0 && landed.Count > 0
            && FreeSite(world, pick, c => settlement.IslandOf(c.Site) == landed[pick % landed.Count] && Staffable(c)) is { } across)
        {
            return new Build(across.Kind, across.Site);
        }

        return random.Next(12) switch
        {
            < 4 => building && FreeSite(world, pick, Staffable) is { } free ? new Build(free.Kind, free.Site) : null,
            4 or 5 => BuiltSite(world, pick, b => b.Level < world.Content.For(b.Kind).MaxLevel) is Hex level ? new Upgrade(level) : null,
            6 => BuiltSite(world, pick, b => !b.HasRoad && world.DepotDistance(b.Tile) is > 1 and < int.MaxValue) is Hex road ? new BuildRoad(road) : null,
            7 => BuiltSite(world, pick, b => b.Staffed && !b.WorkdayToday) is Hex worked ? new Workday(worked) : null,
            8 => new Survey(),
            9 => BuiltSite(world, pick, _ => true) is Hex tile ? (world.BuildingAt(tile)!.Paused ? new ResumeBuilding(tile) : new PauseBuilding(tile)) : null,
            10 => random.Next(2) == 0 ? StaffingMove(world, pick, random.Next(int.MaxValue)) : QueueMove(world, pick),
            _ => new Rush(),
        };
    }

    /// <summary>A building that needs staff moved to another place in the staffing order, or none.</summary>
    private static Prioritize? StaffingMove(World world, int pick, int place)
    {
        var order = world.StaffingOrder;
        return order.Count > 1 && order[pick % order.Count] is var moved && order[place % order.Count] != moved
            ? new Prioritize(moved.Tile, place % order.Count)
            : null;
    }

    /// <summary>A project that is not first in the queue moved first, or none.</summary>
    private static QueueFirst? QueueMove(World world, int pick) =>
        world.Projects.Count > 1 && world.Projects[1 + (pick % (world.Projects.Count - 1))] is var project
            ? new QueueFirst(project.Kind, project.Tile)
            : null;

    /// <summary>
    /// One of the affordable buildings that the settlement would take on a tile and that is <paramref name="wanted"/>,
    /// or none; every kind is as likely, however many tiles would take it.
    /// </summary>
    private static (BuildingKind Kind, Hex Site)? FreeSite(World world, int pick, Func<(BuildingKind Kind, Hex Site), bool> wanted)
    {
        var free = s_buildings
            .Select(k => Scenario.SitesOf(world).Select(tile => (Kind: k, Site: tile))
                .Where(c => wanted(c) && world.CheckBuild(c.Kind, c.Site) == Rejection.None && world.CanAfford(world.Content.For(c.Kind).Cost, c.Site)).ToList())
            .Where(sites => sites.Count > 0)
            .ToList();
        if (free.Count == 0)
        {
            return null;
        }

        var sites = free[pick % free.Count];
        return sites[pick / free.Count % sites.Count];
    }

    /// <summary>The tile of one of the world's producing buildings that <paramref name="can"/> take the command, or none.</summary>
    private static Hex? BuiltSite(World world, int pick, Func<Building, bool> can)
    {
        var built = world.Buildings.Where(b => world.Content.For(b.Kind).Produces && can(b)).ToList();
        return built.Count > 0 ? built[pick % built.Count].Tile : null;
    }

    private static TimeReading Shift(TimeReading reading, long seconds) =>
        new(reading.UtcSeconds + seconds, reading.ZoneId, reading.OffsetSeconds);
}
