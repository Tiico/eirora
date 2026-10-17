using System;
using System.IO;
using Eirora.Client.App;
using Eirora.Simulation.Time;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>Saving and loading the prototype's world on the device (TASK-014, ADR-0006).</summary>
    public sealed class GameSessionTests
    {
        private string _folder = string.Empty;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "eirora-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void DeleteFolder() => Directory.Delete(_folder, recursive: true);

        [Test]
        public void ASavedWorldIsTheSameWorldAfterARestart()
        {
            string path = Path.Combine(_folder, "world.journal");
            var clock = new FixedClock(new TimeReading(1_792_000_000, "Europe/Stockholm", 7200));
            var first = new GameSession(clock, path);
            first.Begin(new RestSchedule(23 * 60, 8 * 60));

            var second = new GameSession(clock, path);

            Assert.That(second.TryLoad(), Is.True);
            Assert.That(second.World!.Fingerprint(), Is.EqualTo(first.World!.Fingerprint()));
        }

        [Test]
        public void AJournalThisBuildCannotReadIsNeitherLoadedNorOverwritten()
        {
            string path = Path.Combine(_folder, "world.journal");
            const string FromOtherRules = "eirora-journal 1 rules 999\ncreate 0 Europe/Stockholm 0 1380 480\n";
            File.WriteAllText(path, FromOtherRules);
            var session = new GameSession(new FixedClock(new TimeReading(1_792_000_000, "Europe/Stockholm", 7200)), path);

            Assert.That(session.TryLoad(), Is.False);
            Assert.That(session.LoadProblem, Is.Not.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo(FromOtherRules));
        }

        [Test]
        public void StartingAnewKeepsTheUnreadableJournalAside()
        {
            string path = Path.Combine(_folder, "world.journal");
            File.WriteAllText(path, "not a journal");
            var session = new GameSession(new FixedClock(new TimeReading(1_792_000_000, "Europe/Stockholm", 7200)), path);
            session.TryLoad();

            string? aside = session.SetAsideSave();
            session.Begin(new RestSchedule(23 * 60, 8 * 60));

            Assert.That(File.ReadAllText(aside!), Is.EqualTo("not a journal"));
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
