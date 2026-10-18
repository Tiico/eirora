using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        // On the phone File.Replace renames the save to the backup and then the new file to the save, so a crash between
        // the two leaves only the backup.
        [Test]
        public void ASwapCutOffAfterTheSaveBecameTheBackupLoadsTheBackup()
        {
            var session = Played();
            string beforeLastCommand = session.World!.Fingerprint();
            session.Apply(new Survey());
            File.Move(SavePath, SavePath + ".tmp");

            var second = Session();

            Assert.That(second.TryLoad(), Is.True);
            Assert.That(second.World!.Fingerprint(), Is.EqualTo(beforeLastCommand));
            second.Apply(new Survey());
            Assert.That(Session().TryLoad(), Is.True);
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
            string body = text.Substring(text.IndexOf('\n', text.IndexOf('\n') + 1) + 1);
            string newer = Sealed("eirora-save 999\n", body);
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

            var aside = session.SetAsideSave();
            session.Begin(Night);

            Assert.That(session.LoadProblem, Is.Null);
            Assert.That(aside, Has.Count.EqualTo(1));
            Assert.That(File.ReadAllText(aside[0]), Is.EqualTo("not a save"));
            Assert.That(Session().TryLoad(), Is.True);
        }

        [Test]
        public void StartingAnewKeepsAnUnreadableBackupAsideToo()
        {
            File.WriteAllText(SavePath, "not a save");
            File.WriteAllText(SavePath + ".bak", "not a backup");
            var session = Session();
            Assert.That(session.TryLoad(), Is.False);
            Assert.That(session.LoadProblem, Is.EqualTo(SaveProblem.Damaged));

            var aside = session.SetAsideSave();
            session.Begin(Night);
            session.Apply(new Workday("O1"));

            Assert.That(aside.Select(File.ReadAllText), Is.EquivalentTo(new[] { "not a save", "not a backup" }));
        }

        // Taking over writes the save like any other, so a crash mid-write leaves the journal to be taken over again.
        [Test]
        public void ATakeOverCutOffMidWriteIsTakenOverAgain()
        {
            var journal = Journal.Start(Clock().Now(), Night);
            journal.Apply("survey", new Survey());
            File.WriteAllText(Path.Combine(_folder, GameSession.PrototypeJournalFile), journal.ToText());
            Assert.That(Session().TryLoad(), Is.True);
            byte[] complete = File.ReadAllBytes(SavePath);
            File.Delete(SavePath);
            File.Delete(SavePath + ".bak");
            File.Move(Path.Combine(_folder, GameSession.PrototypeJournalFile + ".taken-over"), Path.Combine(_folder, GameSession.PrototypeJournalFile));
            File.WriteAllBytes(SavePath, complete.Take(complete.Length / 2).ToArray());

            var session = Session();

            Assert.That(session.TryLoad(), Is.True);
            Assert.That(session.World!.Fingerprint(), Is.EqualTo(journal.World.Fingerprint()));
            Assert.That(Directory.GetFiles(_folder, "*.damaged"), Has.Length.EqualTo(1));
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

        /// <summary>A save's frame around <paramref name="body"/>, as a newer app would write it.</summary>
        private static string Sealed(string header, string body)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(header + body));
            return $"{header}sha256 {BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant()}\n{body}";
        }

        private static FixedClock Clock() =>new(new TimeReading(1_792_000_000, "Europe/Stockholm", 7200));

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
