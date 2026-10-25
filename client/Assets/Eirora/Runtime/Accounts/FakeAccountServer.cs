using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eirora.Contracts;
using Eirora.Simulation.Saving;
using UnityEngine;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// Eirora.Server's account API in memory for tests and the screenshot tour, with the same rules as the real one
    /// (ADR-0008): ID tokens bound to the nonce and accepted once, access tokens for 15 minutes, refresh tokens rotated
    /// and revocable, linking, removing a login and deleting confirmed with a fresh sign-in with one of the account's own
    /// logins, and the Apple rule for deletion, and the cloud save with its versions and key epochs (TASK-023). Speaks
    /// the same wire format, which the contract test pins to the server's (<c>ClientRequestTests</c>), so the client is
    /// tested down to the bytes it sends.
    /// </summary>
    public sealed class FakeAccountServer : IHttpTransport
    {
        public const string Url = "https://eirora.test";
        public static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);

        private const string LoginsPath = "/api/account/logins/";
        private const string TransfersPath = "/api/account/key-transfers/";
        private const string RemovalSuffix = "/removal";

        private readonly Func<DateTimeOffset> _now;
        private readonly HashSet<string> _redeemedIdTokens = new();
        private readonly Dictionary<string, (Guid Account, DateTimeOffset Expires)> _access = new();
        private readonly Dictionary<string, Guid> _refresh = new();
        private readonly Dictionary<Guid, List<(string Provider, string Subject)>> _accounts = new();
        private readonly Dictionary<Guid, StoredCloudSave> _cloudSaves = new();
        private readonly Dictionary<Guid, ContributionConsent> _consents = new();
        private readonly Dictionary<string, StoredKeyTransfer> _keyTransfers = new();
        private int _serial;

        public FakeAccountServer(Func<DateTimeOffset> now) => _now = now;

        /// <summary>When false, every call answers as if the network were down.</summary>
        public bool Online { get; set; } = true;

        /// <summary>When false, the cloud save still comes down but an upload answers as if the connection dropped.</summary>
        public bool TakesUploads { get; set; } = true;

        /// <summary>
        /// Whether a new account has consented to the contribution part (TASK-060): null until the player answers, as on the
        /// real server; true for tests about the cloud save that stand for a player who said yes.
        /// </summary>
        public bool? ConsentOfNewAccounts { get; set; }

        /// <summary>When true, an answer about the consent gets 500, as from a server that is failing.</summary>
        public bool FailsConsentAnswers { get; set; }

        /// <summary>How many uploads that carried a contribution part were stored.</summary>
        public int PartsStored { get; private set; }

        public List<HttpCall> Calls { get; } = new();

        public List<string> AppleCodesRevoked { get; } = new();

        public int Accounts => _accounts.Count;

        /// <summary>How many contribution parts a new key epoch has locked, as the real server keeps them for 90 days.</summary>
        public int LockedParts { get; private set; }

        /// <summary>The cloud save of the account the login belongs to, or null.</summary>
        public CloudSave? CloudSaveOf(string subject)
        {
            var account = _accounts.FirstOrDefault(a => a.Value.Any(l => l.Subject == subject)).Key;
            return _cloudSaves.TryGetValue(account, out var save) ? save.ToCloudSave() : null;
        }

        /// <summary>The answer about the contribution part of the account the login belongs to.</summary>
        public ContributionConsent ConsentOf(string subject)
        {
            var account = _accounts.First(a => a.Value.Any(l => l.Subject == subject)).Key;
            return _consents.TryGetValue(account, out var consent) ? consent : new ContributionConsent(ConsentAnswer.Unanswered, 0, null);
        }

        /// <summary>Forgets the account's answer, as for an account from before the question (TASK-060).</summary>
        public void ForgetConsentOf(string subject) => _consents.Remove(_accounts.First(a => a.Value.Any(l => l.Subject == subject)).Key);

        /// <summary>How many key transfers are pending on the server.</summary>
        public int PendingKeyTransfers => _keyTransfers.Count(t => t.Value.ExpiresAt > _now());

        /// <summary>Lets the transfer's ten minutes run out.</summary>
        public void ExpireKeyTransfer(string id) => _keyTransfers[id].ExpiresAt = _now();

        /// <summary>Replaces the stored contribution part, as someone who reaches the database could.</summary>
        public void TamperWithContributionsOf(string subject)
        {
            var account = _accounts.First(a => a.Value.Any(l => l.Subject == subject)).Key;
            var contributions = _cloudSaves[account].Contributions!;
            contributions[contributions.Length - 1] ^= 0x01;
        }

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
            if (!TakesUploads && call.Method == "PUT" && path == "/api/account/cloud-save")
            {
                return Task.FromResult(new HttpReply(0));
            }

            return Task.FromResult((call.Method, path) switch
            {
                ("POST", "/connect/token") => Token(UrlQuery.Parse(call.Body!)),
                ("POST", "/connect/revoke") => Revoke(UrlQuery.Parse(call.Body!)),
                ("GET", "/api/account") => Authorized(call, GetAccount),
                ("POST", "/api/account/logins") => Authorized(call, account => Link(account, JsonUtility.FromJson<LoginLinkJson>(call.Body))),
                ("POST", _) when path.StartsWith(LoginsPath, StringComparison.Ordinal) && path.EndsWith(RemovalSuffix, StringComparison.Ordinal) =>
                    Authorized(call, account => RemoveLogin(account, Uri.UnescapeDataString(path.Substring(LoginsPath.Length, path.Length - LoginsPath.Length - RemovalSuffix.Length)), Login(call))),
                ("POST", "/api/account/deletion") => Authorized(call, account => Delete(account, Login(call))),
                ("GET", "/api/account/cloud-save") => Authorized(call, GetCloudSave),
                ("PUT", "/api/account/cloud-save") => Authorized(call, account => UploadCloudSave(account, call.Body)),
                ("GET", "/api/account/contribution-consent") => Authorized(call, account => new HttpReply(200, ConsentJson(ConsentOf(account)))),
                ("PUT", "/api/account/contribution-consent") => FailsConsentAnswers ? new HttpReply(500) : Authorized(call, account => AnswerConsent(account, call.Body)),
                ("POST", "/api/account/key-transfers") => Authorized(call, account => CreateKeyTransfer(account, call.Body)),
                ("GET", _) when path.StartsWith(TransfersPath, StringComparison.Ordinal) => Authorized(call, account => TakeKeyTransfer(account, TransferId(path))),
                ("PUT", _) when path.StartsWith(TransfersPath, StringComparison.Ordinal) => Authorized(call, account => DeliverKeyTransfer(account, TransferId(path), call.Body)),
                ("DELETE", _) when path.StartsWith(TransfersPath, StringComparison.Ordinal) => Authorized(call, account => DeleteKeyTransfer(account, TransferId(path))),
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
                    if (ConsentOfNewAccounts is { } given)
                    {
                        _consents[account] = new ContributionConsent(given ? ConsentAnswer.Given : ConsentAnswer.Declined, ContributionConsent.CurrentVersion, _now());
                    }
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

        private HttpReply Link(Guid account, LoginLinkJson link)
        {
            var confirmation = Validate(link.confirmation);
            var identity = Validate(link.login);
            if (confirmation is null || identity is null)
            {
                return new HttpReply(400);
            }

            if (!_accounts[account].Contains(confirmation.Value))
            {
                return new HttpReply(403);
            }

            if (_accounts.Any(a => a.Key != account && a.Value.Contains(identity.Value)) || _accounts[account].Any(l => l.Provider == identity.Value.Provider && l != identity.Value))
            {
                return new HttpReply(409);
            }

            if (!_accounts[account].Contains(identity.Value))
            {
                _accounts[account].Add(identity.Value);
            }

            return new HttpReply(204);
        }

        private HttpReply RemoveLogin(Guid account, string provider, ExternalLoginJson confirmation)
        {
            var identity = Validate(confirmation);
            if (identity is null)
            {
                return new HttpReply(400);
            }

            var logins = _accounts[account];
            if (!logins.Contains(identity.Value))
            {
                return new HttpReply(403);
            }

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
            var identity = Validate(confirmation);
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

        private HttpReply GetCloudSave(Guid account) =>
            _cloudSaves.TryGetValue(account, out var save) ? new HttpReply(200, save.ToJson()) : new HttpReply(404);

        private ContributionConsent ConsentOf(Guid account) =>
            _consents.TryGetValue(account, out var consent) ? consent : new ContributionConsent(ConsentAnswer.Unanswered, 0, null);

        /// <summary>Keeps the answer; a decline deletes the contribution part and the locked ones, as the real server does.</summary>
        private HttpReply AnswerConsent(Guid account, string? body)
        {
            AccountServer.ConsentAnswerJson? answer;
            try
            {
                answer = JsonUtility.FromJson<AccountServer.ConsentAnswerJson>(body);
            }
            catch (ArgumentException)
            {
                return new HttpReply(400);
            }

            if (answer is null || answer.version < 1 || answer.version > ContributionConsent.CurrentVersion)
            {
                return new HttpReply(400);
            }

            var consent = new ContributionConsent(answer.given ? ConsentAnswer.Given : ConsentAnswer.Declined, answer.version, _now());
            _consents[account] = consent;
            if (!answer.given && _cloudSaves.TryGetValue(account, out var save))
            {
                LockedParts = 0;
                _cloudSaves[account] = new StoredCloudSave(save.Version, save.KeyEpoch, save.World, null, save.SavedAt);
            }

            return new HttpReply(200, ConsentJson(consent));
        }

        private static string ConsentJson(ContributionConsent consent) => JsonUtility.ToJson(new AccountServer.ConsentJson
        {
            answer = (int)consent.Answer,
            version = consent.Version,
            answeredAt = consent.AnsweredAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        });

        /// <summary>
        /// The server's rules: the world part must load as one, the base version must be current, the key epoch never
        /// sinks, and the upload carries a contribution part exactly when the player consents (TASK-060).
        /// </summary>
        private HttpReply UploadCloudSave(Guid account, string? body)
        {
            AccountServer.CloudSaveUploadJson? upload;
            try
            {
                upload = JsonUtility.FromJson<AccountServer.CloudSaveUploadJson>(body);
            }
            catch (ArgumentException)
            {
                return new HttpReply(400);
            }

            bool withPart = !string.IsNullOrEmpty(upload?.contributions);
            if (upload is null || upload.baseVersion < 0 || (withPart ? upload.keyEpoch < 1 : upload.keyEpoch != 0) || string.IsNullOrEmpty(upload.world))
            {
                return new HttpReply(400);
            }

            byte[] world, contributions;
            try
            {
                world = Convert.FromBase64String(upload.world);
                contributions = Convert.FromBase64String(upload.contributions ?? string.Empty);
                Save.FromParts(world, null);
            }
            catch (FormatException)
            {
                return new HttpReply(400);
            }

            if (world.Length > CloudSaveUpload.MaxWorldBytes || contributions.Length > CloudSaveUpload.MaxContributionBytes)
            {
                return new HttpReply(413);
            }

            _cloudSaves.TryGetValue(account, out var current);
            long version = current?.Version ?? 0;
            int keyEpoch = current?.KeyEpoch ?? 0;
            var consent = ConsentOf(account).Answer;
            if (consent == ConsentAnswer.Unanswered || (consent == ConsentAnswer.Given) != withPart
                || upload.baseVersion != version || (withPart && upload.keyEpoch < keyEpoch))
            {
                return new HttpReply(409, VersionJson(version, keyEpoch));
            }

            if (current?.Contributions is not null && withPart && upload.keyEpoch > current.KeyEpoch)
            {
                LockedParts++;
            }

            int epoch = Math.Max(keyEpoch, upload.keyEpoch);
            PartsStored += withPart ? 1 : 0;
            _cloudSaves[account] = new StoredCloudSave(version + 1, epoch, world, withPart ? contributions : null, _now());
            return new HttpReply(200, VersionJson(version + 1, epoch));
        }

        private static string TransferId(string path) => Uri.UnescapeDataString(path.Substring(TransfersPath.Length));

        private HttpReply CreateKeyTransfer(Guid account, string? body)
        {
            AccountServer.KeyTransferRequestJson? request;
            try
            {
                request = JsonUtility.FromJson<AccountServer.KeyTransferRequestJson>(body);
            }
            catch (ArgumentException)
            {
                return new HttpReply(400);
            }

            byte[] publicKey;
            try
            {
                publicKey = Convert.FromBase64String(request?.receiverPublicKey ?? string.Empty);
            }
            catch (FormatException)
            {
                return new HttpReply(400);
            }

            if (publicKey.Length != KeyTransfer.PublicKeyLength)
            {
                return new HttpReply(400);
            }

            foreach (var pending in _keyTransfers.Where(t => t.Value.Account == account).Select(t => t.Key).ToList())
            {
                _keyTransfers.Remove(pending);
            }

            var transfer = new StoredKeyTransfer(account, "transfer-" + (++_serial), publicKey, _now() + KeyTransfer.Lifetime);
            _keyTransfers[transfer.Id] = transfer;
            return new HttpReply(201, transfer.ToJson());
        }

        private HttpReply TakeKeyTransfer(Guid account, string id)
        {
            if (!_keyTransfers.TryGetValue(id, out var transfer) || transfer.Account != account || transfer.ExpiresAt <= _now())
            {
                return new HttpReply(404);
            }

            if (transfer.SealedKey is not null)
            {
                _keyTransfers.Remove(id);
            }

            return new HttpReply(200, transfer.ToJson());
        }

        private HttpReply DeliverKeyTransfer(Guid account, string id, string? body)
        {
            AccountServer.KeyTransferDeliveryJson? delivery;
            try
            {
                delivery = JsonUtility.FromJson<AccountServer.KeyTransferDeliveryJson>(body);
            }
            catch (ArgumentException)
            {
                return new HttpReply(400);
            }

            byte[] sealedKey;
            try
            {
                sealedKey = Convert.FromBase64String(delivery?.sealedKey ?? string.Empty);
            }
            catch (FormatException)
            {
                return new HttpReply(400);
            }

            if (sealedKey.Length == 0 || sealedKey.Length > KeyTransfer.MaxSealedKeyBytes)
            {
                return new HttpReply(400);
            }

            if (!_keyTransfers.TryGetValue(id, out var transfer) || transfer.Account != account || transfer.ExpiresAt <= _now())
            {
                return new HttpReply(404);
            }

            if (transfer.SealedKey is not null)
            {
                return new HttpReply(409);
            }

            transfer.SealedKey = sealedKey;
            return new HttpReply(204);
        }

        private HttpReply DeleteKeyTransfer(Guid account, string id) =>
            _keyTransfers.TryGetValue(id, out var transfer) && transfer.Account == account && _keyTransfers.Remove(id)
                ? new HttpReply(204)
                : new HttpReply(404);

        private static string VersionJson(long version, int keyEpoch) =>
            JsonUtility.ToJson(new AccountServer.CloudSaveVersionJson { version = version, keyEpoch = keyEpoch });

        private void Remove(Guid account)
        {
            _accounts.Remove(account);
            _cloudSaves.Remove(account);
            _consents.Remove(account);
            foreach (var pending in _keyTransfers.Where(t => t.Value.Account == account).Select(t => t.Key).ToList())
            {
                _keyTransfers.Remove(pending);
            }

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

        private (string Provider, string Subject)? Validate(ExternalLoginJson? login) =>
            login is null || string.IsNullOrEmpty(login.provider) ? null : Validate(login.provider, login.idToken, login.nonce);

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

        private sealed class StoredCloudSave
        {
            public StoredCloudSave(long version, int keyEpoch, byte[] world, byte[]? contributions, DateTimeOffset savedAt)
            {
                Version = version;
                KeyEpoch = keyEpoch;
                World = world;
                Contributions = contributions;
                SavedAt = savedAt;
            }

            public long Version { get; }

            public int KeyEpoch { get; }

            public byte[] World { get; }

            public byte[]? Contributions { get; }

            public DateTimeOffset SavedAt { get; }

            public CloudSave ToCloudSave() => new(Version, KeyEpoch, World, Contributions, SavedAt);

            public string ToJson() => JsonUtility.ToJson(new AccountServer.CloudSaveJson
            {
                version = Version,
                keyEpoch = KeyEpoch,
                world = Convert.ToBase64String(World),
                contributions = Contributions is null ? null : Convert.ToBase64String(Contributions),
                savedAt = SavedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        private sealed class StoredKeyTransfer
        {
            public StoredKeyTransfer(Guid account, string id, byte[] receiverPublicKey, DateTimeOffset expiresAt)
            {
                Account = account;
                Id = id;
                ReceiverPublicKey = receiverPublicKey;
                ExpiresAt = expiresAt;
            }

            public Guid Account { get; }

            public string Id { get; }

            public byte[] ReceiverPublicKey { get; }

            public byte[]? SealedKey { get; set; }

            public DateTimeOffset ExpiresAt { get; set; }

            public string ToJson() => JsonUtility.ToJson(new AccountServer.KeyTransferJson
            {
                id = Id,
                receiverPublicKey = Convert.ToBase64String(ReceiverPublicKey),
                sealedKey = SealedKey is null ? string.Empty : Convert.ToBase64String(SealedKey),
                expiresAt = ExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            });
        }

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
        private sealed class LoginLinkJson
        {
            public ExternalLoginJson? login;
            public ExternalLoginJson? confirmation;
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
