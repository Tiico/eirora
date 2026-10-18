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

        public Task<HttpReply> LinkAsync(string accessToken, ExternalLogin login) =>
            _http.SendAsync(new HttpCall("POST", _baseUrl + "/api/account/logins", Json(login), HttpCall.Json, accessToken));

        public Task<HttpReply> UnlinkAsync(string accessToken, string provider) =>
            _http.SendAsync(new HttpCall("DELETE", _baseUrl + "/api/account/logins/" + Uri.EscapeDataString(provider), bearer: accessToken));

        public Task<HttpReply> DeleteAsync(string accessToken, ExternalLogin confirmation) =>
            _http.SendAsync(new HttpCall("POST", _baseUrl + "/api/account/deletion", Json(confirmation), HttpCall.Json, accessToken));

        public static string Json(ExternalLogin login) => JsonUtility.ToJson(new ExternalLoginJson
        {
            provider = login.Provider,
            idToken = login.IdToken,
            nonce = login.Nonce,
            authorizationCode = login.AuthorizationCode ?? string.Empty,
        });

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
            if (!reply.Reached)
            {
                return new TokenReply(reply.Status, null);
            }

            TokenJson? tokens;
            try
            {
                tokens = JsonUtility.FromJson<TokenJson>(reply.Body);
            }
            catch (ArgumentException)
            {
                tokens = null; // Not JSON, such as a proxy's error page.
            }

            return new TokenReply(reply.Status, tokens);
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
