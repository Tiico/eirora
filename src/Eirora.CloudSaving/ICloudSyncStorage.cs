using System;
using System.Globalization;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// What the device keeps about the cloud save: the contribution key per account, in the platform's keystore, which
    /// version of the account's cloud save the world on the device is based on, and a no to the contribution part that
    /// has not reached the server yet.
    /// </summary>
    public interface ICloudSyncStorage
    {
        /// <summary>The account's contribution key on this device, or null when it has none.</summary>
        ContributionKey? Key(Guid accountId);

        void KeepKey(Guid accountId, ContributionKey key);

        /// <summary>Removes the key, as when the account is deleted (ADR-0008).</summary>
        void ForgetKey(Guid accountId);

        /// <summary>The cloud save version the device's world is based on, or null for another account or none.</summary>
        CloudSyncState? State(Guid accountId);

        void KeepState(CloudSyncState state);

        void ForgetState();

        /// <summary>
        /// Whether a no to the contribution part, or a withdrawal, waits to reach the server for the account (TASK-060).
        /// It is kept across launches, and no contribution part goes up while it waits.
        /// </summary>
        bool DeclineWaits(Guid accountId);

        void KeepDeclineWaiting(Guid accountId);

        void ForgetDeclineWaiting(Guid accountId);
    }

    /// <summary>
    /// The cloud save version the device's world is based on, which the next upload names as its base, and the key
    /// epoch it was sealed with, or 0 for a cloud save without a contribution part (TASK-060), and how many commands the
    /// device's world had accepted then. Lost or stale, the device reconciles with the server's copy: a merge is idempotent.
    /// </summary>
    public sealed class CloudSyncState
    {
        private const string Header = "eirora-cloud 1";

        public CloudSyncState(Guid accountId, long version, int keyEpoch, int? acceptedCommands = null)
        {
            AccountId = accountId;
            Version = version;
            KeyEpoch = keyEpoch;
            AcceptedCommands = acceptedCommands;
        }

        public Guid AccountId { get; }

        public long Version { get; }

        public int KeyEpoch { get; }

        /// <summary>
        /// <see cref="Simulation.World.AcceptedCommands"/> of the device's world at <see cref="Version"/>, or null for a
        /// state written before it was kept. Still the same, the player has done nothing on the device since.
        /// </summary>
        public int? AcceptedCommands { get; }

        /// <summary>The state as text for a file beside the save.</summary>
        public string ToText() =>
            Header + "\n" + AccountId.ToString("D") + " " + Version.ToString(CultureInfo.InvariantCulture) + " " + KeyEpoch.ToString(CultureInfo.InvariantCulture)
            + (AcceptedCommands is { } commands ? " " + commands.ToString(CultureInfo.InvariantCulture) : string.Empty) + "\n";

        /// <summary>The state <see cref="ToText"/> wrote, or null when the text is not one.</summary>
        public static CloudSyncState? TryParse(string? text)
        {
            if (text is null)
            {
                return null;
            }

            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            if (lines.Length < 2 || lines[0] != Header)
            {
                return null;
            }

            string[] fields = lines[1].Split(' ');
            int commands = 0;
            return fields.Length is 3 or 4
                && Guid.TryParseExact(fields[0], "D", out var accountId)
                && long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out long version)
                && int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int keyEpoch)
                && (fields.Length == 3 || int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out commands))
                ? new CloudSyncState(accountId, version, keyEpoch, fields.Length == 4 ? commands : (int?)null)
                : null;
        }
    }
}
