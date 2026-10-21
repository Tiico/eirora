using System;

namespace Eirora.Contracts
{
    /// <summary>
    /// A contribution part of an earlier key epoch, kept in case its key turns up (ADR-0008): one element of
    /// <c>GET /api/account/cloud-save/locked</c>, deleted with <c>DELETE /api/account/cloud-save/locked/{keyEpoch}</c>.
    /// </summary>
    /// <remarks>
    /// It is locked when an upload with a higher key epoch replaces it, and deleted by the server at
    /// <see cref="DeletedAt"/>.
    /// </remarks>
    public sealed class LockedContributions
    {
        /// <summary>How long a locked contribution part is kept: 90 days (ADR-0008).</summary>
        public const int RetentionDays = 90;

        public LockedContributions(int keyEpoch, long version, byte[] contributions, DateTimeOffset lockedAt, DateTimeOffset deletedAt)
        {
            KeyEpoch = keyEpoch;
            Version = version;
            Contributions = contributions;
            LockedAt = lockedAt;
            DeletedAt = deletedAt;
        }

        /// <summary>The epoch of the key the part is encrypted with.</summary>
        public int KeyEpoch { get; }

        /// <summary>The cloud save version the part was uploaded as, which its associated data names.</summary>
        public long Version { get; }

        /// <summary>The encrypted contribution part, as it was uploaded.</summary>
        public byte[] Contributions { get; }

        public DateTimeOffset LockedAt { get; }

        /// <summary>When the server deletes the part: <see cref="RetentionDays"/> after <see cref="LockedAt"/>.</summary>
        public DateTimeOffset DeletedAt { get; }
    }
}
