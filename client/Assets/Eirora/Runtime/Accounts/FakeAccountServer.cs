using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eirora.Contracts;
using UnityEngine;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// Eirora.Server's account API in memory for tests and the screenshot tour, with the same rules as the real one
    /// (ADR-0008): ID tokens bound to the nonce and accepted once, access tokens for 15 minutes, refresh tokens rotated
    /// and revocable, linking and the Apple rule for deletion. Speaks the same wire format, so the client is tested down
    /// to the bytes it sends.
    /// </summary>
    public sealed class FakeAccountServer : IHttpTransport
    {
        public const string Url = "https://eirora.test";
        public static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);

        private readonly Func<DateTimeOffset> _now;
        private readonly HashSet<string> _redeemedIdTokens = new();
        private readonly Dictionary<string, (Guid Account, DateTimeOffset Expires)> _access = new();
        private readonly Dictionary<string, Guid> _refresh = new();
        private readonly Dictionary<Guid, List<(string Provider, string Subject)>> _accounts = new();
        private int _serial;

        public FakeAccountServer(Func<DateTimeOffset> now) => _now = now;

        /// <summary>When false, every call answers as if the network were down.</summary>
        public bool Online { get; set; } = true;

        public List<HttpCall> Calls { get; } = new();

        public List<string> AppleCodesRevoked { get; } = new();

        public int Accounts => _accounts.Count;

        public IEnumerable<string> LoginsOf(string subject) =>
            _accounts.Values.FirstOrDefault(l => l.Any(x => x.Subject == subject))?.Select(l => l.Provider) ?? Enumerable.Empty<string>();

        public bool HasAccount(string subject) => _accounts.Values.Any(l => l.Any(x => x.Subject == subject));

        /// <summary>Signs every refresh token out, as when it expires after 60 days without use.</summary>
        public void ExpireRefreshTokens() => _refresh.Clear();

        /// <summary>Deletes the account the login belongs to, as from another phone or the web page.</summary>
        public void DeleteAccountOf(string subject)
        {
            var account = _accounts.First(a => a.Value.Any(l => l.Subject == subject)).Key;
            Remove(account);
        }

        public Task<HttpReply> SendAsync(HttpCall call)
        {
            Calls.Add(call);
            if (!Online)
            {
                return Task.FromResult(new HttpReply(0));
            }

            string path = call.Url.Substring(Url.Length);
            return Task.FromResult((call.Method, path) switch
            {
                ("POST", "/connect/token") => Token(UrlQuery.Parse(call.Body!)),
                ("POST", "/connect/revoke") => Revoke(UrlQuery.Parse(call.Body!)),
                ("GET", "/api/account") => Authorized(call, GetAccount),
                ("POST", "/api/account/logins") => Authorized(call, account => Link(account, Login(call))),
                ("DELETE", _) when path.StartsWith("/api/account/logins/", StringComparison.Ordinal) =>
                    Authorized(call, account => Unlink(account, path.Substring("/api/account/logins/".Length))),
                ("POST", "/api/account/deletion") => Authorized(call, account => Delete(account, Login(call))),
                _ => new HttpReply(404),
            });
        }

        private HttpReply Token(Dictionary<string, string> form)
        {
            if (form["grant_type"] == SignInGrant.GrantType)
            {
                if (form.GetValueOrDefault("scope") != SignInGrant.OfflineAccessScope)
                {
                    return Error("invalid_request");
                }

                var identity = Validate(form[SignInGrant.ProviderParameter], form[SignInGrant.IdTokenParameter], form[SignInGrant.NonceParameter]);
                if (identity is null)
                {
                    return Error("invalid_grant");
                }

                var account = _accounts.FirstOrDefault(a => a.Value.Contains(identity.Value)).Key;
                bool created = account == Guid.Empty;
                if (created)
                {
                    account = Guid.NewGuid();
                    _accounts[account] = new List<(string, string)> { identity.Value };
                }

                return Issue(account, created);
            }

            if (form["grant_type"] == "refresh_token")
            {
                if (!_refresh.Remove(form["refresh_token"], out var account) || !_accounts.ContainsKey(account))
                {
                    return Error("invalid_grant");
                }

                return Issue(account, created: false);
            }

            return Error("unsupported_grant_type");
        }

        private HttpReply Revoke(Dictionary<string, string> form)
        {
            _refresh.Remove(form["token"]);
            return new HttpReply(200);
        }

        private HttpReply GetAccount(Guid account) => new(200, JsonUtility.ToJson(new AccountJson
        {
            id = account.ToString(),
            createdAt = "2026-10-18T10:00:00+00:00",
            logins = _accounts[account].Select(l => new LoginJson { provider = l.Provider, linkedAt = "2026-10-18T10:00:00+00:00" }).ToArray(),
        }));

        private HttpReply Link(Guid account, ExternalLoginJson login)
        {
            var identity = Validate(login.provider, login.idToken, login.nonce);
            if (identity is null)
            {
                return new HttpReply(400);
            }

            if (_accounts.Any(a => a.Key != account && a.Value.Contains(identity.Value)) || _accounts[account].Any(l => l.Provider == login.provider && l != identity.Value))
            {
                return new HttpReply(409);
            }

            if (!_accounts[account].Contains(identity.Value))
            {
                _accounts[account].Add(identity.Value);
            }

            return new HttpReply(204);
        }

        private HttpReply Unlink(Guid account, string provider)
        {
            var logins = _accounts[account];
            if (!logins.Any(l => l.Provider == provider))
            {
                return new HttpReply(404);
            }

            if (logins.Count == 1)
            {
                return new HttpReply(409);
            }

            logins.RemoveAll(l => l.Provider == provider);
            return new HttpReply(204);
        }

        private HttpReply Delete(Guid account, ExternalLoginJson confirmation)
        {
            var identity = Validate(confirmation.provider, confirmation.idToken, confirmation.nonce);
            if (identity is null)
            {
                return new HttpReply(400);
            }

            var logins = _accounts[account];
            if (!logins.Contains(identity.Value))
            {
                return new HttpReply(403);
            }

            if (logins.Any(l => l.Provider == LoginProviders.Apple))
            {
                if (confirmation.provider != LoginProviders.Apple || string.IsNullOrEmpty(confirmation.authorizationCode))
                {
                    return new HttpReply(400);
                }

                AppleCodesRevoked.Add(confirmation.authorizationCode);
            }

            Remove(account);
            return new HttpReply(204);
        }

        private void Remove(Guid account)
        {
            _accounts.Remove(account);
            foreach (var token in _access.Where(a => a.Value.Account == account).Select(a => a.Key).ToList())
            {
                _access.Remove(token);
            }

            foreach (var token in _refresh.Where(r => r.Value == account).Select(r => r.Key).ToList())
            {
                _refresh.Remove(token);
            }
        }

        private HttpReply Authorized(HttpCall call, Func<Guid, HttpReply> handle)
        {
            if (call.Bearer is null || !_access.TryGetValue(call.Bearer, out var access) || access.Expires <= _now() || !_accounts.ContainsKey(access.Account))
            {
                return new HttpReply(401);
            }

            return handle(access.Account);
        }

        /// <summary>A <see cref="FakeLogin"/> token, accepted once and only with the nonce whose hash it carries.</summary>
        private (string Provider, string Subject)? Validate(string provider, string idToken, string nonce)
        {
            var parts = idToken.Split('|');
            if (parts.Length != 4 || parts[0] != provider || parts[2] != Nonce.Hash(nonce) || !_redeemedIdTokens.Add(idToken))
            {
                return null;
            }

            return (provider, parts[1]);
        }

        private HttpReply Issue(Guid account, bool created)
        {
            string access = "access-" + ++_serial;
            string refresh = "refresh-" + _serial;
            _access[access] = (account, _now() + AccessLifetime);
            _refresh[refresh] = account;
            return new HttpReply(200, $"{{\"access_token\":\"{access}\",\"token_type\":\"Bearer\",\"expires_in\":{(int)AccessLifetime.TotalSeconds},\"refresh_token\":\"{refresh}\",\"{SignInGrant.AccountCreatedParameter}\":{(created ? "true" : "false")}}}");
        }

        private static ExternalLoginJson Login(HttpCall call) => JsonUtility.FromJson<ExternalLoginJson>(call.Body);

        private static HttpReply Error(string error) => new(400, $"{{\"error\":\"{error}\",\"error_description\":\"x\"}}");

