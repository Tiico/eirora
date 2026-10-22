using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eirora.Contracts;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// The player's account on this device (ADR-0008): signing in with Apple or Google, keeping the refresh token in the
    /// platform keystore and the access token in memory, refreshing, linking and removing logins, signing out and deleting.
    /// </summary>
    /// <remarks>
    /// Nothing here is needed to play: the own world never waits for the account (ACC-04, HLT-12), and every action is
    /// administrative, so it is allowed during rest (RST-07). An unreachable server leaves the tokens as they are.
    /// </remarks>
    public sealed class AccountSession
    {
        public const string RefreshTokenKey = "eirora.account.refresh-token";

        /// <summary>An access token this close to expiring is refreshed first, so that it cannot run out on the way.</summary>
        private static readonly TimeSpan s_expiryMargin = TimeSpan.FromMinutes(1);

        private readonly AccountServer _server;
        private readonly ISecureStore _store;
        private readonly Func<DateTimeOffset> _now;
        private readonly Dictionary<string, ILoginProvider> _providers;
        private string? _accessToken;
        private DateTimeOffset _accessExpiresAt;
        private Task<Access>? _refreshing;
        private bool _busy;

        public AccountSession(
            string serverUrl,
            IHttpTransport http,
            ISecureStore store,
            IEnumerable<ILoginProvider> providers,
            Func<DateTimeOffset>? now = null)
        {
            _server = new AccountServer(serverUrl, http);
            DeletionPage = serverUrl.TrimEnd('/') + "/account/delete";
            _store = store;
            _now = now ?? (() => DateTimeOffset.UtcNow);
            _providers = providers.ToDictionary(p => p.Provider, StringComparer.Ordinal);
            Providers = providers.Select(p => p.Provider).ToList();
        }

        /// <summary>Something about the account changed: signed in or out, a login linked or removed, the account deleted.</summary>
        public event Action? Changed;

        /// <summary>The account was deleted from this device, with its id: what the device kept for it goes too (ADR-0008).</summary>
        public event Action<Guid>? Deleted;

        /// <summary>The providers the player can sign in with on this device, in the order they are offered.</summary>
        public IReadOnlyList<string> Providers { get; }

        /// <summary>Whether this device holds a sign-in; it may still turn out expired the next time the server is asked.</summary>
        public bool IsSignedIn => _store.Read(RefreshTokenKey) is not null;

        /// <summary>The account as the server last described it, or null before it is known.</summary>
        public AccountInfo? Account { get; private set; }

        /// <summary>The web page where the account can be deleted without the app, which Google Play requires.</summary>
        public string DeletionPage { get; }

        /// <summary>Whether an action is under way; another one is refused until it is done.</summary>
        public bool IsBusy => _busy;

        /// <summary>The server's API on the wire, for the cloud save alongside the account.</summary>
        internal AccountServer Server => _server;

        /// <summary>The device's keystore, which the contribution key shares with the refresh token.</summary>
        internal ISecureStore Store => _store;

        /// <summary>Signs in, creating the account if this is the login's first sign-in (D-72).</summary>
        public Task<AccountOutcome> SignInAsync(string provider) => Exclusive(async () =>
        {
            var (outcome, login) = await FreshLoginAsync(provider);
            if (login is null)
            {
                return outcome;
            }

            var reply = await _server.SignInAsync(login.Provider, login.IdToken, login.Nonce);
            if (reply.Status == 0)
            {
                return AccountOutcome.Offline;
            }

            if (reply.Status != 200 || reply.AccessToken is null || reply.RefreshToken is null)
            {
                return AccountOutcome.SignInFailed;
            }

            Keep(reply);
            await LoadAccountAsync();
            return reply.AccountCreated ? AccountOutcome.Created : AccountOutcome.Done;
        });

        /// <summary>Asks the server for the account, for example when the account settings are opened.</summary>
        public Task<AccountOutcome> LoadAsync() => Exclusive(LoadAccountAsync);

        /// <summary>
        /// Links another provider's login, so that either one reaches the account (account recovery). The player first
        /// confirms with <see cref="ConfirmationProvider"/>, so that a stolen token alone cannot add a login (threat model).
        /// </summary>
        public Task<AccountOutcome> LinkAsync(string provider) => Exclusive(async () =>
        {
            if (!_providers.ContainsKey(provider))
            {
                return AccountOutcome.ProviderUnavailable;
            }

            var (ready, confirmationProvider) = await ConfirmationAsync(() => ConfirmationProvider);
            if (confirmationProvider is null)
            {
                return ready;
            }

            var (confirmed, confirmation) = await FreshLoginAsync(confirmationProvider);
            if (confirmation is null)
            {
                return confirmed;
            }

            var (outcome, login) = await FreshLoginAsync(provider);
            if (login is null)
            {
                return outcome;
            }

            var (result, reply) = await AuthorizedAsync(access => _server.LinkAsync(access, login, confirmation));
            if (reply is null)
            {
                return result;
            }

            switch (reply.Status)
            {
                case 204:
                    await LoadAccountAsync();
                    return AccountOutcome.Done;
                case 403:
                    return AccountOutcome.NotThisAccount;
                case 409:
                    return AccountOutcome.LoginOfAnotherAccount;
                case 400:
                    return AccountOutcome.SignInFailed;
                default:
                    return AccountOutcome.Failed;
            }
        });

        /// <summary>
        /// Removes a linked login, after the player confirms with <see cref="RemovalProvider"/>; the last one only goes
        /// with the account.
        /// </summary>
        public Task<AccountOutcome> RemoveLoginAsync(string provider) => Exclusive(async () =>
        {
            var (ready, confirmationProvider) = await ConfirmationAsync(() => RemovalProvider(provider));
            if (confirmationProvider is null)
            {
                return ready;
            }

            var (confirmed, confirmation) = await FreshLoginAsync(confirmationProvider);
            if (confirmation is null)
            {
                return confirmed;
            }

            var (result, reply) = await AuthorizedAsync(access => _server.RemoveLoginAsync(access, provider, confirmation));
            if (reply is null)
            {
                return result;
            }

            switch (reply.Status)
            {
                case 204:
                case 404: // Already removed, from another device.
                    await LoadAccountAsync();
                    return AccountOutcome.Done;
                case 403:
                    return AccountOutcome.NotThisAccount;
                case 409:
                    return AccountOutcome.LastLogin;
                case 400:
                    return AccountOutcome.SignInFailed;
                default:
                    return AccountOutcome.Failed;
            }
        });

        /// <summary>
        /// The provider the player confirms linking with: the first of the account's logins that this device can sign in
        /// with. Null before the account is known or when this device has none of them.
        /// </summary>
        public string? ConfirmationProvider => Account is null ? null : Providers.FirstOrDefault(IsLinked);

        /// <summary>
        /// The provider the player confirms removing <paramref name="provider"/>'s login with: preferably a login that
        /// stays, so that the player shows the account can still be reached. Null like <see cref="ConfirmationProvider"/>.
        /// </summary>
        public string? RemovalProvider(string provider) =>
            Account is null ? null : Providers.FirstOrDefault(p => p != provider && IsLinked(p)) ?? ConfirmationProvider;

        /// <summary>
        /// Revokes the refresh token on the server and forgets the sign-in on this device. Without a connection only the
        /// device forgets; the token then runs out on the server after 60 days without use.
        /// </summary>
        public Task<AccountOutcome> SignOutAsync() => Exclusive(async () =>
        {
            if (_store.Read(RefreshTokenKey) is { } refresh)
            {
                await _server.RevokeAsync(refresh);
            }

            Forget();
            return AccountOutcome.Done;
        });

        /// <summary>
        /// The provider the player confirms a deletion with: Apple if the account has an Apple login, so that the server
        /// can revoke the app at Apple, otherwise one of the account's logins that this device can sign in with.
        /// Null before the account is known or when this device has none of them.
        /// </summary>
        public string? DeletionProvider =>
            !IsLinked(LoginProviders.Apple) ? ConfirmationProvider
            : _providers.ContainsKey(LoginProviders.Apple) ? LoginProviders.Apple
            : null;

        /// <summary>
        /// Deletes the account and everything the server stores for it, after a fresh sign-in with
        /// <see cref="DeletionProvider"/> (ACC-05, App Store Review Guideline 5.1.1(v)), and forgets it on this device.
        /// </summary>
        public Task<AccountOutcome> DeleteAsync() => Exclusive(async () =>
        {
            var (ready, provider) = await ConfirmationAsync(() => DeletionProvider);
            if (provider is null)
            {
                return ready;
            }

            var (outcome, login) = await FreshLoginAsync(provider, forDeletion: true);
            if (login is null)
            {
                return outcome;
            }

            var (result, reply) = await AuthorizedAsync(access => _server.DeleteAsync(access, login));
            if (reply is null)
            {
                return result;
            }

            switch (reply.Status)
            {
                case 204:
                    var deleted = Account?.Id;
                    Forget();
                    if (deleted is { } id)
                    {
                        Deleted?.Invoke(id);
                    }

                    return AccountOutcome.Deleted;
                case 403:
                    return AccountOutcome.NotThisAccount;
                case 400:
                    return AccountOutcome.SignInFailed;
                default:
                    return AccountOutcome.Failed;
            }
        });

        private async Task<AccountOutcome> LoadAccountAsync()
        {
            var (result, reply) = await AuthorizedAsync(async access =>
            {
                var (got, account) = await _server.GetAccountAsync(access);
                if (account is not null)
                {
                    Account = account;
                    Changed?.Invoke();
                }

                return got;
            });
            return reply is null ? result : reply.Status == 200 ? AccountOutcome.Done : AccountOutcome.Failed;
        }

        /// <summary>
        /// The provider to confirm a change of the account with, once the account is known, or why there is none: signed
        /// out, offline, or no login of the account on this device.
        /// </summary>
        private async Task<(AccountOutcome Outcome, string? Provider)> ConfirmationAsync(Func<string?> provider)
        {
            if (!IsSignedIn)
            {
                return (AccountOutcome.SignedOut, null);
            }

            if (Account is null)
            {
                var loaded = await LoadAccountAsync();
                if (loaded != AccountOutcome.Done)
                {
                    return (loaded, null);
                }
            }

            return provider() is { } chosen ? (AccountOutcome.Done, chosen) : (AccountOutcome.ConfirmationUnavailable, null);
        }

        private bool IsLinked(string provider) => Account?.Logins.Any(l => l.Provider == provider) == true;

        /// <summary>
        /// A sign-in with the provider, bound to a new nonce, or why there is none. Apple's authorization code is only kept
        /// for a deletion, the one request the server needs it for (to revoke the app at Apple).
        /// </summary>
        private async Task<(AccountOutcome Outcome, ExternalLogin? Login)> FreshLoginAsync(string provider, bool forDeletion = false)
        {
            if (!_providers.TryGetValue(provider, out var source))
            {
                return (AccountOutcome.ProviderUnavailable, null);
            }

            string nonce = Nonce.Create();
            try
            {
                var signIn = await source.SignInAsync(Nonce.Hash(nonce));
                return (AccountOutcome.Done, new ExternalLogin(provider, signIn.IdToken, nonce, forDeletion ? signIn.AuthorizationCode : null));
            }
            catch (SignInCancelledException)
            {
                return (AccountOutcome.Cancelled, null);
            }
            catch (LoginProviderException)
            {
                return (AccountOutcome.SignInFailed, null);
            }
        }

        /// <summary>
        /// Calls the API with a valid access token, refreshing it first when needed and once more if the server rejects
        /// it. Answers with the server's reply, or without one and the reason (offline, signed out).
        /// </summary>
        internal async Task<(AccountOutcome Outcome, HttpReply? Reply)> AuthorizedAsync(Func<string, Task<HttpReply>> call)
        {
            var access = await AccessAsync(force: false);
            if (access.Token is null)
            {
                return (access.Outcome, null);
            }

            var reply = await call(access.Token);
            if (reply.Status == 401)
            {
                access = await AccessAsync(force: true);
                if (access.Token is null)
                {
                    return (access.Outcome, null);
                }

                reply = await call(access.Token);
            }

            return reply.Status switch
            {
                0 => (AccountOutcome.Offline, null),
                401 => (AccountOutcome.Failed, null),
                _ => (AccountOutcome.Done, reply),
            };
        }

        /// <summary>The access token, refreshed when it is about to expire; concurrent callers share one refresh.</summary>
        private async Task<Access> AccessAsync(bool force)
        {
            if (!force && _accessToken is { } token && _now() < _accessExpiresAt - s_expiryMargin)
            {
                return new Access(AccountOutcome.Done, token);
            }

            var refreshing = _refreshing ??= RefreshTokensAsync();
            try
            {
                return await refreshing;
            }
            finally
            {
                if (_refreshing == refreshing)
                {
                    _refreshing = null;
                }
            }
        }

        private async Task<Access> RefreshTokensAsync()
        {
            if (_store.Read(RefreshTokenKey) is not { } refresh)
            {
                return new Access(AccountOutcome.SignedOut, null);
            }

            var reply = await _server.RefreshAsync(refresh);
            if (reply.Status == 200 && reply.AccessToken is not null && reply.RefreshToken is not null)
            {
                Keep(reply);
                return new Access(AccountOutcome.Done, reply.AccessToken);
            }

            // The refresh token expired or was revoked, or the account was deleted on another device: the player is signed
            // out here too. Anything else (offline, the server down) keeps the sign-in for the next try.
            if (reply.Error == "invalid_grant")
            {
                Forget();
                return new Access(AccountOutcome.SignedOut, null);
            }

            return new Access(reply.Status == 0 ? AccountOutcome.Offline : AccountOutcome.Failed, null);
        }

        /// <summary>Keeps new tokens: the rotated refresh token in the keystore, the access token in memory only.</summary>
        private void Keep(TokenReply reply)
        {
            _store.Write(RefreshTokenKey, reply.RefreshToken!);
            _accessToken = reply.AccessToken;
            _accessExpiresAt = _now() + reply.ExpiresIn;
        }

        /// <summary>Forgets the sign-in on this device. The own world is untouched (ACC-04).</summary>
        private void Forget()
        {
            bool wasSignedIn = IsSignedIn || Account is not null;
            _store.Delete(RefreshTokenKey);
            _accessToken = null;
            Account = null;
            if (wasSignedIn)
            {
                Changed?.Invoke();
            }
        }

        private async Task<AccountOutcome> Exclusive(Func<Task<AccountOutcome>> action)
        {
            if (_busy)
            {
                return AccountOutcome.Busy;
            }

            _busy = true;
            try
            {
                return await action();
            }
            finally
            {
                _busy = false;
            }
        }

        private readonly struct Access
        {
            public Access(AccountOutcome outcome, string? token)
            {
                Outcome = outcome;
                Token = token;
            }

            public AccountOutcome Outcome { get; }

            public string? Token { get; }
        }
    }

    /// <summary>How an account action ended, for the interface to say in words.</summary>
    public enum AccountOutcome
    {
        Done,

        /// <summary>Signed in, and the sign-in created the account.</summary>
        Created,

        Deleted,

        /// <summary>The player closed the provider's dialog.</summary>
        Cancelled,

        /// <summary>The server could not be reached; nothing changed.</summary>
        Offline,

        /// <summary>Not signed in on this device, or the sign-in expired.</summary>
        SignedOut,

        /// <summary>The provider or the server did not accept the sign-in.</summary>
        SignInFailed,

        ProviderUnavailable,

        /// <summary>The login belongs to another account, and accounts are never merged.</summary>
        LoginOfAnotherAccount,

        /// <summary>The last login can only be removed by deleting the account.</summary>
        LastLogin,

        /// <summary>The change was confirmed with a login of another account.</summary>
        NotThisAccount,

        /// <summary>This device cannot sign in with any login the change can be confirmed with.</summary>
        ConfirmationUnavailable,

        /// <summary>Another action is under way.</summary>
        Busy,

        Failed,
    }
}
