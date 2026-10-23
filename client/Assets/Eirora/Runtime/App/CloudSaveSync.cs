using System;
using System.Threading.Tasks;
using Eirora.Client.Accounts;
using Eirora.CloudSaving;
using Eirora.Contracts;

namespace Eirora.Client.App
{
    /// <summary>How the cloud save stands, for the account sheet.</summary>
    public enum CloudSyncStatus
    {
        /// <summary>No sign-in on this device: the world lives on the phone only (ACC-04).</summary>
        Off,

        /// <summary>A change waits to be uploaded, or an upload is under way.</summary>
        Working,

        /// <summary>The cloud save holds the world as it was last saved.</summary>
        Synced,

        /// <summary>Neither the phone nor the account has a world yet.</summary>
        NoWorld,

        /// <summary>
        /// The player has not answered whether their self-care may be saved on the account (TASK-060); nothing goes up
        /// until they do.
        /// </summary>
        ConsentNeeded,

        /// <summary>The server could not be reached; the next change or launch tries again.</summary>
        Offline,

        /// <summary>The phone and the account hold different worlds; the player chooses.</summary>
        ChoiceNeeded,

        /// <summary>The account has a world whose self-care this phone cannot read: the key from the old phone or a recovery code unlocks it.</summary>
        KeyNeeded,

        /// <summary>The cloud save was written by a newer app.</summary>
        AppOutdated,

        /// <summary>The server rejected the sign-in; signing in again is needed.</summary>
        SignedOut,

        /// <summary>The server answered with something unexpected.</summary>
        Failed,
    }

    /// <summary>What became of a recovery code the player typed in.</summary>
    public enum RecoveryCodeOutcome
    {
        /// <summary>The code is the account's key; the cloud save is being read with it.</summary>
        Unlocked,

        /// <summary>The text is not a whole code, or a character is wrong.</summary>
        NotACode,

        /// <summary>The code is from an earlier key epoch; a newer one was shown since.</summary>
        OlderCode,

        /// <summary>The code does not open the account's contribution part; it is another account's or another phone's.</summary>
        NotThisAccountsCode,

        /// <summary>The server could not be reached to check the code.</summary>
        Offline,

        /// <summary>The server answered with something unexpected.</summary>
        Failed,
    }

    /// <summary>
    /// Keeps the world on the phone and the account's cloud save the same (ADR-0008, TASK-023): uploads after every
    /// save, a little later so that a burst of commands becomes one upload, reconciles at launch and sign-in, and
    /// surfaces what the player must know: a choice between two worlds, a new key epoch, or a world that came down.
    /// </summary>
    /// <remarks>
    /// One sync runs at a time; a change during a run makes it go once more. The world never waits for any of this
    /// (ACC-04): everything here follows a save that is already on the phone. The key also goes to the platform's
    /// end-to-end encrypted backup (<see cref="IKeyBackup"/>), where a new phone on the same platform finds it before
    /// the player is asked (TASK-023b). Before anything goes up, the player is asked once whether their self-care, the
    /// contribution part, may be saved on the account (TASK-060); the answer is the account's, so a decline is never
    /// asked again, and it can be changed in the account sheet.
    /// </remarks>
    public sealed class CloudSaveSync
    {
        private const int LoadAttempts = 3;

        private readonly AccountSession _account;
        private readonly ICloudSyncStorage _storage;
        private readonly CloudSync _sync;
        private readonly AccountKeyTransferStore _transfers;
        private readonly KeyTransferReceiver _receiver;
        private readonly IKeyBackup? _backup;
        private readonly Func<Task> _delay;
        private readonly Func<DateTimeOffset> _now;
        private bool _dirty;
        private bool _reconcile;
        private WorldChoice? _choice;
        private bool _wasSignedIn;
        private bool _lockPending;
        private Task? _running;
        private string? _backedUpKey;
        private bool? _pendingAnswer;
        private bool _contributionsOnAccount;

        /// <param name="delay">How long an upload waits after a save, so that a burst becomes one upload.</param>
        /// <param name="backup">The platform's backup of the key, or null where there is none.</param>
        public CloudSaveSync(AccountSession account, GameSession session, ICloudSyncStorage storage, Func<Task>? delay = null, Func<DateTimeOffset>? now = null, IKeyBackup? backup = null)
        {
            _account = account;
            _storage = storage;
            _sync = new CloudSync(new AccountCloudSaveStore(account), storage, session);
            _transfers = new AccountKeyTransferStore(account);
            _receiver = new KeyTransferReceiver(_transfers, _sync);
            _delay = delay ?? (() => Task.Delay(TimeSpan.FromSeconds(2)));
            _now = now ?? (() => DateTimeOffset.UtcNow);
            _backup = backup;
            _wasSignedIn = account.IsSignedIn;
            session.Saved += Schedule;
            account.Changed += OnAccountChanged;
            account.Deleted += OnAccountDeleted;
        }

