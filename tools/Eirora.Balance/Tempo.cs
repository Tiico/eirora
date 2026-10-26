using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;

namespace Eirora.Balance;

/// <summary>
/// Epoch 1's tempo in a run (epoch-1.md, section 5; TASK-054): the game days on which the world reached each step the
/// simulation has, and an estimate of the day Vårdkasen could be lit, since its stages, Ytterön, the fog over the inner
/// sea and the research are not in the simulation yet (M5).
/// </summary>
/// <remarks>
/// The estimate lays the milestone on top of the run: the Exploration and Build Power that the self-care gave and the bot
/// did not spend go to the milestone in its order, each stage with Build Power when that comes before its 21 days by hand
/// (D-65). It counts the forces and the prerequisites the simulation has, a sea route and 50 villagers, but neither the
/// materials and work of the stages nor the research, which costs no force (D-66); the bot also spends what it would
/// leave a player who saves for a stage, so the estimate is late rather than early where Build Power decides.
/// </remarks>
public sealed class Tempo
{
    /// <summary>Build Power for each of Vårdkasen's three stages.</summary>
    public const int StagePower = 500;

    /// <summary>Game days for a stage built by hand (D-65).</summary>
    public const int StageByHandDays = 21;

    /// <summary>Exploration for the second expedition, to Ytterön.</summary>
    public const int SecondExpedition = 400;

    /// <summary>Game days the second expedition is away: 48 active hours.</summary>
    public const int SecondExpeditionDays = 3;

    /// <summary>Fog tiles over the inner sea that must be lifted before the fire.</summary>
    public const int InnerSeaTiles = 150;

    /// <summary>Villagers the fire needs.</summary>
    public const int FireVillagers = 50;

    private Tempo(RunResult run)
    {
        int? Met(string system) => run.SystemsMet.TryGetValue(system, out int day) ? day : null;
        SeaRoute = run.SeaRouteOn;
        Farmers = Met(Systems.Farmers);
        Craftsmen = Met(Systems.Craftsmen);
        Seafaring = Met(Systems.Seafaring);
        int fifty = run.VillagersByDay.ToList().FindIndex(v => v >= FireVillagers);
        FiftyVillagers = fifty < 0 ? null : fifty + 1;
        Beacon = EstimateBeacon(run);
    }

    /// <summary>The first game day that ended with a sea route.</summary>
    public int? SeaRoute { get; }

    /// <summary>The first game day of step 1, Farmers.</summary>
    public int? Farmers { get; }

    /// <summary>The first game day of step 2, Craftsmen.</summary>
    public int? Craftsmen { get; }

    /// <summary>The first game day of step 3, Seafaring.</summary>
    public int? Seafaring { get; }

    /// <summary>The first game day that ended with 50 villagers or more, as the fire needs.</summary>
    public int? FiftyVillagers { get; }

    /// <summary>The estimated game day Vårdkasen is lit, or null when it is not within the run.</summary>
    public int? Beacon { get; }

    public static Tempo Of(RunResult run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new Tempo(run);
    }

    /// <summary>The game week a game day falls in, from week 1.</summary>
    public static int? Week(int? day) => day is int d ? ((d - 1) / 7) + 1 : null;

    private int? EstimateBeacon(RunResult run)
    {
        if (SeaRoute is not int seaRoute)
        {
            return null;
        }

        int days = run.Spec.Days;
        var earned = run.Spec.Profile.Earned(days);
        long[] exploration = new long[days + 1], power = new long[days + 1];
        long earnedExploration = run.StartForces[0], earnedPower = run.StartForces[1];
        for (int day = 1; day <= days; day++)
        {
            earnedExploration += earned[day - 1].Exploration;
            earnedPower += earned[day - 1].BuildPower;
            exploration[day] = earnedExploration - run.SpentByDay[day - 1][0];
            power[day] = earnedPower - run.SpentByDay[day - 1][1];
        }

        long usedPower = 0;
        int? Pay(int ready, int cost, int byHandDays)
        {
            for (int day = ready; day <= days && day < ready + byHandDays; day++)
            {
                if (power[day] - usedPower >= cost)
                {
                    usedPower += cost;
                    return day;
                }
            }

            return ready + byHandDays <= days ? ready + byHandDays : null;
        }

        int? Explore(int ready, long needed)
        {
            for (int day = ready; day <= days; day++)
            {
                if (exploration[day] >= needed)
                {
                    return day;
                }
            }

            return null;
        }

        var jetty = ContentCatalog.Current.For(BuildingKind.Jetty);
        if (Pay(seaRoute, StagePower, StageByHandDays) is not int foundation
            || Explore(seaRoute, SecondExpedition) is not int sent
            || Pay(sent + SecondExpeditionDays, jetty.BuildPower, jetty.HandBuildDays) is not int ytterön
            || Pay(Math.Max(foundation, ytterön), StagePower, StageByHandDays) is not int frame
            || Explore(sent, SecondExpedition + ((long)InnerSeaTiles * LiftFog.Cost)) is not int innerSea
            || FiftyVillagers is not int fifty)
        {
            return null;
        }

        return Pay(Math.Max(frame, Math.Max(innerSea, fifty)), StagePower, StageByHandDays);
    }
}
