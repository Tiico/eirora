using System;
using System.Linq;
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
    /// <para>
    /// The contribution part leaves the device only while the player consents to it (TASK-060): before the first answer
    /// nothing goes up and the player is asked (<see cref="SyncOutcome.ConsentNeeded"/>), and without consent the cloud
    /// save is the world part alone. Two devices whose cloud save has no contribution part cannot merge their journals,
    /// so a device that did not upload the cloud's version itself asks the player, as between different worlds.
    /// </para>
    /// </remarks>
    public sealed class CloudSync
    {
        /// <summary>How many times a conflict makes the reconciliation start over before it gives up for now.</summary>
        private const int Attempts = 3;

        private readonly ICloudSaveStore _store;
        private readonly ICloudSyncStorage _storage;
        private readonly ILocalWorld _world;
        private Guid _consentAccount;
        private ContributionConsent? _consent;

        public CloudSync(ICloudSaveStore store, ICloudSyncStorage storage, ILocalWorld world)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// Brings the device and the account's cloud save together, at sign-in and at launch: the one that has a world
        /// gives it to the other, two of the same world merge, and two different worlds wait for the player's
        /// <paramref name="choice"/> (<see cref="SyncOutcome.ChoiceNeeded"/>). A phone without the key for the cloud's
        /// contribution part waits too (<see cref="SyncOutcome.KeyNeeded"/>): the key may come from the old phone or a
        /// recovery code, and <see cref="WorldChoice.TakeCloud"/> goes on without it.
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

            if (await SendWaitingDeclineAsync(accountId) is { } notSent)
            {
                return new SyncResult(notSent, new SyncChanges());
            }

            // The consent is the one the last reconciliation heard; when it changed since, the server answers 409.
            var state = _storage.State(accountId);
            if (state is null || Consent(accountId) is not { NeedsAnswer: false } consent)
            {
                return await ReconcileAsync(accountId);
            }

            ContributionKey? key = null;
            if (consent.Answer == ConsentAnswer.Given)
            {
                key = _storage.Key(accountId);
                if (key is null || key.Epoch != state.KeyEpoch)
                {
                    return await ReconcileAsync(accountId);
                }
            }

            var changes = new SyncChanges();
            var (status, result) = await SendAsync(accountId, local, state.Version, key, changes);
            return status == StoreStatus.Conflict ? await ReconcileAsync(accountId, null, changes, attempt: 1) : result;
        }

        /// <summary>
        /// The player's answer about the contribution part as this device last heard it from the server, or null before
        /// it asked (TASK-060). A no that waits to reach the server is the answer already: nothing is sealed meanwhile.
        /// </summary>
        public ContributionConsent? Consent(Guid accountId) => _storage.DeclineWaits(accountId)
            ? new ContributionConsent(ConsentAnswer.Declined, ContributionConsent.CurrentVersion, null)
            : _consentAccount == accountId ? _consent : null;

        /// <summary>Whether a no, or a withdrawal, waits on this device to reach the server.</summary>
        public bool DeclineWaits(Guid accountId) => _storage.DeclineWaits(accountId);

        /// <summary>
        /// Gives the server the player's answer about the contribution part (TASK-060). A decline, or a withdrawal,
        /// deletes the contribution part and the locked ones on the server; the device keeps its key and its world. A no
        /// is kept on the device first, across launches, until the server has it, and every reconciliation and upload
        /// sends it before anything else, so that no contribution part goes up after the player said no. A yes replaces a
        /// no that is still waiting. The caller reconciles next, which uploads by the answer.
        /// </summary>
        public async Task<StoreStatus> AnswerConsentAsync(Guid accountId, bool given)
        {
            if (given)
            {
                _storage.ForgetDeclineWaiting(accountId);
            }
            else
            {
                _storage.KeepDeclineWaiting(accountId);
            }

            var reply = await _store.AnswerConsentAsync(new ContributionConsentAnswer(given, ContributionConsent.CurrentVersion));
            if (reply.Status != StoreStatus.Ok)
            {
                return reply.Status;
            }

            if (reply.Value is not { } consent)
            {
                return StoreStatus.Failed;
            }

            _storage.ForgetDeclineWaiting(accountId);
            Remember(accountId, consent);
            return StoreStatus.Ok;
        }

        /// <summary>
        /// Keeps a contribution key that came from outside, a recovery code or a key transfer (TASK-023b), once the
        /// cloud save confirms it: its epoch must be the cloud save's, and the contribution part must open with it. A
        /// key of an older epoch, or one that does not open the part, is not kept, so a stale code cannot replace a key
        /// the device still needs. Without a cloud save or a contribution part, there is nothing to check against and a
        /// key that is not of an older epoch is kept.
        /// </summary>
        public async Task<KeyAcceptance> AcceptKeyAsync(Guid accountId, ContributionKey key)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var got = await _store.GetAsync();
            switch (got.Status)
            {
                case StoreStatus.Ok when got.Value is { } remote:
                    if (key.Epoch < remote.KeyEpoch)
                    {
                        return KeyAcceptance.OlderEpoch;
                    }

                    if (remote.Contributions is not { } sealedPart)
                    {
                        _storage.KeepKey(accountId, key);
                        return KeyAcceptance.Accepted;
                    }

                    if (key.Epoch > remote.KeyEpoch || ContributionCipher.Open(key, accountId, remote.Version, sealedPart) is null)
                    {
                        return KeyAcceptance.DoesNotOpen;
                    }

                    _storage.KeepKey(accountId, key);
                    return KeyAcceptance.Accepted;
                case StoreStatus.NotFound:
                    _storage.KeepKey(accountId, key);
                    return KeyAcceptance.Accepted;
                case StoreStatus.Offline:
                    return KeyAcceptance.Offline;
                case StoreStatus.SignedOut:
                    return KeyAcceptance.SignedOut;
                default:
                    return KeyAcceptance.Failed;
            }
        }

        /// <summary>Forgets the account's key, state and waiting answer on this device, as when the account is deleted (ADR-0008).</summary>
        public void Forget(Guid accountId)
        {
            _storage.ForgetKey(accountId);
            _storage.ForgetState();
            _storage.ForgetDeclineWaiting(accountId);
        }

        private async Task<SyncResult> ReconcileAsync(Guid accountId, WorldChoice? choice, SyncChanges changes, int attempt)
        {
            if (await SendWaitingDeclineAsync(accountId) is { } notSent)
            {
                return new SyncResult(notSent, changes);
            }

            var asked = await _store.GetConsentAsync();
            if (asked.Status != StoreStatus.Ok || asked.Value is not { } consent)
            {
                return new SyncResult(asked.Status == StoreStatus.Ok ? SyncOutcome.Failed : Outcome(asked.Status), changes);
            }

            Remember(accountId, consent);
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

            // Nothing goes up before the player has answered; a part the account already has stays until then.
            if (consent.NeedsAnswer)
            {
                return new SyncResult(SyncOutcome.ConsentNeeded, changes, contributionsOnAccount: remote?.Contributions is not null);
            }

            bool withContributions = consent.Answer == ConsentAnswer.Given;
            var local = _world.Current;
            var key = _storage.Key(accountId);
            if (remote is null)
            {
                if (local is null)
                {
                    return new SyncResult(SyncOutcome.NoWorld, changes);
                }

                // The account's first cloud save: the device's key, or a new one for epoch 1, or the world part alone.
                return await SendOrStartOverAsync(accountId, local, 0, withContributions ? key ?? NewKey(accountId, 1) : null, changes, choice, attempt);
            }

            if (!withContributions || remote.Contributions is not { } sealedPart)
            {
                return await FromTheWorldPartAsync(accountId, remote, local, withContributions, changes, choice, attempt);
            }

            // The contribution part opens only with the key of its epoch; what does not open is never merged.
            Save? cloud = null;
            if (key is not null && key.Epoch == remote.KeyEpoch)
            {
                byte[]? contributions = ContributionCipher.Open(key, accountId, remote.Version, sealedPart);
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

            // Before a new epoch begins, the player is asked: the key may be on the old phone or in a recovery code
            // (TASK-023b). A phone with a world of its own chooses between the worlds as well.
            if (choice is null)
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

                return new SyncResult(local is null ? SyncOutcome.KeyNeeded : SyncOutcome.ChoiceNeeded, changes, null, alone.World.GameDay, remote.SavedAt, contributionsUnreadable: true);
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
                return await BeginEpochAsync(accountId, alone, cloudsWorld: true, remote.Version, NewKey(accountId, epoch), changes, choice, attempt);
            }

            // The phone's world is kept and replaces the cloud's under a new epoch, so the server locks the old part. The
            // phone's own key goes on when it is already newer than the cloud's epoch.
            var phoneKey = key is not null && key.Epoch > remote.KeyEpoch ? key : NewKey(accountId, epoch);
            changes.ContributionPartLocked = true;
            return await BeginEpochAsync(accountId, local!, cloudsWorld: false, remote.Version, phoneKey, changes, choice, attempt);
        }

        /// <summary>
        /// The cloud save holds no contribution part this device takes: the account keeps none without consent
        /// (TASK-060), or has none yet since consent was given. Its world goes on from the world part alone, with its game
        /// days so far closed, as on a phone without the key (D-101). A device that holds the cloud's world, or uploaded
        /// the cloud's version itself, goes on from its own world; a device where nothing was played since the version it
        /// is based on takes the cloud's newer world; any other device with a world asks the player, since there are no
        /// journals to merge.
        /// </summary>
        private async Task<SyncResult> FromTheWorldPartAsync(Guid accountId, CloudSave remote, Save? local, bool withContributions, SyncChanges changes, WorldChoice? choice, int attempt)
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

            // The phone holds the cloud's world, or goes on from the version it uploaded itself.
            var state = _storage.State(accountId);
            bool sameWorld = local is not null && local.ToParts().World.SequenceEqual(remote.World);
            if (local is not null && (sameWorld || state?.Version == remote.Version))
            {
                var key = withContributions ? SealingKey(accountId, remote) : null;
                if (key is null && sameWorld)
                {
                    KeepState(accountId, remote.Version, 0, local);
                    return new SyncResult(SyncOutcome.Synced, changes, remote.Version);
                }

                return await SendOrStartOverAsync(accountId, local, remote.Version, key, changes, choice, attempt);
            }

            // Nothing was played on the phone since the version it is based on, and the cloud moved on from that version:
            // the cloud's world is newer and the phone has nothing of its own to lose, so nothing is asked.
            bool untouched = local is not null && state is { AcceptedCommands: { } commands } && state.Version < remote.Version
                && local.World.AcceptedCommands == commands;
            if (local is not null && !untouched && choice is null)
            {
                return new SyncResult(SyncOutcome.ChoiceNeeded, changes, null, alone.World.GameDay, remote.SavedAt, cloudWithoutContributions: true);
            }

            if (local is not null && !untouched && choice == WorldChoice.KeepPhone)
            {
                _world.KeepAside(alone);
                return await GoOnFromAsync(accountId, local, cloudsWorld: false, remote, withContributions, changes, choice, attempt);
            }

            // The world part alone closed the game days so far, so it goes up again as the device's world.
            _world.Replace(alone, local is null ? ReplaceReason.Downloaded : untouched ? ReplaceReason.FastForwarded : ReplaceReason.CloudChosen);
            changes.Downloaded = local is null || untouched;
            changes.WorldPartOnly = true;
            return await GoOnFromAsync(accountId, alone, cloudsWorld: true, remote, withContributions, changes, choice, attempt);
        }

        /// <summary>
        /// Uploads the device's world in place of a cloud save without a contribution part. The device's world is based on
        /// the cloud's version before the upload runs, so that an upload which does not get through is tried again rather
        /// than asked about.
        /// </summary>
        private Task<SyncResult> GoOnFromAsync(Guid accountId, Save save, bool cloudsWorld, CloudSave remote, bool withContributions, SyncChanges changes, WorldChoice? choice, int attempt)
        {
            var key = withContributions ? SealingKey(accountId, remote) : null;
            KeepState(accountId, remote.Version, key?.Epoch ?? 0, cloudsWorld ? save : null);
            return SendOrStartOverAsync(accountId, save, remote.Version, key, changes, choice, attempt);
        }

        /// <summary>
        /// The key for the first contribution part of a cloud save that has none: the device's key, unless it is of an
        /// older epoch than the cloud save's last part, when a new key gets the epoch after it.
        /// </summary>
        private ContributionKey SealingKey(Guid accountId, CloudSave remote) =>
            _storage.Key(accountId) is { } key && key.Epoch >= remote.KeyEpoch ? key : NewKey(accountId, remote.KeyEpoch + 1);

        /// <summary>
        /// Uploads the device's world under a new key epoch. The device's world is based on <paramref name="baseVersion"/>
        /// before the upload runs, so that an upload which does not get through is tried again rather than asked about.
        /// </summary>
        private Task<SyncResult> BeginEpochAsync(Guid accountId, Save save, bool cloudsWorld, long baseVersion, ContributionKey key, SyncChanges changes, WorldChoice? choice, int attempt)
        {
            KeepState(accountId, baseVersion, key.Epoch, cloudsWorld ? save : null);
            return SendOrStartOverAsync(accountId, save, baseVersion, key, changes, choice, attempt);
        }

        /// <summary>The device now holds the cloud's version: remembers that, or uploads when the device's world is ahead of it.</summary>
        private async Task<SyncResult> KeepOrSendAsync(Guid accountId, Save save, CloudSave remote, ContributionKey key, SyncChanges changes, WorldChoice? choice, int attempt, bool upload)
        {
            if (upload)
            {
                return await SendOrStartOverAsync(accountId, save, remote.Version, key, changes, choice, attempt);
            }

            KeepState(accountId, remote.Version, key.Epoch, save);
            return new SyncResult(SyncOutcome.Synced, changes, remote.Version);
        }

        private async Task<SyncResult> SendOrStartOverAsync(Guid accountId, Save save, long baseVersion, ContributionKey? key, SyncChanges changes, WorldChoice? choice, int attempt)
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

        /// <summary>
        /// Seals and uploads the save as the next version after <paramref name="baseVersion"/>; without a key, the world
        /// part alone, as without consent.
        /// </summary>
        private async Task<(StoreStatus Status, SyncResult Result)> SendAsync(Guid accountId, Save save, long baseVersion, ContributionKey? key, SyncChanges changes)
        {
            var (world, contributions) = save.ToParts();
            var upload = key is null
                ? new CloudSaveUpload(baseVersion, 0, world, null)
                : new CloudSaveUpload(baseVersion, key.Epoch, world, ContributionCipher.Seal(key, accountId, baseVersion + 1, contributions));
            var reply = await _store.UploadAsync(upload);
            if (reply.Status == StoreStatus.Ok && reply.Value is { } version)
            {
                KeepState(accountId, version.Version, upload.KeyEpoch, save);
                return (StoreStatus.Ok, new SyncResult(SyncOutcome.Synced, changes, version.Version));
            }

            return (reply.Status, new SyncResult(Outcome(reply.Status), changes));
        }

        /// <summary>
        /// Remembers the cloud save version the device's world is based on, and how much the player had done then when the
        /// device's world <em>is</em> that version (<paramref name="cloudsWorld"/>). Before the phone's own world has gone
        /// up in place of the cloud's, the count is left out, so the phone does not pass for untouched and lose that world
        /// to a newer one without a question.
        /// </summary>
        private void KeepState(Guid accountId, long version, int keyEpoch, Save? cloudsWorld) =>
            _storage.KeepState(new CloudSyncState(accountId, version, keyEpoch, cloudsWorld?.World.AcceptedCommands));

        /// <summary>Sends a no that waits on the device; the outcome when it still does not get through, or null when it did or none waited.</summary>
        private async Task<SyncOutcome?> SendWaitingDeclineAsync(Guid accountId)
        {
            if (!_storage.DeclineWaits(accountId))
            {
                return null;
            }

            var sent = await AnswerConsentAsync(accountId, given: false);
            return sent == StoreStatus.Ok ? null : Outcome(sent);
        }

        private void Remember(Guid accountId, ContributionConsent consent)
        {
            _consentAccount = accountId;
            _consent = consent;
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
