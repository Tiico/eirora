using Eirora.Simulation.Forces;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The simulation package compiles inside Unity and gives the same results as on .NET (NFR-02).
    /// The expected values are the worked examples in docs/design/forces.md, also asserted by Eirora.Simulation.Tests.
    /// </summary>
    public class SimulationInUnityTests
    {
        [TestCase(0, 0)]
        [TestCase(7_500, 86)]
        [TestCase(15_000, 103)]
        [TestCase(50_000, 110)]
        public void Exploration_matches_dotnet(int steps, int expected) =>
            Assert.AreEqual(expected, ForceRules.Exploration(steps));

        [TestCase(null, 40)]
        [TestCase(390, 92)]
        [TestCase(720, 100)]
        public void DawnPower_matches_dotnet(int? sleepMinutes, int expected) =>
            Assert.AreEqual(expected, ForceRules.DawnPower(sleepMinutes));

        [Test]
        public void HexPrism_has_flat_faces()
        {
            var mesh = HexMesh.Prism(radius: 1f, height: 0.5f);

            Assert.AreEqual(6 + (6 * 4), mesh.vertexCount);
            Assert.AreEqual((4 + (6 * 2)) * 3, mesh.triangles.Length);
        }
    }
}