        /// <summary>The status, the choice or a notice changed; the interface follows.</summary>
        public event Action? Changed;

        public CloudSyncStatus Status { get; private set; }

        /// <summary>When the cloud save last held the phone's world, by this device's clock.</summary>
        public DateTimeOffset? SyncedAt { get; private set; }

        /// <summary>
        /// What the player must decide, or null: between the phone's world and the cloud's
        /// (<see cref="CloudSyncStatus.ChoiceNeeded"/>), or where the key comes from (<see cref="CloudSyncStatus.KeyNeeded"/>).
        /// </summary>
        public SyncResult? Choice { get; private set; }

        /// <summary>
        /// The ticket of the key transfer this phone waits on (TASK-023b), as text for the QR code, or null when none is
        /// open. Opened by <see cref="BeginKeyTransferAsync"/>; gone once the key arrived, the transfer ran out, or the
        /// player gave it up.
        /// </summary>
        public string? TransferTicket => _receiver.Ticket?.ToText();

        /// <summary>When the server forgets the open transfer, or null when none is open.</summary>
        public DateTimeOffset? TransferExpiresAt => _receiver.ExpiresAt;

        /// <summary>The open transfer's check code, shown under the QR code for the player to compare with the old phone's.</summary>
        public string? TransferCheckCode => _receiver.Ticket?.CheckCode;

        /// <summary>How the open transfer stands after its last call, or null when none was begun since the last one ended.</summary>
        public KeyTransferOutcome? TransferOutcome { get; private set; }

        /// <summary>The account's contribution key as a recovery code (TASK-023b), or null before the account or the key is known.</summary>
        public string? RecoveryCode =>
            _account.Account is { } known && _storage.Key(known.Id) is { } key ? CloudSaving.RecoveryCode.Of(key) : null;

        /// <summary>
        /// How far the account's key reaches beyond this phone, once it was kept in the platform's backup; null before
        /// that, when the backup did not answer, or when it could not say. Without a screen lock (<see cref="KeyBackupReach.ThisPhone"/>) the
        /// account sheet suggests the recovery code or a key transfer for a new phone (ADR-0008).
        /// </summary>
        public KeyBackupReach? KeyReach { get; private set; }

        /// <summary>
        /// A new key epoch began on this phone: the contribution part could not be read here, so the game days so far
        /// are closed (D-101). Shown until <see cref="AcknowledgeKeyEpoch"/>.
        /// </summary>
        public bool KeyEpochBegan { get; private set; }

        /// <summary>
        /// With <see cref="KeyEpochBegan"/>: the phone had the key for the cloud save's epoch, but the contribution part
        /// did not open with it; otherwise the phone had no key for that epoch.
        /// </summary>
        public bool CloudSaveUnreadable { get; private set; }

        /// <summary>
        /// The phone's world replaced a cloud save whose contribution part could not be read here (D-102); the server
        /// keeps that part locked for 90 days. Shown until <see cref="AcknowledgeLocked"/>.
        /// </summary>
        public bool ContributionPartLocked { get; private set; }

        /// <summary>The account's world came down to this phone: "Välkommen tillbaka till din ö." Shown until <see cref="AcknowledgeWelcome"/>.</summary>
        public bool Welcome { get; private set; }

        /// <summary>
        /// With <see cref="Welcome"/>: the world came down without the self-care, which the account does not keep without
        /// consent (TASK-060), so the game days so far are closed.
        /// </summary>
        public bool WelcomeWithoutSelfCare { get; private set; }

        /// <summary>
        /// Whether to ask the player if their self-care may be saved on the account (TASK-060): before the first upload,
        /// until an answer is on its way.
        /// </summary>
        public bool ConsentQuestion => Status == CloudSyncStatus.ConsentNeeded && !AnswerWaiting;

        /// <summary>
        /// A no, or a withdrawal, has not reached the server yet. It is kept on the phone across launches and sent before
        /// anything else, and no self-care goes up meanwhile.
        /// </summary>
        public bool DeclineWaiting => _account.Account is { } known && _sync.DeclineWaits(known.Id);

