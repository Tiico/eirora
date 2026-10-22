using System;

namespace Eirora.CloudSaving
{
    /// <summary>How a reconciliation or an upload went, and what it did to the device's world.</summary>
    public sealed class SyncResult
    {
        internal SyncResult(SyncOutcome outcome, SyncChanges changes, long? version = null, int? cloudGameDay = null, DateTimeOffset? cloudSavedAt = null, bool contributionsUnreadable = false)
        {
            Outcome = outcome;
            Downloaded = changes.Downloaded;
            Merged = changes.Merged;
            KeyEpochBegan = changes.KeyEpochBegan;
            ContributionPartLocked = changes.ContributionPartLocked;
            CloudSaveUnreadable = changes.CloudSaveUnreadable;
            Version = version;
            CloudGameDay = cloudGameDay;
            CloudSavedAt = cloudSavedAt;
            ContributionsUnreadable = contributionsUnreadable;
        }

        public SyncOutcome Outcome { get; }

        /// <summary>The device had no world and the cloud save's came down.</summary>
        public bool Downloaded { get; }

        /// <summary>The device's journal and the cloud save's were merged (D-99).</summary>
        public bool Merged { get; }

        /// <summary>
        /// The cloud's world was taken without its contribution part, which this device could not read, so a new key
        /// epoch began: the game days so far are closed and the old part is locked on the server for 90 days
        /// (ADR-0008, D-101).
        /// </summary>
        public bool KeyEpochBegan { get; }

        /// <summary>
        /// The phone's world replaces a cloud save whose contribution part this device could not read (D-102): it goes
        /// up under a new key epoch, so the server locks the old part for 90 days rather than losing it. Nothing on the
        /// phone changed. Set when the upload is tried, like <see cref="KeyEpochBegan"/>: the part is locked only once
        /// an upload under the new epoch gets through, which can be a later try.
        /// </summary>
        public bool ContributionPartLocked { get; }

        /// <summary>
        /// The cloud save's contribution part did not open with the device's key for its epoch, so it was never merged
        /// (threat model): a new phone goes on from the world part alone, and a phone with a world asks the player.
        /// Either way a new key epoch begins, which locks the part on the server rather than losing it.
        /// </summary>
        public bool CloudSaveUnreadable { get; }

        /// <summary>The cloud save version the device's world is now based on, when it is.</summary>
        public long? Version { get; }

        /// <summary>For <see cref="SyncOutcome.ChoiceNeeded"/>: the game day the cloud's world is on.</summary>
        public int? CloudGameDay { get; }

        /// <summary>For <see cref="SyncOutcome.ChoiceNeeded"/>: when the cloud's world was last saved.</summary>
        public DateTimeOffset? CloudSavedAt { get; }

        /// <summary>
        /// For <see cref="SyncOutcome.ChoiceNeeded"/>: this device cannot read the cloud's contribution part, so taking
        /// the cloud's world begins a new key epoch.
        /// </summary>
        public bool ContributionsUnreadable { get; }
    }

    public enum SyncOutcome
    {
        /// <summary>The device's world and the cloud save are the same now.</summary>
        Synced,

        /// <summary>Neither the device nor the account has a world yet.</summary>
        NoWorld,

        /// <summary>
        /// The phone and the cloud hold different worlds; the player chooses, and the reconciliation is run again with
        /// the choice.
        /// </summary>
        ChoiceNeeded,

        /// <summary>The server could not be reached; nothing changed, and the next change or launch tries again.</summary>
        Offline,

        /// <summary>The device holds no sign-in, or the server no longer accepts it.</summary>
        SignedOut,

        /// <summary>The cloud save was written by a newer app than this one; updating the app will read it.</summary>
        AppOutdated,

        /// <summary>The server or the cloud save answered with something this version cannot handle.</summary>
        Failed,
    }

    /// <summary>The player's choice when the phone and the cloud hold different worlds.</summary>
    public enum WorldChoice
    {
        /// <summary>The phone's world replaces the cloud save; the cloud's world is kept aside on the phone.</summary>
        KeepPhone,

        /// <summary>The cloud's world replaces the phone's, which is kept aside.</summary>
        TakeCloud,
    }

    /// <summary>What a reconciliation did so far, carried through its retries after a conflict.</summary>
    internal sealed class SyncChanges
    {
        public bool Downloaded { get; set; }

        public bool Merged { get; set; }

        public bool KeyEpochBegan { get; set; }

        public bool ContributionPartLocked { get; set; }

        public bool CloudSaveUnreadable { get; set; }
    }
}
