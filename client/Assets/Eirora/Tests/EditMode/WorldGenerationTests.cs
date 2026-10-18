using System.Linq;
using Eirora.Simulation.Map;
using Eirora.WorldGeneration;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// Unity draws the same random numbers and makes the same maps as .NET (TASK-029, ADR-0010): the same test vectors
    /// as Pcg32Tests and the same golden map as GoldenMapTests in Eirora.Simulation.Tests.
    /// </summary>
    public sealed class WorldGenerationTests
    {
        // tests/Eirora.Simulation.Tests/WorldGeneration/Golden/maps.txt.
        private const string PrototypeHomeHash = "235f3ca98f52f6f90c0b4de7af1cedac3cd78a66ca2b95309a49771d2ec8f3eb";

        private static readonly uint[] s_reference = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };

        [Test]
        public void UnityDrawsTheReferenceNumbers()
        {
            var random = new Pcg32(42, 54);

            Assert.That(Enumerable.Range(0, s_reference.Length).Select(_ => random.Next()), Is.EqualTo(s_reference));
            Assert.That(Pcg32.Fnv1a64("foobar"), Is.EqualTo(0x85944171f73967e8UL));
        }

        [Test]
        public void UnityMakesTheGoldenPrototypeMap()
        {
            var map = WorldGenerators.Get(PrototypeHome.GeneratorId, PrototypeHome.GeneratorVersion).Generate(0);

            Assert.That(map.Hash, Is.EqualTo(PrototypeHomeHash));
            Assert.That(WorldMap.Parse(map.ToText()).Hash, Is.EqualTo(PrototypeHomeHash));
        }
    }
}
