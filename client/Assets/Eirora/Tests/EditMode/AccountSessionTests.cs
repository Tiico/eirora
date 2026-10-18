using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eirora.Client.Accounts;
using Eirora.Contracts;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>The account on the device against a fake server (TASK-022, ADR-0008, ACC-01, ACC-04, ACC-05).</summary>
    public sealed class AccountSessionTests
    {
        private DateTimeOffset _now;
        private FakeAccountServer _server = null!;
        private MemorySecureStore _store = null!;
        private FakeLogin _apple = null!;
        private FakeLogin _google = null!;
        private AccountSession _session = null!;
        private SynchronizationContext? _context;

        [SetUp]
        public void SetUp()
        {
            // Continuations run inline, as they would after each frame in the game, so a test sees every step at once.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _now = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
            _server = new FakeAccountServer(() => _now);
            _store = new MemorySecureStore();
            _apple = new FakeLogin(LoginProviders.Apple, "apple-player");
            _google = new FakeLogin(LoginProviders.Google, "google-player");
            _session = NewSession();
        }

        [TearDown]
        public void TearDown() => SynchronizationContext.SetSynchronizationContext(_context);

        [Test]
        public void TheFirstSignInCreatesTheAccountAndKeepsTheRefreshTokenInTheKeystore()
        {
            Assert.That(Run(_session.SignInAsync(LoginProviders.Google)), Is.EqualTo(AccountOutcome.Created));

            Assert.That(_session.IsSignedIn, Is.True);
            Assert.That(_store.Read(AccountSession.RefreshTokenKey), Does.StartWith("refresh-"));
            Assert.That(_session.Account!.Logins.Select(l => l.Provider), Is.EqualTo(new[] { LoginProviders.Google }));
        }

        [Test]
        public void SigningInAgainReachesTheSameAccount()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            var first = _session.Account!.Id;
            Run(_session.SignOutAsync());

            Assert.That(Run(_session.SignInAsync(LoginProviders.Google)), Is.EqualTo(AccountOutcome.Done));
            Assert.That(_session.Account!.Id, Is.EqualTo(first));
            Assert.That(_server.Accounts, Is.EqualTo(1));
        }

        [Test]
        public void TheProviderGetsTheNonceHashAndTheServerTheNonce()
        {
            Run(_session.SignInAsync(LoginProviders.Google));

            var form = UrlQuery.Parse(_server.Calls.First(c => c.Url.EndsWith("/connect/token", StringComparison.Ordinal)).Body!);
            Assert.That(form["grant_type"], Is.EqualTo(SignInGrant.GrantType));
            Assert.That(form[SignInGrant.ProviderParameter], Is.EqualTo(LoginProviders.Google));
            Assert.That(form["scope"], Is.EqualTo(SignInGrant.OfflineAccessScope));
            Assert.That(Nonce.Hash(form[SignInGrant.NonceParameter]), Is.EqualTo(_google.NonceHashes.Single()));
            Assert.That(_google.NonceHashes.Single(), Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void EverySignInHasItsOwnNonce()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            Run(_session.SignOutAsync());
            Run(_session.SignInAsync(LoginProviders.Google));

            Assert.That(_google.NonceHashes.Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void AnExpiredAccessTokenIsRefreshedAndTheRotatedRefreshTokenKept()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            var first = _store.Read(AccountSession.RefreshTokenKey);
            _now += TimeSpan.FromMinutes(20);

            Assert.That(Run(_session.LoadAsync()), Is.EqualTo(AccountOutcome.Done));

            Assert.That(_store.Read(AccountSession.RefreshTokenKey), Is.Not.EqualTo(first));
            Assert.That(_server.Calls.Count(c => c.Body?.Contains("grant_type=refresh_token") == true), Is.EqualTo(1));
        }

        [Test]
        public void ARefreshTokenSurvivesARestart()
        {
            Run(_session.SignInAsync(LoginProviders.Google));

            var restarted = NewSession();

            Assert.That(restarted.IsSignedIn, Is.True);
            Assert.That(Run(restarted.LoadAsync()), Is.EqualTo(AccountOutcome.Done));
            Assert.That(restarted.Account!.Logins.Single().Provider, Is.EqualTo(LoginProviders.Google));
        }

        [Test]
        public void AnExpiredSignInSignsOutOnThisDeviceToo()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            _server.ExpireRefreshTokens();
            _now += TimeSpan.FromMinutes(20);

            Assert.That(Run(_session.LoadAsync()), Is.EqualTo(AccountOutcome.SignedOut));
            Assert.That(_session.IsSignedIn, Is.False);
            Assert.That(_store.Read(AccountSession.RefreshTokenKey), Is.Null);
        }

        [Test]
        public void WithoutAConnectionNothingIsForgotten()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            _server.Online = false;
            _now += TimeSpan.FromMinutes(20);

            Assert.That(Run(_session.LoadAsync()), Is.EqualTo(AccountOutcome.Offline));
            Assert.That(_session.IsSignedIn, Is.True);

            _server.Online = true;
            Assert.That(Run(_session.LoadAsync()), Is.EqualTo(AccountOutcome.Done));
        }

        [Test]
        public void SigningInWithoutAConnectionSaysSoAndChangesNothing()
        {
            _server.Online = false;

            Assert.That(Run(_session.SignInAsync(LoginProviders.Google)), Is.EqualTo(AccountOutcome.Offline));
            Assert.That(_session.IsSignedIn, Is.False);
        }

        [Test]
        public void ClosingTheProvidersDialogSendsNothing()
        {
            _google.CancelNext = true;

            Assert.That(Run(_session.SignInAsync(LoginProviders.Google)), Is.EqualTo(AccountOutcome.Cancelled));
            Assert.That(_server.Calls, Is.Empty);
        }

        [Test]
        public void AProviderErrorIsAFailedSignIn()
        {
            _apple.FailNext = true;

            Assert.That(Run(_session.SignInAsync(LoginProviders.Apple)), Is.EqualTo(AccountOutcome.SignInFailed));
            Assert.That(_session.IsSignedIn, Is.False);
        }

        [Test]
        public void SigningOutRevokesTheRefreshTokenOnTheServer()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            var refresh = _store.Read(AccountSession.RefreshTokenKey)!;

            Assert.That(Run(_session.SignOutAsync()), Is.EqualTo(AccountOutcome.Done));

            var revoke = UrlQuery.Parse(_server.Calls.Last().Body!);
            Assert.That(_server.Calls.Last().Url, Does.EndWith("/connect/revoke"));
            Assert.That(revoke["token"], Is.EqualTo(refresh));
            Assert.That(revoke["token_type_hint"], Is.EqualTo("refresh_token"));
            Assert.That(_session.IsSignedIn, Is.False);
            Assert.That(_session.Account, Is.Null);
        }

        [Test]
        public void SigningOutWithoutAConnectionStillForgetsTheSignIn()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            _server.Online = false;

            Assert.That(Run(_session.SignOutAsync()), Is.EqualTo(AccountOutcome.Done));
            Assert.That(_session.IsSignedIn, Is.False);
        }

        [Test]
        public void ASecondLoginIsLinkedAndReachesTheSameAccount()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            var account = _session.Account!.Id;

            Assert.That(Run(_session.LinkAsync(LoginProviders.Apple)), Is.EqualTo(AccountOutcome.Done));
            Assert.That(_session.Account!.Logins.Select(l => l.Provider), Is.EquivalentTo(new[] { LoginProviders.Google, LoginProviders.Apple }));

            Run(_session.SignOutAsync());
            Run(_session.SignInAsync(LoginProviders.Apple));
            Assert.That(_session.Account!.Id, Is.EqualTo(account));
        }

        [Test]
        public void ALoginOfAnotherAccountIsNotLinked()
        {
            var other = NewSession();
            Run(other.SignInAsync(LoginProviders.Apple));
            Run(_session.SignInAsync(LoginProviders.Google));

            Assert.That(Run(_session.LinkAsync(LoginProviders.Apple)), Is.EqualTo(AccountOutcome.LoginOfAnotherAccount));
            Assert.That(_server.LoginsOf("google-player"), Is.EqualTo(new[] { LoginProviders.Google }));
        }

        [Test]
        public void TheLastLoginStaysUntilTheAccountIsDeleted()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            Run(_session.LinkAsync(LoginProviders.Apple));

            Assert.That(Run(_session.UnlinkAsync(LoginProviders.Apple)), Is.EqualTo(AccountOutcome.Done));
            Assert.That(Run(_session.UnlinkAsync(LoginProviders.Google)), Is.EqualTo(AccountOutcome.LastLogin));
            Assert.That(_session.Account!.Logins.Single().Provider, Is.EqualTo(LoginProviders.Google));
        }

        [Test]
        public void DeletingAnAccountWithAnAppleLoginIsConfirmedWithAppleAndItsCode()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            Run(_session.LinkAsync(LoginProviders.Apple));

            Assert.That(_session.DeletionProvider, Is.EqualTo(LoginProviders.Apple));
            Assert.That(Run(_session.DeleteAsync()), Is.EqualTo(AccountOutcome.Deleted));

            Assert.That(_server.AppleCodesRevoked, Has.Count.EqualTo(1));
            Assert.That(_server.HasAccount("google-player"), Is.False);
            Assert.That(_session.IsSignedIn, Is.False);
            Assert.That(_store.Read(AccountSession.RefreshTokenKey), Is.Null);
        }

        [Test]
        public void DeletingAGoogleOnlyAccountIsConfirmedWithGoogle()
        {
            Run(_session.SignInAsync(LoginProviders.Google));

            Assert.That(_session.DeletionProvider, Is.EqualTo(LoginProviders.Google));
            Assert.That(Run(_session.DeleteAsync()), Is.EqualTo(AccountOutcome.Deleted));
            Assert.That(_server.Accounts, Is.Zero);
        }

        [Test]
        public void DeletionConfirmedAsSomeoneElseDeletesNothing()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            _google.Subject = "someone-else";

            Assert.That(Run(_session.DeleteAsync()), Is.EqualTo(AccountOutcome.NotThisAccount));
            Assert.That(_server.HasAccount("google-player"), Is.True);
            Assert.That(_session.IsSignedIn, Is.True);
        }

        [Test]
        public void CancellingTheConfirmationDeletesNothing()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            _google.CancelNext = true;

            Assert.That(Run(_session.DeleteAsync()), Is.EqualTo(AccountOutcome.Cancelled));
            Assert.That(_server.Accounts, Is.EqualTo(1));
        }

        [Test]
        public void AnAppleAccountCannotBeDeletedFromADeviceWithoutApple()
        {
            Run(_session.SignInAsync(LoginProviders.Apple));
            var googleOnly = new AccountSession(FakeAccountServer.Url, _server, _store, new ILoginProvider[] { _google }, () => _now);
            Run(googleOnly.LoadAsync());

            Assert.That(googleOnly.DeletionProvider, Is.Null);
            Assert.That(Run(googleOnly.DeleteAsync()), Is.EqualTo(AccountOutcome.ConfirmationUnavailable));
        }

        [Test]
        public void AnAccountDeletedOnAnotherDeviceSignsOutHere()
        {
            Run(_session.SignInAsync(LoginProviders.Google));
            _server.DeleteAccountOf("google-player");

            Assert.That(Run(_session.LoadAsync()), Is.EqualTo(AccountOutcome.SignedOut));
            Assert.That(_session.IsSignedIn, Is.False);
        }

        [Test]
        public void OneActionAtATime()
        {
            var slow = new SlowLogin(LoginProviders.Google);
            var session = new AccountSession(FakeAccountServer.Url, _server, _store, new ILoginProvider[] { slow }, () => _now);

            var first = session.SignInAsync(LoginProviders.Google);

            Assert.That(Run(session.SignOutAsync()), Is.EqualTo(AccountOutcome.Busy));
            Assert.That(session.IsBusy, Is.True);
            slow.Answer.SetException(new SignInCancelledException());
            Assert.That(Run(first), Is.EqualTo(AccountOutcome.Cancelled));
            Assert.That(session.IsBusy, Is.False);
        }

        [Test]
        public void ChangesAreAnnounced()
        {
            int changes = 0;
            _session.Changed += () => changes++;

            Run(_session.SignInAsync(LoginProviders.Google));
            int afterSignIn = changes;
            Run(_session.SignOutAsync());

            Assert.That(afterSignIn, Is.GreaterThan(0));
            Assert.That(changes, Is.GreaterThan(afterSignIn));
        }

        private AccountSession NewSession() =>
            new(FakeAccountServer.Url, _server, _store, new ILoginProvider[] { _apple, _google }, () => _now);

        /// <summary>The fakes answer at once, so every action completes before it returns.</summary>
        private static T Run<T>(Task<T> task)
        {
            Assert.That(task.IsCompleted, Is.True, "The action waited for something the fakes never do.");
            return task.GetAwaiter().GetResult();
        }

        private sealed class SlowLogin : ILoginProvider
        {
            public SlowLogin(string provider) => Provider = provider;

            public string Provider { get; }

            public TaskCompletionSource<ProviderSignIn> Answer { get; } = new();

            public Task<ProviderSignIn> SignInAsync(string nonceHash) => Answer.Task;
        }
    }
}
