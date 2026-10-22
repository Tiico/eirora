using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eirora.Client.Accounts;
using Eirora.Client.App;
using Eirora.CloudSaving;
using Eirora.Contracts;
using Eirora.Simulation.Commands;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The world on the phone and the account's cloud save (TASK-023, ADR-0008) through the client's own pieces: the
    /// account session, the keystore, the save on disk and the fake server with the real server's rules.
    /// </summary>
    public sealed class CloudSaveSyncTests
    {
        private static readonly RestSchedule Night = new(23 * 60, 8 * 60);

        private SynchronizationContext? _context;
        private string _folder = string.Empty;
        private DateTimeOffset _now;
        private FakeAccountServer _server = null!;

        [SetUp]
        public void SetUp()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _folder = Path.Combine(Path.GetTempPath(), "eirora-cloud-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _now = new DateTimeOffset(2026, 10, 21, 20, 0, 0, TimeSpan.Zero);
            _server = new FakeAccountServer(() => _now);
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_context);
            Directory.Delete(_folder, recursive: true);
        }

        [Test]
        public void SigningInUploadsTheWorldSealedWithThePhonesKey()
        {
            var phone = Phone("a", withWorld: true);

            phone.SignIn();
            Wait(phone.Cloud.ReconcileAsync());

            var save = _server.CloudSaveOf("player")!;
            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That((save.Version, save.KeyEpoch), Is.EqualTo((1L, 1)));
            Assert.That(save.World, Is.EqualTo(phone.Game.Current!.ToParts().World));
            var key = phone.Storage.Key(phone.Account.Account!.Id)!;
            Assert.That(ContributionCipher.Open(key, phone.Account.Account!.Id, 1, save.Contributions), Is.EqualTo(phone.Game.Current!.ToParts().Contributions));
            Assert.That(File.Exists(Path.Combine(_folder, "a", DeviceCloudSyncStorage.StateFile)), Is.True);
        }

        [Test]
        public void EverySaveIsUploadedALittleLater()
        {
            var phone = Phone("a", withWorld: true);
            phone.SignIn();
            Wait(phone.Cloud.ReconcileAsync());

            phone.Game.Apply(new RecordSelfCare(phone.Game.World!.GameDay, Habit.Steps, ContributionSource.SelfReported, 5_000));

            Assert.That(phone.Delays, Is.EqualTo(1), "the upload waits once, so that a burst of commands becomes one upload");
            Assert.That(_server.CloudSaveOf("player")!.Version, Is.EqualTo(2));
            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(phone.Cloud.SyncedAt, Is.EqualTo(_now));
        }

        [Test]
        public void ANewPhoneWithTheKeyGetsTheWorldAndIsWelcomed()
        {
            var old = Phone("a", withWorld: true);
            old.SignIn();
            Wait(old.Cloud.ReconcileAsync());
            var fresh = Phone("b", withWorld: false, keystore: old.Keystore);

            fresh.SignIn();
            Wait(fresh.Cloud.ReconcileAsync());

            Assert.That(fresh.Game.World, Is.Not.Null);
            Assert.That(fresh.Game.World!.Fingerprint(), Is.EqualTo(old.Game.World!.Fingerprint()));
            Assert.That(fresh.Cloud.Welcome, Is.True);
            Assert.That(fresh.Cloud.KeyEpochBegan, Is.False);
            Assert.That(File.Exists(Path.Combine(_folder, "b", GameSession.SaveFile)), Is.True);
        }

        // Before a new epoch begins, the new phone asks for the key (TASK-023b): the recovery code from the old phone's
        // account sheet unlocks the account's island as it is, with its self-care.
        [Test]
        public void ANewPhoneWithoutTheKeyIsAskedAndARecoveryCodeUnlocksTheIsland()
        {
            var old = Phone("a", withWorld: true);
            old.SignIn();
            Wait(old.Cloud.ReconcileAsync());
            var fresh = Phone("b", withWorld: false);
            fresh.SignIn();
            Wait(fresh.Cloud.ReconcileAsync());

            Assert.That(fresh.Cloud.Status, Is.EqualTo(CloudSyncStatus.KeyNeeded));
            Assert.That(fresh.Cloud.Choice!.Outcome, Is.EqualTo(SyncOutcome.KeyNeeded));
            Assert.That(fresh.Game.World, Is.Null);
            Assert.That(Wait(fresh.Cloud.EnterRecoveryCodeAsync("not a code")), Is.EqualTo(RecoveryCodeOutcome.NotACode));
            Assert.That(Wait(fresh.Cloud.EnterRecoveryCodeAsync(RecoveryCode.Of(ContributionKey.Generate(1)))), Is.EqualTo(RecoveryCodeOutcome.NotThisAccountsCode));
            Assert.That(fresh.Cloud.Status, Is.EqualTo(CloudSyncStatus.KeyNeeded));
            Assert.That(fresh.Storage.Key(fresh.Account.Account!.Id), Is.Null, "a code that is not the account's is not kept");

            Assert.That(Wait(fresh.Cloud.EnterRecoveryCodeAsync(old.Cloud.RecoveryCode!.ToLowerInvariant())), Is.EqualTo(RecoveryCodeOutcome.Unlocked));

            Assert.That(fresh.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(fresh.Cloud.Choice, Is.Null);
            Assert.That(fresh.Game.World!.Fingerprint(), Is.EqualTo(old.Game.World!.Fingerprint()));
            Assert.That(fresh.Cloud.KeyEpochBegan, Is.False);
            Assert.That(fresh.Cloud.RecoveryCode, Is.EqualTo(old.Cloud.RecoveryCode));
            Assert.That(_server.CloudSaveOf("player")!.KeyEpoch, Is.EqualTo(1));
        }

        // A code written down before a new epoch began is out of date: it is refused with a reason, and the phone keeps
        // the key it has, which still opens the part the server locked.
        [Test]
        public void AStaleRecoveryCodeIsRefusedAndTheKeyThePhoneHoldsStays()
        {
            var old = Phone("a", withWorld: true);
            old.SignIn();
            Wait(old.Cloud.ReconcileAsync());
            string staleCode = old.Cloud.RecoveryCode!;
            var another = Phone("b", withWorld: false);
            another.SignIn();
            Wait(another.Cloud.ReconcileAsync());
            Wait(another.Cloud.ChooseAsync(WorldChoice.TakeCloud)); // Epoch 2 on the server.
            string oldKey = old.Storage.Key(old.Account.Account!.Id)!.ToText();

            Assert.That(Wait(old.Cloud.EnterRecoveryCodeAsync(staleCode)), Is.EqualTo(RecoveryCodeOutcome.OlderCode));

            Assert.That(old.Storage.Key(old.Account.Account!.Id)!.ToText(), Is.EqualTo(oldKey));
        }

        [Test]
        public void ANewPhoneWithoutTheKeyBeginsANewKeyEpochAndIsTold()
        {
            var old = Phone("a", withWorld: true);
            old.SignIn();
            Wait(old.Cloud.ReconcileAsync());
            var fresh = Phone("b", withWorld: false);

            fresh.SignIn();
            Wait(fresh.Cloud.ReconcileAsync());
            Wait(fresh.Cloud.ChooseAsync(WorldChoice.TakeCloud));

            Assert.That(fresh.Cloud.KeyEpochBegan, Is.True);
            Assert.That(fresh.Game.World!.LastClosedGameDay, Is.EqualTo(old.Game.World!.GameDay));
            Assert.That(_server.CloudSaveOf("player")!.KeyEpoch, Is.EqualTo(2));
            Assert.That(_server.LockedParts, Is.EqualTo(1));
        }

        [Test]
        public void TwoPhonesThatPlayedApartEndUpWithTheSameWorld()
        {
            var a = Phone("a", withWorld: true);
            a.SignIn();
            Wait(a.Cloud.ReconcileAsync());
            var b = Phone("b", withWorld: false, keystore: a.Keystore);
            b.SignIn();
            Wait(b.Cloud.ReconcileAsync());
            int day = a.Game.World!.GameDay;

            a.Game.Apply(new RecordSelfCare(day, Habit.Steps, ContributionSource.SelfReported, 9_000));
            b.Clock.Current = Later(b.Clock.Current, 3600);
            b.Game.Apply(new ChangeRestSchedule(new RestSchedule(22 * 60, 8 * 60)));
            a.Clock.Current = b.Clock.Current;
            Wait(a.Cloud.ReconcileAsync());

            Assert.That(a.Game.World!.Fingerprint(), Is.EqualTo(b.Game.World!.Fingerprint()));
            Assert.That(b.Game.World!.RecordedSelfCare(day, Habit.Steps, ContributionSource.SelfReported), Is.EqualTo(9_000));
            Assert.That(a.Game.World!.PendingRestSchedule, Is.EqualTo(new RestSchedule(22 * 60, 8 * 60)));
            Assert.That(_server.CloudSaveOf("player")!.Version, Is.EqualTo(3));
        }

        [Test]
        public void DifferentWorldsAskThePlayerAndTheChoiceReplacesThePhonesWorldButKeepsIt()
        {
            var a = Phone("a", withWorld: true);
            a.SignIn();
            Wait(a.Cloud.ReconcileAsync());
            var b = Phone("b", withWorld: true, keystore: a.Keystore, installOffset: 60);
            string phones = b.Game.World!.Fingerprint();

            b.SignIn();
            Wait(b.Cloud.ReconcileAsync());

            Assert.That(b.Cloud.Status, Is.EqualTo(CloudSyncStatus.ChoiceNeeded));
            Assert.That(b.Cloud.Choice, Is.Not.Null);
            Assert.That(b.Cloud.Choice!.CloudGameDay, Is.EqualTo(a.Game.World!.GameDay));
            Assert.That(b.Game.World!.Fingerprint(), Is.EqualTo(phones), "nothing changes until the player chooses");

            Wait(b.Cloud.ChooseAsync(WorldChoice.TakeCloud));

            Assert.That(b.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(b.Cloud.Choice, Is.Null);
            a.Clock.Current = b.Clock.Current;
            a.Game.CatchUp();
            Assert.That(b.Game.World!.Fingerprint(), Is.EqualTo(a.Game.World!.Fingerprint()));
            Assert.That(Directory.GetFiles(Path.Combine(_folder, "b"), "*.replaced"), Has.Length.EqualTo(1));
        }

        [Test]
        public void KeepingThePhonesWorldReplacesTheCloudsAndKeepsItAside()
        {
            var a = Phone("a", withWorld: true);
            a.SignIn();
            Wait(a.Cloud.ReconcileAsync());
            var b = Phone("b", withWorld: true, keystore: a.Keystore, installOffset: 60);
            string phones = b.Game.World!.Fingerprint();
            b.SignIn();
            Wait(b.Cloud.ReconcileAsync());

            Wait(b.Cloud.ChooseAsync(WorldChoice.KeepPhone));

            Assert.That(b.Game.World!.Fingerprint(), Is.EqualTo(phones));
            Assert.That(b.Cloud.KeyEpochBegan, Is.False);
            Assert.That(b.Cloud.ContributionPartLocked, Is.False, "the cloud's part opened with the key, so nothing was locked");
            Assert.That(_server.CloudSaveOf("player")!.World, Is.EqualTo(b.Game.Current!.ToParts().World));
            Assert.That(Directory.GetFiles(Path.Combine(_folder, "b"), GameSession.SaveFile + ".*.cloud"), Has.Length.EqualTo(1));
        }

        [Test]
        public void AContributionPartChangedOnTheServerIsNeverMerged()
        {
            var phone = Phone("a", withWorld: true);
            phone.SignIn();
            Wait(phone.Cloud.ReconcileAsync());
            _server.TamperWithContributionsOf("player");
            string phones = phone.Game.World!.Fingerprint();

            Wait(phone.Cloud.ReconcileAsync());

            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.ChoiceNeeded));
            Assert.That(phone.Cloud.Choice!.CloudSaveUnreadable, Is.True);
            Assert.That(phone.Game.World!.Fingerprint(), Is.EqualTo(phones));
        }

        // Keeping the phone's world over a part that does not open locks that part under a new epoch; the phone is
        // told so, without the notice about closed game days, which are only for a world that came down.
        [Test]
        public void KeepingThePhonesWorldOverAnUnreadablePartLocksItAndSaysSo()
        {
            var phone = Phone("a", withWorld: true);
            phone.SignIn();
            Wait(phone.Cloud.ReconcileAsync());
            _server.TamperWithContributionsOf("player");
            Wait(phone.Cloud.ReconcileAsync());

            Wait(phone.Cloud.ChooseAsync(WorldChoice.KeepPhone));

            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(phone.Cloud.ContributionPartLocked, Is.True);
            Assert.That(phone.Cloud.KeyEpochBegan, Is.False);
            Assert.That(_server.CloudSaveOf("player")!.KeyEpoch, Is.EqualTo(2));
            Assert.That(_server.LockedParts, Is.EqualTo(1));
        }

        // The part is locked only when the phone's world reaches the server, so the phone says so only then.
        [Test]
        public void KeepingThePhonesWorldSaysThePartIsLockedOnlyOnceTheUploadGetsThrough()
        {
            var phone = Phone("a", withWorld: true);
            phone.SignIn();
            Wait(phone.Cloud.ReconcileAsync());
            _server.TamperWithContributionsOf("player");
            Wait(phone.Cloud.ReconcileAsync());
            _server.TakesUploads = false;

            Wait(phone.Cloud.ChooseAsync(WorldChoice.KeepPhone));

            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Offline));
            Assert.That(phone.Cloud.ContributionPartLocked, Is.False);
            Assert.That(_server.LockedParts, Is.EqualTo(0));

            _server.TakesUploads = true;
            phone.Game.Apply(new RecordSelfCare(phone.Game.World!.GameDay, Habit.Steps, ContributionSource.SelfReported, 5_000));

            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(phone.Cloud.ContributionPartLocked, Is.True);
            Assert.That(phone.Cloud.Choice, Is.Null);
            Assert.That(_server.LockedParts, Is.EqualTo(1));
        }

        // The QR flow end to end (TASK-023b): the new phone shows a ticket, the old phone reads it and sends the key
        // sealed for the new phone, which keeps it once the cloud save confirms it and then takes the island down.
        [Test]
        public void TheOldPhoneSendsTheKeyToTheNewOneThroughTheTicketItShows()
        {
            var old = Phone("a", withWorld: true);
            old.SignIn();
            Wait(old.Cloud.ReconcileAsync());
            var fresh = Phone("b", withWorld: false);
            fresh.SignIn();
            Wait(fresh.Cloud.ReconcileAsync());
            Assert.That(fresh.Cloud.Status, Is.EqualTo(CloudSyncStatus.KeyNeeded));

            Assert.That(Wait(fresh.Cloud.BeginKeyTransferAsync()), Is.EqualTo(KeyTransferOutcome.Waiting));
            string ticket = fresh.Cloud.TransferTicket!;
            Assert.That(KeyTransferTicket.TryParse(ticket), Is.Not.Null);
            Assert.That(fresh.Cloud.TransferExpiresAt, Is.EqualTo(_now + KeyTransfer.Lifetime));
            Assert.That(Wait(fresh.Cloud.PollKeyTransferAsync()), Is.EqualTo(KeyTransferOutcome.Waiting));
            Assert.That(fresh.Cloud.Status, Is.EqualTo(CloudSyncStatus.KeyNeeded));

            Assert.That(Wait(old.Cloud.SendKeyAsync("not a ticket")), Is.EqualTo(KeyDeliveryOutcome.BadTicket));
            Assert.That(Wait(old.Cloud.SendKeyAsync(ticket)), Is.EqualTo(KeyDeliveryOutcome.Delivered));
            Assert.That(Wait(old.Cloud.SendKeyAsync(ticket)), Is.EqualTo(KeyDeliveryOutcome.AlreadyDelivered));

            Assert.That(Wait(fresh.Cloud.PollKeyTransferAsync()), Is.EqualTo(KeyTransferOutcome.Received));

            Assert.That(fresh.Cloud.TransferTicket, Is.Null);
            Assert.That(fresh.Cloud.TransferOutcome, Is.Null);
            Assert.That(fresh.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(fresh.Cloud.Choice, Is.Null);
            Assert.That(fresh.Game.World!.Fingerprint(), Is.EqualTo(old.Game.World!.Fingerprint()));
            Assert.That(fresh.Cloud.RecoveryCode, Is.EqualTo(old.Cloud.RecoveryCode));
            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(0));
            Assert.That(Wait(fresh.Cloud.PollKeyTransferAsync()), Is.EqualTo(KeyTransferOutcome.Expired), "polling after the transfer is over is harmless");
        }

        // A ticket the server has forgotten is useless on both sides, and a new one can be shown.
        [Test]
        public void ARunOutTicketIsSeenOnBothPhonesAndANewOneCanBeShown()
        {
            var old = Phone("a", withWorld: true);
            old.SignIn();
            Wait(old.Cloud.ReconcileAsync());
            var fresh = Phone("b", withWorld: false);
            fresh.SignIn();
            Wait(fresh.Cloud.ReconcileAsync());
            Wait(fresh.Cloud.BeginKeyTransferAsync());
            string ticket = fresh.Cloud.TransferTicket!;

            _now += KeyTransfer.Lifetime;

            Assert.That(Wait(old.Cloud.SendKeyAsync(ticket)), Is.EqualTo(KeyDeliveryOutcome.Expired));
            Assert.That(Wait(fresh.Cloud.PollKeyTransferAsync()), Is.EqualTo(KeyTransferOutcome.Expired));
            Assert.That(fresh.Cloud.TransferTicket, Is.Null);
            Assert.That(fresh.Cloud.TransferOutcome, Is.EqualTo(KeyTransferOutcome.Expired));

            Assert.That(Wait(fresh.Cloud.BeginKeyTransferAsync()), Is.EqualTo(KeyTransferOutcome.Waiting));
            Assert.That(fresh.Cloud.TransferTicket, Is.Not.Null.And.Not.EqualTo(ticket));
            Assert.That(Wait(old.Cloud.SendKeyAsync(fresh.Cloud.TransferTicket!)), Is.EqualTo(KeyDeliveryOutcome.Delivered));
            Assert.That(Wait(fresh.Cloud.PollKeyTransferAsync()), Is.EqualTo(KeyTransferOutcome.Received));
            Assert.That(fresh.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
        }

        // Going on without the key, or unlocking with the recovery code, gives the shown ticket up: the server forgets
        // it, so a key sent to it later goes nowhere. A phone without the key has nothing to send.
        [Test]
        public void LeavingTheQuestionGivesTheTicketUp()
        {
            var old = Phone("a", withWorld: true);
            old.SignIn();
            Wait(old.Cloud.ReconcileAsync());
            var fresh = Phone("b", withWorld: false);
            fresh.SignIn();
            Wait(fresh.Cloud.ReconcileAsync());
            Wait(fresh.Cloud.BeginKeyTransferAsync());
            string ticket = fresh.Cloud.TransferTicket!;
            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(1));
            Assert.That(Wait(fresh.Cloud.SendKeyAsync(ticket)), Is.EqualTo(KeyDeliveryOutcome.NoKey), "the new phone has no key to send");

            Wait(fresh.Cloud.EnterRecoveryCodeAsync(old.Cloud.RecoveryCode!));

            Assert.That(fresh.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(fresh.Cloud.TransferTicket, Is.Null);
            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(0));
            Assert.That(Wait(old.Cloud.SendKeyAsync(ticket)), Is.EqualTo(KeyDeliveryOutcome.Expired));

            var another = Phone("c", withWorld: false);
            another.SignIn();
            Wait(another.Cloud.ReconcileAsync());
            Wait(another.Cloud.BeginKeyTransferAsync());
            Wait(another.Cloud.ChooseAsync(WorldChoice.TakeCloud));

            Assert.That(another.Cloud.TransferTicket, Is.Null);
            Assert.That(another.Cloud.KeyEpochBegan, Is.True);
            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(0));
        }

        // Offline, the ticket stays open and the new phone keeps asking; signing out drops it.
        [Test]
        public void OfflineTheTicketStaysAndSigningOutDropsIt()
        {
            var old = Phone("a", withWorld: true);
            old.SignIn();
            Wait(old.Cloud.ReconcileAsync());
            var fresh = Phone("b", withWorld: false);
            fresh.SignIn();
            Wait(fresh.Cloud.ReconcileAsync());
            Wait(fresh.Cloud.BeginKeyTransferAsync());
            string ticket = fresh.Cloud.TransferTicket!;

            _server.Online = false;
            Assert.That(Wait(fresh.Cloud.PollKeyTransferAsync()), Is.EqualTo(KeyTransferOutcome.Offline));
            Assert.That(fresh.Cloud.TransferTicket, Is.EqualTo(ticket), "the code stays on screen while the server is away");
            Assert.That(Wait(old.Cloud.SendKeyAsync(ticket)), Is.EqualTo(KeyDeliveryOutcome.Offline));

            _server.Online = true;
            Wait(fresh.Account.SignOutAsync());

            Assert.That(fresh.Cloud.TransferTicket, Is.Null);
            Assert.That(fresh.Cloud.TransferOutcome, Is.Null);
            _now += KeyTransfer.Lifetime;
            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(0), "the server forgets a transfer the signed-out phone could not cancel");
        }

        [Test]
        public void OfflineTheWorldWaitsAndTheNextChangeUploadsIt()
        {
            var phone = Phone("a", withWorld: true, signedInBefore: true);
            _server.Online = false;

            Wait(phone.Cloud.ReconcileAsync());
            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Offline));
            Assert.That(_server.CloudSaveOf("player"), Is.Null);

            _server.Online = true;
            phone.Game.Apply(new RecordSelfCare(phone.Game.World!.GameDay, Habit.Steps, ContributionSource.SelfReported, 5_000));

            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(_server.CloudSaveOf("player"), Is.Not.Null);
        }

        [Test]
        public void SigningOutTurnsTheCloudSaveOffAndDeletingTheAccountForgetsTheKey()
        {
            var phone = Phone("a", withWorld: true);
            phone.SignIn();
            Wait(phone.Cloud.ReconcileAsync());
            var account = phone.Account.Account!.Id;

            Wait(phone.Account.SignOutAsync());
            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Off));
            Assert.That(phone.Storage.Key(account), Is.Not.Null, "the key stays for the next sign-in");

            phone.SignIn();
            Wait(phone.Cloud.ReconcileAsync());
            Assert.That(Wait(phone.Account.DeleteAsync()), Is.EqualTo(AccountOutcome.Deleted));

            Assert.That(phone.Storage.Key(account), Is.Null);
            Assert.That(phone.Storage.State(account), Is.Null);
            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Off));
        }

        [Test]
        public void WithoutASignInNothingIsUploaded()
        {
            var phone = Phone("a", withWorld: true);

            phone.Game.Apply(new RecordSelfCare(phone.Game.World!.GameDay, Habit.Steps, ContributionSource.SelfReported, 5_000));

            Assert.That(phone.Cloud.Status, Is.EqualTo(CloudSyncStatus.Off));
            Assert.That(_server.Calls, Is.Empty);
        }

        private static TimeReading Later(TimeReading reading, long seconds) => new(reading.UtcSeconds + seconds, reading.ZoneId, reading.OffsetSeconds);

        private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

        private static void Wait(Task task) => task.GetAwaiter().GetResult();

        /// <param name="signedInBefore">Signed in on an earlier launch, so that creating the sync starts nothing.</param>
        private Phone Phone(string name, bool withWorld, MemorySecureStore? keystore = null, int installOffset = 0, bool signedInBefore = false)
        {
            string folder = Path.Combine(_folder, name);
            Directory.CreateDirectory(folder);
            var clock = new FixedClock(Later(ScriptedWeek.At("2026-10-14 14:00"), installOffset));
            var game = new GameSession(clock, Path.Combine(folder, GameSession.SaveFile));
            if (withWorld)
            {
                game.Begin(Night);
                clock.Current = Later(clock.Current, 2 * 3600);
                game.Apply(new RecordSelfCare(game.World!.GameDay, Habit.Steps, ContributionSource.SelfReported, 6_000));
            }

            keystore ??= new MemorySecureStore();
            var account = new AccountSession(FakeAccountServer.Url, _server, keystore, new ILoginProvider[] { new FakeLogin(LoginProviders.Google, "player") }, () => _now);
            var storage = new DeviceCloudSyncStorage(keystore, Path.Combine(folder, DeviceCloudSyncStorage.StateFile));
            var phone = new Phone(game, clock, account, keystore, storage);
            if (signedInBefore)
            {
                phone.SignIn();
            }

            phone.Cloud = new CloudSaveSync(account, game, storage, phone.Delay, () => _now);
            return phone;
        }
    }

    /// <summary>One phone in the tests: its clock, save, account, keystore and cloud sync.</summary>
    internal sealed class Phone
    {
        public Phone(GameSession game, FixedClock clock, AccountSession account, MemorySecureStore keystore, DeviceCloudSyncStorage storage)
        {
            Game = game;
            Clock = clock;
            Account = account;
            Keystore = keystore;
            Storage = storage;
        }

        public GameSession Game { get; }

        public FixedClock Clock { get; }

        public AccountSession Account { get; }

        public MemorySecureStore Keystore { get; }

        public DeviceCloudSyncStorage Storage { get; }

        public CloudSaveSync Cloud { get; set; } = null!;

        public int Delays { get; private set; }

        public Task Delay()
        {
            Delays++;
            return Task.CompletedTask;
        }

        public void SignIn() => Account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
    }
}
