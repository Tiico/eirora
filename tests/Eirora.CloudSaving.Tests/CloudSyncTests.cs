using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;

namespace Eirora.CloudSaving.Tests;

/// <summary>
/// The device and the cloud save kept the same (TASK-023, ADR-0008): the first phone uploads, a new phone gets the
/// world, two phones that played apart merge (D-99), a phone without the key begins a new key epoch (D-101), two
/// different worlds wait for the player, and a contribution part that does not open is never merged (threat model).
/// </summary>
public sealed class CloudSyncTests
{
    private const long Hour = 3600;
    private static readonly Guid s_account = Guid.NewGuid();
    private static readonly TimeReading s_install = new(1_780_304_400, "Europe/Stockholm", 7200);

    [Fact]
    public async Task TheFirstPhoneUploadsUnderKeyEpochOne()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());

        var result = await phone.Sync.ReconcileAsync(s_account);

        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.Equal((1L, 1), (store.Current!.Version, store.Current.KeyEpoch));
        Assert.Equal(1, phone.Storage.Key(s_account)!.Epoch);
        Assert.Equal((1L, 1), (phone.Storage.State(s_account)!.Version, phone.Storage.State(s_account)!.KeyEpoch));
        Assert.Equal(phone.Save.ToParts().World, store.Current.World);
        Assert.Null(ContributionCipher.Open(ContributionKey.Generate(1), s_account, 1, store.Current.Contributions));
    }

    [Fact]
    public async Task TheContributionPartOnTheServerOpensOnlyWithThePhonesKey()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);

        byte[]? opened = ContributionCipher.Open(phone.Storage.Key(s_account)!, s_account, 1, store.Current!.Contributions);

        Assert.Equal(phone.Save.ToParts().Contributions, opened);
    }

    [Fact]
    public async Task ANewPhoneWithTheKeyGetsTheWorld()
    {
        var store = new FakeCloudSaveStore();
        var old = new Device(store, PlayedSave());
        await old.Sync.ReconcileAsync(s_account);
        var fresh = new Device(store);
        old.Storage.CopyKeyTo(fresh.Storage, s_account);

        var result = await fresh.Sync.ReconcileAsync(s_account);

        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.True(result.Downloaded);
        Assert.False(result.KeyEpochBegan);
        Assert.Equal(old.Fingerprint, fresh.Fingerprint);
        Assert.Equal(1L, fresh.Storage.State(s_account)!.Version);
        Assert.Equal(1, store.Uploads);
    }

    [Fact]
    public async Task ANewPhoneWithoutTheKeyBeginsANewKeyEpochWithTheGameDaysClosed()
    {
        var store = new FakeCloudSaveStore();
        var old = new Device(store, PlayedSave());
        await old.Sync.ReconcileAsync(s_account);
        var fresh = new Device(store);

        var result = await fresh.Sync.ReconcileAsync(s_account);

        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.True(result.Downloaded && result.KeyEpochBegan);
        Assert.Equal(old.Save.ToParts().World, fresh.Save.ToParts().World);
        Assert.Equal(old.Save.World.GameDay, fresh.Save.World.LastClosedGameDay);
        Assert.Equal(2, fresh.Storage.Key(s_account)!.Epoch);
        Assert.Equal((2L, 2), (store.Current!.Version, store.Current.KeyEpoch));
        Assert.Equal([1], store.Locked.Select(l => l.KeyEpoch));
        Assert.NotNull(ContributionCipher.Open(fresh.Storage.Key(s_account)!, s_account, 2, store.Current.Contributions));
    }

    [Fact]
    public async Task TheOldPhoneWithTheOldKeyCannotReplaceTheNewEpoch()
    {
        var store = new FakeCloudSaveStore();
        var old = new Device(store, PlayedSave());
        await old.Sync.ReconcileAsync(s_account);
        var fresh = new Device(store);
        await fresh.Sync.ReconcileAsync(s_account);
        Advance(old.Save, Hour);
        old.Save.Apply("old-rush", new Rush());

        var result = await old.Sync.UploadAsync(s_account);

        // The old phone's world and the new epoch's are different segments now: the player chooses.
        Assert.Equal(SyncOutcome.ChoiceNeeded, result.Outcome);
        Assert.True(result.ContributionsUnreadable);
        Assert.Equal((2L, 2), (store.Current!.Version, store.Current.KeyEpoch));
    }

    [Fact]
    public async Task TwoPhonesThatPlayedApartMergeTheirJournals()
    {
        var store = new FakeCloudSaveStore();
        var a = new Device(store, PlayedSave());
        await a.Sync.ReconcileAsync(s_account);
        var b = new Device(store);
        a.Storage.CopyKeyTo(b.Storage, s_account);
        await b.Sync.ReconcileAsync(s_account);
        int day = a.Save.World.GameDay;
        a.Save.Apply("a-steps", new RecordSelfCare(day, Habit.Steps, ContributionSource.Verified, 8_000));
        Advance(b.Save, Hour);
        b.Save.Apply("b-schedule", new ChangeRestSchedule(new RestSchedule(21 * 60, 9 * 60)));

        var fromA = await a.Sync.UploadAsync(s_account);
        var fromB = await b.Sync.UploadAsync(s_account);
        var backToA = await a.Sync.UploadAsync(s_account);

        Assert.Equal((SyncOutcome.Synced, false), (fromA.Outcome, fromA.Merged));
        Assert.Equal((SyncOutcome.Synced, true), (fromB.Outcome, fromB.Merged));
        Assert.Equal((SyncOutcome.Synced, true), (backToA.Outcome, backToA.Merged));
        Assert.Equal(a.Fingerprint, b.Fingerprint);
        Assert.Equal(8_000, b.Save.World.RecordedSelfCare(day, Habit.Steps, ContributionSource.Verified));
        Assert.Equal(new RestSchedule(21 * 60, 9 * 60), a.Save.World.PendingRestSchedule);
        Assert.Equal(3L, store.Current!.Version);
        Assert.Equal(ReplaceReason.Merged, b.World.Replacements.Single().Reason);
    }

    [Fact]
    public async Task APhoneThatHasEverythingTheCloudHasUploadsNothing()
    {
        var store = new FakeCloudSaveStore();
        var a = new Device(store, PlayedSave());
        await a.Sync.ReconcileAsync(s_account);
        var again = new Device(store, Save.Load(a.Save.ToBytes()), a.Storage);

        var result = await again.Sync.ReconcileAsync(s_account);

        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.False(result.Merged);
        Assert.Equal(1, store.Uploads);
        Assert.Empty(again.World.Replacements);
    }

    [Fact]
    public async Task AnUploadAfterAChangeIsBasedOnTheRememberedVersion()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        Advance(phone.Save, Hour);

        var result = await phone.Sync.UploadAsync(s_account);

        Assert.Equal((SyncOutcome.Synced, 2L), (result.Outcome, result.Version));
        Assert.Equal(2L, store.Current!.Version);
        Assert.Equal(2L, phone.Storage.State(s_account)!.Version);
    }

    [Fact]
    public async Task DifferentWorldsWaitForThePlayer()
    {
        var store = new FakeCloudSaveStore();
        var cloudPhone = new Device(store, PlayedSave());
        await cloudPhone.Sync.ReconcileAsync(s_account);
        var phone = new Device(store, PlayedSave(installOffset: 1));
        cloudPhone.Storage.CopyKeyTo(phone.Storage, s_account);

        var result = await phone.Sync.ReconcileAsync(s_account);

        Assert.Equal(SyncOutcome.ChoiceNeeded, result.Outcome);
        Assert.False(result.ContributionsUnreadable);
        Assert.Equal(cloudPhone.Save.World.GameDay, result.CloudGameDay);
        Assert.Equal(store.Current!.SavedAt, result.CloudSavedAt);
        Assert.Equal(1, store.Uploads);
        Assert.Empty(phone.World.Replacements);
    }

    [Fact]
    public async Task KeepingThePhonesWorldReplacesTheCloudsAndKeepsItAside()
    {
        var store = new FakeCloudSaveStore();
        var cloudPhone = new Device(store, PlayedSave());
        await cloudPhone.Sync.ReconcileAsync(s_account);
        var phone = new Device(store, PlayedSave(installOffset: 1));
        cloudPhone.Storage.CopyKeyTo(phone.Storage, s_account);
        string phones = phone.Fingerprint;

        var result = await phone.Sync.ReconcileAsync(s_account, WorldChoice.KeepPhone);

        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.Equal(phones, phone.Fingerprint);
        Assert.Equal(cloudPhone.Fingerprint, phone.World.Aside.Single().World.Fingerprint());
        Assert.Equal(phone.Save.ToParts().World, store.Current!.World);
        Assert.Equal(2L, store.Current.Version);
    }

    [Fact]
    public async Task TakingTheCloudsWorldReplacesThePhonesWhichIsKeptAside()
    {
        var store = new FakeCloudSaveStore();
        var cloudPhone = new Device(store, PlayedSave());
        await cloudPhone.Sync.ReconcileAsync(s_account);
        var phone = new Device(store, PlayedSave(installOffset: 1));
        cloudPhone.Storage.CopyKeyTo(phone.Storage, s_account);
        string phones = phone.Fingerprint;

        var result = await phone.Sync.ReconcileAsync(s_account, WorldChoice.TakeCloud);

        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.Equal(cloudPhone.Fingerprint, phone.Fingerprint);
        Assert.Equal((phones, ReplaceReason.CloudChosen), (phone.World.Replacements.Single().Replaced.World.Fingerprint(), phone.World.Replacements.Single().Reason));
        Assert.Equal(1, store.Uploads);
    }

    [Fact]
    public async Task WithoutTheKeyTakingTheCloudsWorldBeginsANewEpochAndKeepingThePhonesLocksTheCloudsPart()
    {
        var store = new FakeCloudSaveStore();
        await new Device(store, PlayedSave()).Sync.ReconcileAsync(s_account);
        var taker = new Device(store, PlayedSave(installOffset: 1));
        var keeper = new Device(store, PlayedSave(installOffset: 2));

        var asked = await taker.Sync.ReconcileAsync(s_account);
        var taken = await taker.Sync.ReconcileAsync(s_account, WorldChoice.TakeCloud);
        var kept = await keeper.Sync.ReconcileAsync(s_account, WorldChoice.KeepPhone);

        Assert.True(asked.Outcome == SyncOutcome.ChoiceNeeded && asked.ContributionsUnreadable);
        Assert.True(taken.Outcome == SyncOutcome.Synced && taken.KeyEpochBegan);
        Assert.Equal(2, taker.Storage.Key(s_account)!.Epoch);
        Assert.Equal(ReplaceReason.CloudChosen, taker.World.Replacements.Single().Reason);
        Assert.True(kept.Outcome == SyncOutcome.Synced && kept.KeyEpochBegan);
        Assert.Equal(3, keeper.Storage.Key(s_account)!.Epoch);
        Assert.Equal([1, 2], store.Locked.Select(l => l.KeyEpoch));
        Assert.Equal(keeper.Save.ToParts().World, store.Current!.World);
    }

    // A thief with a token can replace the cloud save but not seal a contribution part the key opens: such a save is
    // never merged. The player chooses, and keeping the phone's world locks the part on the server under a new epoch.
    [Fact]
    public async Task AContributionPartThatDoesNotOpenIsNeverMergedAndThePlayerChooses()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        var tampered = (byte[])store.Current!.Contributions.Clone();
        tampered[^1] ^= 0x01;
        store.Current = new Contracts.CloudSave(store.Current.Version, store.Current.KeyEpoch, store.Current.World, tampered, store.Current.SavedAt);
        Advance(phone.Save, Hour);
        string phones = phone.Fingerprint;

        var asked = await phone.Sync.ReconcileAsync(s_account);
        var kept = await phone.Sync.ReconcileAsync(s_account, WorldChoice.KeepPhone);

        Assert.True(asked.Outcome == SyncOutcome.ChoiceNeeded && asked.CloudSaveUnreadable && asked.ContributionsUnreadable);
        Assert.True(kept.Outcome == SyncOutcome.Synced && kept.KeyEpochBegan);
        Assert.Equal(phones, phone.Fingerprint);
        Assert.Empty(phone.World.Replacements);
        Assert.Equal(phone.Save.ToParts().World, store.Current.World);
        Assert.Equal((2L, 2), (store.Current.Version, store.Current.KeyEpoch));
        Assert.Equal([1], store.Locked.Select(l => l.KeyEpoch));
        Assert.NotNull(ContributionCipher.Open(phone.Storage.Key(s_account)!, s_account, 2, store.Current.Contributions));
    }

    [Fact]
    public async Task APartMovedToAnotherVersionDoesNotOpen()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        var first = store.Current!;
        Advance(phone.Save, Hour);
        await phone.Sync.UploadAsync(s_account);
        store.Current = new Contracts.CloudSave(2, 1, store.Current!.World, first.Contributions, store.Current.SavedAt);
        var fresh = new Device(store);
        phone.Storage.CopyKeyTo(fresh.Storage, s_account);

        var result = await fresh.Sync.ReconcileAsync(s_account);

        Assert.True(result.CloudSaveUnreadable);
        Assert.True(result.KeyEpochBegan, "without a readable part, the new phone goes on from the world part alone");
    }

    // The new epoch begins with the upload; when that does not get through, the phone's next try finishes it.
    [Fact]
    public async Task ANewEpochWhoseUploadDidNotGetThroughIsUploadedOnTheNextTry()
    {
        var store = new FakeCloudSaveStore();
        await new Device(store, PlayedSave()).Sync.ReconcileAsync(s_account);
        var fresh = new Device(store);
        store.TakesUploads = false;
        var first = await fresh.Sync.ReconcileAsync(s_account);
        store.TakesUploads = true;
        Advance(fresh.Save, Hour);
        fresh.Save.Apply("fresh-rush", new Rush());
        string played = fresh.Fingerprint;

        var upload = await fresh.Sync.UploadAsync(s_account);

        Assert.True(first.Outcome == SyncOutcome.Offline && first.Downloaded);
        Assert.Equal(SyncOutcome.Synced, upload.Outcome);
        Assert.Equal(played, fresh.Fingerprint);
        Assert.Empty(fresh.World.Replacements);
        Assert.Equal((2L, 2), (store.Current!.Version, store.Current.KeyEpoch));
        Assert.Equal(fresh.Save.ToParts().World, store.Current.World);
        Assert.NotNull(ContributionCipher.Open(fresh.Storage.Key(s_account)!, s_account, 2, store.Current.Contributions));
    }

    [Theory]
    [InlineData(WorldChoice.TakeCloud)]
    [InlineData(WorldChoice.KeepPhone)]
    public async Task AChosenNewEpochWhoseUploadDidNotGetThroughIsUploadedAtTheNextLaunch(WorldChoice choice)
    {
        var store = new FakeCloudSaveStore();
        await new Device(store, PlayedSave()).Sync.ReconcileAsync(s_account);
        var phone = new Device(store, PlayedSave(installOffset: 1));
        store.TakesUploads = false;
        var chosen = await phone.Sync.ReconcileAsync(s_account, choice);
        store.TakesUploads = true;
        string kept = phone.Fingerprint;

        var launch = await phone.Sync.ReconcileAsync(s_account);

        Assert.Equal(SyncOutcome.Offline, chosen.Outcome);
        Assert.Equal(SyncOutcome.Synced, launch.Outcome);
        Assert.Equal(kept, phone.Fingerprint);
        Assert.Equal((2L, 2), (store.Current!.Version, store.Current.KeyEpoch));
        Assert.Equal(phone.Save.ToParts().World, store.Current.World);
    }

    // Another phone moved the old epoch on meanwhile: its contributions do not open here, so the player chooses.
    [Fact]
    public async Task ANewEpochWhoseUploadDidNotGetThroughAsksWhenTheOldEpochMovedOn()
    {
        var store = new FakeCloudSaveStore();
        var old = new Device(store, PlayedSave());
        await old.Sync.ReconcileAsync(s_account);
        var fresh = new Device(store);
        store.TakesUploads = false;
        await fresh.Sync.ReconcileAsync(s_account);
        store.TakesUploads = true;
        Advance(old.Save, Hour);
        old.Save.Apply("old-rush", new Rush());
        await old.Sync.UploadAsync(s_account);

        var result = await fresh.Sync.UploadAsync(s_account);

        Assert.True(result.Outcome == SyncOutcome.ChoiceNeeded && result.ContributionsUnreadable);
        Assert.Equal((2L, 1), (store.Current!.Version, store.Current.KeyEpoch));
    }

    [Fact]
    public async Task OfflineNothingChangesAndTheNextTryUploads()
    {
        var store = new FakeCloudSaveStore { Online = false };
        var phone = new Device(store, PlayedSave());

        var offline = await phone.Sync.ReconcileAsync(s_account);
        var stateWhileOffline = phone.Storage.State(s_account);
        store.Online = true;
        var online = await phone.Sync.UploadAsync(s_account);

        Assert.Equal(SyncOutcome.Offline, offline.Outcome);
        Assert.Null(stateWhileOffline);
        Assert.Equal(SyncOutcome.Synced, online.Outcome);
        Assert.Equal(1L, store.Current!.Version);
    }

    [Fact]
    public async Task SignedOutNothingChanges()
    {
        var store = new FakeCloudSaveStore { SignedIn = false };
        var phone = new Device(store, PlayedSave());

        Assert.Equal(SyncOutcome.SignedOut, (await phone.Sync.ReconcileAsync(s_account)).Outcome);
        Assert.Equal(SyncOutcome.SignedOut, (await phone.Sync.UploadAsync(s_account)).Outcome);
        Assert.Null(store.Current);
    }

    [Fact]
    public async Task WithoutAWorldAnywhereThereIsNothingToDo()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store);

        Assert.Equal(SyncOutcome.NoWorld, (await phone.Sync.ReconcileAsync(s_account)).Outcome);
        Assert.Equal(SyncOutcome.NoWorld, (await phone.Sync.UploadAsync(s_account)).Outcome);
        Assert.Null(phone.Storage.Key(s_account));
    }

    [Fact]
    public async Task ForgettingTheAccountRemovesTheKeyAndTheState()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);

        phone.Sync.Forget(s_account);

        Assert.Null(phone.Storage.Key(s_account));
        Assert.Null(phone.Storage.State(s_account));
    }

    [Fact]
    public async Task ASecondAccountOnTheSamePhoneGetsItsOwnKey()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        var other = Guid.NewGuid();
        var otherStore = new FakeCloudSaveStore();
        var sameStorage = new Device(otherStore, phone.World.Current, phone.Storage);

        await sameStorage.Sync.ReconcileAsync(other);

        Assert.NotEqual(phone.Storage.Key(s_account)!.ToText(), phone.Storage.Key(other)!.ToText());
        Assert.Null(ContributionCipher.Open(phone.Storage.Key(s_account)!, other, 1, otherStore.Current!.Contributions));
    }

    private static void Advance(Save save, long seconds)
    {
        var now = save.World.Now;
        save.AdvanceTo(new TimeReading(now.UtcSeconds + seconds, now.ZoneId, now.OffsetSeconds));
    }

    /// <summary>A world a few hours old with some self-care recorded; another <paramref name="installOffset"/> is another world.</summary>
    private static Save PlayedSave(int installOffset = 0)
    {
        var save = Save.Begin(new TimeReading(s_install.UtcSeconds + installOffset, s_install.ZoneId, s_install.OffsetSeconds), new RestSchedule(23 * 60, 8 * 60));
        Advance(save, 2 * Hour);
        save.Apply("steps", new RecordSelfCare(save.World.GameDay, Habit.Steps, ContributionSource.Verified, 6_000));
        Advance(save, Hour);
        return save;
    }
}
