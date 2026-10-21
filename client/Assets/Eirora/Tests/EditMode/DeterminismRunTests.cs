using System;
using System.Collections;
using System.IO;
using System.Linq;
using Eirora.Client.App;
using Eirora.Simulation;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>The determinism build reports the same entries as .NET, in the same order (TASK-021).</summary>
    public sealed class DeterminismRunTests
    {
        [Test]
        public void TheCorpusGivesEachEntrysStateHashInOrdinalOrder()
        {
            var replays = Directory.GetFiles(DeterminismRun.ReplayFolder, "*.replay")
                .Select(path => (Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)))
                .Reverse()
                .ToList();
            var saves = Directory.GetFiles(DeterminismRun.GoldenSaveFolder, "*.save")
                .Select(path => (Path.GetFileNameWithoutExtension(path), File.ReadAllBytes(path)))
                .ToList();
            var journals = Directory.GetFiles(DeterminismRun.GoldenSaveFolder, "*.journal")
                .Select(path => (Path.GetFileNameWithoutExtension(path), File.ReadAllText(path)))
                .ToList();
            string maps = File.ReadAllText(DeterminismRun.GoldenMapList);

            var hashes = DeterminismRun.Run(replays, saves, journals, maps);

            Assert.That(hashes.Select(h => h.Name), Is.Ordered.Using((IComparer)StringComparer.Ordinal));
            Assert.That(hashes.Count(h => h.Name.StartsWith("replay/", StringComparison.Ordinal)), Is.EqualTo(replays.Count));
            Assert.That(hashes.Count(h => h.Name.StartsWith("save/", StringComparison.Ordinal)), Is.EqualTo(saves.Count));
            Assert.That(hashes.Count(h => h.Name.StartsWith("journal/", StringComparison.Ordinal)), Is.EqualTo(journals.Count));
            Assert.That(hashes.Count(h => h.Name.StartsWith("map/", StringComparison.Ordinal)), Is.EqualTo(maps.Split('\n').Count(line => line.Trim().Length > 0)));
            Assert.That(
                hashes.Where(h => h.Name.StartsWith("replay/", StringComparison.Ordinal)),
                Has.All.Matches<(string Name, string Hash)>(h => h.Hash == Replay.StateHash(replays.Single(r => "replay/" + r.Item1 == h.Name).Item2)));
        }
    }
}
