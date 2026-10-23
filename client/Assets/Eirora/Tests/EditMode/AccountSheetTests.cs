using System;
using System.Linq;
using System.Reflection;
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
        private DeviceCloudSyncStorage _cloudStorage = null!;

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
            _server = new FakeAccountServer(() => DateTimeOffset.UtcNow) { ConsentOfNewAccounts = true };
            _cloudStorage = new DeviceCloudSyncStorage(new MemorySecureStore(), null); // The phone's keystore and state, kept across a relaunch.
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

        // TASK-060: the question comes before anything goes up, and both answers look the same (ux-journeys.md).
        [Test]
        public void TheQuestionAboutTheSelfCareComesBeforeTheFirstUploadAndAYesSavesIt()
        {
            _server.ConsentOfNewAccounts = null;
            var cloud = SignedInCloud();

            var question = Labels();
            var answers = _root.Query<Button>(className: "button").ToList().Where(b => b.text is "Ja, spara den" or "Nej tack").ToList();
            bool nothingUploaded = _server.CloudSaveOf("player") is null;
            Press(answers.Single(b => b.text == "Ja, spara den"));

            Assert.That(question, Does.Contain("Egenvården på kontot?"));
            Assert.That(question.Any(l => l.Contains("servern kan aldrig läsa den", StringComparison.Ordinal)), Is.True);
            Assert.That(question.Any(l => l.Contains("Den sparas vad du än svarar här", StringComparison.Ordinal)), Is.True);
            Assert.That(answers.Select(b => string.Join(" ", b.GetClasses())).Distinct().ToList(), Has.Count.EqualTo(1), "both answers weigh the same");
            Assert.That(nothingUploaded, Is.True);
            Assert.That(Labels(), Does.Not.Contain("Egenvården på kontot?"));
            Assert.That(_server.ConsentOf("player").Answer, Is.EqualTo(ConsentAnswer.Given));
            Assert.That(_server.CloudSaveOf("player")!.Contributions, Is.Not.Null);
            Assert.That(cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
        }

        // Acceptance criterion 4: a no is the account's answer, so the next launch does not ask, and Konto can change it.
        [Test]
        public void ANoIsNotAskedAgainAndCanBeChangedUnderTheAccount()
        {
            _server.ConsentOfNewAccounts = null;
            SignedInCloud();
            Press(_root.Query<Button>(className: "button").ToList().Single(b => b.text == "Nej tack"));
            bool worldOnly = _server.CloudSaveOf("player") is { Contributions: null };

            _root = new VisualElement();
            _hud = new Hud(_root, command => _game.Apply(command));
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var relaunched = new CloudSaveSync(_account, _game, _cloudStorage, () => Task.CompletedTask);
            _hud.UseCloudSync(relaunched);
            relaunched.ReconcileAsync().GetAwaiter().GetResult();
            var atLaunch = Labels();
            _hud.OpenAccount();
            var sheet = Labels();
            Press(Action("Spara egenvården på kontot"));
            var asked = Labels();
            Press(_root.Query<Button>(className: "button").ToList().Single(b => b.text == "Ja, spara den"));

            Assert.That(worldOnly, Is.True);
            Assert.That(atLaunch, Does.Not.Contain("Egenvården på kontot?"));
            Assert.That(relaunched.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(sheet, Does.Contain("Din egenvård stannar på telefonen. Din ö sparas på kontot ändå."));
            Assert.That(sheet, Does.Not.Contain("Visa återställningskod"), "the key unlocks nothing the account keeps");
            Assert.That(asked, Does.Contain("Egenvården på kontot?"));
            Assert.That(_server.ConsentOf("player").Answer, Is.EqualTo(ConsentAnswer.Given));
            Assert.That(_server.CloudSaveOf("player")!.Contributions, Is.Not.Null);
            Assert.That(Actions(), Does.Contain("Sluta spara egenvården på kontot"));
        }

        // Acceptance criterion 3, through the account sheet.
        [Test]
        public void TheConsentIsWithdrawnInTheAccountSheetAndTheSelfCareLeavesTheServer()
        {
            var cloud = SignedInCloud();
            _hud.OpenAccount();
            Press(Action("Sluta spara egenvården på kontot"));
            var confirmation = Labels();
            var answers = _root.Query<Button>(className: "button").ToList().Where(b => b.text is "Sluta spara" or "Avbryt").ToList();

            Press(answers.Single(b => b.text == "Sluta spara"));

            Assert.That(confirmation, Does.Contain("Sluta spara egenvården på kontot?"));
            Assert.That(answers.Select(b => string.Join(" ", b.GetClasses())).Distinct().ToList(), Has.Count.EqualTo(1), "both answers weigh the same");
            Assert.That(_server.ConsentOf("player").Answer, Is.EqualTo(ConsentAnswer.Declined));
            Assert.That(_server.CloudSaveOf("player")!.Contributions, Is.Null);
            Assert.That(cloud.Status, Is.EqualTo(CloudSyncStatus.Synced));
            Assert.That(Actions(), Does.Contain("Spara egenvården på kontot").And.Not.Contain("Sluta spara egenvården på kontot"));
        }

        // Konto always offers to stop, also when the phone could not ask the server what the account said.
        [Test]
        public void TheWithdrawalIsOfferedAlsoBeforeThePhoneHeardTheAnswer()
        {
            SignedInCloudOffline();

            _hud.OpenAccount();

            Assert.That(Actions(), Does.Contain("Sluta spara egenvården på kontot"));
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

        // Without a screen lock the key does not follow to a new phone by itself (ADR-0008): the sheet says so beside the
        // recovery code, and stops saying it once the phone has a screen lock.
        [Test]
        public void WithoutAScreenLockTheSheetSuggestsTheRecoveryCodeOrAKeyTransfer()
        {
            const string Suggestion = "Telefonen har inget skärmlås, så nyckeln till din egenvård följer inte med till en ny telefon av sig själv. Skriv ner återställningskoden, eller skicka nyckeln med QR-koden när du byter telefon.";
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var backup = new MemoryKeyBackup { Reach = KeyBackupReach.ThisPhone };
            var cloud = new CloudSaveSync(_account, _game, new DeviceCloudSyncStorage(new MemorySecureStore(), null), () => Task.CompletedTask, backup: backup);
            _hud.UseCloudSync(cloud);
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            cloud.ReconcileAsync().GetAwaiter().GetResult();

            _hud.OpenAccount();
            Assert.That(Labels(), Does.Contain(Suggestion));

            backup.Reach = KeyBackupReach.EndToEnd;
            cloud.ReconcileAsync().GetAwaiter().GetResult();

            Assert.That(Labels(), Does.Not.Contain(Suggestion));
            Assert.That(Labels(), Does.Contain("Visa återställningskod"));

            backup.Reach = KeyBackupReach.ThisPhone;
            cloud.ReconcileAsync().GetAwaiter().GetResult();
            Assert.That(Labels(), Does.Contain(Suggestion));
            backup.Reach = null; // The platform could not say: the sheet claims no missing screen lock.
            cloud.ReconcileAsync().GetAwaiter().GetResult();

            Assert.That(Labels(), Does.Not.Contain(Suggestion));
            Assert.That(Labels(), Does.Contain("Visa återställningskod"));
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

        private string[] Actions() => _root.Query<Button>(className: "action").ToList().Select(b => b.Q<Label>(className: "action-title").text).ToArray();

        private Button Action(string title) =>
            _root.Query<Button>(className: "action").ToList().Single(b => b.Q<Label>(className: "action-title").text == title);

        /// <summary>The player signed in with Google on this phone, which has a world, and the cloud save reconciled once.</summary>
        private CloudSaveSync SignedInCloud()
        {
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var cloud = new CloudSaveSync(_account, _game, _cloudStorage, () => Task.CompletedTask);
            _hud.UseCloudSync(cloud);
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            cloud.ReconcileAsync().GetAwaiter().GetResult();
            return cloud;
        }

        /// <summary>The player signed in on an earlier launch, and this launch could not reach the server.</summary>
        private void SignedInCloudOffline()
        {
            _server.Online = true;
            _account.SignInAsync(LoginProviders.Google).GetAwaiter().GetResult();
            _server.Online = false;
            _hud.UseAccount(_account);
            _hud.Show(_game.World!, _game.SinceDawn);
            var cloud = new CloudSaveSync(_account, _game, _cloudStorage, () => Task.CompletedTask);
            _hud.UseCloudSync(cloud);
            cloud.ReconcileAsync().GetAwaiter().GetResult();
        }

        /// <summary>Clicks a button as a tap would, through its <see cref="Clickable"/>; the root is in no panel to send events to.</summary>
        private static void Press(Button button)
        {
            var invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null, "Clickable.Invoke is gone; find another way to press a button.");
            using var tap = NavigationSubmitEvent.GetPooled();
            invoke!.Invoke(button.clickable, new object[] { tap });
        }
    }
}
