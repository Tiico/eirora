using System.Collections.Generic;
using Eirora.Simulation.Map;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The shared pathfinder chooses the same path among equally cheap ones inside Unity as on .NET (TASK-028, NFR-02);
    /// PathFinderTests in Eirora.Simulation.Tests asserts the same path.
    /// </summary>
    public sealed class PathFinderTiesTests
    {
        [Test]
        public void AmongEquallyCheapPathsUnityChoosesTheSameAsDotNet()
        {
            var path = new List<Hex>();

            new PathFinder(new HexDisc(24)).TryFind(new Hex(-3, 2), new Hex(2, -1), new OpenSea(), path, out int cost);

            Assert.That(cost, Is.EqualTo(5));
            Assert.That(string.Join(" ", path), Is.EqualTo("(-3, 2) (-2, 1) (-1, 0) (0, -1) (1, -1) (2, -1)"));
        }

        [Test]
        public void ALineAlongAnEdgeTakesTheSameSideAsOnDotNet()
        {
            Assert.That(new Hex(0, 0).LineTo(new Hex(2, -1)), Is.EqualTo(new[] { new Hex(0, 0), new Hex(1, 0), new Hex(2, -1) }));
        }

        private readonly struct OpenSea : IStepCost
        {
            public int MinimumCost => 1;

            public int Cost(Hex origin, Hex neighbour) => 1;
        }
    }
}
