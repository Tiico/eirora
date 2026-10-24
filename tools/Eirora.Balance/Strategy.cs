using Eirora.Simulation;
using Eirora.Simulation.Economy;

namespace Eirora.Balance;

/// <summary>
/// A way to play (economy-v1.md, section 7): a plan of goals pursued in order at each dawn, and moves made every dawn
/// after them. A goal that cannot be afforded yet waits without holding up the goals after it, as in the model.
/// </summary>
public sealed class Strategy
{
    public Strategy(string id, string name, IReadOnlyList<Goal> goals, IReadOnlyList<DailyMove> daily)
    {
        Id = id;
        Name = name;
        Goals = goals;
        Daily = daily;
    }

    /// <summary>Närområdet: survey the home island and build a production cluster around its richest ground.</summary>
    public static Strategy Near { get; } = new(
        "near",
        "Närområdet",
        [
            new SurveyEverything(),
            new BuildOne(BuildingKind.Woodcutter, Placement.Richest),
            new BuildOne(BuildingKind.Quarry, Placement.Richest),
            new BuildOne(BuildingKind.Storehouse, Placement.Depot),
            new BuildOne(BuildingKind.Sawmill, Placement.Village),
            new BuildOne(BuildingKind.Longhouse, Placement.Village),
            new BuildOne(BuildingKind.FishingHut, Placement.Richest),
            new BuildOne(BuildingKind.Longhouse, Placement.Village),
            new UpgradeOne(BuildingKind.Sawmill),
            new BuildOne(BuildingKind.Field, Placement.Richest),
            new BuildOne(BuildingKind.Longhouse, Placement.Village),
        ],
        [new WorkdayOn(BuildingKind.Quarry), new WorkdayOn(BuildingKind.Sawmill), new RushFirst()]);

    /// <summary>
    /// Närområdet, ordnad: Närområdet with the work ordered (economy-v1.md, section 4.1): the first longhouse first in
    /// the building queue, and the quarry paused from 100 stone in store until it is down below 80, so that its villagers
    /// build instead, until the last day.
    /// </summary>
    public static Strategy NearOrdered { get; } = new(
        "near-ordered",
        "Närområdet, ordnad",
        Near.Goals,
        [.. Near.Daily, new QueueFirstUntilBuilt(BuildingKind.Longhouse), new PauseWhileStocked(BuildingKind.Quarry, Resource.Stone, 100, 80)]);

    /// <summary>
    /// Närområdet, flyttad: Närområdet with the start woodcutter moved to the richest forest once the surveys have found
    /// it, by demolishing it and building it again (D-108), which must not beat building there from the start (TASK-047).
    /// </summary>
    public static Strategy NearMoved { get; } = new(
        "near-moved",
        "Närområdet, flyttad",
        [Near.Goals[0], new MoveOne(BuildingKind.Woodcutter, Placement.Richest), .. Near.Goals.Skip(1)],
        Near.Daily);

    /// <summary>
    /// Havet: a jetty and an expedition, sheep and stone on the island it finds, wadmal for faster growth. The jetty,
    /// sheep and stone on the island go before the first housing, which is a cottage (economy-v2.md, section 9).
    /// </summary>
    public static Strategy Sea { get; } = new(
        "sea",
        "Havet",
        [
            new BuildOne(BuildingKind.Jetty, Placement.Depot),
            new BuildOne(BuildingKind.Woodcutter, Placement.Richest),
            new BuildOne(BuildingKind.Sawmill, Placement.Village),
            new SendExpeditionOnce(),
            new BuildOne(BuildingKind.Jetty, Placement.RichGround, Where.ExpeditionIsland),
            new BuildOne(BuildingKind.SheepFarm, Placement.Richest, Where.ExpeditionIslandElseHome),
            new BuildOne(BuildingKind.Quarry, Placement.Richest, Where.ExpeditionIslandElseHome),
            new BuildOne(BuildingKind.Cottage, Placement.Village),
            new BuildOne(BuildingKind.Weavery, Placement.Village),
            new BuildOne(BuildingKind.Longhouse, Placement.Village),
        ],
        [new WorkdayOn(BuildingKind.Sawmill), new WorkdayOn(BuildingKind.SheepFarm), new RushFirst()]);

    /// <summary>Naiv: builds the first thing it can wherever it fits, upgrades what it has, and never reads the map.</summary>
    public static Strategy Naive { get; } = new(
        "naive",
        "Naiv",
        [
            new BuildOne(BuildingKind.Sawmill, Placement.FirstFit),
            new BuildOne(BuildingKind.Quarry, Placement.FirstFit),
            new BuildOne(BuildingKind.Longhouse, Placement.FirstFit),
            new BuildOne(BuildingKind.Weavery, Placement.FirstFit),
            new BuildOne(BuildingKind.SheepFarm, Placement.FirstFit),
            new UpgradeOne(BuildingKind.FishingHut),
            new UpgradeOne(BuildingKind.Woodcutter),
            new BuildOne(BuildingKind.Longhouse, Placement.FirstFit),
            new UpgradeOne(BuildingKind.Field),
            new SurveyEverything(),
        ],
        [new WorkdayOn(BuildingKind.FishingHut), new WorkdayOn(BuildingKind.Field), new WorkdayOn(BuildingKind.Woodcutter)]);

