namespace Eirora.Contracts
{
    /// <summary>
    /// Uploads the account's cloud save (<c>PUT /api/account/cloud-save</c>), replacing the version it was based on.
    /// </summary>
    /// <remarks>
    /// The server answers 200 with the new <see cref="CloudSaveVersion"/>, or 409 with the current one when the upload is
    /// based on another version or an older key epoch: then another device has uploaded since, and the client downloads
    /// that save, merges the journals and uploads again (D-99). The server checks that the world part loads and that both
    /// parts are within their limits (413), but never reads the contributions (ADR-0001, ADR-0008).
    /// </remarks>
    public sealed class CloudSaveUpload
    {
        /// <summary>
        /// The largest world part the server takes: 1 MiB. A world part is the map and one snapshot without anything per
        /// game day, about 8 KiB on the prototype's map of radius 24, so this leaves room for maps a hundred times larger.
        /// </summary>
        public const int MaxWorldBytes = 1 << 20;

        /// <summary>
        /// The largest contribution part the server takes: 8 MiB. The part grows with the journal, by well under 1 KiB per
        /// played day, until a rules change starts a new segment; 8 MiB holds over ten years of play with both segments kept.
        /// </summary>
        public const int MaxContributionBytes = 8 << 20;

        public CloudSaveUpload(long baseVersion, int keyEpoch, byte[]? world, byte[]? contributions)
        {
            BaseVersion = baseVersion;
            KeyEpoch = keyEpoch;
            World = world;
            Contributions = contributions;
        }

        /// <summary>
        /// The version this upload replaces: the <see cref="CloudSave.Version"/> the device last downloaded or uploaded,
        /// or 0 when the account has no cloud save yet. The upload becomes version <c>BaseVersion + 1</c>, which the
        /// contribution part's associated data names (ADR-0008).
        /// </summary>
        public long BaseVersion { get; }

        /// <summary>
        /// The epoch of the contribution key the contribution part is encrypted with: 1 or more, and never lower than the
        /// cloud save's. A higher epoch locks the current contribution part (<see cref="LockedContributions"/>).
        /// </summary>
        public int KeyEpoch { get; }

        /// <summary>The save's world part (<c>Save.ToParts().World</c>); null when a request leaves it out, which the server rejects.</summary>
        public byte[]? World { get; }

        /// <summary>The encrypted contribution part; null when a request leaves it out, which the server rejects.</summary>
        public byte[]? Contributions { get; }
    }
}
