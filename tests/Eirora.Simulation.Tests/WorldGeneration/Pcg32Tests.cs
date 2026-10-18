using Eirora.WorldGeneration;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.WorldGeneration;

/// <summary>
/// The generators' random numbers against the published test vectors (ADR-0010, TASK-029). The Unity EditMode tests
/// check the same vectors (Pcg32VectorTests), so .NET and Unity draw the same numbers.
/// </summary>
public sealed class Pcg32Tests
{
    // pcg32-demo from the reference implementation (pcg-c-basic), seeded with pcg32_srandom_r(&rng, 42u, 54u).
    private static readonly uint[] s_reference = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];

    [Fact]
    public void TheFirstNumbersAreTheReferenceImplementations()
    {
        var random = new Pcg32(42, 54);

        Assert.Equal(s_reference, Enumerable.Range(0, s_reference.Length).Select(_ => random.Next()));
    }

    [Theory]
    [InlineData("", 0xcbf29ce484222325UL)]
    [InlineData("a", 0xaf63dc4c8601ec8cUL)]
    [InlineData("foobar", 0x85944171f73967e8UL)]
    public void StepsAreNamedWithTheFnv1aHash(string text, ulong expected)
    {
        Assert.Equal(expected, Pcg32.Fnv1a64(text));
    }

    [Fact]
    public void EveryStepHasAStreamOfItsOwn()
    {
        var land = Pcg32.ForStep(7, "land");
        var rivers = Pcg32.ForStep(7, "rivers");
        var landAgain = Pcg32.ForStep(7, "land");

        var first = Enumerable.Range(0, 8).Select(_ => land.Next()).ToList();

        Assert.NotEqual(first, Enumerable.Range(0, 8).Select(_ => rivers.Next()));
        Assert.Equal(first, Enumerable.Range(0, 8).Select(_ => landAgain.Next()));
    }

    [Property(MaxTest = 200)]
    public bool BoundedNumbersStayWithinTheirBounds(ulong seed, int a, int b)
    {
        int minimum = Math.Min(a, b) / 2;
        int maximum = Math.Max(a, b) / 2;
        var random = new Pcg32(seed, 1);

        return Enumerable.Range(0, 50).Select(_ => random.Between(minimum, maximum)).All(n => n >= minimum && n <= maximum);
    }

    [Fact]
    public void BoundedNumbersAreSpreadEvenly()
    {
        var random = new Pcg32(20261018, 3);
        var counts = new int[6];

        for (int i = 0; i < 60_000; i++)
        {
            counts[random.Below(6)]++;
        }

        Assert.All(counts, count => Assert.InRange(count, 9_500, 10_500));
    }
}