    /// <summary>Utan träning: only housing and production that cost no Build Power (D-43).</summary>
    public static Strategy NoTraining { get; } = new(
        "no-training",
        "Utan träning",
        [
            new SurveyEverything(),
            new BuildOne(BuildingKind.Woodcutter, Placement.Richest),
            new BuildOne(BuildingKind.Quarry, Placement.Richest),
            new BuildOne(BuildingKind.Cottage, Placement.Village),
            new BuildOne(BuildingKind.FishingHut, Placement.Richest),
            new BuildOne(BuildingKind.Cottage, Placement.Village),
            new BuildOne(BuildingKind.Field, Placement.Richest),
            new BuildOne(BuildingKind.Cottage, Placement.Village),
        ],
        [new WorkdayOn(BuildingKind.Quarry), new WorkdayOn(BuildingKind.Woodcutter), new RushFirst()]);

    /// <summary>Ingen spelare: no commands at all; only self-care, if any, is recorded.</summary>
    public static Strategy Idle { get; } = new("idle", "Ingen spelare", [], []);

    public static IReadOnlyList<Strategy> All { get; } = [Near, NearOrdered, NearMoved, Sea, Naive, NoTraining, Idle];

    /// <summary>The id the tool takes on its command line.</summary>
    public string Id { get; }

    /// <summary>The strategy's name in economy-v1.md.</summary>
    public string Name { get; }

    public IReadOnlyList<Goal> Goals { get; }

    public IReadOnlyList<DailyMove> Daily { get; }

    public static Strategy Get(string id) =>
        All.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException($"There is no strategy {id}; choose among {string.Join(", ", All.Select(s => s.Id))}.", nameof(id));
}

/// <summary>A strategy's goals as one run pursues them.</summary>
internal sealed class Plan
{
    private readonly Strategy _strategy;
    private readonly GoalStatus[] _status;
    private readonly int[] _settledOn;

    public Plan(Strategy strategy)
    {
        _strategy = strategy;
        _status = new GoalStatus[strategy.Goals.Count];
        _settledOn = new int[strategy.Goals.Count];
    }

    /// <summary>
    /// The game day on which the plan was carried out: every goal done or skipped and the building queue empty; null
    /// until then.
    /// </summary>
    public int? FinishedOn { get; private set; }

    public int Count(GoalStatus status) => _status.Count(s => s == status);

    public IEnumerable<string> Pending() => _strategy.Goals.Where((_, i) => _status[i] == GoalStatus.Pending).Select(g => g.Name);

    public IEnumerable<string> Skipped() => _strategy.Goals.Where((_, i) => _status[i] == GoalStatus.Skipped).Select(g => g.Name);

    /// <summary>Each goal and the game day it was done or skipped on, in the plan's order.</summary>
    public IEnumerable<string> Log() => _strategy.Goals.Select((goal, i) => _status[i] switch
    {
        GoalStatus.Done => $"{goal.Name} dygn {_settledOn[i]}",
        GoalStatus.Skipped => $"{goal.Name} överhoppat dygn {_settledOn[i]}",
        _ => $"{goal.Name} kvar",
    });

    public bool IsPending<T>()
        where T : Goal => _strategy.Goals.Where((g, i) => g is T && _status[i] == GoalStatus.Pending).Any();

    public bool StillBuilds(BuildingKind kind) =>
        _strategy.Goals.Where((g, i) => g is BuildOne build && build.Building == kind && _status[i] == GoalStatus.Pending).Any();

    /// <summary>Pursues every goal not yet done, in order, then makes the daily moves.</summary>
    public void Play(Turn turn)
    {
        for (int i = 0; i < _status.Length; i++)
        {
            if (_status[i] == GoalStatus.Pending)
            {
                _status[i] = _strategy.Goals[i].Pursue(turn);
                _settledOn[i] = turn.Day;
            }
        }

        foreach (var move in _strategy.Daily)
        {
            move.Make(turn);
        }
    }

    /// <summary>Notes the game day the plan is carried out on, once every goal is settled and nothing is left to build.</summary>
    public void Note(World world, int day)
    {
        if (FinishedOn is null && world.Projects.Count == 0 && _status.All(s => s != GoalStatus.Pending))
        {
            FinishedOn = day;
        }
    }
}
