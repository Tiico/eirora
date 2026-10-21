namespace Eirora.Server.CloudSaves;

/// <summary>
/// A contribution part of an earlier key epoch, locked when an upload with a higher epoch replaced it and kept for
/// <see cref="Contracts.LockedContributions.RetentionDays"/> days in case its key turns up (ADR-0008).
/// </summary>
public sealed class StoredLockedContributions
{
    public Guid AccountId { get; init; }

    public int KeyEpoch { get; init; }

    /// <summary>The cloud save version the part was uploaded as, which its associated data names.</summary>
    public long Version { get; init; }

    public required byte[] Contributions { get; init; }

    public DateTimeOffset LockedAt { get; init; }
}
