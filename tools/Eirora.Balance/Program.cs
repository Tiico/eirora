using System.Globalization;
using Eirora.Balance;

// Usage:
//   dotnet run -c Release --project tools/Eirora.Balance                       (economy v1's week and 20 generated maps for four weeks)
//   dotnet run -c Release --project tools/Eirora.Balance -- --suite economy-v1 (only the runs of economy v1, section 7)
//   dotnet run -c Release --project tools/Eirora.Balance -- --suite generated --from 0 --seeds 20 --days 28
//   dotnet run -c Release --project tools/Eirora.Balance -- --maps generated --from 100 --seeds 5 --strategies near,sea,naive --profiles normal,low --days 56
// Writes balance.tsv and balance.md to --out (artifacts/balance by default) and the Markdown to standard output.
var options = Options(args);
ulong from = ulong.Parse(options.GetValueOrDefault("from", "0"), CultureInfo.InvariantCulture);
int seeds = int.Parse(options.GetValueOrDefault("seeds", "20"), CultureInfo.InvariantCulture);
string output = options.GetValueOrDefault("out", Path.Combine("artifacts", "balance"));

List<RunSpec> specs;
if (options.ContainsKey("strategies") || options.ContainsKey("profiles") || options.ContainsKey("maps"))
{
    // Every strategy with every profile on every map.
    var maps = options.GetValueOrDefault("maps", "prototype") == "prototype"
        ? [MapChoice.Prototype]
        : Enumerable.Range(0, seeds).Select(i => MapChoice.Generated(from + (ulong)i)).ToList();
    int days = int.Parse(options.GetValueOrDefault("days", "7"), CultureInfo.InvariantCulture);
    specs =
    [
        .. from map in maps
           from strategy in List(options, "strategies", "near,sea").Select(Strategy.Get)
           from profile in List(options, "profiles", "normal").Select(SelfCareProfile.Get)
           select new RunSpec(map, strategy, profile, days),
    ];
}
else
{
    string suite = options.GetValueOrDefault("suite", "all");
    int days = int.Parse(options.GetValueOrDefault("days", "28"), CultureInfo.InvariantCulture);
    specs = suite switch
    {
        "economy-v1" => [.. Suites.EconomyV1()],
        "generated" => [.. Suites.Generated(from, seeds, days)],
        "all" => [.. Suites.EconomyV1(), .. Suites.Generated(from, seeds, days)],
        _ => throw new ArgumentException($"There is no suite {suite}; choose economy-v1, generated or all."),
    };
}

var runs = specs.Select(BalanceRun.Play).ToList();
Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output, "balance.tsv"), Report.Tsv(runs));
string markdown = Report.Markdown(runs);
File.WriteAllText(Path.Combine(output, "balance.md"), markdown);
Console.Write(markdown);
Console.Error.WriteLine($"Wrote {runs.Count} runs to {Path.Combine(output, "balance.tsv")} and balance.md.");
return 0;

static Dictionary<string, string> Options(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int i = 0; i + 1 < args.Length; i += 2)
    {
        options[args[i].TrimStart('-')] = args[i + 1];
    }

    return options;
}

static string[] List(Dictionary<string, string> options, string name, string fallback) =>
    options.GetValueOrDefault(name, fallback).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
