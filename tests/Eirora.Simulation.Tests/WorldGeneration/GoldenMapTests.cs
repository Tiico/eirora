using System.Globalization;
using System.Text;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.WorldGeneration;

namespace Eirora.Simulation.Tests.WorldGeneration;

/// <summary>
/// Golden maps (ADR-0010): for every generator version, the hash of the map it makes from each of a few seeds. A locked
/// generator never changes, so a change to its code that changes a single byte of a map fails here. A new generator's
/// golden maps are written when missing, to be reviewed and committed, like golden saves. The founding each map gives
/// is pinned the same way.
/// </summary>
public sealed class GoldenMapTests
{
    private static readonly ulong[] s_seeds = [0, 1, 20261018, ulong.MaxValue];

    private static readonly string s_folder = Path.Combine(Repository.Root, "tests", "Eirora.Simulation.Tests", "WorldGeneration", "Golden");
    private static readonly string s_hashes = Path.Combine(s_folder, "maps.txt");
    private static readonly string s_foundings = Path.Combine(s_folder, "foundings.txt");

    public static TheoryData<string, int> Generators => [.. WorldGenerators.All.Select(g => (g.Id, g.Version))];

    [Theory]
    [MemberData(nameof(Generators))]
    public void EveryGeneratorVersionMakesItsGoldenMaps(string id, int version)
    {
        var generator = WorldGenerators.Get(id, version);
        var expected = Golden();
        var made = s_seeds.ToDictionary(seed => Key(id, version, seed), seed => generator.Generate(seed).Hash);

        var missing = made.Where(m => !expected.ContainsKey(m.Key)).ToList();
        if (missing.Count > 0)
        {
            Directory.CreateDirectory(s_folder);
            File.AppendAllLines(s_hashes, missing.Select(m => $"{m.Key} {m.Value}"), new UTF8Encoding(false));
            Assert.Fail($"Wrote golden maps for {id} version {version}; review them and commit them.");
        }

        Assert.All(made, m => Assert.Equal(expected[m.Key], m.Value));
    }

    // A save on a generated map reads its surveys and its expedition's island from the founding rule, which is never
    // stored: a change to the rule would quietly change worlds already played (economy-v1.md, Founding).
    [Theory]
    [MemberData(nameof(Generators))]
    public void EveryGeneratorVersionFoundsItsGoldenWorlds(string id, int version)
    {
        var generator = WorldGenerators.Get(id, version);
        var expected = File.Exists(s_foundings)
            ? File.ReadAllLines(s_foundings, Encoding.UTF8).Where(l => l.Length > 0).Select(l => l.Split(' ', 4)).ToDictionary(f => $"{f[0]} {f[1]} {f[2]}", f => f[3])
            : [];
        var founded = s_seeds.ToDictionary(seed => Key(id, version, seed), seed => Describe(Founding.For(generator.Generate(seed))));

        var missing = founded.Where(f => !expected.ContainsKey(f.Key)).ToList();
        if (missing.Count > 0)
        {
            Directory.CreateDirectory(s_folder);
            File.AppendAllLines(s_foundings, missing.Select(f => $"{f.Key} {f.Value}"), new UTF8Encoding(false));
            Assert.Fail($"Wrote golden foundings for {id} version {version}; review them and commit them.");
        }

        Assert.All(founded, f => Assert.Equal(expected[f.Key], f.Value));
    }

    [Fact]
    public void TheGoldenPrototypeMapIsTheMapEveryPrototypeWorldIsOn()
    {
        string path = Path.Combine(s_folder, "prototype-home-1.map");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(s_folder);
            File.WriteAllText(path, PrototypeHome.Map.ToText(), new UTF8Encoding(false));
            Assert.Fail("Wrote the golden prototype map; review it and commit it.");
        }

        string golden = File.ReadAllText(path, Encoding.UTF8);

        Assert.Equal(golden, PrototypeHome.Map.ToText());
        Assert.Equal(PrototypeHome.Map.Hash, WorldMap.Parse(golden).Hash);
    }

    [Fact]
    public void AGeneratorsMapNamesItsGeneratorAndSource()
    {
        Assert.All(WorldGenerators.All, generator =>
        {
            var map = generator.Generate(1);
            Assert.Equal((generator.Id, generator.Version, generator.Source), (map.GeneratorId, map.GeneratorVersion, map.Source));
        });
    }

    [Fact]
    public void AnUnknownGeneratorVersionIsNotMadeUp()
    {
        Assert.Throws<ArgumentException>(() => WorldGenerators.Get(PrototypeHome.GeneratorId, 99));
    }

    private static Dictionary<string, string> Golden() =>
        File.Exists(s_hashes)
            ? File.ReadAllLines(s_hashes, Encoding.UTF8).Where(l => l.Length > 0).Select(l => l.Split(' ')).ToDictionary(f => $"{f[0]} {f[1]} {f[2]}", f => f[3])
            : [];

    /// <summary>A founding on one line: the buildings, each survey's cost and tiles, and the expedition's island.</summary>
    private static string Describe(Founding founding)
    {
        static string At(Hex hex) => string.Create(CultureInfo.InvariantCulture, $"{hex.Q},{hex.R}");
        var parts = new List<string> { string.Join(" ", founding.StartBuildings.Select(b => $"{b.Kind}@{At(b.Tile)}")) };
        parts.AddRange(founding.Surveys.Select(s => string.Create(CultureInfo.InvariantCulture, $"survey {s.Cost}: {string.Join(" ", s.Reveals.Select(At))}")));
        parts.Add(founding.ExpeditionFinds is Hex finds ? $"expedition {founding.ExpeditionIsland}@{At(finds)}" : "no expedition");
        return string.Join(" | ", parts);
    }

    private static string Key(string id, int version, ulong seed) =>
        string.Create(CultureInfo.InvariantCulture, $"{id} {version} {seed:x16}");
}
