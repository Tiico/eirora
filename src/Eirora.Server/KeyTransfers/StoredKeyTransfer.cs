namespace Eirora.Server.KeyTransfers;

/// <summary>
/// A key transfer between two of an account's phones (ADR-0008, TASK-023b): the new phone's one-time public key, and
/// the sealed contribution key once the old phone delivered it. Short-lived and handed over once.
/// </summary>
public sealed class StoredKeyTransfer
{
    public required string Id { get; init; }

    public Guid AccountId { get; init; }

    public required byte[] ReceiverPublicKey { get; init; }

    public byte[]? SealedKey { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }
}
