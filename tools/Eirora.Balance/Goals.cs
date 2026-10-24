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

/// <summary>Builds one building where <see cref="Placement"/> chooses among the free sites the map offers.</summary>
public sealed class BuildOne : Goal
{
    public BuildOne(BuildingKind building, Placement placement, Where where = Where.Home)
    {
        Building = building;
        Placement = placement;
        Where = where;
    }

    public BuildingKind Building { get; }

    public Placement Placement { get; }

    public Where Where { get; }

    public override string Name => Where == Where.Home ? Names.Of(Building) : $"{Names.Of(Building)} på {Names.Of(Where)}";

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
            return turn.Apply(new Build(Building, site)).Accepted ? GoalStatus.Done : GoalStatus.Pending;
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

/// <summary>Rushes the first project in the queue (economy-v1.md, section 6).</summary>
public sealed class RushFirst : DailyMove
{
    public override string Name => "skynda på";

    internal override void Make(Turn turn)
    {
        if (turn.World.Projects.Count > 0)
        {
            turn.Apply(new Rush());
        }
    }
}

/// <summary>
/// Moves the first queued building of a kind first in the building queue, until one of its kind stands
/// (economy-v1.md, section 4.1); the work it already has goes with it.
/// </summary>
public sealed class QueueFirstUntilBuilt : DailyMove
{
    public QueueFirstUntilBuilt(BuildingKind building) => Building = building;

    public BuildingKind Building { get; }

    public override string Name => $"{Names.Of(Building)} först i byggkön";

    internal override void Make(Turn turn)
    {
        var world = turn.World;
        if (world.Buildings.Any(b => b.Kind == Building)
            || world.Projects.FirstOrDefault(p => p.Kind == ProjectKind.Build && p.Building == Building) is not Project project
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
