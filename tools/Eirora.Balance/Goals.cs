using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;

namespace Eirora.Balance;

/// <summary>Where a plan's goal is pursued: the home island, the island the expedition finds, or that island before home.</summary>
public enum Where
{
    Home,
    ExpeditionIsland,
    ExpeditionIslandElseHome,
}

/// <summary>How far a plan's goal has come.</summary>
public enum GoalStatus
{
    Pending,
    Done,

    /// <summary>The map offers no site for it and nothing the plan still does would reveal one.</summary>
    Skipped,
}

/// <summary>
/// One step of a strategy's plan, pursued at each dawn until it is done or the map shows that it cannot be. Goals are
/// immutable; a <see cref="Plan"/> keeps their status for one run.
/// </summary>
public abstract class Goal
{
    /// <summary>The goal in a few words, for the report.</summary>
    public abstract string Name { get; }

    internal abstract GoalStatus Pursue(Turn turn);
}

/// <summary>Surveys every area left, one after the other, as long as the Exploration lasts (economy-v1.md, section 6).</summary>
public sealed class SurveyEverything : Goal
{
    public override string Name => "kartlägg allt";

    internal override GoalStatus Pursue(Turn turn)
    {
        while (Survey.NextCost(turn.World) is not null)
        {
            if (!turn.Apply(new Survey()).Accepted)
            {
                return GoalStatus.Pending;
            }
        }

        return GoalStatus.Done;
    }
}

/// <summary>
/// Surveys the next area each dawn, two on the first, as long as the Exploration lasts, as the guided tour does
/// (ux-journeys.md, resa 1); done once every area is surveyed.
/// </summary>
public sealed class SurveyNext : Goal
{
    public override string Name => "kartlägg nästa område";

    internal override GoalStatus Pursue(Turn turn)
    {
        for (int surveys = turn.Day == 1 ? 2 : 1; surveys > 0 && Survey.NextCost(turn.World) is not null; surveys--)
        {
            if (!turn.Apply(new Survey()).Accepted)
            {
                return GoalStatus.Pending;
            }
        }

        return Survey.NextCost(turn.World) is null ? GoalStatus.Done : GoalStatus.Pending;
    }
}

/// <summary>Sends the expedition once there is a jetty and Exploration for it; skipped on a map without an island to find.</summary>
public sealed class SendExpeditionOnce : Goal
{
    public override string Name => "expedition";

    internal override GoalStatus Pursue(Turn turn)
    {
        if (turn.Sites.ExpeditionIsland is null)
        {
            return GoalStatus.Skipped;
        }

        return turn.Sites.ExpeditionIslandKnown || turn.Apply(new SendExpedition()).Accepted ? GoalStatus.Done : GoalStatus.Pending;
    }
}

/// <summary>
/// Builds one building where <see cref="Placement"/> chooses among the free sites the map offers; with
/// <see cref="ByHand"/>, by hand when the Build Power does not suffice and the catalog lets it (D-65), as the epoch model
/// does for a player whose Build Power does not grow (epoch-1.md, section 5).
/// </summary>
public sealed class BuildOne : Goal
{
    public BuildOne(BuildingKind building, Placement placement, Where where = Where.Home, bool byHand = false)
    {
        Building = building;
        Placement = placement;
        Where = where;
        ByHand = byHand;
    }

    public BuildingKind Building { get; }

    public Placement Placement { get; }

    public Where Where { get; }

    public bool ByHand { get; }

    public override string Name => (Where == Where.Home ? Names.Of(Building) : $"{Names.Of(Building)} på {Names.Of(Where)}") + (ByHand ? " eller med handkraft" : string.Empty);

    internal override GoalStatus Pursue(Turn turn)
    {
        foreach (string? island in Islands(turn.Sites))
        {
            if (island is null)
            {
                continue;
            }

            var free = turn.Sites.Free(Building, island);
            if (free.Count == 0)
            {
                if (turn.MayReveal(island))
                {
                    return GoalStatus.Pending;
                }

                continue;
            }

            var ready = free.Where(tile => turn.World.CheckBuild(Building, tile) == Rejection.None).ToList();
            if (ready.Count == 0)
            {
                return GoalStatus.Pending; // Such as land on another island before its jetty.
            }

            var site = Placements.Choose(Placement, Building, ready, turn.Sites, turn.World.Map.Start);
            var spec = turn.World.Content.For(Building);
            bool byHand = ByHand && spec.HandBuild && turn.World.Available(Force.BuildPower) < spec.BuildPower;
            return turn.Apply(byHand ? new BuildByHand(Building, site) : new Build(Building, site)).Accepted ? GoalStatus.Done : GoalStatus.Pending;
        }

        return GoalStatus.Skipped;
    }