        /// <summary>An answer about the self-care waits to reach the server: a yes until the next sync, a no until it gets there.</summary>
        public bool AnswerWaiting => _pendingAnswer is not null || DeclineWaiting;

        /// <summary>With <see cref="ConsentQuestion"/>: the account already holds self-care from before the question, which a decline deletes.</summary>
        public bool ContributionsOnAccount => ConsentQuestion && _contributionsOnAccount;

        /// <summary>
        /// The player's answer about their self-care as the server last told this phone, or the one on its way; null
        /// before the phone has asked.
        /// </summary>
        public ConsentAnswer? Consent => _pendingAnswer is { } pending
            ? (pending ? ConsentAnswer.Given : ConsentAnswer.Declined)
            : _account.Account is { } known ? _sync.Consent(known.Id)?.Answer : null;

        /// <summary>The player was welcomed; the interface that called this follows by itself.</summary>
        public void AcknowledgeWelcome() => Welcome = WelcomeWithoutSelfCare = false;

        /// <summary>The player saw the notice about the new key epoch; the interface that called this follows by itself.</summary>
        public void AcknowledgeKeyEpoch() => KeyEpochBegan = false;

        /// <summary>The player was told the old part is locked; the interface that called this follows by itself.</summary>
        public void AcknowledgeLocked() => ContributionPartLocked = false;

        /// <summary>Brings the phone and the cloud save together now: at launch, and at sign-in.</summary>
        public Task ReconcileAsync()
        {
            _reconcile = true;
            return RunAsync(immediate: true);
        }

        /// <summary>Uploads the world a little later, as after every save.</summary>
        public void Schedule()
        {
            if (!_account.IsSignedIn)
            {
                return;
            }

            _dirty = true;
            _ = RunAsync(immediate: false);
        }

        /// <summary>Uploads what waits without the delay, as when the app goes to the background.</summary>
        public Task FlushAsync() => _dirty || _reconcile ? RunAsync(immediate: true) : Task.CompletedTask;

        /// <summary>
        /// Gives the player's answer about their self-care to the server and syncs by it (TASK-060): the question, a
        /// consent given later, or a withdrawal, which deletes the self-care on the server. A no is kept on the phone at
        /// once, across launches, until the server has it (<see cref="DeclineWaiting"/>); a yes that cannot reach the
        /// server waits for the next sync.
        /// </summary>
        public Task AnswerConsentAsync(bool given)
        {
            if (!given && _account.Account is { } known)
            {
                _storage.KeepDeclineWaiting(known.Id);
            }

            _pendingAnswer = given;
            _reconcile = true;
            Changed?.Invoke();
            return RunAsync(immediate: true);
        }

        /// <summary>Resolves <see cref="Choice"/>; a transfer still waiting for the key is given up.</summary>
        public async Task ChooseAsync(WorldChoice choice)
        {
            await CancelKeyTransferAsync();
            _choice = choice;
            _reconcile = true;
            await RunAsync(immediate: true);
        }

        /// <summary>
        /// Begins a key transfer from the old phone (TASK-023b): on <see cref="KeyTransferOutcome.Waiting"/> the
        /// <see cref="TransferTicket"/> is ready to show. The interface asks the server for the key with
        /// <see cref="PollKeyTransferAsync"/> while the code is on screen.
        /// </summary>
        public async Task<KeyTransferOutcome> BeginKeyTransferAsync()
        {
            var (accountId, outcome) = await AccountIdAsync();
            var begun = accountId is null ? TransferOutcomeOf(outcome) : await _receiver.BeginAsync();
            TransferOutcome = begun;
            Changed?.Invoke();
            return begun;
        }

        /// <summary>
        /// Asks the server once whether the old phone delivered the key. On <see cref="KeyTransferOutcome.Received"/>
        /// the key is kept and the cloud save is reconciled with it; the transfer is then over.
        /// </summary>
        public async Task<KeyTransferOutcome> PollKeyTransferAsync()
        {
            if (TransferOutcome is null)
            {
                return KeyTransferOutcome.Expired;
            }

            var (accountId, outcome) = await AccountIdAsync();
            var polled = accountId is null ? TransferOutcomeOf(outcome) : await _receiver.PollAsync(accountId.Value);
            if (polled == KeyTransferOutcome.Offline && _receiver.ExpiresAt is { } expiresAt && expiresAt <= _now())
            {
                // The server has forgotten the transfer by now, whether or not it can be asked: the code must come down.
                await _receiver.CancelAsync();
                polled = KeyTransferOutcome.Expired;
            }

            TransferOutcome = polled == KeyTransferOutcome.Received ? null : polled;
            if (polled == KeyTransferOutcome.Received)
            {
                await ReconcileAsync();
            }
            else
            {
                Changed?.Invoke();
            }

            return polled;
        }

