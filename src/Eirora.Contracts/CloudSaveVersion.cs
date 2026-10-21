namespace Eirora.Contracts
{
    /// <summary>
    /// The cloud save's version and key epoch: the answer to an upload (<see cref="CloudSaveUpload"/>), with the new ones
    /// when it was stored and the current ones when it conflicted (409).
    /// </summary>
    public sealed class CloudSaveVersion
    {
        public CloudSaveVersion(long version, int keyEpoch)
        {
            Version = version;
            KeyEpoch = keyEpoch;
        }

        /// <summary>The cloud save's <see cref="CloudSave.Version"/>; 0 when the account has none.</summary>
        public long Version { get; }

        /// <summary>The cloud save's <see cref="CloudSave.KeyEpoch"/>; 0 when the account has none.</summary>
        public int KeyEpoch { get; }
    }
}
