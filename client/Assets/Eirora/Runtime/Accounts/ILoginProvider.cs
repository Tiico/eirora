using System;
using System.Threading.Tasks;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// Sign in with Apple or Google on this device (ADR-0008): native on iOS, Credential Manager for Google on Android and
    /// the web flow with a Services ID for Apple on Android, all behind this interface.
    /// </summary>
    public interface ILoginProvider
    {
        /// <summary>One of <see cref="Contracts.LoginProviders"/>.</summary>
        string Provider { get; }

        /// <summary>
        /// Lets the player sign in with the provider and answers with its ID token, whose <c>nonce</c> claim is
        /// <paramref name="nonceHash"/> (the SHA-256 of the client's nonce as lowercase hex, <see cref="Nonce.Hash"/>).
        /// </summary>
        /// <exception cref="SignInCancelledException">The player closed the provider's dialog.</exception>
        /// <exception cref="LoginProviderException">The provider could not sign the player in.</exception>
        Task<ProviderSignIn> SignInAsync(string nonceHash);
    }

    /// <summary>What a provider gives the client after a sign-in.</summary>
    public sealed class ProviderSignIn
    {
        public ProviderSignIn(string idToken, string? authorizationCode = null)
        {
            IdToken = idToken;
            AuthorizationCode = authorizationCode;
        }

        public string IdToken { get; }

        /// <summary>Apple's authorization code, which the server needs to revoke the app at Apple when the account is deleted.</summary>
        public string? AuthorizationCode { get; }
    }

    /// <summary>The player closed the provider's sign-in without signing in.</summary>
    public sealed class SignInCancelledException : Exception
    {
        public SignInCancelledException()
            : base("The sign-in was cancelled.")
        {
        }
    }

    /// <summary>The provider could not sign the player in; the message names the reason, never a token or identifier.</summary>
    public sealed class LoginProviderException : Exception
    {
        public LoginProviderException(string message)
            : base(message)
        {
        }
    }
}
