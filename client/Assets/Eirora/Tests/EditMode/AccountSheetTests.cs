using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eirora.Client.Accounts;
using Eirora.Client.App;
using Eirora.Client.UI;
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

        [Test]
        public void WithoutAnAccountTheGameLooksAsBefore()
        {
            _hud.Show(_game.World!, _game.SinceDawn);

            Assert.That(_root.Query<Button>(className: "button-icon").ToList(), Is.Empty);
        }

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();
    }
}
