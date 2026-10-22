using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eirora.Client.Accounts;
using Eirora.Client.App;
using Eirora.Client.UI;
using Eirora.CloudSaving;
using Eirora.Contracts;
using Eirora.Simulation;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>The account in the interface: always reachable, also during rest, and never in the way (RST-07, ACC-04).</summary>
    public sealed class AccountSheetTests
    {
        private SynchronizationContext? _context;
        private VisualElement _root = null!;
        private GameSession _game = null!;
        private Hud _hud = null!;
        private AccountSession _account = null!;
        private FakeAccountServer _server = null!;

        [SetUp]
        public void SetUp()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            _game = new GameSession(clock, null);
            _game.Begin(new RestSchedule(23 * 60, 8 * 60));
            clock.Current = ScriptedWeek.At("2026-10-15 01:00");
            _game.CatchUp();
            _root = new VisualElement();
            _hud = new Hud(_root, command => _game.Apply(command));
            _server = new FakeAccountServer(() => DateTimeOffset.UtcNow);
            _account = new AccountSession(
                FakeAccountServer.Url,
                _server,
                new MemorySecureStore(),
                new ILoginProvider[] { new FakeLogin(LoginProviders.Apple, "player"), new FakeLogin(LoginProviders.Google, "player") });
        }

        [TearDown]
        public void TearDown() => SynchronizationContext.SetSynchronizationContext(_context);

        [Test]
        public void TheAccountIsReachableWhileTheVillageRests()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);

            Assert.That(_game.World!.Phase, Is.EqualTo(Phase.Resting));
            Assert.That(_root.Query<Button>(className: "button-icon").ToList(), Has.Count.EqualTo(1));

            _hud.OpenAccount();

            Assert.That(Labels(), Does.Contain("Logga in med Apple").And.Contain("Logga in med Google"));
        }

        [Test]
        public void SigningInDuringRestShowsTheAccountsActions()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();

            _hud.OpenAccount();

            Assert.That(Labels(), Does.Contain("Inloggad med Google").And.Contain("Koppla Apple").And.Contain("Logga ut").And.Contain("Radera kontot"));
        }

        [Test]
        public void TheDeletionIsConfirmedWithApplesSignInForAnAppleAccount()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            _account.SignInAsync(LoginProviders.Apple).GetAwaiter().GetResult();

            _hud.OpenAccountDeletion();

            Assert.That(Labels(), Does.Contain("Radera kontot?"));
            Assert.That(_root.Query<Button>().ToList().Select(b => b.text), Does.Contain("Radera med Apple"));
        }

        [Test]
        public void TheSheetSaysHowTheCloudSaveStands()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var synced = new DateTimeOffset(2026, 10, 21, 20, 0, 0, TimeSpan.Zero);
            var cloud = new CloudSaveSync(_account, _game, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask, () => synced);
            _hud.UseCloudSync(cloud);
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            cloud.ReconcileAsync().GetAwaiter().GetResult();

            _hud.OpenAccount();

            Assert.That(Labels().Where(l => l.StartsWith("Molnsparning", StringComparison.Ordinal)).ToList(), Has.Count.EqualTo(1));
            Assert.That(Labels(), Does.Contain("Molnsparning: din ö är sparad på kontot, senast " + synced.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Test]
        public void TheFirstLaunchOffersToFetchTheIslandFromTheAccount()
        {
            var fresh = new GameSession(new FixedClock(ScriptedWeek.At("2026-10-14 14:00")), null);
            var root = new VisualElement();
            var hud = new Hud(root, command => fresh.Apply(command));
            hud.UseAccount(_account);

            hud.ShowSetup(_ => { });

            Assert.That(root.Query<Button>().ToList().Select(b => b.text), Does.Contain("Logga in med Google").And.Contain("Börja"));
        }

        [Test]
        public void TwoDifferentWorldsArePutToThePlayer()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var cloud = new CloudSaveSync(_account, _game, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask);
            _hud.UseCloudSync(cloud);
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            cloud.ReconcileAsync().GetAwaiter().GetResult();
            _server.TamperWithContributionsOf("player");

            cloud.ReconcileAsync().GetAwaiter().GetResult();

            Assert.That(Labels(), Does.Contain("Vilken ö vill du behålla?"));
            Assert.That(_root.Query<Button>().ToList().Select(b => b.text), Does.Contain("Hämta ön från kontot").And.Contain("Behåll telefonens ö"));
            Assert.That(_root.Query<Button>().ToList().Select(b => b.text), Does.Contain("Visa QR-kod för din gamla telefon"), "a phone with its own island can still get the key from the old phone");
            Assert.That(_root.Query<TextField>().ToList(), Has.Count.EqualTo(1), "or from the recovery code");
        }

        [Test]
        public void TheRecoveryCodeIsShownOnRequestOnly()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var cloud = new CloudSaveSync(_account, _game, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask);
            _hud.UseCloudSync(cloud);
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            cloud.ReconcileAsync().GetAwaiter().GetResult();

            _hud.OpenAccount();
            Assert.That(Labels(), Does.Contain("Visa återställningskod"));
            Assert.That(Labels().Any(l => l.Replace("\n", "-") == cloud.RecoveryCode), Is.False);

            _hud.OpenRecoveryCode();

            Assert.That(Labels(), Does.Contain("Återställningskod"));
            Assert.That(Labels().Any(l => l.Replace("\n", "-") == cloud.RecoveryCode), Is.True);
        }

        [Test]
        public void ANewPhoneIsAskedForTheKeyBeforeAnythingIsDownloaded()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var oldPhone = new CloudSaveSync(_account, _game, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask);
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            oldPhone.ReconcileAsync().GetAwaiter().GetResult();
            var fresh = new GameSession(new FixedClock(ScriptedWeek.At("2026-10-15 01:00")), null);
            var root = new VisualElement();
            var hud = new Hud(root, command => fresh.Apply(command));
            var newAccount = new AccountSession(FakeAccountServer.Url, _server, new MemorySecureStore(), new ILoginProvider[] { new FakeLogin(LoginProviders.Google, "player") });
            hud.UseAccount(newAccount);
            var newPhone = new CloudSaveSync(newAccount, fresh, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask);
            hud.UseCloudSync(newPhone);

            newAccount.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            newPhone.ReconcileAsync().GetAwaiter().GetResult();

            Assert.That(fresh.World, Is.Null);
            Assert.That(root.Query<Label>().ToList().Select(l => l.text), Does.Contain("Din ö finns på kontot"));
            Assert.That(root.Query<Button>().ToList().Select(b => b.text), Does.Contain("Lås upp med koden").And.Contain("Fortsätt utan nyckeln"));
            Assert.That(root.Query<TextField>().ToList(), Has.Count.EqualTo(1));
        }

        // The QR flow in the interface (TASK-023b): the new phone shows the ticket as a code under the question, the old
        // phone's account sheet opens the camera, and a read ticket sends the key, which the new phone picks up.
        [Test]
        public void TheNewPhoneShowsACodeAndTheOldPhoneScansItToSendTheKey()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var scanner = new FakeQrScanner();
            var oldPhone = new CloudSaveSync(_account, _game, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask);
            _hud.UseCloudSync(oldPhone, scanner);
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            oldPhone.ReconcileAsync().GetAwaiter().GetResult();
            var fresh = new GameSession(new FixedClock(ScriptedWeek.At("2026-10-15 01:00")), null);
            var root = new VisualElement();
            var hud = new Hud(root, command => fresh.Apply(command));
            var newAccount = new AccountSession(FakeAccountServer.Url, _server, new MemorySecureStore(), new ILoginProvider[] { new FakeLogin(LoginProviders.Google, "player") });
            hud.UseAccount(newAccount);
            var newPhone = new CloudSaveSync(newAccount, fresh, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask);
            hud.UseCloudSync(newPhone);
            newAccount.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            newPhone.ReconcileAsync().GetAwaiter().GetResult();
            Assert.That(root.Query<Image>().ToList(), Is.Empty, "no code before the player asks for one");

            Assert.That(root.Query<Button>().ToList().Select(b => b.text), Does.Contain("Visa QR-kod för din gamla telefon"));
            hud.ShowTransferCode();

            var code = root.Query<Image>(className: "qr-code").First();
            Assert.That(code.image, Is.Not.Null, "the ticket is on screen as a QR code");
            Assert.That(newPhone.TransferTicket, Is.Not.Null);
            Assert.That(root.Query<Label>().ToList().Select(l => l.text), Has.Some.StartsWith("Väntar på din gamla telefon"));
            Assert.That(root.Query<TextField>().ToList(), Is.Empty, "the code replaces the recovery code field until the player goes back");

            _hud.OpenAccount();
            Assert.That(Labels(), Does.Contain("Skicka nyckeln till en ny telefon"));
            _hud.OpenKeySending();
            Assert.That(Labels(), Does.Contain("Skicka nyckeln").And.Contain("Startar kameran …"));
            Assert.That(scanner.Started, Is.True, "the camera runs while the sheet is open");

            scanner.Status = ScannerStatus.Scanning;
            scanner.Next = "some other code";
            _hud.ScanNow();
            Assert.That(scanner.Started, Is.True, "a code that is not a ticket keeps the camera on");
            Assert.That(Labels(), Does.Contain("Det är inte en kod från Eirora. Sök koden på den nya telefonen."));
            string ticket = newPhone.TransferTicket!;
            string crafted = ticket.Replace(" " + KeyTransferTicket.TryParse(ticket)!.Id + " ", "  ");
            scanner.Next = crafted;
            _hud.ScanNow();
            Assert.That(scanner.Started, Is.True, "a crafted code with an empty id keeps the camera on too");
            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(1), "nothing was sent for it");

            scanner.Next = ticket;
            _hud.ScanNow();

            string checkCode = KeyTransferTicket.TryParse(ticket)!.CheckCode;
            Assert.That(scanner.Started, Is.False, "the camera is off while the player is asked");
            Assert.That(Labels(), Does.Contain("Skicka nyckeln till telefonen som visar koden?").And.Contain("Kontrollkod " + checkCode));
            Assert.That(root.Query<Label>().ToList().Select(l => l.text), Has.Some.StartsWith("Kontrollkod " + checkCode), "the new phone shows the same check code under its code");
            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(1), "nothing is sent before the player confirms");

            _hud.ConfirmSend();

            Assert.That(Labels(), Does.Contain("Nyckeln är skickad. Den nya telefonen hämtar din ö nu."));
            Assert.That(Labels(), Does.Not.Contain("Skicka nyckeln"), "the sheet is back on the account");
            Assert.That(scanner.Started, Is.False, "the camera is off as soon as the key is sent");

            hud.PollTransferNow();

            Assert.That(fresh.World, Is.Not.Null, "the island came down with the key");
            Assert.That(fresh.World!.Fingerprint(), Is.EqualTo(_game.World!.Fingerprint()));
            Assert.That(root.Query<Image>().ToList(), Is.Empty, "the code is gone with the question");
            Assert.That(root.Query<Label>().ToList().Select(l => l.text), Does.Contain("Nyckeln kom fram. Din ö hämtas från kontot."));
        }

        [Test]
        public void GoingBackFromTheCodeGivesTheTicketUpAndClosingTheSheetStopsTheCamera()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var scanner = new FakeQrScanner();
            var oldPhone = new CloudSaveSync(_account, _game, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask);
            _hud.UseCloudSync(oldPhone, scanner);
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            oldPhone.ReconcileAsync().GetAwaiter().GetResult();
            var fresh = new GameSession(new FixedClock(ScriptedWeek.At("2026-10-15 01:00")), null);
            var root = new VisualElement();
            var hud = new Hud(root, command => fresh.Apply(command));
            var newAccount = new AccountSession(FakeAccountServer.Url, _server, new MemorySecureStore(), new ILoginProvider[] { new FakeLogin(LoginProviders.Google, "player") });
            hud.UseAccount(newAccount);
            var newPhone = new CloudSaveSync(newAccount, fresh, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask);
            hud.UseCloudSync(newPhone);
            newAccount.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            newPhone.ReconcileAsync().GetAwaiter().GetResult();
            hud.ShowTransferCode();
            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(1));
            Assert.That(root.Query<Button>().ToList().Select(b => b.text), Does.Contain("Avbryt"));

            hud.LeaveTransferCode();

            Assert.That(newPhone.TransferTicket, Is.Null);
            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(0), "the server forgot the ticket");
            Assert.That(root.Query<Image>().ToList(), Is.Empty);
            Assert.That(root.Query<TextField>().ToList(), Has.Count.EqualTo(1), "the question is back, with the recovery code field");

            _hud.OpenKeySending();
            Assert.That(scanner.Started, Is.True);
            _hud.OpenAccount(); // Toggles the sheet shut.
            Assert.That(scanner.Started, Is.False, "the camera never runs with the sheet closed");

            hud.ShowTransferCode();
            _hud.OpenKeySending();
            scanner.Status = ScannerStatus.Scanning;
            scanner.Next = newPhone.TransferTicket;
            _hud.ScanNow();
            Assert.That(Labels(), Does.Contain("Skicka nyckeln till telefonen som visar koden?"));
            Assert.That(_root.Query<Button>().ToList().Select(b => b.text), Does.Contain("Skanna igen"));

            _hud.OpenAccount(); // Shut while the question is up: nothing is sent, and the next opening scans afresh.

            Assert.That(_server.PendingKeyTransfers, Is.EqualTo(1));
            _hud.OpenKeySending();
            Assert.That(Labels(), Does.Contain("Söker koden …").And.Not.Contain("Skicka nyckeln till telefonen som visar koden?"));
        }

        /// <summary>A scanner the tests feed: the next read gives <see cref="Next"/> once.</summary>
        private sealed class FakeQrScanner : IQrScanner
        {
            public ScannerStatus Status { get; set; } = ScannerStatus.Starting;

            public UnityEngine.Texture? Preview => null;

            public int PreviewRotation => 0;

            public bool PreviewMirrored => false;

            public bool Started { get; private set; }

            public string? Next { get; set; }

            public void Start() => Started = true;

            public string? Read()
            {
                string? next = Next;
                Next = null;
                return next;
            }

            public void Stop() => Started = false;
        }

        [Test]
        public void WithoutAnAccountTheGameLooksAsBefore()
        {
            _hud.Show(_game.World!, _game.SinceDawn);

            Assert.That(_root.Query<Button>(className: "button-icon").ToList(), Is.Empty);
        }

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();
    }
}
