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
        private const string PrototypeHomeHash = "325611d2e9d0e003617871263295e1dc809663bdee6d2d3d500f793e6c8760ed";

        private static readonly uint[] s_reference = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };

        [Test]
        public void UnityDrawsTheReferenceNumbers()
        {
            var random = new Pcg32(42, 54);

            Assert.That(Enumerable.Range(0, s_reference.Length).Select(_ => random.Next()), Is.EqualTo(s_reference));
            Assert.That(Pcg32.Fnv1a64("foobar"), Is.EqualTo(0x85944171f73967e8UL));
        }

        [Test]
        public void UnityMakesTheSameGeneratedMapAsDotNet()
        {
            // procedural-archipelago 1 from seed 20261018 (0x135289a), in maps.txt.
            var map = WorldGenerators.Get(ProceduralArchipelagoV1.GeneratorId, 1).Generate(20261018);

            Assert.That(map.Hash, Is.EqualTo("676177b153294e50c9a691ae2efcfb1130743740acafe870e76e2f015e36a8c7"));
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
