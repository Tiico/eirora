using System;

namespace Eirora.Contracts
{
    /// <summary>A login linked to the account; the provider's identifier for the player is never sent back.</summary>
    public sealed class LinkedLogin
    {
        public LinkedLogin(string provider, DateTimeOffset linkedAt)
        {
            Provider = provider;
            LinkedAt = linkedAt;
        }

        /// <summary>One of <see cref="LoginProviders"/>.</summary>
        public string Provider { get; }

        public DateTimeOffset LinkedAt { get; }
    }
}
