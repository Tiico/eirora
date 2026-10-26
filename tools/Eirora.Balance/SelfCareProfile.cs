using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Forces;
using Eirora.Simulation.SelfCare;

namespace Eirora.Balance;

/// <summary>
/// A synthetic player's self-care week (epoch-1.md, Profilerna), recorded as daily totals the way the client records
/// health data (GLOSSARY: Bidrag). The week starts on game day 1, the first whole day played.
/// </summary>
/// <remarks>
/// The whole day is recorded at its dawn, as the economy model credits it, so a day's forces are there for the
/// morning's decisions. A habit the player has deselected, or a day without data, is not recorded at all.
/// </remarks>
public sealed class SelfCareProfile
{
    private const int Week = 7;

    private readonly int[]? _steps;
    private readonly int[]? _workouts;
    private readonly int?[]? _sleepMinutes;

    private SelfCareProfile(string id, string name, ContributionSource source, int[]? steps, int[]? workouts, int?[]? sleepMinutes)
    {
        Id = id;
        Name = name;
        Source = source;
        _steps = steps;
        _workouts = workouts;
        _sleepMinutes = sleepMinutes;
    }

    /// <summary>15 000 steps, six sessions and 8 h sleep.</summary>
    public static SelfCareProfile High { get; } = new("high", "hög", ContributionSource.Verified, Every(15_000), [1, 1, 1, 1, 1, 1, 0], Every<int?>(8 * 60));

    /// <summary>7 500 steps, three sessions and 6.5 h sleep; economy v1 calls it moderate (måttlig).</summary>
    public static SelfCareProfile Normal { get; } = new("normal", "normal", ContributionSource.Verified, Every(7_500), [1, 0, 1, 0, 1, 0, 0], Every<int?>(6 * 60 + 30));

    /// <summary>40 active minutes five days a week (D-41), three sessions and 7 h sleep five nights, entered by hand.</summary>
    public static SelfCareProfile ManualOnly { get; } = new(
        "manual",
        "enbart manuell",
        ContributionSource.SelfReported,
        [.. Enumerable.Repeat(ForceRules.StepsFromActiveMinutes(40), 5), 0, 0],
        [1, 0, 1, 0, 1, 0, 0],
        [.. Enumerable.Repeat<int?>(7 * 60, 5), null, null]);

    /// <summary>3 000 steps, one session and 6 h sleep.</summary>
    public static SelfCareProfile Low { get; } = new("low", "låg", ContributionSource.Verified, Every(3_000), [1, 0, 0, 0, 0, 0, 0], Every<int?>(6 * 60));

    /// <summary>Normal, with training deselected (HLT-01).</summary>
    public static SelfCareProfile NoTraining { get; } = new("no-training", "utan träning", ContributionSource.Verified, Every(7_500), null, Every<int?>(6 * 60 + 30));

    /// <summary>No self-care and no sleep data: Dawn Power stays at its baseline (D-16).</summary>
    public static SelfCareProfile None { get; } = new("none", "ingen", ContributionSource.Verified, null, null, null);

    public static IReadOnlyList<SelfCareProfile> All { get; } = [High, Normal, ManualOnly, Low, NoTraining, None];

    /// <summary>The id the tool takes on its command line.</summary>
    public string Id { get; }

    /// <summary>The profile's name in epoch-1.md.</summary>
    public string Name { get; }

    public ContributionSource Source { get; }

    public static SelfCareProfile Get(string id) =>
        All.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException($"There is no profile {id}; choose among {string.Join(", ", All.Select(p => p.Id))}.", nameof(id));

    /// <summary>
    /// The Exploration and Build Power the self-care gives each game day from game day 1, before the saving's soft cap
    /// (forces.md, sections 3 and 5): what a player who spends as the forces come in gets of them.
    /// </summary>
    public IReadOnlyList<(int Exploration, int BuildPower)> Earned(int days)
    {
        var earned = new (int Exploration, int BuildPower)[days];
        for (int day = 1; day <= days; day++)
        {
            int sessions = 0, power = 0;
            for (int before = Math.Max(1, day - 6); before < day; before++)
            {
                sessions += _workouts?[(before - 1) % Week] ?? 0;
                power += earned[before - 1].BuildPower;
            }

            int weekday = (day - 1) % Week;
            earned[day - 1] = (
                _steps is null ? 0 : ForceRules.Exploration(_steps[weekday]),
                _workouts is null ? 0 : ForceRules.BuildPower(sessions, _workouts[weekday], power));
        }

        return earned;
    }

    /// <summary>Records a game day's self-care; the world must be awake at that day's dawn.</summary>
    internal void Record(World world, int gameDay)
    {
        int weekday = (gameDay - 1) % Week;
        RecordIfAny(world, gameDay, Habit.Steps, _steps?[weekday]);
        RecordIfAny(world, gameDay, Habit.Workouts, _workouts?[weekday]);
        RecordIfAny(world, gameDay, Habit.Sleep, _sleepMinutes?[weekday]);
    }

    private void RecordIfAny(World world, int gameDay, Habit habit, int? total)
    {
        if (total is not int amount || amount == 0)
        {
            return;
        }

        var result = world.Apply($"self-care-{gameDay}-{habit}", new RecordSelfCare(gameDay, habit, Source, amount));
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"Recording {habit} for game day {gameDay} was rejected: {result.Rejection}.");
        }
    }

    private static T[] Every<T>(T value) => [.. Enumerable.Repeat(value, Week)];
}