        /// <summary>Gives up the open transfer, if any; the server forgets it too when it can be reached.</summary>
        public async Task CancelKeyTransferAsync()
        {
            if (TransferOutcome is null)
            {
                return;
            }

            TransferOutcome = null;
            await _receiver.CancelAsync();
            Changed?.Invoke();
        }

        /// <summary>
        /// Sends this phone's key to a new phone whose ticket was read from its QR code (TASK-023b). The key is sealed for
        /// that phone alone; the server relays what it cannot read.
        /// </summary>
        public Task<KeyDeliveryOutcome> SendKeyAsync(string ticketText) =>
            KeyTransferTicket.TryParse(ticketText) is { } ticket ? SendKeyAsync(ticket) : Task.FromResult(KeyDeliveryOutcome.BadTicket);

        /// <summary>Sends this phone's key to the new phone that showed <paramref name="ticket"/>, once the player has confirmed it.</summary>
        public async Task<KeyDeliveryOutcome> SendKeyAsync(KeyTransferTicket ticket)
        {
            var (accountId, outcome) = await AccountIdAsync();
            if (accountId is null)
            {
                return outcome == AccountOutcome.Offline ? KeyDeliveryOutcome.Offline
                    : outcome == AccountOutcome.SignedOut ? KeyDeliveryOutcome.SignedOut
                    : KeyDeliveryOutcome.Failed;
            }

            return await new KeyTransferSender(_transfers, _storage).SendAsync(accountId.Value, ticket);
        }

        /// <summary>
        /// Unlocks the account's contribution part with a recovery code typed in by the player (TASK-023b). The key is
        /// kept only once the cloud save confirms it (<see cref="CloudSync.AcceptKeyAsync"/>), and the cloud save is
        /// then reconciled again, now with it.
        /// </summary>
        public async Task<RecoveryCodeOutcome> EnterRecoveryCodeAsync(string code)
        {
            if (CloudSaving.RecoveryCode.TryParse(code) is not { } key)
            {
                return RecoveryCodeOutcome.NotACode;
            }

            var (accountId, outcome) = await AccountIdAsync();
            if (accountId is null)
            {
                return outcome == AccountOutcome.Offline ? RecoveryCodeOutcome.Offline : RecoveryCodeOutcome.Failed;
            }

            switch (await _sync.AcceptKeyAsync(accountId.Value, key))
            {
                case KeyAcceptance.Accepted:
                    await CancelKeyTransferAsync();
                    await ReconcileAsync();
                    return RecoveryCodeOutcome.Unlocked;
                case KeyAcceptance.OlderEpoch:
                    return RecoveryCodeOutcome.OlderCode;
                case KeyAcceptance.DoesNotOpen:
                    return RecoveryCodeOutcome.NotThisAccountsCode;
                case KeyAcceptance.Offline:
                    return RecoveryCodeOutcome.Offline;
                default:
                    return RecoveryCodeOutcome.Failed;
            }
        }

        private void OnAccountChanged()
        {
            bool signedIn = _account.IsSignedIn;
            if (signedIn == _wasSignedIn)
            {
                return;
            }

            _wasSignedIn = signedIn;
            if (signedIn)
            {
                _ = ReconcileAsync();
            }
            else
            {
                DropKeyTransfer();
                Choice = null;
                _pendingAnswer = null;
                Set(CloudSyncStatus.Off);
            }
        }

        private void OnAccountDeleted(Guid accountId)
        {
            DropKeyTransfer();
            _sync.Forget(accountId);
            _ = ForgetBackedUpKeyAsync(accountId);
            Choice = null;
            _pendingAnswer = null;
            Set(CloudSyncStatus.Off);
        }

