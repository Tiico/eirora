namespace Eirora.Contracts
{
    /// <summary>
    /// The token request a client sends to <c>POST /connect/token</c> to sign in with an Apple or Google ID token (ADR-0008).
    /// </summary>
    /// <remarks>
    /// The client generates a random nonce, gives the provider its SHA-256 as lowercase hex, and sends the nonce itself here.
    /// The first sign-in with a login creates the account (D-72). Refreshing uses the standard <c>refresh_token</c> grant and
    /// signing out revokes the refresh token at <c>POST /connect/revoke</c>.
    /// </remarks>
    public static class SignInGrant
    {
        public const string GrantType = "urn:eirora:params:oauth:grant-type:external-id-token";

        /// <summary>One of <see cref="LoginProviders"/>.</summary>
        public const string ProviderParameter = "provider";

        /// <summary>The ID token the provider gave the client.</summary>
        public const string IdTokenParameter = "external_id_token";

        /// <summary>The nonce whose SHA-256 the client gave the provider.</summary>
        public const string NonceParameter = "nonce";

        /// <summary>Scope to request for a refresh token.</summary>
        public const string OfflineAccessScope = "offline_access";

        /// <summary>Response parameter that is <c>true</c> when this sign-in created the account.</summary>
        public const string AccountCreatedParameter = "account_created";
    }
}
