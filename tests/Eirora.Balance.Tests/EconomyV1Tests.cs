using System.Globalization;
using System.Text;
using Eirora.Simulation.Economy;

namespace Eirora.Balance.Tests;

/// <summary>
/// The bots' seven days on the prototype's map against the tables of economy v1, section 7: the model's within the
/// tolerance section 7.1 documents, and the bots' own exactly, so the design document always shows what the game does.
/// </summary>
public sealed class EconomyV1Tests
{
    private const string ModelTable = "## 7. Sju dygn med två strategier";
    private const string BotTable = "### 7.1 Balansbotarna i simuleringen";

    private static readonly Lazy<List<RunResult>> s_week = new(() => [.. Suites.EconomyV1().Select(BalanceRun.Play)]);

    [Fact]
    public void TheBotsWeekIsWithinTheDocumentedToleranceOfTheModel()
    {
        var model = Rows(ModelTable);
        Assert.Equal(s_week.Value.Count, model.Count);

        // The tolerance of economy-v1.md, section 7.1: about one decision's worth.
        foreach (var run in s_week.Value)
        {
            var bot = Row.Of(run);
            var expected = model[bot.Key];
            Within(bot.Key, "bybor", expected.Villagers, bot.Villagers, 1);
            Within(bot.Key, "byggnader", expected.Buildings, bot.Buildings, 1);
            Within(bot.Key, "öar", expected.Islands, bot.Islands, 0);
            Within(bot.Key, "sten i lager", expected.Stone, bot.Stone, 10);
            Within(bot.Key, "dagsproduktion", expected.DailyOutput, bot.DailyOutput, expected.DailyOutput / 10);
            Within(bot.Key, "utvecklingsvärde", expected.Value, bot.Value, expected.Value / 20);
            for (int force = 0; force < 3; force++)
            {
                Within(bot.Key, "använd kraft", expected.Used[force], bot.Used[force], 60);
                Within(bot.Key, "sparad kraft", expected.Saved[force], bot.Saved[force], 60);
            }

            Assert.Equal(expected.PerForce is null, bot.PerForce is null);
            if (expected.PerForce is decimal perForce)
            {
                Assert.True(Math.Abs(perForce - bot.PerForce!.Value) <= 0.07m, $"{bot.Key}: värde per använd kraft {bot.PerForce} against {perForce}.");
            }
        }
    }

    [Fact]
    public void TheBotsWeekIsTheOneEconomyV1Reports()
    {
        var documented = Rows(BotTable);

        var played = s_week.Value.Select(Row.Of).ToList();

        Assert.True(
            played.Count == documented.Count && played.All(row => documented.TryGetValue(row.Key, out var doc) && doc == row),
            "The bots no longer play the week section 7.1 of docs/design/economy-v1.md shows. If the change is meant, " +
            "replace the rows of the table in section 7.1 with these (the tool's own report has more columns):\n" + string.Join('\n', played.Select(r => r.ToMarkdown())));
    }

    [Fact]
    public void StrategyCatchesUpWithSelfCare()
    {
        // D-34 (forces.md): the thoughtful strategies get at least 1.3 times the naive strategy's value per force.
        decimal naive = Row.Of(s_week.Value.Single(r => r.Spec.Strategy == Strategy.Naive)).PerForce!.Value;

        Assert.All(
            s_week.Value.Where(r => r.Spec.Strategy == Strategy.Near || r.Spec.Strategy == Strategy.Sea),
            run => Assert.True(Row.Of(run).PerForce >= naive * 1.3m, $"{run.Spec.Strategy.Name}: {Row.Of(run).PerForce} against naive {naive}."));
    }

    private static void Within(string key, string measure, long expected, long actual, long tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"{key}: {measure} {actual} against {expected} ± {tolerance}.");

    /// <summary>The rows of the first table after <paramref name="heading"/> in economy-v1.md, by run and self-care.</summary>
    private static Dictionary<string, Row> Rows(string heading)
    {
        string[] lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), "docs", "design", "economy-v1.md"), Encoding.UTF8);
        int at = Array.IndexOf(lines, heading);
        Assert.True(at >= 0, $"economy-v1.md has no heading '{heading}'.");
        var table = lines.Skip(at + 1).SkipWhile(l => !l.StartsWith('|')).TakeWhile(l => l.StartsWith('|')).Skip(2);
        return table.Select(Row.Parse).ToDictionary(r => r.Key, StringComparer.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Eirora.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("The tests run outside the repository.");
    }

    /// <summary>A row of section 7's tables: | Körning | Egenvård | Bybor | Byggnader | Öar | Sten i lager | Dagsproduktion | Utvecklingsvärde | Använt U/S/O | Sparat U/S/O | Värde per använd kraft |.</summary>
    private sealed record Row(string Run, string SelfCare, int Villagers, int Buildings, int Islands, long Stone, long DailyOutput, long Value, string UsedText, string SavedText, decimal? PerForce)
    {
        public string Key => $"{Run}, {SelfCare}";

        public long[] Used => Forces(UsedText);

        public long[] Saved => Forces(SavedText);

        public static Row Of(RunResult run) =>
            new(
                run.Spec.Strategy.Name,
                run.Spec.Profile == SelfCareProfile.Normal ? "måttlig" : run.Spec.Profile.Name,
                run.Villagers,
                run.Buildings,
                run.Islands,
                run.Stock[(int)Resource.Stone],
                Whole(run.DailyOutput),
                Whole(run.Value),
                string.Join('/', run.Used),
                string.Join('/', run.Saved),
                run.ValuePerForceHundredths / 100m);

        public static Row Parse(string line)
        {
            string[] cells = [.. line.Trim('|').Split('|').Select(c => c.Trim())];
            return new Row(
                cells[0],
                cells[1],
                int.Parse(cells[2], CultureInfo.InvariantCulture),
                int.Parse(cells[3], CultureInfo.InvariantCulture),
                int.Parse(cells[4], CultureInfo.InvariantCulture),
                Number(cells[5]),
                Number(cells[6]),
                Number(cells[7]),
                cells[8],
                cells[9],
                cells[10] == "–" ? null : decimal.Parse(cells[10].Replace(',', '.'), CultureInfo.InvariantCulture));
        }

        public string ToMarkdown() => string.Create(
            CultureInfo.InvariantCulture,
            $"| {Run} | {SelfCare} | {Villagers} | {Buildings} | {Islands} | {Grouped(Stone)} | {Grouped(DailyOutput)} | {Grouped(Value)} | {UsedText} | {SavedText} | {(PerForce is decimal p ? p.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',') : "–")} |");

        /// <summary>Millionths rounded to whole units, as the report rounds them.</summary>
        private static long Whole(long millionths) => (millionths + (Quantity.PerUnit / 2)) / Quantity.PerUnit;

        /// <summary>A number as the design documents write it: 1 320.</summary>
        private static string Grouped(long value) => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');

        private static long Number(string cell) => long.Parse(cell.Replace(" ", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);

        private static long[] Forces(string cell) => [.. cell.Split('/').Select(f => long.Parse(f, CultureInfo.InvariantCulture))];
    }
}
