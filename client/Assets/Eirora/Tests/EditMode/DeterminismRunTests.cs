using System.IO;
using System.Linq;
using Eirora.Client.App;
using Eirora.Simulation;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>The determinism build reports the same hashes as .NET, in the same order (TASK-021).</summary>
    public sealed class DeterminismRunTests
    {
        [Test]
        public void TheCorpusGivesEachReplaysStateHashInOrdinalOrder()
        {
            var replays = Directory.GetFiles(DeterminismRun.CorpusFolder, "*.replay")
                .Select(path => (Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)))
                .Reverse()
                .ToList();

            var hashes = DeterminismRun.Run(replays);

            Assert.That(hashes.Select(h => h.Name), Is.EqualTo(replays.Select(r => r.Item1).OrderBy(n => n, System.StringComparer.Ordinal)));
            Assert.That(hashes, Has.All.Matches<(string Name, string Hash)>(h => h.Hash == Replay.StateHash(replays.Single(r => r.Item1 == h.Name).Item2)));
        }
    }
}
