using Eirora.Contracts;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Saving;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;

namespace Eirora.CloudSaving.Tests;

/// <summary>
/// The consent to storing the contribution part on the account (TASK-060, GDPR Article 9(2)(a)): nothing goes up before
/// the player answered, without consent only the world part does, a consent sends the contribution part with the next
/// upload, a withdrawal deletes it and stops it, and a decline is never asked again.
/// </summary>
public sealed class ContributionConsentTests
{
    private const long Hour = 3600;
    private static readonly Guid s_account = Guid.NewGuid();
    private static readonly TimeReading s_install = new(1_780_304_400, "Europe/Stockholm", 7200);

    [Fact]
    public async Task BeforeThePlayerAnswersNothingGoesUpAndThePlayerIsAsked()
    {
        var store = new FakeCloudSaveStore { Consent = Unanswered() };
        var phone = new Device(store, PlayedSave());

        var result = await phone.Sync.ReconcileAsync(s_account);

        Assert.Equal((SyncOutcome.ConsentNeeded, false), (result.Outcome, result.ContributionsOnAccount));
        Assert.Null(store.Current);
        Assert.Equal(0, store.Uploads);
        Assert.Null(phone.Storage.Key(s_account));
    }

    // An account from before the question keeps the part it uploaded until the player answers (the issue's migration).
    [Fact]
    public async Task AContributionPartFromBeforeTheQuestionStaysUntilThePlayerAnswers()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        byte[] uploaded = store.Current!.Contributions!;
        store.Consent = Unanswered();
        Advance(phone.Save, Hour);

        var asked = await phone.Sync.ReconcileAsync(s_account);
        var change = await phone.Sync.UploadAsync(s_account);

