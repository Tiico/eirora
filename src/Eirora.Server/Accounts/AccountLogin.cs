namespace Eirora.Server.Accounts;

/// <summary>A sign-in provider's identifier for the player, linked to an account; at most one per provider.</summary>
public sealed class AccountLogin
{
    /// <summary>One of <see cref="Contracts.LoginProviders"/>.</summary>
    public required string Provider { get; init; }

    /// <summary>The provider's <c>sub</c>: stable per player, and for Apple per developer team.</summary>
    public required string Subject { get; init; }

    public Guid AccountId { get; init; }

    public DateTimeOffset LinkedAt { get; init; }
}
