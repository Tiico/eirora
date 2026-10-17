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
}