        Assert.Equal((SyncOutcome.ConsentNeeded, true), (asked.Outcome, asked.ContributionsOnAccount));
        Assert.Equal(SyncOutcome.ConsentNeeded, change.Outcome);
        Assert.Equal((1L, uploaded), (store.Current.Version, store.Current.Contributions));
    }

    [Fact]
    public async Task DecliningDeletesAContributionPartFromBeforeTheQuestion()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        store.Consent = Unanswered();

        var answered = await phone.Sync.AnswerConsentAsync(s_account, given: false);
        var result = await phone.Sync.ReconcileAsync(s_account);

        Assert.Equal(StoreStatus.Ok, answered);
        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.Null(store.Current!.Contributions);
        Assert.Equal(1L, store.Current.Version);
    }

    // Acceptance criterion 1.
    [Fact]
    public async Task WithoutConsentOnlyTheWorldPartGoesUp()
    {
        var store = new FakeCloudSaveStore { Consent = Unanswered() };
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);

        var answered = await phone.Sync.AnswerConsentAsync(s_account, given: false);
        var first = await phone.Sync.ReconcileAsync(s_account);
        RecordSteps(phone.Save, 3_000);
        var second = await phone.Sync.UploadAsync(s_account);

        Assert.Equal(StoreStatus.Ok, answered);
        Assert.Equal((SyncOutcome.Synced, SyncOutcome.Synced), (first.Outcome, second.Outcome));
        Assert.Equal((2L, 0), (store.Current!.Version, store.Current.KeyEpoch));
        Assert.Null(store.Current.Contributions);
        Assert.Equal(phone.Save.ToParts().World, store.Current.World);
        Assert.Null(phone.Storage.Key(s_account));
        Assert.Equal((2L, 0), (phone.Storage.State(s_account)!.Version, phone.Storage.State(s_account)!.KeyEpoch));
    }

    // Acceptance criterion 2.
    [Fact]
    public async Task AfterAConsentTheNextUploadCarriesTheContributionPart()
    {
        var store = new FakeCloudSaveStore { Consent = Declined() };
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);

        var answered = await phone.Sync.AnswerConsentAsync(s_account, given: true);
        var result = await phone.Sync.ReconcileAsync(s_account);

        Assert.Equal(StoreStatus.Ok, answered);
        Assert.Equal((ConsentAnswer.Given, ContributionConsent.CurrentVersion), (store.Consent.Answer, store.Consent.Version));
        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.Equal((2L, 1), (store.Current!.Version, store.Current.KeyEpoch));
        Assert.Equal(phone.Save.ToParts().Contributions, ContributionCipher.Open(phone.Storage.Key(s_account)!, s_account, 2, store.Current.Contributions!));
    }

    // Acceptance criterion 3.
    [Fact]
    public async Task AWithdrawalDeletesThePartOnTheServerAndNoneGoesUpAfterIt()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        int uploads = store.Uploads;

        var withdrawn = await phone.Sync.AnswerConsentAsync(s_account, given: false);
        var reconciled = await phone.Sync.ReconcileAsync(s_account);
        bool deletedAtOnce = store.Current!.Contributions is null;
        RecordSteps(phone.Save, 3_000);
        var later = await phone.Sync.UploadAsync(s_account);

        Assert.Equal(StoreStatus.Ok, withdrawn);
        Assert.True(deletedAtOnce);
        Assert.Equal((SyncOutcome.Synced, uploads), (reconciled.Outcome, store.Uploads - 1));
        Assert.Equal(SyncOutcome.Synced, later.Outcome);
        Assert.Null(store.Current.Contributions);
        Assert.Equal(phone.Save.ToParts().World, store.Current.World);
    }

    // A withdrawal on one phone reaches another at its next upload: the server says 409 to a part, and the phone asks.
    [Fact]
    public async Task AWithdrawalOnAnotherPhoneStopsThisPhonesPartAtItsNextUpload()
    {
        var store = new FakeCloudSaveStore();
        var a = new Device(store, PlayedSave());
        await a.Sync.ReconcileAsync(s_account);
        var b = new Device(store);
        a.Storage.CopyKeyTo(b.Storage, s_account);
        await b.Sync.ReconcileAsync(s_account);

        await a.Sync.AnswerConsentAsync(s_account, given: false);
        RecordSteps(b.Save, 3_000);
        var result = await b.Sync.UploadAsync(s_account);

        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.Equal(ConsentAnswer.Declined, b.Sync.Consent(s_account)!.Answer);
        Assert.Null(store.Current!.Contributions);
        Assert.Equal(b.Save.ToParts().World, store.Current.World);
    }

    // Acceptance criterion 4: the answer is the account's, so neither a new launch nor another phone asks again.
    [Fact]
    public async Task ADeclinedConsentIsNotAskedAgain()
    {
        var store = new FakeCloudSaveStore { Consent = Unanswered() };
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        await phone.Sync.AnswerConsentAsync(s_account, given: false);

        var reopened = new Device(store, phone.Save, phone.Storage);
        var atLaunch = await reopened.Sync.ReconcileAsync(s_account);
        var another = await new Device(store, null).Sync.ReconcileAsync(s_account);

        Assert.Equal(SyncOutcome.Synced, atLaunch.Outcome);
        Assert.Equal(SyncOutcome.Synced, another.Outcome);
    }

    [Fact]
    public void AConsentToAnEarlierQuestionIsAskedAgainButADeclineNeverIs()
    {
        var at = DateTimeOffset.UtcNow;

        Assert.True(Unanswered().NeedsAnswer);
        Assert.True(new ContributionConsent(ConsentAnswer.Given, ContributionConsent.CurrentVersion - 1, at).NeedsAnswer);
        Assert.False(new ContributionConsent(ConsentAnswer.Given, ContributionConsent.CurrentVersion, at).NeedsAnswer);
        Assert.False(new ContributionConsent(ConsentAnswer.Declined, ContributionConsent.CurrentVersion - 1, at).NeedsAnswer);
    }

    [Fact]
    public async Task WithoutConsentANewPhoneGetsTheWorldWithItsGameDaysClosedAndIsNotAskedForTheKey()
    {
        var store = new FakeCloudSaveStore { Consent = Declined() };
        var old = new Device(store, PlayedSave());
        await old.Sync.ReconcileAsync(s_account);
        var fresh = new Device(store);

        var result = await fresh.Sync.ReconcileAsync(s_account);

        Assert.Equal((SyncOutcome.Synced, true, true), (result.Outcome, result.Downloaded, result.WorldPartOnly));
        Assert.Equal(old.Save.World.GameDay, fresh.Save.World.LastClosedGameDay);
        Assert.Equal(old.Save.ToParts().World, fresh.Save.ToParts().World);
        Assert.Null(store.Current!.Contributions);
        Assert.Equal(2L, store.Current.Version);
    }

    // Without contribution parts there are no journals to merge, so two phones that played apart leave it to the player.
    [Fact]
    public async Task WithoutConsentTwoPhonesThatPlayedApartAskThePlayer()
    {
        var store = new FakeCloudSaveStore { Consent = Declined() };
        var a = new Device(store, PlayedSave());
        await a.Sync.ReconcileAsync(s_account);
        var b = new Device(store);
        await b.Sync.ReconcileAsync(s_account);
        RecordSteps(a.Save, 3_000);
        Advance(b.Save, Hour); // The world part alone closed b's game days so far, so b plays on otherwise.
        Assert.True(b.Save.Apply("b-schedule", new ChangeRestSchedule(new RestSchedule(22 * 60, 8 * 60))).Accepted);

        var fromB = await b.Sync.UploadAsync(s_account);
        var asked = await a.Sync.UploadAsync(s_account);
        var kept = await a.Sync.ReconcileAsync(s_account, WorldChoice.KeepPhone);

        Assert.Equal(SyncOutcome.Synced, fromB.Outcome);
        Assert.Equal((SyncOutcome.ChoiceNeeded, true, false), (asked.Outcome, asked.CloudWithoutContributions, asked.ContributionsUnreadable));
        Assert.Equal(SyncOutcome.Synced, kept.Outcome);
        Assert.Single(a.World.Aside);
        Assert.Equal(a.Save.ToParts().World, store.Current!.World);
    }

    // A key of an epoch the account has since gone past cannot seal the first part after a decline: a new one goes above it.
    [Fact]
    public async Task AConsentAfterADeclineSealsUnderAnEpochNotBelowTheCloudSaves()
    {
        var store = new FakeCloudSaveStore();
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        await phone.Sync.AnswerConsentAsync(s_account, given: false);
        await phone.Sync.ReconcileAsync(s_account);
        var current = store.Current!;
        store.Current = new CloudSave(current.Version, 4, current.World, null, current.SavedAt);

        await phone.Sync.AnswerConsentAsync(s_account, given: true);
        var result = await phone.Sync.ReconcileAsync(s_account);

        Assert.Equal(SyncOutcome.Synced, result.Outcome);
        Assert.Equal(5, phone.Storage.Key(s_account)!.Epoch);
        Assert.Equal(5, store.Current.KeyEpoch);
        Assert.NotNull(ContributionCipher.Open(phone.Storage.Key(s_account)!, s_account, store.Current.Version, store.Current.Contributions!));
    }

    [Fact]
    public async Task AnAnswerThatDoesNotReachTheServerChangesNothing()
    {
        var store = new FakeCloudSaveStore { Consent = Unanswered() };
        var phone = new Device(store, PlayedSave());
        await phone.Sync.ReconcileAsync(s_account);
        store.Online = false;

        var answered = await phone.Sync.AnswerConsentAsync(s_account, given: false);

        Assert.Equal(StoreStatus.Offline, answered);
        Assert.Equal(ConsentAnswer.Unanswered, store.Consent.Answer);
        Assert.True(phone.Sync.Consent(s_account)!.NeedsAnswer);
    }

    private static ContributionConsent Unanswered() => new(ConsentAnswer.Unanswered, 0, null);

    private static ContributionConsent Declined() => new(ConsentAnswer.Declined, ContributionConsent.CurrentVersion, DateTimeOffset.UtcNow);

    private static void RecordSteps(Save save, int steps)
    {
        Advance(save, Hour);
        Assert.True(save.Apply("steps-" + steps, new RecordSelfCare(save.World.GameDay, Habit.Steps, ContributionSource.Verified, steps)).Accepted);
    }

    private static void Advance(Save save, long seconds)
    {
        var now = save.World.Now;
        save.AdvanceTo(new TimeReading(now.UtcSeconds + seconds, now.ZoneId, now.OffsetSeconds));
    }

    private static Save PlayedSave()
    {
        var save = Save.Begin(s_install, new RestSchedule(23 * 60, 8 * 60));
        Advance(save, 2 * Hour);
        save.Apply("steps", new RecordSelfCare(save.World.GameDay, Habit.Steps, ContributionSource.Verified, 6_000));
        Advance(save, Hour);
        return save;
    }
}
