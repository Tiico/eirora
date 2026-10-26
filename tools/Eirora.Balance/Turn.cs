using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;

namespace Eirora.Balance;

/// <summary>
/// A bot's dawn: the world as it stands, and the one door into it, <see cref="Apply"/>, which sends a command with
/// its own idempotency key like the client and counts what it costs.
/// </summary>
internal sealed class Turn
{
    private static readonly Force[] s_forces = [Force.Exploration, Force.BuildPower, Force.DawnPower];

    private readonly Plan _plan;
    private readonly Tally _tally;

    public Turn(World world, int day, int lastDay, Plan plan, Tally tally)
    {
        World = world;
        Day = day;
        LastDay = lastDay;
        Sites = new Sites(world);
        _plan = plan;
        _tally = tally;
    }

    public World World { get; }

    /// <summary>The game day that just dawned.</summary>
    public int Day { get; }

    /// <summary>The last game day of the run.</summary>
    public int LastDay { get; }

    public Sites Sites { get; }

    public CommandResult Apply(GameCommand command)
    {
        int[] before = [.. s_forces.Select(World.Available)];
        var result = World.Apply($"bot-{Day}-{++_tally.Commands}", command);
        if (result.Accepted)
        {
            _tally.Accepted++;
            _tally.Decisions.Add((Day, command));
            for (int i = 0; i < s_forces.Length; i++)
            {
                _tally.Spent[i] += before[i] - World.Available(s_forces[i]);
            }
        }

        return result;
    }

    /// <summary>Whether something the plan still does may reveal more of <paramref name="island"/>: a survey at home, the expedition at sea.</summary>
    public bool MayReveal(string island)
    {
        if (island == Sites.Home)
        {
            return _plan.IsPending<SurveyEverything>() && Survey.NextCost(World) is not null;
        }

        return island == Sites.ExpeditionIsland && !Sites.ExpeditionIslandKnown
            && (_plan.IsPending<SendExpeditionOnce>() || World.ExpeditionHoursLeft > 0);
    }

    /// <summary>Whether a goal of the plan still waits to build <paramref name="kind"/>.</summary>
    public bool StillBuilds(BuildingKind kind) => _plan.StillBuilds(kind);
}

/// <summary>What a bot's commands came to over a run.</summary>
internal sealed class Tally
{
    /// <summary>Commands sent, accepted or not.</summary>
    public int Commands { get; set; }

    public int Accepted { get; set; }

    /// <summary>Exploration, Build Power and Dawn Power spent.</summary>
    public int[] Spent { get; } = new int[3];

    /// <summary>Every accepted command and the game day it was given on.</summary>
    public List<(int Day, GameCommand Command)> Decisions { get; } = [];

    /// <summary>The Dawn Power left once each game day's decisions were made.</summary>
    public List<int> DawnPowerLeft { get; } = [];

    /// <summary>The development value at the end of each game day.</summary>
    public List<long> Values { get; } = [];

    /// <summary>The first game day that ended with a sea route.</summary>
    public int? SeaRouteOn { get; set; }

    /// <summary>Each system the player has met and the first game day it was met on (economy-v2.md, section 5).</summary>
    public Dictionary<string, int> SystemsMet { get; } = new(StringComparer.Ordinal);
}
