using Eirora.Simulation.Map;
using FsCheck.Xunit;

namespace Eirora.Simulation.Tests.Map;

/// <summary>The shared hex geometry (TASK-028): coordinates, distance, rings, areas, lines and discs.</summary>
public sealed class HexTests
{
    private const int Span = 40;

    [Property(MaxTest = 300)]
    public bool DistanceIsTheNumberOfStepsOnAnOpenMap(int a, int b, int c, int d)
    {
        var from = At(a, b);
        var to = At(c, d);

        return from.DistanceTo(to) == StepsOnOpenMap(from, to);
    }

    [Property(MaxTest = 300)]
    public bool DistanceIsSymmetricAndObeysTheTriangleInequality(int a, int b, int c, int d, int e, int f)
    {
        var x = At(a, b);
        var y = At(c, d);
        var z = At(e, f);

        return x.DistanceTo(y) == y.DistanceTo(x) && x.DistanceTo(z) <= x.DistanceTo(y) + y.DistanceTo(z);
    }

    [Property(MaxTest = 100)]
    public bool EveryNeighbourIsOneStepAwayAndTheyAreAllDifferent(int a, int b)
    {
        var hex = At(a, b);
        var neighbours = Enumerable.Range(0, Hex.Directions).Select(hex.Neighbour).ToList();

        return neighbours.All(n => hex.DistanceTo(n) == 1) && neighbours.Distinct().Count() == Hex.Directions;
    }

    [Property(MaxTest = 100)]
    public bool CubeCoordinatesRoundTrip(int a, int b)
    {
        var hex = At(a, b);

        return hex.Q + hex.R + hex.S == 0 && Hex.FromCube(hex.Q, hex.R, hex.S) == hex;
    }

    [Fact]
    public void CubeCoordinatesThatDoNotAddUpToZeroAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Hex.FromCube(1, 1, 1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 6)]
    [InlineData(2, 12)]
    [InlineData(24, 144)]
    public void ARingHoldsEveryHexAtExactlyThatDistance(int radius, int count)
    {
        var center = new Hex(3, -2);

        var ring = center.Ring(radius).ToList();

        Assert.Equal(count, ring.Count);
        Assert.Equal(count, ring.Distinct().Count());
        Assert.All(ring, hex => Assert.Equal(radius, center.DistanceTo(hex)));
    }

    [Fact]
    public void ARingWalksCounterClockwiseFromTheSouthWest()
    {
        var ring = Hex.Origin.Ring(1).ToList();

        Assert.Equal(new[] { new Hex(-1, 1), new Hex(0, 1), new Hex(1, 0), new Hex(1, -1), new Hex(0, -1), new Hex(-1, 0) }, ring);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(24)]
    public void AnAreaHoldsEveryHexWithinTheDistanceInMapOrder(int radius)
    {
        var center = new Hex(-4, 7);

        var area = center.Within(radius).ToList();

        Assert.Equal(HexDisc.CountWithin(radius), area.Count);
        Assert.All(area, hex => Assert.True(center.DistanceTo(hex) <= radius));
        Assert.Equal(area.OrderBy(hex => hex).ToList(), area);
        Assert.Equal(area.Count, area.Distinct().Count());
    }

    [Property(MaxTest = 300)]
    public bool ALineTakesOneNeighbourPerStepFromEndToEnd(int a, int b, int c, int d)
    {
        var from = At(a, b);
        var to = At(c, d);

        var line = from.LineTo(to);

        return line.Length == from.DistanceTo(to) + 1
            && line[0] == from
            && line[^1] == to
            && line.Zip(line.Skip(1)).All(pair => pair.First.DistanceTo(pair.Second) == 1);
    }

    [Fact]
    public void ALineAlongAnEdgeAlwaysTakesTheSameSide()
    {
        // From (0, 0) to (2, -1) the line runs exactly between (1, 0) and (1, -1).
        Assert.Equal(new[] { new Hex(0, 0), new Hex(1, 0), new Hex(2, -1) }, new Hex(0, 0).LineTo(new Hex(2, -1)));
        Assert.Equal(new[] { new Hex(2, -1), new Hex(1, 0), new Hex(0, 0) }, new Hex(2, -1).LineTo(new Hex(0, 0)));
    }

    [Fact]
    public void HexesAreOrderedByRowAndThenColumn()
    {
        var hexes = new[] { new Hex(2, 1), new Hex(-3, 1), new Hex(5, -1), new Hex(0, 0) };

        Assert.Equal(new[] { new Hex(5, -1), new Hex(0, 0), new Hex(-3, 1), new Hex(2, 1) }, hexes.OrderBy(hex => hex));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 7)]
    [InlineData(24, 1801)]
    public void ADiscNumbersEveryHexInMapOrder(int radius, int count)
    {
        var disc = new HexDisc(radius);

        Assert.Equal(count, disc.Count);
        Assert.Equal(Hex.Origin.Within(radius), Enumerable.Range(0, disc.Count).Select(i => disc[i]));
        Assert.All(Enumerable.Range(0, disc.Count), i => Assert.Equal(i, disc.IndexOf(disc[i])));
    }

    [Fact]
    public void AHexOutsideTheDiscHasNoNumber()
    {
        var disc = new HexDisc(24);

        Assert.Equal(-1, disc.IndexOf(new Hex(25, 0)));
        Assert.Equal(-1, disc.IndexOf(new Hex(13, 12)));
        Assert.Equal(-1, disc.IndexOf(new Hex(-24, -1)));
        Assert.True(disc.Contains(new Hex(12, 12)));
    }

    private static Hex At(int a, int b) => new(Math.Abs(a % Span) - (Span / 2), Math.Abs(b % Span) - (Span / 2));

    /// <summary>Breadth-first search over neighbours: the reference that distance must agree with.</summary>
    private static int StepsOnOpenMap(Hex from, Hex to)
    {
        var steps = new Dictionary<Hex, int> { [from] = 0 };
        var queue = new Queue<Hex>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var hex = queue.Dequeue();
            if (hex == to)
            {
                return steps[hex];
            }

            for (int direction = 0; direction < Hex.Directions; direction++)
            {
                var next = hex.Neighbour(direction);
                if (!steps.ContainsKey(next))
                {
                    steps[next] = steps[hex] + 1;
                    queue.Enqueue(next);
                }
            }
        }

        throw new InvalidOperationException("An open map always has a way.");
    }
}
