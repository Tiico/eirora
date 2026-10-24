using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Eirora.Contracts;
using UnityEngine;

namespace Eirora.Client.Accounts
{
    /// <summary>The server's account API on the wire (ADR-0008): OpenIddict's token endpoints and <c>/api/account</c>.</summary>
    internal sealed class AccountServer
    {
        private readonly string _baseUrl;
        private readonly IHttpTransport _http;

        public AccountServer(string baseUrl, IHttpTransport http)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = http;
        }

        /// <summary><c>POST /connect/token</c> with <see cref="SignInGrant"/>, asking for a refresh token.</summary>
        public Task<TokenReply> SignInAsync(string provider, string idToken, string nonce) => TokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = SignInGrant.GrantType,
            [SignInGrant.ProviderParameter] = provider,
            [SignInGrant.IdTokenParameter] = idToken,
            [SignInGrant.NonceParameter] = nonce,
            ["scope"] = SignInGrant.OfflineAccessScope,
        });

        /// <summary><c>POST /connect/token</c> with the refresh token, which the server rotates.</summary>
        public Task<TokenReply> RefreshAsync(string refreshToken) => TokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        });

        /// <summary><c>POST /connect/revoke</c>: signs the refresh token out on the server.</summary>
        public Task<HttpReply> RevokeAsync(string refreshToken) => _http.SendAsync(new HttpCall(
            "POST",
            _baseUrl + "/connect/revoke",
            UrlQuery.Encode(new Dictionary<string, string> { ["token"] = refreshToken, ["token_type_hint"] = "refresh_token" }),
            HttpCall.Form));

        public async Task<(HttpReply Reply, AccountInfo? Account)> GetAccountAsync(string accessToken)
        {
            var reply = await _http.SendAsync(new HttpCall("GET", _baseUrl + "/api/account", bearer: accessToken));
            return (reply, reply.Status == 200 ? ParseAccount(reply.Body) : null);
        }

        /// <summary><c>POST /api/account/logins</c>: links <paramref name="login"/>, confirmed with one of the account's own logins.</summary>
        public Task<HttpReply> LinkAsync(string accessToken, ExternalLogin login, ExternalLogin confirmation) =>
            _http.SendAsync(new HttpCall("POST", _baseUrl + "/api/account/logins", Json(login, confirmation), HttpCall.Json, accessToken));

        /// <summary><c>POST /api/account/logins/{provider}/removal</c>, confirmed with one of the account's own logins.</summary>
        public Task<HttpReply> RemoveLoginAsync(string accessToken, string provider, ExternalLogin confirmation) =>
            _http.SendAsync(new HttpCall(
                "POST",
                _baseUrl + "/api/account/logins/" + Uri.EscapeDataString(provider) + "/removal",
                Json(confirmation),
                HttpCall.Json,
                accessToken));

        /// <summary><c>POST /api/account/deletion</c>, confirmed with one of the account's own logins.</summary>
        public Task<HttpReply> DeleteAsync(string accessToken, ExternalLogin confirmation) =>
            _http.SendAsync(new HttpCall("POST", _baseUrl + "/api/account/deletion", Json(confirmation), HttpCall.Json, accessToken));

        /// <summary><c>GET /api/account/cloud-save</c>: the account's cloud save (ADR-0008), 404 when it has none.</summary>
        public async Task<(HttpReply Reply, CloudSave? Save)> GetCloudSaveAsync(string accessToken)
        {
            var reply = await _http.SendAsync(new HttpCall("GET", _baseUrl + "/api/account/cloud-save", bearer: accessToken));
            return (reply, reply.Status == 200 ? ParseCloudSave(reply.Body) : null);
        }

        /// <summary><c>PUT /api/account/cloud-save</c>: the new version with 200, the current one with 409.</summary>
        public async Task<(HttpReply Reply, CloudSaveVersion? Version)> UploadCloudSaveAsync(string accessToken, CloudSaveUpload upload)
        {
            var reply = await _http.SendAsync(new HttpCall("PUT", _baseUrl + "/api/account/cloud-save", Json(upload), HttpCall.Json, accessToken));
            return (reply, reply.Status == 200 || reply.Status == 409 ? ParseCloudSaveVersion(reply.Body) : null);
        }

        /// <summary><c>GET /api/account/contribution-consent</c>: the player's answer about the contribution part (TASK-060).</summary>
        public async Task<(HttpReply Reply, ContributionConsent? Consent)> GetConsentAsync(string accessToken)
        {
            var reply = await _http.SendAsync(new HttpCall("GET", _baseUrl + "/api/account/contribution-consent", bearer: accessToken));
            return (reply, reply.Status == 200 ? ParseConsent(reply.Body) : null);
        }

        /// <summary><c>PUT /api/account/contribution-consent</c>: the player's answer, as the server keeps it.</summary>
        public async Task<(HttpReply Reply, ContributionConsent? Consent)> AnswerConsentAsync(string accessToken, ContributionConsentAnswer answer)
        {
            var reply = await _http.SendAsync(new HttpCall("PUT", _baseUrl + "/api/account/contribution-consent", Json(answer), HttpCall.Json, accessToken));
            return (reply, reply.Status == 200 ? ParseConsent(reply.Body) : null);
        }

        /// <summary><c>POST /api/account/key-transfers</c>: a transfer with the new phone's one-time public key (TASK-023b).</summary>
        public async Task<(HttpReply Reply, KeyTransfer? Transfer)> CreateKeyTransferAsync(string accessToken, byte[] receiverPublicKey)
        {
            string body = JsonUtility.ToJson(new KeyTransferRequestJson { receiverPublicKey = Convert.ToBase64String(receiverPublicKey) });
            var reply = await _http.SendAsync(new HttpCall("POST", _baseUrl + "/api/account/key-transfers", body, HttpCall.Json, accessToken));
            return (reply, reply.Status == 201 ? ParseKeyTransfer(reply.Body) : null);
        }

        /// <summary><c>GET /api/account/key-transfers/{id}</c>: the transfer, with the sealed key once delivered.</summary>
        public async Task<(HttpReply Reply, KeyTransfer? Transfer)> GetKeyTransferAsync(string accessToken, string id)
        {
            var reply = await _http.SendAsync(new HttpCall("GET", _baseUrl + "/api/account/key-transfers/" + Uri.EscapeDataString(id), bearer: accessToken));
            return (reply, reply.Status == 200 ? ParseKeyTransfer(reply.Body) : null);
        }

        /// <summary><c>PUT /api/account/key-transfers/{id}</c>: the contribution key sealed for the new phone.</summary>
        public Task<HttpReply> DeliverKeyTransferAsync(string accessToken, string id, byte[] sealedKey) => _http.SendAsync(new HttpCall(
            "PUT",
            _baseUrl + "/api/account/key-transfers/" + Uri.EscapeDataString(id),
            JsonUtility.ToJson(new KeyTransferDeliveryJson { sealedKey = Convert.ToBase64String(sealedKey) }),
            HttpCall.Json,
            accessToken));

        /// <summary><c>DELETE /api/account/key-transfers/{id}</c>: gives the transfer up.</summary>
        public Task<HttpReply> DeleteKeyTransferAsync(string accessToken, string id) =>
            _http.SendAsync(new HttpCall("DELETE", _baseUrl + "/api/account/key-transfers/" + Uri.EscapeDataString(id), bearer: accessToken));

        public static KeyTransfer? ParseKeyTransfer(string json)
        {
            KeyTransferJson? transfer;
            try
            {
                transfer = JsonUtility.FromJson<KeyTransferJson>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (transfer is null || string.IsNullOrEmpty(transfer.id) || transfer.receiverPublicKey is null)
            {
                return null;
            }

            try
            {
                return new KeyTransfer(
                    transfer.id,
                    Convert.FromBase64String(transfer.receiverPublicKey),
                    string.IsNullOrEmpty(transfer.sealedKey) ? null : Convert.FromBase64String(transfer.sealedKey),
                    Time(transfer.expiresAt ?? string.Empty));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>A <see cref="ContributionConsentAnswer"/> as the server reads it.</summary>
        public static string Json(ContributionConsentAnswer answer) =>
            JsonUtility.ToJson(new ConsentAnswerJson { given = answer.Given, version = answer.Version });

        public static ContributionConsent? ParseConsent(string json)
        {
            ConsentJson? consent;
            try
            {
                consent = JsonUtility.FromJson<ConsentJson>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (consent is null || !Enum.IsDefined(typeof(ConsentAnswer), consent.answer) || consent.version < 0)
            {
                return null;
            }

            var answer = (ConsentAnswer)consent.answer;
            return new ContributionConsent(answer, consent.version, answer == ConsentAnswer.Unanswered ? null : Time(consent.answeredAt ?? string.Empty));
        }

        /// <summary>A <see cref="CloudSaveUpload"/> as the server reads it: the parts in base64, the contribution part empty when there is none.</summary>
        public static string Json(CloudSaveUpload upload) => JsonUtility.ToJson(new CloudSaveUploadJson
        {
            baseVersion = upload.BaseVersion,
            keyEpoch = upload.KeyEpoch,
            world = Convert.ToBase64String(upload.World ?? Array.Empty<byte>()),
            contributions = Convert.ToBase64String(upload.Contributions ?? Array.Empty<byte>()),
        });

        public static CloudSave? ParseCloudSave(string json)
        {
            CloudSaveJson? save;
            try
            {
                save = JsonUtility.FromJson<CloudSaveJson>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }

            // Without consent the cloud save is its world part alone (TASK-060): no contribution part, and possibly epoch 0.
            bool withPart = !string.IsNullOrEmpty(save?.contributions);
            if (save is null || save.version < 1 || save.keyEpoch < (withPart ? 1 : 0) || string.IsNullOrEmpty(save.world))
            {
                return null;
            }

            try
            {
                return new CloudSave(
                    save.version,
                    save.keyEpoch,
                    Convert.FromBase64String(save.world),
                    withPart ? Convert.FromBase64String(save.contributions) : null,
                    Time(save.savedAt ?? string.Empty));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public static CloudSaveVersion? ParseCloudSaveVersion(string json)
        {
            CloudSaveVersionJson? version;
            try
            {
                version = JsonUtility.FromJson<CloudSaveVersionJson>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return version is null || version.version < 0 ? null : new CloudSaveVersion(version.version, version.keyEpoch);
        }

        /// <summary>An <see cref="ExternalLogin"/> as the server reads it.</summary>
        public static string Json(ExternalLogin login) => JsonUtility.ToJson(ToJson(login));

        /// <summary>A <see cref="LoginLink"/> as the server reads it.</summary>
        public static string Json(ExternalLogin login, ExternalLogin confirmation) => JsonUtility.ToJson(new LoginLinkJson
        {
            login = ToJson(login),
            confirmation = ToJson(confirmation),
        });

        private static ExternalLoginJson ToJson(ExternalLogin login) => new()
        {
            provider = login.Provider,
            idToken = login.IdToken,
            nonce = login.Nonce,
            authorizationCode = login.AuthorizationCode ?? string.Empty,
        };

        public static AccountInfo? ParseAccount(string json)
        {
            var account = JsonUtility.FromJson<AccountJson>(json);
            if (account is null || !Guid.TryParse(account.id, out var id))
            {
                return null;
            }

            return new AccountInfo(
                id,
                Time(account.createdAt),
                (account.logins ?? new LinkedLoginJson[0]).Select(l => new LinkedLogin(l.provider, Time(l.linkedAt))).ToList());
        }

        private async Task<TokenReply> TokenAsync(Dictionary<string, string> form)
        {
            var reply = await _http.SendAsync(new HttpCall("POST", _baseUrl + "/connect/token", UrlQuery.Encode(form), HttpCall.Form));
            return reply.Reached ? ParseTokens(reply.Status, reply.Body) : new TokenReply(reply.Status, null);
        }

        public static TokenReply ParseTokens(int status, string json)
        {
            TokenJson? tokens;
            try
            {
                tokens = JsonUtility.FromJson<TokenJson>(json);
            }
            catch (ArgumentException)
            {
                tokens = null; // Not JSON, such as a proxy's error page.
            }

            return new TokenReply(status, tokens);
        }

        private static DateTimeOffset Time(string text) =>
            DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time) ? time : default;

#pragma warning disable CS0649, CS8618 // Filled in and written by JsonUtility with the names on the wire.
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
            public ExternalLoginJson login;
            public ExternalLoginJson confirmation;
        }

        [Serializable]
        private sealed class AccountJson
        {
            public string id;
            public string createdAt;
            public LinkedLoginJson[]? logins;
        }

        [Serializable]
        private sealed class LinkedLoginJson
        {
            public string provider;
            public string linkedAt;
        }

        [Serializable]
        internal sealed class CloudSaveUploadJson
        {
            public long baseVersion;
            public int keyEpoch;
            public string world;
            public string contributions;
        }

        [Serializable]
        internal sealed class CloudSaveJson
        {
            public long version;
            public int keyEpoch;
            public string? world;
            public string? contributions;
            public string? savedAt;
        }

        [Serializable]
        internal sealed class KeyTransferRequestJson
        {
            public string receiverPublicKey;
        }

        [Serializable]
        internal sealed class KeyTransferDeliveryJson
        {
            public string sealedKey;
        }

        [Serializable]
        internal sealed class KeyTransferJson
        {
            public string? id;
            public string? receiverPublicKey;
            public string? sealedKey;
            public string? expiresAt;
        }

        [Serializable]
        internal sealed class ConsentAnswerJson
        {
            public bool given;
            public int version;
        }

        [Serializable]
        internal sealed class ConsentJson
        {
            public int answer;
            public int version;
            public string? answeredAt;
        }

        [Serializable]
        internal sealed class CloudSaveVersionJson
        {
            public long version;
            public int keyEpoch;
        }

        [Serializable]
        internal sealed class TokenJson
        {
            public string? access_token;
            public string? refresh_token;
            public long expires_in;
            public bool account_created;
            public string? error;
        }
#pragma warning restore CS0649, CS8618
    }

    /// <summary>The token endpoint's answer.</summary>
    internal sealed class TokenReply
    {
        public TokenReply(int status, AccountServer.TokenJson? tokens)
        {
            Status = status;
            AccessToken = tokens?.access_token is { Length: > 0 } access ? access : null;
            RefreshToken = tokens?.refresh_token is { Length: > 0 } refresh ? refresh : null;
            long seconds = tokens?.expires_in ?? 0;
            ExpiresIn = TimeSpan.FromSeconds(seconds > 0 ? seconds : 0);
            AccountCreated = tokens?.account_created ?? false;
            Error = tokens?.error is { Length: > 0 } error ? error : null;
        }

        /// <summary>The HTTP status, or 0 when the server could not be reached.</summary>
        public int Status { get; }

        public string? AccessToken { get; }

        public string? RefreshToken { get; }

        public TimeSpan ExpiresIn { get; }

        public bool AccountCreated { get; }

        /// <summary>The OAuth error, such as <c>invalid_grant</c> when the refresh token or the account is gone.</summary>
        public string? Error { get; }
    }
}
