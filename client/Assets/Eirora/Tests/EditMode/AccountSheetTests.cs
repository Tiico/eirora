using System;
using System.Linq;
using System.Threading;
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
            var server = new FakeAccountServer(() => DateTimeOffset.UtcNow);
            _account = new AccountSession(
                FakeAccountServer.Url,
                server,
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
        public void WithoutAnAccountTheGameLooksAsBefore()
        {
            _hud.Show(_game.World!, _game.SinceDawn);

            Assert.That(_root.Query<Button>(className: "button-icon").ToList(), Is.Empty);
        }

        private string[] Labels() => _root.Query<Label>().ToList().Select(l => l.text).ToArray();
    }
}
