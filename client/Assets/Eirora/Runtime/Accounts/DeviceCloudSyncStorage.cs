using System;
using System.IO;
using Eirora.CloudSaving;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// What the device keeps about the cloud save: the contribution key per account in the platform keystore, beside
    /// the refresh token, and the cloud save version the world is based on in a small file beside the save.
    /// </summary>
    /// <remarks>
    /// The keystore is not synced or restored to another device (<see cref="ISecureStore"/>); the key's way to a new
    /// phone is TASK-023b. Without the state file the next sync reconciles with the server, which is idempotent.
    /// </remarks>
    public sealed class DeviceCloudSyncStorage : ICloudSyncStorage
    {
        public const string KeyPrefix = "eirora.cloud.contribution-key.";
        public const string StateFile = "world.cloud";

        private readonly ISecureStore _store;
        private readonly string? _statePath;
        private string? _stateInMemory;

        /// <param name="statePath">Where the state file lives, or null to keep the state in memory only.</param>
        public DeviceCloudSyncStorage(ISecureStore store, string? statePath)
        {
            _store = store;
            _statePath = statePath;
        }

        public static string KeyName(Guid accountId) => KeyPrefix + accountId.ToString("N");

        public ContributionKey? Key(Guid accountId) => ContributionKey.TryParse(_store.Read(KeyName(accountId)));

        public void KeepKey(Guid accountId, ContributionKey key) => _store.Write(KeyName(accountId), key.ToText());

        public void ForgetKey(Guid accountId) => _store.Delete(KeyName(accountId));

        public CloudSyncState? State(Guid accountId)
        {
            string? text = _statePath is null ? _stateInMemory : File.Exists(_statePath) ? File.ReadAllText(_statePath) : null;
            return CloudSyncState.TryParse(text) is { } state && state.AccountId == accountId ? state : null;
        }

        public void KeepState(CloudSyncState state)
        {
            if (_statePath is null)
            {
                _stateInMemory = state.ToText();
                return;
            }

            string temporary = _statePath + ".tmp";
            File.WriteAllText(temporary, state.ToText());
            if (File.Exists(_statePath))
            {
                File.Replace(temporary, _statePath, null);
            }
            else
            {
                File.Move(temporary, _statePath);
            }
        }

        public void ForgetState()
        {
            _stateInMemory = null;
            if (_statePath is not null && File.Exists(_statePath))
            {
                File.Delete(_statePath);
            }
        }
    }
}
