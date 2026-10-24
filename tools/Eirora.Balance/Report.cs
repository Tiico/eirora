using System.Globalization;
using System.Text;
using Eirora.Simulation.Economy;

namespace Eirora.Balance;

/// <summary>
/// The report of a set of runs, as TSV for scripts and Markdown for people. It holds nothing but the runs' results, in
/// the order they were given, so the same runs always give the same bytes.
/// </summary>
public static class Report
{
    private static readonly string[] s_columns =
    [
        "map", "seed", "strategy", "profile", "days", "villagers", "housing", "buildings", "level_2", "islands",
        "fish", "grain", "timber", "planks", "stone", "wool", "wadmal", "daily_output", "value", "start_value",
        "used_exploration", "used_build_power", "used_dawn_power", "saved_exploration", "saved_build_power", "saved_dawn_power",
        "value_per_force", "plan_finished_day", "goals_done", "goals_skipped", "goals_pending", "commands", "accepted", "hours_checked",
    ];

    public static string Tsv(IEnumerable<RunResult> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var text = new StringBuilder();
        text.Append(string.Join('\t', s_columns)).Append('\n');
        foreach (var run in runs)
        {
            string[] cells =
            [
                run.Spec.Map.Name,
                Seed(run),
                run.Spec.Strategy.Id,
                run.Spec.Profile.Id,
                Whole(run.Spec.Days),
                Whole(run.Villagers),
                Whole(run.Housing),
                Whole(run.Buildings),
                Whole(run.LevelTwo),
                Whole(run.Islands),
                .. run.Stock.Select(Whole),
                Rounded(run.DailyOutput),
                Rounded(run.Value),
                Rounded(run.StartValue),
                .. run.Used.Select(u => Whole(u)),
                .. run.Saved.Select(s => Whole(s)),
                PerForce(run) ?? string.Empty,
                run.PlanFinishedOn is int day ? Whole(day) : string.Empty,
                Whole(run.GoalsDone),
                string.Join("; ", run.GoalsSkipped),
                string.Join("; ", run.GoalsPending),
                Whole(run.Commands),
                Whole(run.Accepted),
                Whole(run.HoursChecked),
            ];
            text.Append(string.Join('\t', cells)).Append('\n');
        }

        return text.ToString();
    }

    public static string Markdown(IEnumerable<RunResult> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var all = runs.ToList();
        var text = new StringBuilder();
        text.Append("# Balansrapport\n\n");
        text.Append(Invariant($"{all.Count} körningar av balansbotarna (TASK-048) med invarianterna kontrollerade varje timme, {all.Sum(r => r.HoursChecked)} timmar i allt.\n"));
        text.Append("Måtten är ekonomi v1:s (avsnitt 7) och visas aldrig för spelaren.\n");

        foreach (var group in all.GroupBy(r => (r.Spec.Map.Name, r.Spec.Days)))
        {
            text.Append(Invariant($"\n## {group.Key.Name}, {group.Key.Days} dagar\n\n"));
            text.Append("| Karta | Körning | Egenvård | Bybor | Byggnader | Öar | Sten i lager | Dagsproduktion | Utvecklingsvärde | Använt U/S/O | Sparat U/S/O | Värde per använd kraft | Planen klar | Mål kvar |\n");
            text.Append("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var run in group)
            {
                string[] cells =
                [
                    run.Spec.Map.Seed is null ? "prototyp" : $"seed {Seed(run)}",
                    run.Spec.Strategy.Name,
                    run.Spec.Profile.Name,
                    Whole(run.Villagers),
                    Whole(run.Buildings),
                    Whole(run.Islands),
                    Whole(run.Stock[(int)Resource.Stone]),
                    Rounded(run.DailyOutput),
                    Rounded(run.Value),
                    string.Join('/', run.Used.Select(u => Whole(u))),
                    string.Join('/', run.Saved.Select(s => Whole(s))),
                    PerForce(run)?.Replace('.', ',') ?? "–",
                    run.PlanFinishedOn is int day ? $"dygn {Whole(day)}" : "–",
                    run.GoalsPending.Count == 0 ? "–" : string.Join(", ", run.GoalsPending),
                ];
                text.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");
            }

            text.Append("\n<details><summary>Planerna och byggnaderna vid slutet</summary>\n\n");
            foreach (var run in group)
            {
                string map = run.Spec.Map.Seed is null ? string.Empty : $"seed {Seed(run)}, ";
                string plan = run.GoalLog.Count == 0 ? string.Empty : $"Plan: {string.Join(", ", run.GoalLog)}. ";
                text.Append(Invariant($"- {map}{run.Spec.Strategy.Name}, {run.Spec.Profile.Name}: {plan}Byggnader: {string.Join(", ", run.BuildingList)}.\n"));
            }

            text.Append("\n</details>\n");
        }

        return text.ToString();
    }

    private static string Seed(RunResult run) => run.Spec.Map.Seed?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Whole(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Millionths as whole units, rounded half away from zero.</summary>
    private static string Rounded(long millionths) =>
        Whole(millionths >= 0 ? (millionths + (Quantity.PerUnit / 2)) / Quantity.PerUnit : -((-millionths + (Quantity.PerUnit / 2)) / Quantity.PerUnit));

    private static string? PerForce(RunResult run) =>
        run.ValuePerForceHundredths is long hundredths ? (hundredths / 100m).ToString("0.00", CultureInfo.InvariantCulture) : null;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
