using Eirora.Contracts;
using Eirora.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Eirora.Server.CloudSaves;

/// <summary>
/// Stores the account's cloud save (ADR-0008): versioned, so that a device whose upload is based on an old version merges
/// with the newer one instead of overwriting it (D-99), and keyed by epoch, so that a new key epoch keeps the old
/// contribution part for a while instead of dropping it.
/// </summary>
public sealed partial class CloudSaveService(EiroraDbContext db, TimeProvider time, ILogger<CloudSaveService> logger)
{
    public static readonly TimeSpan LockedRetention = TimeSpan.FromDays(LockedContributions.RetentionDays);

    public async Task<CloudSave?> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
        await db.CloudSaves.AsNoTracking()
            .Where(s => s.AccountId == accountId)
            .Select(s => new CloudSave(s.Version, s.KeyEpoch, s.World, s.Contributions, s.SavedAt))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Stores an upload whose format and size have been checked, if it is based on the current version and is not of an
    /// older key epoch; otherwise the result carries the current version.
    /// </summary>
    public async Task<UploadResult> UploadAsync(Guid accountId, CloudSaveUpload upload, CancellationToken cancellationToken)
    {
        // The account's row is locked until the upload commits, so that two uploads at once cannot both replace the same
        // version, and an upload that waited for a deletion finds no account instead of a missing foreign key.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var account = await db.Database
            .SqlQuery<int>($"""SELECT 1 AS "Value" FROM "Accounts" WHERE "Id" = {accountId} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        if (account.Count == 0)
        {
            return new UploadResult(UploadStatus.NoAccount, new CloudSaveVersion(0, 0));
        }

        var save = await db.CloudSaves.SingleOrDefaultAsync(s => s.AccountId == accountId, cancellationToken);
        if (upload.BaseVersion != (save?.Version ?? 0) || upload.KeyEpoch < (save?.KeyEpoch ?? 0))
        {
            return new UploadResult(UploadStatus.Conflict, new CloudSaveVersion(save?.Version ?? 0, save?.KeyEpoch ?? 0));
        }

        var now = time.GetUtcNow();
        if (save is null)
        {
            save = new StoredCloudSave { AccountId = accountId, World = upload.World!, Contributions = upload.Contributions! };
            db.CloudSaves.Add(save);
        }
        else if (upload.KeyEpoch > save.KeyEpoch)
        {
            // A device without the key began a new epoch; the old part is kept in case the key turns up (ADR-0008).
            db.LockedContributions.Add(new StoredLockedContributions
            {
                AccountId = accountId,
                KeyEpoch = save.KeyEpoch,
                Version = save.Version,
                Contributions = save.Contributions,
                LockedAt = now,
            });
            LogLocked(logger);
        }

        save.Version = upload.BaseVersion + 1;
        save.KeyEpoch = upload.KeyEpoch;
        save.World = upload.World!;
        save.Contributions = upload.Contributions!;
        save.SavedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new UploadResult(UploadStatus.Saved, new CloudSaveVersion(save.Version, save.KeyEpoch));
    }

    public async Task<IReadOnlyList<LockedContributions>> GetLockedAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var locked = await db.LockedContributions.AsNoTracking()
            .Where(l => l.AccountId == accountId)
            .OrderBy(l => l.KeyEpoch)
            .ToListAsync(cancellationToken);
        return [.. locked.Select(l => new LockedContributions(l.KeyEpoch, l.Version, l.Contributions, l.LockedAt, l.LockedAt + LockedRetention))];
    }

    /// <summary>Deletes a locked contribution part before its time, as the player may choose (ADR-0008).</summary>
    public async Task<bool> DeleteLockedAsync(Guid accountId, int keyEpoch, CancellationToken cancellationToken) =>
        await db.LockedContributions
            .Where(l => l.AccountId == accountId && l.KeyEpoch == keyEpoch)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Deletes the locked contribution parts that have been kept for <see cref="LockedRetention"/>.</summary>
    public async Task PruneLockedAsync(CancellationToken cancellationToken)
    {
        var lockedBefore = time.GetUtcNow() - LockedRetention;
        await db.LockedContributions.Where(l => l.LockedAt <= lockedBefore).ExecuteDeleteAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Locked a cloud save's contributions for a new key epoch")]
    private static partial void LogLocked(ILogger logger);
}

public enum UploadStatus
{
    Saved,
    Conflict,
    NoAccount,
}

/// <summary>Whether an upload was stored, and the cloud save's version after it.</summary>
public readonly record struct UploadResult(UploadStatus Status, CloudSaveVersion Current);
