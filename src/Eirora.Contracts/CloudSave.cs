using System;

namespace Eirora.Contracts
{
    /// <summary>
    /// Response of <c>GET /api/account/cloud-save</c>: the account's cloud save (ADR-0008), exactly as the last upload
    /// left it.
    /// </summary>
    public sealed class CloudSave
    {
        public CloudSave(long version, int keyEpoch, byte[] world, byte[]? contributions, DateTimeOffset savedAt)
        {
            Version = version;
            KeyEpoch = keyEpoch;
            World = world;
            Contributions = contributions;
            SavedAt = savedAt;
        }

        /// <summary>
        /// 1 for the first upload and one more for each upload after it; the next upload is based on it
        /// (<see cref="CloudSaveUpload.BaseVersion"/>), and the contribution part is bound to it (ADR-0008).
        /// </summary>
        public long Version { get; }

        /// <summary>
        /// The epoch of the contribution key that <see cref="Contributions"/> is encrypted with. Without a contribution
        /// part, the epoch of the last one the account had, or 0; a new key gets an epoch above it.
        /// </summary>
        public int KeyEpoch { get; }

        /// <summary>The save's world part (<c>Save.ToParts().World</c>), which the server has checked loads.</summary>
        public byte[] World { get; }

        /// <summary>
        /// The save's contribution part, encrypted on the device; the server never decrypts it (D-57). Null when the
        /// player has not consented to storing it (<see cref="ContributionConsent"/>): then the cloud save is its world
        /// part alone.
        /// </summary>
        public byte[]? Contributions { get; }

        /// <summary>When the server stored the upload.</summary>
        public DateTimeOffset SavedAt { get; }
    }
}