        /// <summary>
        /// Takes the key from the platform's backup when the cloud's contribution part cannot be read here, so that a new
        /// phone on the same platform asks nothing (TASK-023b). The key is kept only once the cloud save confirms it, as
        /// a recovery code is; true when it was.
        /// </summary>
        private async Task<bool> RestoreBackedUpKeyAsync(Guid accountId)
        {
            if (_backup is null)
            {
                return false;
            }

            string? text;
            try
            {
                text = await _backup.ReadAsync(KeyBackups.Name(accountId));
            }
            catch (KeyBackupException problem)
            {
                Report(problem);
                return false; // The player is asked, as without a backup.
            }

            // The phone's own key already failed to open the part; the same key from the backup would too.
            if (ContributionKey.TryParse(text) is not { } key || _storage.Key(accountId)?.ToText() == key.ToText())
            {
                return false;
            }

            return await _sync.AcceptKeyAsync(accountId, key) == KeyAcceptance.Accepted;
        }

        /// <summary>
        /// Keeps the phone's key in the platform's backup once the cloud save holds it: a new key, and once a launch
        /// for a key kept before there was a backup, or before the phone got a screen lock.
        /// </summary>
        private async Task BackUpKeyAsync(Guid accountId, bool again)
        {
            if (_storage.Key(accountId) is not { } key)
            {
                return;
            }

            string text = key.ToText();
            if (text == _backedUpKey && !again)
            {
                return;
            }

            if (_backup is null)
            {
                _backedUpKey = text;
                SetKeyReach(KeyBackupReach.ThisPhone);
                return;
            }

            try
            {
                var reach = await _backup.KeepAsync(KeyBackups.Name(accountId), text);
                _backedUpKey = text;
                SetKeyReach(reach);
            }
            catch (KeyBackupException problem)
            {
                // The next sync tries again; until then the account sheet suggests nothing it cannot back up.
                Report(problem);
                SetKeyReach(null);
            }
        }

        /// <summary>
        /// Removes the deleted account's key from this platform's backup, and iCloud Keychain passes the removal on to the
        /// player's other devices. Copies stay where Block Store already gave the key to another Android phone, in the
        /// other platform's backup, and everywhere when the account is deleted on the web page; they open nothing the
        /// server still keeps (threat model).
        /// </summary>
        private async Task ForgetBackedUpKeyAsync(Guid accountId)
        {
            _backedUpKey = null;
            KeyReach = null;
            if (_backup is null)
            {
                return;
            }

            try
            {
                await _backup.ForgetAsync(KeyBackups.Name(accountId));
            }
            catch (KeyBackupException problem)
            {
                // Without the account, a key left in the backup opens nothing the server still keeps (ADR-0008).
                Report(problem);
            }
        }

        /// <summary>
        /// Logs why the platform's backup failed, so that it can be told apart on a device: the reason is a status code
        /// or one of our own answers, never a key (<see cref="KeyBackupException"/>).
        /// </summary>
        private static void Report(KeyBackupException problem) => UnityEngine.Debug.LogWarning("Key backup: " + problem.Message);

        private void SetKeyReach(KeyBackupReach? reach)
        {
            if (KeyReach != reach)
            {
                KeyReach = reach;
                Changed?.Invoke();
            }
        }

        /// <summary>Forgets an open transfer without the server: the sign-in it needs is gone, and so is the transfer soon.</summary>
        private void DropKeyTransfer()
        {
            if (TransferOutcome is not null)
            {
                TransferOutcome = null;
                _ = _receiver.CancelAsync();
            }
        }

        private static KeyTransferOutcome TransferOutcomeOf(AccountOutcome outcome) => outcome switch
        {
            AccountOutcome.Offline => KeyTransferOutcome.Offline,
            AccountOutcome.SignedOut => KeyTransferOutcome.SignedOut,
            _ => KeyTransferOutcome.Failed,
        };

        private async Task RunAsync(bool immediate)
        {
            if (_running is { } running)
            {
                // The run under way sees the flags once its current call is done.
                await running;
                return;
            }

            _running = LoopAsync(immediate);
            try
            {
                await _running;
            }
            finally
            {
                _running = null;
            }
        }

