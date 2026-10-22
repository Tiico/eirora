namespace Eirora.Simulation.Tests.Architecture.Fixtures;

/// <summary>
/// Deliberately breaks every purity rule, so <see cref="PurityScanner"/> is proven to catch each one.
/// Never referenced at runtime.
/// </summary>
internal static class ImpureFixture
{
    private static double s_storedFloat;

    public static long ReadsWallClock() => DateTime.UtcNow.Ticks;

    public static int ReadsSystemRandomness() => new Random().Next();

    public static Guid MakesGuid() => Guid.NewGuid();

    public static bool ReadsFiles() => File.Exists("save.json");

    public static string ReadsLocalTimeZone() => TimeZoneInfo.Local.Id;

    public static int HashesString(string text) => text.GetHashCode(StringComparison.Ordinal);

    public static long ComputesWithFloats(long amount)
    {
        s_storedFloat = amount * 0.5;
        return (long)s_storedFloat;
    }

    public static int CallsMath(int value) => Math.Abs(value);

    public static int ReadsTickCount() => Environment.TickCount;

    public static IEnumerable<int> IteratesWithYield()
    {
        yield return 1;
    }

    public static IObservable<int>? UsesInterfaceNamedLikeSystemIo() => null;

    public static int ReadsTheRuntime() => typeof(object).Assembly.GetName().Name!.Length;

#pragma warning disable CA1304, CA1305, CA1307, CA1309, CA1310, CA1311, CA1862 // Deliberately culture-dependent.
    public static string FormatsNumberByCulture(int value) => value.ToString();

    public static int ParsesNumberByCulture(string text) => int.Parse(text);

    public static int ComparesStringsByCulture(string a, string b) => string.Compare(a, b);

    public static bool StartsWithByCulture(string text) => text.StartsWith("xy");

    public static string LowersByCulture(string text) => text.ToLower();

    public static SortedSet<string> SortsStringsByCulture() => [];

    public static string AppendsNumberByCulture(System.Text.StringBuilder text, int value) => text.Append(value).ToString();

    public static List<string> OrdersStringsByCulture(IEnumerable<string> names) => names.OrderBy(n => n).ToList();

    public static void SortsStringListByCulture(List<string> names) => names.Sort();

    public static void SortsStringArrayByCulture(string[] names) => Array.Sort(names);

#pragma warning restore CA1304, CA1305, CA1307, CA1309, CA1310, CA1311, CA1862

    public static string FormatsNumberInvariantly(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static bool FindsCharacterOrdinally(string text) => text.IndexOf('x') == 1;

    public static SortedSet<string> SortsStringsOrdinally() => new(StringComparer.Ordinal);

    public static string AppendsNumberInvariantly(System.Text.StringBuilder text, int value) =>
        text.Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToString();

    public static List<string> OrdersStringsOrdinally(IEnumerable<string> names) => names.OrderBy(n => n, StringComparer.Ordinal).ToList();

    public static int IteratesDictionary(Dictionary<string, int> values)
    {
        int sum = 0;
        foreach (var entry in values)
        {
            sum += entry.Value;
        }

        return sum;
    }

    public static int IteratesDictionaryKeys(Dictionary<string, int> values) => values.Keys.Count(k => k.Length > 1);

    public static int IteratesReadOnlyDictionary(IReadOnlyDictionary<string, int> values)
    {
        int sum = 0;
        foreach (var entry in values)
        {
            sum += entry.Value;
        }

        return sum;
    }

    public static int IteratesReadOnlyDictionaryValues(IReadOnlyDictionary<string, int> values) => values.Values.Count(v => v > 1);

    public static int QueriesDictionary(Dictionary<string, int> values) => values.Count(entry => entry.Value > 1);

    public static List<KeyValuePair<string, int>> CopiesDictionary(IReadOnlyDictionary<string, int> values) => new(values);

    public static int IteratesSortedDictionary(SortedDictionary<string, int> values)
    {
        int sum = 0;
        foreach (var entry in values)
        {
            sum += entry.Value;
        }

        return sum;
    }
}
