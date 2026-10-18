using System.Globalization;
using Eirora.Simulation.Map;
using Eirora.WorldGeneration;
using Eirora.WorldGeneration.Validation;

// Usage:
//   dotnet run --project tools/Eirora.MapCheck -- [--generator prototype-home] [--version 1] [--from 0] [--seeds 10000]
//   dotnet run --project tools/Eirora.MapCheck -- --seed 42     (the report of one map, rule by rule)
var options = Options(args);
var generator = WorldGenerators.Get(options.GetValueOrDefault("generator", PrototypeHome.GeneratorId), int.Parse(options.GetValueOrDefault("version", "1"), CultureInfo.InvariantCulture));

if (options.TryGetValue("seed", out string? seed))
{
    var report = MapValidator.Validate(generator.Generate(ulong.Parse(seed, CultureInfo.InvariantCulture)));
    Console.Write(report.ToText());
    return report.Passed ? 0 : 1;
}

var bulk = BulkValidation.Run(
    generator,
    ulong.Parse(options.GetValueOrDefault("from", "0"), CultureInfo.InvariantCulture),
    int.Parse(options.GetValueOrDefault("seeds", "10000"), CultureInfo.InvariantCulture));
Console.Write(bulk.ToText());
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
