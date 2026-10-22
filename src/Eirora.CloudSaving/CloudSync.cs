using System;
using System.Threading.Tasks;
using Eirora.Contracts;
using Eirora.Simulation.Saving;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// Keeps the device's world and the account's cloud save the same (ADR-0008, TASK-023): uploads the world after a
    /// change, brings the cloud save down to a new phone, merges the journals when two devices played apart (D-99),
    /// and begins a new key epoch when the device cannot read the contribution part (D-101).
    /// </summary>
    /// <remarks>
    /// The world never waits for the server (ACC-04): everything here happens after the world changed and was saved on
    /// the device, and an unreachable server only means the next change or launch tries again. The contribution part is
    /// sealed with the account's contribution key before it leaves the device, and a part that does not open with the
    /// device's key is never merged (threat model). One reconciliation or upload runs at a time; the caller sees to that.
    /// </remarks>
    public sealed class CloudSync
    {
        /// <summary>How many times a conflict makes the reconciliation start over before it gives up for now.</summary>
        private const int Attempts = 3;

        private readonly ICloudSaveStore _store;
        private readonly ICloudSyncStorage _storage;
        private readonly ILocalWorld _world;

        public CloudSync(ICloudSaveStore store, ICloudSyncStorage storage, ILocalWorld world)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// Brings the device and the account's cloud save together, at sign-in and at launch: the one that has a world
        /// gives it to the other, two of the same world merge, and two different worlds wait for the player's
        /// <paramref name="choice"/> (<see cref="SyncOutcome.ChoiceNeeded"/>).
        /// </summary>
        public Task<SyncResult> ReconcileAsync(Guid accountId, WorldChoice? choice = null) =>
            ReconcileAsync(accountId, choice, new SyncChanges(), attempt: 0);

        /// <summary>
        /// Uploads the device's world after it changed, based on the version it came from. When the cloud save moved
        /// on meanwhile, the journals are merged and the merge uploaded (D-99).
        /// </summary>
        public async Task<SyncResult> UploadAsync(Guid accountId)
        {
            if (_world.Current is not { } local)
            {
                return new SyncResult(SyncOutcome.NoWorld, new SyncChanges());
            }

            var state = _storage.State(accountId);
            var key = _storage.Key(accountId);
            if (state is null || key is null || key.Epoch != state.KeyEpoch)
            {
                return await ReconcileAsync(accountId);
            }

            var changes = new SyncChanges();
            var (status, result) = await SendAsync(accountId, local, state.Version, key, changes);
            return status == StoreStatus.Conflict ? await ReconcileAsync(accountId, null, changes, attempt: 1) : result;
        }

        /// <summary>Forgets the account's key and state on this device, as when the account is deleted (ADR-0008).</summary>
        public void Forget(Guid accountId)
        {
            _storage.ForgetKey(accountId);
            _storage.ForgetState();
        }

        private async Task<SyncResult> ReconcileAsync(Guid accountId, WorldChoice? choice, SyncChanges changes, int attempt)
        {
            var got = await _store.GetAsync();
            CloudSave? remote;
            switch (got.Status)
            {
                case StoreStatus.Ok when got.Value is not null:
                    remote = got.Value;
                    break;
                case StoreStatus.NotFound:
                    remote = null;
                    break;
                default:
                    return new SyncResult(Outcome(got.Status), changes);
            }

            var local = _world.Current;
            var key = _storage.Key(accountId);
            if (remote is null)
            {
                if (local is null)
                {
                    return new SyncResult(SyncOutcome.NoWorld, changes);
                }

                // The account's first cloud save: the device's key, or a new one for epoch 1.
                key ??= NewKey(accountId, 1);
                return await SendOrStartOverAsync(accountId, local, 0, key, changes, choice, attempt);
            }

            // The contribution part opens only with the key of its epoch; what does not open is never merged.
            Save? cloud = null;
            if (key is not null && key.Epoch == remote.KeyEpoch)
            {
                byte[]? contributions = ContributionCipher.Open(key, accountId, remote.Version, remote.Contributions);
                if (contributions is null)
                {
                    changes.CloudSaveUnreadable = true;
                }
                else
                {
                    try
                    {
                        cloud = Save.FromParts(remote.World, contributions);
                    }
                    catch (SavedByNewerVersionException)
                    {
                        return new SyncResult(SyncOutcome.AppOutdated, changes);
                    }
                    catch (FormatException)
                    {
                        changes.CloudSaveUnreadable = true;
                    }
                }
            }

            if (cloud is null)
            {
                return await WithoutTheContributionsAsync(accountId, remote, local, key, changes, choice, attempt);
            }

            if (local is null)
            {
                _world.Replace(cloud, ReplaceReason.Downloaded);
                changes.Downloaded = true;
                return await KeepOrSendAsync(accountId, cloud, remote, key!, changes, choice, attempt, upload: cloud.ChangedOnLoad);
            }

            Save? merged;
            try
            {
                merged = Save.Merge(local, cloud);
            }
            catch (FormatException)
            {
                merged = null; // A journal this version cannot replay: the player chooses, as between different worlds.
            }

            if (merged is not null)
            {
                bool cloudHasEverything = merged.World.Fingerprint() == cloud.World.Fingerprint();
                if (merged.World.Fingerprint() != local.World.Fingerprint())
                {
                    _world.Replace(merged, ReplaceReason.Merged);
                    changes.Merged = true;
                }

                return await KeepOrSendAsync(accountId, merged, remote, key!, changes, choice, attempt, upload: !cloudHasEverything);
            }

            switch (choice)
            {
                case WorldChoice.KeepPhone:
                    _world.KeepAside(cloud);
                    return await SendOrStartOverAsync(accountId, local, remote.Version, key!, changes, choice, attempt);
                case WorldChoice.TakeCloud:
                    _world.Replace(cloud, ReplaceReason.CloudChosen);
                    return await KeepOrSendAsync(accountId, cloud, remote, key!, changes, choice, attempt, upload: cloud.ChangedOnLoad);
                default:
                    return ChoiceNeeded(remote, cloud.World.GameDay, changes, contributionsUnreadable: false);
            }
        }

        /// <summary>
        /// The device cannot read the cloud save's contribution part: it has no key for its epoch, or the part did not
        /// open. The cloud's world goes on from its world part alone, with its game days closed, under a new key epoch
        /// (D-101); or the phone's world replaces it, under a new epoch too so that the old part is locked, not lost.
        /// </summary>
        private async Task<SyncResult> WithoutTheContributionsAsync(Guid accountId, CloudSave remote, Save? local, ContributionKey? key, SyncChanges changes, WorldChoice? choice, int attempt)
        {
            // This device already began a newer epoch from this very version, but the upload that begins it on the
            // server did not get through: the device's world goes on from that version, so it is uploaded, not asked.
            if (local is not null && key is not null && key.Epoch > remote.KeyEpoch
                && _storage.State(accountId) is { } state && state.Version == remote.Version && state.KeyEpoch == key.Epoch)
            {
                return await SendOrStartOverAsync(accountId, local, remote.Version, key, changes, choice, attempt);
            }

            if (local is not null && choice is null)
            {
                Save alone;
                try
                {
                    alone = Save.FromParts(remote.World, null);
                }
                catch (SavedByNewerVersionException)
                {
                    return new SyncResult(SyncOutcome.AppOutdated, changes);
                }
                catch (FormatException)
                {
                    return new SyncResult(SyncOutcome.Failed, changes);
                }

                return ChoiceNeeded(remote, alone.World.GameDay, changes, contributionsUnreadable: true);
            }

            int epoch = Math.Max(remote.KeyEpoch, key?.Epoch ?? 0) + 1;
            if (local is null || choice == WorldChoice.TakeCloud)
            {
                Save alone;
                try
                {
                    alone = Save.FromParts(remote.World, null);
                }
                catch (SavedByNewerVersionException)
                {
                    return new SyncResult(SyncOutcome.AppOutdated, changes);
                }
                catch (FormatException)
                {
                    return new SyncResult(SyncOutcome.Failed, changes);
                }

                _world.Replace(alone, local is null ? ReplaceReason.Downloaded : ReplaceReason.CloudChosen);
                changes.Downloaded = local is null;
                changes.KeyEpochBegan = true;
                return await BeginEpochAsync(accountId, alone, remote.Version, NewKey(accountId, epoch), changes, choice, attempt);
            }

            // The phone's world is kept and replaces the cloud's under a new epoch, so the server locks the old part. The
            // phone's own key goes on when it is already newer than the cloud's epoch.
            var phoneKey = key is not null && key.Epoch > remote.KeyEpoch ? key : NewKey(accountId, epoch);
            changes.ContributionPartLocked = true;
            return await BeginEpochAsync(accountId, local!, remote.Version, phoneKey, changes, choice, attempt);
        }

        /// <summary>
        /// Uploads the device's world under a new key epoch. The device's world is based on <paramref name="baseVersion"/>
        /// before the upload runs, so that an upload which does not get through is tried again rather than asked about.
        /// </summary>
        private Task<SyncResult> BeginEpochAsync(Guid accountId, Save save, long baseVersion, ContributionKey key, SyncChanges changes, WorldChoice? choice, int attempt)
        {
            _storage.KeepState(new CloudSyncState(accountId, baseVersion, key.Epoch));
            return SendOrStartOverAsync(accountId, save, baseVersion, key, changes, choice, attempt);
        }

        /// <summary>The device now holds the cloud's version: remembers that, or uploads when the device's world is ahead of it.</summary>
        private async Task<SyncResult> KeepOrSendAsync(Guid accountId, Save save, CloudSave remote, ContributionKey key, SyncChanges changes, WorldChoice? choice, int attempt, bool upload)
        {
            if (upload)
            {
                return await SendOrStartOverAsync(accountId, save, remote.Version, key, changes, choice, attempt);
            }

            _storage.KeepState(new CloudSyncState(accountId, remote.Version, key.Epoch));
            return new SyncResult(SyncOutcome.Synced, changes, remote.Version);
        }

        private async Task<SyncResult> SendOrStartOverAsync(Guid accountId, Save save, long baseVersion, ContributionKey key, SyncChanges changes, WorldChoice? choice, int attempt)
        {
            var (status, result) = await SendAsync(accountId, save, baseVersion, key, changes);
            if (status != StoreStatus.Conflict)
            {
                return result;
            }

            // Another device uploaded meanwhile: start over from the cloud save as it is now.
            return attempt + 1 < Attempts
                ? await ReconcileAsync(accountId, choice, changes, attempt + 1)
                : new SyncResult(SyncOutcome.Failed, changes);
        }

        /// <summary>Seals and uploads the save as the next version after <paramref name="baseVersion"/>.</summary>
        private async Task<(StoreStatus Status, SyncResult Result)> SendAsync(Guid accountId, Save save, long baseVersion, ContributionKey key, SyncChanges changes)
        {
            var (world, contributions) = save.ToParts();
            byte[] sealedContributions = ContributionCipher.Seal(key, accountId, baseVersion + 1, contributions);
            var reply = await _store.UploadAsync(new CloudSaveUpload(baseVersion, key.Epoch, world, sealedContributions));
            if (reply.Status == StoreStatus.Ok && reply.Value is { } version)
            {
                _storage.KeepState(new CloudSyncState(accountId, version.Version, key.Epoch));
                return (StoreStatus.Ok, new SyncResult(SyncOutcome.Synced, changes, version.Version));
            }

            return (reply.Status, new SyncResult(Outcome(reply.Status), changes));
        }

        private ContributionKey NewKey(Guid accountId, int epoch)
        {
            var key = ContributionKey.Generate(epoch);
            _storage.KeepKey(accountId, key);
            return key;
        }

        private static SyncResult ChoiceNeeded(CloudSave remote, int cloudGameDay, SyncChanges changes, bool contributionsUnreadable) =>
            new(SyncOutcome.ChoiceNeeded, changes, null, cloudGameDay, remote.SavedAt, contributionsUnreadable);

        private static SyncOutcome Outcome(StoreStatus status) => status switch
        {
            StoreStatus.Offline => SyncOutcome.Offline,
            StoreStatus.SignedOut => SyncOutcome.SignedOut,
            _ => SyncOutcome.Failed,
        };
    }
}
