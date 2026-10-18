namespace Eirora.Contracts
{
    /// <summary>
    /// A fresh sign-in with a provider, sent to link a login (<c>POST /api/account/logins</c>) or to confirm deleting the
    /// account (<c>POST /api/account/deletion</c>).
    /// </summary>
    public sealed class ExternalLogin
    {
        public ExternalLogin(string provider, string idToken, string nonce, string? authorizationCode = null)
        {
            Provider = provider;
            IdToken = idToken;
            Nonce = nonce;
            AuthorizationCode = authorizationCode;
        }

        /// <summary>One of <see cref="LoginProviders"/>.</summary>
        public string Provider { get; }

        public string IdToken { get; }

        /// <summary>The nonce whose SHA-256 as lowercase hex the client gave the provider.</summary>
        public string Nonce { get; }

        /// <summary>
        /// Apple's authorization code from the same sign-in; required to delete an account with an Apple login, so that the
        /// server can revoke the app's access to the Apple ID.
        /// </summary>
        public string? AuthorizationCode { get; }
    }
}
