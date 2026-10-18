namespace Eirora.Server.Accounts;

/// <summary>
/// A player's identity on the server, created at the first sign-in with Apple or Google (D-72).
/// </summary>
/// <remarks>
/// The id is random and never derived from a provider, so no provider owns the player's identity. The account holds
/// nothing else about the player: no name, no email (ADR-0008).
/// </remarks>
public sealed class Account
{
    public Guid Id { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public List<AccountLogin> Logins { get; init; } = [];
}