    private string?[] Islands(Sites sites) => Where switch
    {
        Where.Home => [sites.Home],
        Where.ExpeditionIsland => [sites.ExpeditionIsland],
        _ => [sites.ExpeditionIsland, sites.Home],
    };
}

/// <summary>
/// Builds one building on a tile the guided tour names on the prototype's map (economy-v2.md, section 5), once the world
/// takes it there and it can be paid; skipped on any other map, whose tiles the tour does not know.
/// </summary>
public sealed class BuildAt : Goal
{
    public BuildAt(BuildingKind building, Hex tile)
    {
        Building = building;
        Tile = tile;
    }

    public BuildingKind Building { get; }

    public Hex Tile { get; }

    public override string Name => $"{Names.Of(Building)} på {Tile}";

    internal override GoalStatus Pursue(Turn turn)
    {
        var world = turn.World;
        if (world.Map.Hash != PrototypeHome.Map.Hash)
        {
            return GoalStatus.Skipped;
        }

        return world.CheckBuild(Building, Tile) == Rejection.None && world.CanAfford(world.Content.For(Building).Cost, Tile)
            && turn.Apply(new Build(Building, Tile)).Accepted
            ? GoalStatus.Done
            : GoalStatus.Pending;
    }
}

/// <summary>
/// Moves the home island's first building of a kind to where <see cref="Placement"/> chooses, by demolishing it and
/// building it again there at the same dawn once the materials and Build Power for the new one are at hand (D-38, D-108):
/// half the old one's materials come back and no force. Done once it stands, or is queued, where the placement chooses.
/// </summary>
public sealed class MoveOne : Goal
{
    public MoveOne(BuildingKind building, Placement placement)
    {
        Building = building;
        Placement = placement;
    }

    public BuildingKind Building { get; }

    public Placement Placement { get; }

    public override string Name => $"flytta {Names.Of(Building)}";

    internal override GoalStatus Pursue(Turn turn)
    {
        var world = turn.World;
        string home = turn.Sites.Home;
        var free = turn.Sites.Free(Building, home).Where(tile => world.CheckBuild(Building, tile) == Rejection.None).ToList();
        if (free.Count == 0)
        {
            return turn.MayReveal(home) ? GoalStatus.Pending : GoalStatus.Skipped;
        }

        var site = Placements.Choose(Placement, Building, free, turn.Sites, world.Map.Start);
        var spec = world.Content.For(Building);
        bool affordable = spec.Cost.All(c => world.Stock(home, c.Key) >= c.Value) && world.Available(Force.BuildPower) >= spec.BuildPower;
        if (!affordable)
        {
            return GoalStatus.Pending;
        }

        // Demolished at an earlier dawn but not built again, as when the build was refused: it is built now.
        if (world.Buildings.FirstOrDefault(b => b.Kind == Building && b.Island == home) is { } old && !turn.Apply(new Demolish(old.Tile)).Accepted)
        {
            return GoalStatus.Pending;
        }

        return turn.Apply(new Build(Building, site)).Accepted ? GoalStatus.Done : GoalStatus.Pending;
    }
}

/// <summary>Upgrades the building of a kind that yields the most, once one stands.</summary>
public sealed class UpgradeOne : Goal
{
    public UpgradeOne(BuildingKind building) => Building = building;

    public BuildingKind Building { get; }

    public override string Name => $"uppgradera {Names.Of(Building)}";

    internal override GoalStatus Pursue(Turn turn)
    {
        var world = turn.World;
        var candidates = world.Buildings
            .Where(b => b.Kind == Building && b.Level < world.Content.For(b.Kind).MaxLevel && !world.Projects.Any(p => p.Kind == ProjectKind.Upgrade && p.Tile == b.Tile))
            .Select(b => b.Tile)
            .ToList();
        if (candidates.Count == 0)
        {
            bool coming = world.Projects.Any(p => p.Kind == ProjectKind.Build && p.Building == Building) || turn.StillBuilds(Building);
            return coming ? GoalStatus.Pending : GoalStatus.Skipped;
        }

        var tile = Moves.MostProductive(candidates, Building, turn.Sites, world);
        return turn.Apply(new Upgrade(tile)).Accepted ? GoalStatus.Done : GoalStatus.Pending;
    }
}

/// <summary>Something a strategy does every dawn once its goals for the day are pursued, such as spending Dawn Power.</summary>
public abstract class DailyMove
{
    public abstract string Name { get; }

    internal abstract void Make(Turn turn);
}

/// <summary>A workday on the most productive staffed building of a kind (economy-v1.md, section 6).</summary>
public sealed class WorkdayOn : DailyMove
{
    public WorkdayOn(BuildingKind building) => Building = building;

    public BuildingKind Building { get; }

    public override string Name => $"extraarbete på {Names.Of(Building)}";

