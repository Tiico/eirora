namespace Eirora.Server.SignIn;

/// <summary>A provider ID token that has been used once, kept until it expires so it cannot be replayed.</summary>
public sealed class RedeemedIdToken
{
    /// <summary>SHA-256 of the token as lowercase hex; the token itself is never stored.</summary>
    public required string Hash { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }
}
