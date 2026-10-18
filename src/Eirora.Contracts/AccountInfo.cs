using System;
using System.Collections.Generic;

namespace Eirora.Contracts
{
    /// <summary>Response of <c>GET /api/account</c>: everything the server stores about the account itself.</summary>
    public sealed class AccountInfo
    {
        public AccountInfo(Guid id, DateTimeOffset createdAt, IReadOnlyList<LinkedLogin> logins)
        {
            Id = id;
            CreatedAt = createdAt;
            Logins = logins;
        }

        public Guid Id { get; }

        public DateTimeOffset CreatedAt { get; }

        public IReadOnlyList<LinkedLogin> Logins { get; }
    }
}