    internal override void Make(Turn turn)
    {
        var candidates = turn.World.Buildings.Where(b => b.Kind == Building && b.Staffed && !b.WorkdayToday).Select(b => b.Tile).ToList();
        if (candidates.Count > 0)
        {
            turn.Apply(new Workday(Moves.MostProductive(candidates, Building, turn.Sites, turn.World)));
        }
    }
}

/// <summary>A move made only at the first dawn, as the guided tour makes some of its own (ux-journeys.md, resa 1).</summary>
public sealed class OnFirstDawn : DailyMove
{
    public OnFirstDawn(DailyMove move) => Move = move;

    public DailyMove Move { get; }

    public override string Name => $"{Move.Name} första dygnet";

    internal override void Make(Turn turn)
    {
        if (turn.Day == 1)
        {
            Move.Make(turn);
        }
    }
}

/// <summary>
/// Puts a resource first in the order the ships take each other island's goods home in, once the island has a store
/// (D-126, LOG-06), so that, say, wool for clothes is not crowded out by stone on a sea route that carries 40 a day.
/// </summary>
public sealed class ImportFirst : DailyMove
{
    public ImportFirst(Resource resource) => Resource = resource;

    public Resource Resource { get; }

    public override string Name => $"{Names.Of(Resource)} först i importprioriteten";

    internal override void Make(Turn turn)
    {
        var world = turn.World;
        foreach (var island in world.Settlements.Select(s => s.Island).Where(i => i != world.HomeIsland))
        {
            if (world.ImportOrder(island)[0] != Resource)
            {
                turn.Apply(new MoveImport(island, Resource, 0));
            }
        }
    }
}

/// <summary>Rushes the first project in the queue that the villagers work on (economy-v1.md, section 6); one built by hand is not.</summary>
public sealed class RushFirst : DailyMove
{
    public override string Name => "skynda på";

    internal override void Make(Turn turn)
    {
        if (turn.World.Projects.Any(p => !p.ByHand))
        {
            turn.Apply(new Rush());
        }
    }
}

/// <summary>
/// Moves the first queued building of a kind first in the building queue, until one of its kind stands on its island
/// (economy-v1.md, section 4.1); the work it already has goes with it. One built by hand has no place in the order.
/// </summary>
public sealed class QueueFirstUntilBuilt : DailyMove
{
    public QueueFirstUntilBuilt(BuildingKind building) => Building = building;

    public BuildingKind Building { get; }

    public override string Name => $"{Names.Of(Building)} först i byggkön";

    internal override void Make(Turn turn)
    {
        var world = turn.World;
        if (world.Projects.FirstOrDefault(p => p.Kind == ProjectKind.Build && p.Building == Building && !p.ByHand) is not Project project
            || world.Buildings.Any(b => b.Kind == Building && b.Island == world.IslandOf(project.Tile))
            || world.Projects[0] == project)
        {
            return;
        }

        turn.Apply(new QueueFirst(ProjectKind.Build, project.Tile));
    }
}

/// <summary>
/// Pauses the buildings of a kind while the store holds enough of a resource, so that their villagers build instead,
/// and resumes them when it holds less than <see cref="Resume"/> or on the run's last day (economy-v1.md, section 4.1).
/// The gap between the two keeps a day's use from switching them off and on.
/// </summary>
public sealed class PauseWhileStocked : DailyMove
{
    public PauseWhileStocked(BuildingKind building, Resource resource, int enough, int resume)
    {
        Building = building;
        Resource = resource;
        Enough = enough;
        Resume = resume;
    }

    public BuildingKind Building { get; }

    public Resource Resource { get; }

    public int Enough { get; }

    public int Resume { get; }

    public override string Name => $"pausa {Names.Of(Building)} vid {Enough} i lager till under {Resume}";

    internal override void Make(Turn turn)
    {
        var world = turn.World;
        foreach (var building in world.Buildings.Where(b => b.Kind == Building).ToList())
        {
            bool pause = world.Stock(Resource) >= (building.Paused ? Resume : Enough) && turn.Day < turn.LastDay;
            if (building.Paused != pause)
            {
                turn.Apply(pause ? new PauseBuilding(building.Tile) : new ResumeBuilding(building.Tile));
            }
        }
    }
}

internal static class Moves
{
    /// <summary>The building that delivers the most of what it makes now, the first in map order among equals.</summary>
    public static Hex MostProductive(IReadOnlyList<Hex> tiles, BuildingKind kind, Sites sites, World world)
    {
        var best = tiles[0];
        long top = long.MinValue;
        foreach (var tile in tiles)
        {
            long delivered = Measures.Delivered(world, sites, kind, tile);
            if (delivered > top || (delivered == top && tile < best))
            {
                (best, top) = (tile, delivered);
            }
        }

        return best;
    }
}
