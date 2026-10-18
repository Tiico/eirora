namespace Eirora.Contracts
{
    /// <summary>
    /// Links a login to the account (<c>POST /api/account/logins</c>): a fresh sign-in with the new login, confirmed by a
    /// fresh sign-in with one of the account's own logins, so that a stolen token alone cannot add a login.
    /// </summary>
    public sealed class LoginLink
    {
        public LoginLink(ExternalLogin? login, ExternalLogin? confirmation)
        {
            Login = login;
            Confirmation = confirmation;
        }

        /// <summary>The login to link; null when a request leaves it out, which the server rejects.</summary>
        public ExternalLogin? Login { get; }

        /// <summary>A sign-in with one of the logins the account already has; null when a request leaves it out, which the server rejects.</summary>
        public ExternalLogin? Confirmation { get; }
    }
}