#pragma warning disable CS0649, CS8618 // Written and read by JsonUtility.
        [Serializable]
        private sealed class ExternalLoginJson
        {
            public string provider;
            public string idToken;
            public string nonce;
            public string authorizationCode;
        }

        [Serializable]
        private sealed class AccountJson
        {
            public string id;
            public string createdAt;
            public LoginJson[] logins;
        }

        [Serializable]
        private sealed class LoginJson
        {
            public string provider;
            public string linkedAt;
        }
#pragma warning restore CS0649, CS8618
    }

    /// <summary>
    /// Apple or Google for one player: hands out tokens the fake server accepts, carrying the nonce hash it was given.
    /// </summary>
    public sealed class FakeLogin : ILoginProvider
    {
        private int _serial;

        public FakeLogin(string provider, string subject)
        {
            Provider = provider;
            Subject = subject;
        }

        public string Provider { get; }

        /// <summary>The player at the provider; change it to sign in as someone else.</summary>
        public string Subject { get; set; }

        public bool CancelNext { get; set; }

        public bool FailNext { get; set; }

        public List<string> NonceHashes { get; } = new();

        public Task<ProviderSignIn> SignInAsync(string nonceHash)
        {
            NonceHashes.Add(nonceHash);
            if (CancelNext || FailNext)
            {
                bool cancel = CancelNext;
                CancelNext = FailNext = false;
                return Task.FromException<ProviderSignIn>(cancel ? new SignInCancelledException() : new LoginProviderException("down"));
            }

            string idToken = $"{Provider}|{Subject}|{nonceHash}|{++_serial}";
            return Task.FromResult(new ProviderSignIn(idToken, Provider == LoginProviders.Apple ? "apple-code-" + _serial : null));
        }
    }
}
