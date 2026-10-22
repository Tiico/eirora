using System.Buffers.Text;
using System.Security.Cryptography;
using Eirora.Contracts;
using Eirora.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Eirora.Server.KeyTransfers;

/// <summary>
/// Relays a contribution key between two of an account's phones (ADR-0008, TASK-023b). The server stores the new
/// phone's public key and the sealed key for a few minutes, hands the sealed key over once, and never holds anything
/// it could read. An account has one transfer at a time: a new one replaces what was pending.
/// </summary>
public sealed class KeyTransferService(EiroraDbContext db, TimeProvider time)
{
    private const int IdBytes = 16;

    public async Task<KeyTransfer> CreateAsync(Guid accountId, byte[] receiverPublicKey, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await db.KeyTransfers.Where(t => t.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        var transfer = new StoredKeyTransfer
        {
            Id = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(IdBytes)),
            AccountId = accountId,
            ReceiverPublicKey = receiverPublicKey,
            CreatedAt = now,
            ExpiresAt = now + KeyTransfer.Lifetime,
        };
        db.KeyTransfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken);
        return ToContract(transfer);
    }

    /// <summary>
    /// The transfer, for the new phone. A transfer whose sealed key has been delivered is handed over once: it is
    /// deleted as it is read, so that the sealed bytes do not stay on the server.
    /// </summary>
    public async Task<KeyTransfer?> TakeAsync(Guid accountId, string id, CancellationToken cancellationToken)
    {
        var transfer = await PendingAsync(accountId, id, cancellationToken);
        if (transfer is null)
        {
            return null;
        }

        // Deleted by the same statement that checks it is still there, so that two takes at once hand it over once.
        if (transfer.SealedKey is not null
            && await db.KeyTransfers.Where(t => t.Id == id && t.AccountId == accountId && t.SealedKey != null).ExecuteDeleteAsync(cancellationToken) == 0)
        {
            return null;
        }

        return ToContract(transfer);
    }

    /// <summary>Stores the sealed key from the old phone, once: a second delivery, even at the same moment, conflicts.</summary>
    public async Task<DeliveryStatus> DeliverAsync(Guid accountId, string id, byte[] sealedKey, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        int delivered = await db.KeyTransfers
            .Where(t => t.Id == id && t.AccountId == accountId && t.ExpiresAt > now && t.SealedKey == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.SealedKey, sealedKey), cancellationToken);
        if (delivered > 0)
        {
            return DeliveryStatus.Delivered;
        }

        return await PendingAsync(accountId, id, cancellationToken) is null ? DeliveryStatus.NotFound : DeliveryStatus.AlreadyDelivered;
    }

    public async Task<bool> DeleteAsync(Guid accountId, string id, CancellationToken cancellationToken) =>
        await db.KeyTransfers.Where(t => t.AccountId == accountId && t.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Deletes the transfers whose time has run out.</summary>
    public async Task PruneAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await db.KeyTransfers.Where(t => t.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<StoredKeyTransfer?> PendingAsync(Guid accountId, string id, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return await db.KeyTransfers.SingleOrDefaultAsync(t => t.AccountId == accountId && t.Id == id && t.ExpiresAt > now, cancellationToken);
    }

    private static KeyTransfer ToContract(StoredKeyTransfer transfer) =>
        new(transfer.Id, transfer.ReceiverPublicKey, transfer.SealedKey, transfer.ExpiresAt);
}

public enum DeliveryStatus
{
    Delivered,
    AlreadyDelivered,
    NotFound,
}
