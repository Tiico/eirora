using System.Globalization;

namespace Eirora.Balance;

/// <summary>
/// The tool's arguments: a named suite, or every strategy with every profile on every map. Anything the tool would
/// otherwise ignore or guess at is an error, so that a typo never plays other runs than were asked for.
/// </summary>
public sealed class CommandLine
{
    public const string Usage =
        "Usage: Eirora.Balance [--suite all|economy-v1|generated] [--from N] [--seeds N] [--days N] [--out DIR]\n" +
        "       Eirora.Balance --maps prototype|generated [--from N --seeds N] [--strategies a,b] [--profiles a,b] [--days N] [--out DIR]\n" +
        "--from, --seeds and --days apply to generated maps; economy-v1 always plays seven days on the prototype's map.";

    private static readonly string[] s_suiteOptions = ["suite", "from", "seeds", "days", "out"];
    private static readonly string[] s_combinationOptions = ["maps", "strategies", "profiles", "from", "seeds", "days", "out"];

    private CommandLine(IReadOnlyList<RunSpec> runs, string output)
    {
        Runs = runs;
        Output = output;
    }

    public IReadOnlyList<RunSpec> Runs { get; }

    /// <summary>The folder the report goes to.</summary>
    public string Output { get; }

    /// <exception cref="ArgumentException">An argument is unknown, repeated, missing its value or out of place.</exception>
    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = Options(args);
        string output = options.GetValueOrDefault("out", Path.Combine("artifacts", "balance"));
        bool combination = options.ContainsKey("maps") || options.ContainsKey("strategies") || options.ContainsKey("profiles");
        Allow(options, combination ? s_combinationOptions : s_suiteOptions);
        if (combination)
        {
            return new CommandLine(Combinations(options), output);
        }

        string suite = options.GetValueOrDefault("suite", "all");
        if (suite == "economy-v1")
        {
            Allow(options, ["suite", "out"]);
            return new CommandLine(Suites.EconomyV1(), output);
        }

        var generated = Suites.Generated(Unsigned(options, "from", 0), Count(options, "seeds", 20), Count(options, "days", 28));
        return suite switch
        {
            "generated" => new CommandLine(generated, output),
            "all" => new CommandLine([.. Suites.EconomyV1(), .. generated], output),
            _ => throw new ArgumentException($"There is no suite {suite}; choose all, economy-v1 or generated."),
        };
    }

    private static List<RunSpec> Combinations(Dictionary<string, string> options)
    {
        string maps = options.GetValueOrDefault("maps", "prototype");
        List<MapChoice> choices;
        if (maps == "prototype")
        {
            Allow(options, ["maps", "strategies", "profiles", "days", "out"]);
            choices = [MapChoice.Prototype];
        }
        else if (maps == "generated")
        {
            ulong from = Unsigned(options, "from", 0);
            choices = [.. Enumerable.Range(0, Count(options, "seeds", 20)).Select(i => MapChoice.Generated(from + (ulong)i))];
        }
        else
        {
            throw new ArgumentException($"There are no maps {maps}; choose prototype or generated.");
        }

        int days = Count(options, "days", 7);
        return
        [
            .. from map in choices
               from strategy in List(options, "strategies", "near,sea").Select(Strategy.Get)
               from profile in List(options, "profiles", "normal").Select(SelfCareProfile.Get)
               select new RunSpec(map, strategy, profile, days),
        ];
    }

    private static Dictionary<string, string> Options(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Count; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || args[i].Length == 2)
            {
                throw new ArgumentException($"Expected an option such as --suite, not '{args[i]}'.");
            }

            string name = args[i][2..];
            if (i + 1 == args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"--{name} needs a value.");
            }

            if (!options.TryAdd(name, args[i + 1]))
            {
                throw new ArgumentException($"--{name} is given twice.");
            }
        }

        return options;
    }

    private static void Allow(Dictionary<string, string> options, string[] allowed)
    {
        foreach (string name in options.Keys.Order(StringComparer.Ordinal))
        {
            if (!allowed.Contains(name))
            {
                throw new ArgumentException($"--{name} does not apply here; these do: {string.Join(", ", allowed.Select(a => "--" + a))}.");
            }
        }
    }

    private static ulong Unsigned(Dictionary<string, string> options, string name, ulong fallback) =>
        !options.TryGetValue(name, out string? text) ? fallback
        : ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) ? value
        : throw new ArgumentException($"--{name} takes a whole number, not '{text}'.");

    private static int Count(Dictionary<string, string> options, string name, int fallback) =>
        !options.TryGetValue(name, out string? text) ? fallback
        : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0 ? value
        : throw new ArgumentException($"--{name} takes a positive whole number, not '{text}'.");

    private static string[] List(Dictionary<string, string> options, string name, string fallback) =>
        options.GetValueOrDefault(name, fallback).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
