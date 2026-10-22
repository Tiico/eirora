using System;

namespace Eirora.Contracts
{
    /// <summary>
    /// A key transfer between two of the account's phones (ADR-0008, TASK-023b): the new phone creates it with its
    /// one-time public key, the old phone delivers the contribution key sealed to that key, and the new phone fetches
    /// the sealed key once. The server relays the sealed bytes and can read none of them.
    /// </summary>
    public sealed class KeyTransfer
    {
        /// <summary>How long a transfer waits for the old phone before the server forgets it.</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        public const int PublicKeyLength = 32;

        public const int MaxSealedKeyBytes = 1024;

        public KeyTransfer(string id, byte[] receiverPublicKey, byte[]? sealedKey, DateTimeOffset expiresAt)
        {
            Id = id;
            ReceiverPublicKey = receiverPublicKey;
            SealedKey = sealedKey;
            ExpiresAt = expiresAt;
        }

        /// <summary>The transfer's id, random and unguessable, as the ticket carries it.</summary>
        public string Id { get; }

        /// <summary>The new phone's one-time X25519 public key.</summary>
        public byte[] ReceiverPublicKey { get; }

        /// <summary>The contribution key sealed for the new phone, once the old phone has delivered it.</summary>
        public byte[]? SealedKey { get; }

        public DateTimeOffset ExpiresAt { get; }
    }

    /// <summary>What the new phone sends to create a transfer: its one-time public key.</summary>
    public sealed class KeyTransferRequest
    {
        public KeyTransferRequest(byte[]? receiverPublicKey) => ReceiverPublicKey = receiverPublicKey;

        public byte[]? ReceiverPublicKey { get; }
    }

    /// <summary>What the old phone sends to deliver the key: the contribution key sealed for the new phone.</summary>
    public sealed class KeyTransferDelivery
    {
        public KeyTransferDelivery(byte[]? sealedKey) => SealedKey = sealedKey;

        public byte[]? SealedKey { get; }
    }
}
