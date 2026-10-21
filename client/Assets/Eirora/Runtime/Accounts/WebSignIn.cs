using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// A provider's sign-in page in the system browser: ASWebAuthenticationSession on iOS, a Custom Tab on Android.
    /// </summary>
    public interface IWebAuthenticator
    {
        /// <summary>
        /// Opens <paramref name="url"/> and answers with the URL the page finally redirects to on
        /// <paramref name="callbackScheme"/>, with its query.
        /// </summary>
        /// <exception cref="SignInCancelledException">The player closed the page.</exception>
        /// <exception cref="LoginProviderException">No browser could be opened.</exception>
        Task<string> AuthenticateAsync(string url, string callbackScheme);
    }

    /// <summary>Form and query strings as OAuth uses them.</summary>
    public static class UrlQuery
    {
        public static string Encode(IEnumerable<KeyValuePair<string, string>> values) =>
            string.Join("&", values.Select(v => Uri.EscapeDataString(v.Key) + "=" + Uri.EscapeDataString(v.Value)));

        /// <summary>The parameters in a URL's query and fragment together.</summary>
        public static Dictionary<string, string> ParseUrl(string url)
        {
            int start = url.IndexOfAny(new[] { '?', '#' });
            return Parse(start < 0 ? string.Empty : url.Substring(start + 1).Replace('#', '&'));
        }

        /// <summary>The parameters of a query string or a form body.</summary>
        public static Dictionary<string, string> Parse(string query)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in query.Split('&'))
            {
                if (pair.Length == 0)
                {
                    continue;
                }

                int equals = pair.IndexOf('=');
                string key = equals < 0 ? pair : pair.Substring(0, equals);
                string value = equals < 0 ? string.Empty : pair.Substring(equals + 1);
                values[Decode(key)] = Decode(value);
            }

            return values;
        }

        private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
    }

    /// <summary>
    /// Sign in with Apple on Android, as a web flow with the app's Services ID (ADR-0008). Apple posts the result to the
    /// server's callback, which hands it to the app on the <c>eirora-signin</c> scheme, restricted to the app's package.
    /// </summary>
    /// <remarks>
    /// No scopes are asked for, so Apple sends neither name nor email (D-72). The state ties the answer to this attempt.
    /// </remarks>
    public sealed class AppleWebLogin : ILoginProvider
    {
        public const string CallbackScheme = "eirora-signin";
        public const string CallbackPath = "/signin/apple/callback";
        private const string AuthorizeUrl = "https://appleid.apple.com/auth/authorize";

        private readonly string _servicesId;
        private readonly string _serverUrl;
        private readonly IWebAuthenticator _browser;

        public AppleWebLogin(string servicesId, string serverUrl, IWebAuthenticator browser)
        {
            _servicesId = servicesId;
            _serverUrl = serverUrl.TrimEnd('/');
            _browser = browser;
        }

        public string Provider => Contracts.LoginProviders.Apple;

        public async Task<ProviderSignIn> SignInAsync(string nonceHash)
        {
            string state = Nonce.Random(16);
            string url = AuthorizeUrl + "?" + UrlQuery.Encode(new Dictionary<string, string>
            {
                ["client_id"] = _servicesId,
                ["redirect_uri"] = _serverUrl + CallbackPath,
                ["response_type"] = "code id_token",
                ["response_mode"] = "form_post",
                ["state"] = state,
                ["nonce"] = nonceHash,
            });

            var answer = UrlQuery.ParseUrl(await _browser.AuthenticateAsync(url, CallbackScheme));
            if (answer.TryGetValue("error", out var error))
            {
                throw error == "user_cancelled_authorize" ? new SignInCancelledException() : new LoginProviderException("Apple answered with an error");
            }

            if (!answer.TryGetValue("state", out var returned) || returned != state)
            {
                throw new LoginProviderException("The answer belongs to another sign-in");
            }

            if (!answer.TryGetValue("id_token", out var idToken) || idToken.Length == 0)
            {
                throw new LoginProviderException("Apple sent no ID token");
            }

            return new ProviderSignIn(idToken, answer.TryGetValue("code", out var code) && code.Length > 0 ? code : null);
        }
    }

    /// <summary>
    /// Google sign-in on iOS, as Google's OAuth flow for installed apps: the authorization code with PKCE in
    /// ASWebAuthenticationSession, exchanged for an ID token with the app's iOS client id (no client secret).
    /// </summary>
    /// <remarks>Only the <c>openid</c> scope is asked for, so the ID token carries no name or email (D-72).</remarks>
    public sealed class GoogleWebLogin : ILoginProvider
    {
        public const string AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth";
        public const string TokenUrl = "https://oauth2.googleapis.com/token";
        private const string ClientIdSuffix = ".apps.googleusercontent.com";

        private readonly string _clientId;
        private readonly IWebAuthenticator _browser;
        private readonly IHttpTransport _http;

        public GoogleWebLogin(string clientId, IWebAuthenticator browser, IHttpTransport http)
        {
            _clientId = clientId;
            _browser = browser;
            _http = http;
        }

        public string Provider => Contracts.LoginProviders.Google;

        /// <summary>The reversed client id, the redirect scheme Google gives every iOS client.</summary>
        public string CallbackScheme => "com.googleusercontent.apps." + _clientId.Replace(ClientIdSuffix, string.Empty);

        public async Task<ProviderSignIn> SignInAsync(string nonceHash)
        {
            string state = Nonce.Random(16);
            string verifier = Nonce.Random(32);
            string redirect = CallbackScheme + ":/oauth2redirect";
            string url = AuthorizeUrl + "?" + UrlQuery.Encode(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["redirect_uri"] = redirect,
                ["response_type"] = "code",
                ["scope"] = "openid",
                ["code_challenge"] = Challenge(verifier),
                ["code_challenge_method"] = "S256",
                ["state"] = state,
                ["nonce"] = nonceHash,
                ["prompt"] = "select_account",
            });

            var answer = UrlQuery.ParseUrl(await _browser.AuthenticateAsync(url, CallbackScheme));
            if (answer.TryGetValue("error", out var error))
            {
                throw error == "access_denied" ? new SignInCancelledException() : new LoginProviderException("Google answered with an error");
            }

            if (!answer.TryGetValue("state", out var returned) || returned != state || !answer.TryGetValue("code", out var code))
            {
                throw new LoginProviderException("The answer belongs to another sign-in");
            }

            var reply = await _http.SendAsync(new HttpCall("POST", TokenUrl, UrlQuery.Encode(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirect,
            }), HttpCall.Form));
            if (!reply.Succeeded)
            {
                throw new LoginProviderException(reply.Reached ? "Google did not exchange the code" : "Google could not be reached");
            }

            var tokens = UnityEngine.JsonUtility.FromJson<GoogleTokens>(reply.Body);
            if (string.IsNullOrEmpty(tokens?.id_token))
            {
                throw new LoginProviderException("Google sent no ID token");
            }

            return new ProviderSignIn(tokens!.id_token);
        }

        /// <summary>The PKCE S256 challenge: base64url of the verifier's SHA-256.</summary>
        public static string Challenge(string verifier)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return Nonce.Base64Url(sha.ComputeHash(System.Text.Encoding.ASCII.GetBytes(verifier)));
        }

        [Serializable]
        private sealed class GoogleTokens
        {
#pragma warning disable CS0649 // Filled in by JsonUtility, with the name Google uses.
            public string id_token = string.Empty;
#pragma warning restore CS0649
        }
    }
}