        private async Task LoopAsync(bool immediate)
        {
            if (!immediate)
            {
                await _delay();
            }

            int loadAttempts = 0;
            bool backupTried = false;
            while (_dirty || _reconcile)
            {
                if (!_account.IsSignedIn)
                {
                    _dirty = _reconcile = false;
                    Choice = null;
                    Set(CloudSyncStatus.Off);
                    return;
                }

                Set(CloudSyncStatus.Working);
                var (accountId, outcome) = await AccountIdAsync();
                if (accountId is null)
                {
                    if (outcome == AccountOutcome.Busy && ++loadAttempts < LoadAttempts)
                    {
                        await _delay(); // An account action is under way; the account is known once it is done.
                        continue;
                    }

                    _dirty = _reconcile = false;
                    Set(outcome switch
                    {
                        AccountOutcome.Offline => CloudSyncStatus.Offline,
                        AccountOutcome.SignedOut => CloudSyncStatus.SignedOut,
                        _ => CloudSyncStatus.Failed,
                    });
                    return;
                }

                if (_pendingAnswer is { } answer)
                {
                    var answered = await _sync.AnswerConsentAsync(accountId.Value, answer);
                    if (answered == StoreStatus.Offline)
                    {
                        // The answer waits on the phone for the next sync; the question is not asked again meanwhile.
                        _dirty = _reconcile = false;
                        Set(CloudSyncStatus.Offline);
                        return;
                    }

                    _pendingAnswer = null;
                    if (answered != StoreStatus.Ok && !answer)
                    {
                        // The no stays on the phone and goes first at the next sync; nothing goes up until then.
                        _dirty = _reconcile = false;
                        Set(answered == StoreStatus.SignedOut ? CloudSyncStatus.SignedOut : CloudSyncStatus.Failed);
                        return;
                    }

                    _reconcile = true; // After a yes that did not get through, the reconciliation asks again.
                }

                bool reconcile = _reconcile;
                var choice = _choice;
                _dirty = _reconcile = false;
                _choice = null;
                var result = reconcile ? await _sync.ReconcileAsync(accountId.Value, choice) : await _sync.UploadAsync(accountId.Value);
                if (!backupTried && result.ContributionsUnreadable && result.Outcome is SyncOutcome.KeyNeeded or SyncOutcome.ChoiceNeeded)
                {
                    // Before the player is asked, the key may be in the platform's backup (TASK-023b).
                    backupTried = true;
                    if (await RestoreBackedUpKeyAsync(accountId.Value))
                    {
                        _reconcile = true;
                        continue;
                    }
                }

                Apply(result);
                if (result.Outcome == SyncOutcome.Synced && _sync.Consent(accountId.Value)?.Answer == ConsentAnswer.Given)
                {
                    await BackUpKeyAsync(accountId.Value, again: reconcile);
                }
            }
        }

        private async Task<(Guid? Id, AccountOutcome Outcome)> AccountIdAsync()
        {
            if (_account.Account is { } known)
            {
                return (known.Id, AccountOutcome.Done);
            }

            var outcome = await _account.LoadAsync();
            return (_account.Account?.Id, outcome);
        }

        private void Apply(SyncResult result)
        {
            if (result.Downloaded)
            {
                Welcome = true;
                WelcomeWithoutSelfCare = result.WorldPartOnly;
            }

            _contributionsOnAccount = result.ContributionsOnAccount;

            if (result.KeyEpochBegan)
            {
                KeyEpochBegan = true;
                CloudSaveUnreadable = result.CloudSaveUnreadable;
            }

            // The old part is locked only once the phone's world got through under the new epoch, which can be a later try.
            if (result.ContributionPartLocked)
            {
                _lockPending = true;
            }

            if (result.KeyEpochBegan || result.Outcome == SyncOutcome.ChoiceNeeded)
            {
                _lockPending = false;
            }
            else if (_lockPending && result.Outcome == SyncOutcome.Synced)
            {
                _lockPending = false;
                ContributionPartLocked = true;
            }

            Choice = result.Outcome is SyncOutcome.ChoiceNeeded or SyncOutcome.KeyNeeded ? result : null;
            if (result.Outcome == SyncOutcome.Synced)
            {
                SyncedAt = _now();
            }

            Set(result.Outcome switch
            {
                SyncOutcome.Synced => CloudSyncStatus.Synced,
                SyncOutcome.NoWorld => CloudSyncStatus.NoWorld,
                SyncOutcome.ConsentNeeded => CloudSyncStatus.ConsentNeeded,
                SyncOutcome.ChoiceNeeded => CloudSyncStatus.ChoiceNeeded,
                SyncOutcome.KeyNeeded => CloudSyncStatus.KeyNeeded,
                SyncOutcome.Offline => CloudSyncStatus.Offline,
                SyncOutcome.SignedOut => CloudSyncStatus.SignedOut,
                SyncOutcome.AppOutdated => CloudSyncStatus.AppOutdated,
                _ => CloudSyncStatus.Failed,
            });
        }

        private void Set(CloudSyncStatus status)
        {
            Status = status;
            Changed?.Invoke();
        }
    }
}
