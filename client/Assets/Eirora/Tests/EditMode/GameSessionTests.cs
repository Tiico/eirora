using System;
using System.IO;
using System.Linq;
using System.Text;
using Eirora.Client.App;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Time;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>Saving and loading the world on the device (TASK-014, TASK-020, ADR-0006, NFR-01).</summary>
    public sealed class GameSessionTests
    {
        private static readonly RestSchedule Night = new(23 * 60, 8 * 60);

        private string _folder = string.Empty;

        private string SavePath => Path.Combine(_folder, GameSession.SaveFile);

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
            var first = Played();

            var second = Session();

            Assert.That(second.TryLoad(), Is.True);
            Assert.That(second.World!.Fingerprint(), Is.EqualTo(first.World!.Fingerprint()));
        }

        [Test]
        public void AWriteCutOffBeforeTheSwapLeavesTheLastCompleteSave()
        {
            var first = Played();
            byte[] complete = File.ReadAllBytes(SavePath);
            File.WriteAllBytes(SavePath + ".tmp", complete.Take(complete.Length / 2).ToArray());

            var second = Session();

            Assert.That(second.TryLoad(), Is.True);
            Assert.That(second.World!.Fingerprint(), Is.EqualTo(first.World!.Fingerprint()));
        }

        [Test]
        public void ADamagedSaveFallsBackToTheBackupAndIsKept()
        {
            var session = Played();
            string beforeLastCommand = session.World!.Fingerprint();
            session.Apply(new Survey());
            byte[] damaged = File.ReadAllBytes(SavePath).Take(100).ToArray();
            File.WriteAllBytes(SavePath, damaged);

            var second = Session();

            Assert.That(second.TryLoad(), Is.True);
            Assert.That(second.World!.Fingerprint(), Is.EqualTo(beforeLastCommand));
            var setAside = Directory.GetFiles(_folder, "*.damaged");
            Assert.That(setAside, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllBytes(setAside[0]), Is.EqualTo(damaged));
        }

        [Test]
        public void ASaveFromANewerVersionIsNeitherLoadedNorOverwritten()
        {
            Played();
            string text = File.ReadAllText(SavePath);
            string newer = "eirora-save 999" + text.Substring(text.IndexOf('\n'));
            File.WriteAllText(SavePath, newer);

            var session = Session();

            Assert.That(session.TryLoad(), Is.False);
            Assert.That(session.LoadProblem, Is.EqualTo(SaveProblem.FromNewerVersion));
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(newer));
        }

        [Test]
        public void StartingAnewKeepsTheUnreadableSaveAside()
        {
            File.WriteAllText(SavePath, "not a save");
            var session = Session();
            session.TryLoad();

            string? aside = session.SetAsideSave();
            session.Begin(Night);

            Assert.That(session.LoadProblem, Is.Null);
            Assert.That(File.ReadAllText(aside!), Is.EqualTo("not a save"));
            Assert.That(Session().TryLoad(), Is.True);
        }

        [Test]
        public void APrototypeJournalIsTakenOverOnceAndKept()
        {
            var journal = Journal.Start(Clock().Now(), Night);
            journal.Apply("survey", new Survey());
            string journalPath = Path.Combine(_folder, GameSession.PrototypeJournalFile);
            File.WriteAllText(journalPath, journal.ToText());

            var session = Session();

            Assert.That(session.TryLoad(), Is.True);
            Assert.That(session.World!.Fingerprint(), Is.EqualTo(journal.World.Fingerprint()));
            Assert.That(File.Exists(SavePath), Is.True);
            Assert.That(File.Exists(journalPath), Is.False);
            Assert.That(File.ReadAllText(journalPath + ".taken-over", Encoding.UTF8), Is.EqualTo(journal.ToText()));
        }

        private static FixedClock Clock() => new(new TimeReading(1_792_000_000, "Europe/Stockholm", 7200));

        private GameSession Session() => new(Clock(), SavePath);

        private GameSession Played()
        {
            var session = Session();
            session.Begin(Night);
            session.Apply(new Workday("O1"));
            return session;
        }
    }
}
