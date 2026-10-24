using System.Net;
using Eirora.Contracts;
using Eirora.Server.CloudSaves;
using Eirora.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eirora.Server.Tests;

/// <summary>
/// The consent to storing the contribution part (TASK-060, GDPR Article 9(2)(a)): the server takes no upload before the
/// player has answered, a contribution part only with consent, keeps every answer with its version and time, and
/// deletes the contribution part and the locked ones when consent is declined or withdrawn.
/// </summary>
public sealed class ContributionConsentTests(ServerFixture fixture)
{
    private readonly ServerFactory _server = fixture.Server;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static ContributionConsentAnswer Yes => new(true, ContributionConsent.CurrentVersion);

    private static ContributionConsentAnswer No => new(false, ContributionConsent.CurrentVersion);

    [Fact]
    public async Task ANewAccountHasNotAnsweredAndTakesNoUploadUntilItHas()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));

        var (status, consent) = await api.GetConsentAsync(tokens.Access);
        var withPart = await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 0));
        var worldOnly = await api.UploadCloudSaveAsync(tokens.Access, WorldOnly(baseVersion: 0));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal((ConsentAnswer.Unanswered, 0, (DateTimeOffset?)null), (consent!.Answer, consent.Version, consent.AnsweredAt));
        Assert.Equal((HttpStatusCode.Conflict, 0L), (withPart.Status, withPart.Version!.Version));
        Assert.Equal(HttpStatusCode.Conflict, worldOnly.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetCloudSaveAsync(tokens.Access)).Status);
    }

    // Acceptance criterion 1.
    [Fact]
    public async Task WithoutConsentOnlyTheWorldPartIsStored()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        await api.AnswerConsentAsync(tokens.Access, No);
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        var upload = WorldOnly(baseVersion: 0);

        var stored = await api.UploadCloudSaveAsync(tokens.Access, upload);
        var withPart = await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 1));

        Assert.Equal((HttpStatusCode.OK, 1L, 0), (stored.Status, stored.Version!.Version, stored.Version.KeyEpoch));
        Assert.Equal((HttpStatusCode.Conflict, 1L), (withPart.Status, withPart.Version!.Version));
        var save = (await api.GetCloudSaveAsync(tokens.Access)).Save!;
        Assert.Equal((1L, 0), (save.Version, save.KeyEpoch));
        Assert.Equal(upload.World, save.World);
        Assert.Null(save.Contributions);
        await using var db = fixture.CreateDbContext();
        Assert.Null((await db.CloudSaves.SingleAsync(s => s.AccountId == accountId, Cancel)).Contributions);
    }

    // Acceptance criterion 2.
    [Fact]
    public async Task AConsentIsKeptWithItsVersionAndTimeAndTheNextUploadStoresThePart()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        await api.AnswerConsentAsync(tokens.Access, No);
        await api.UploadCloudSaveAsync(tokens.Access, WorldOnly(baseVersion: 0));
        var before = DateTimeOffset.UtcNow;

        var (answered, consent) = await api.AnswerConsentAsync(tokens.Access, Yes);
        var upload = TestSaves.Upload(baseVersion: 1, keyEpoch: 1);
        var (status, version) = await api.UploadCloudSaveAsync(tokens.Access, upload);

        Assert.Equal(HttpStatusCode.OK, answered);
        Assert.Equal((ConsentAnswer.Given, ContributionConsent.CurrentVersion), (consent!.Answer, consent.Version));
        Assert.InRange(consent.AnsweredAt!.Value, before.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal(consent.AnsweredAt, (await api.GetConsentAsync(tokens.Access)).Consent!.AnsweredAt);
        Assert.Equal((HttpStatusCode.OK, 2L, 1), (status, version!.Version, version.KeyEpoch));
        Assert.Equal(upload.Contributions, (await api.GetCloudSaveAsync(tokens.Access)).Save!.Contributions);
    }

    // Linux clocks tick in 100 ns, PostgreSQL keeps microseconds: the answer must say the time the account keeps, so that
    // what the player is shown agreeing to is what the account can show later.
    [Fact]
    public async Task TheAnswerSaysTheTimeTheAccountKeepsToTheMicrosecond()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        var clock = new FixedTime(new DateTimeOffset(2026, 10, 23, 13, 57, 41, TimeSpan.Zero).AddTicks(2_389_917));
        await using var scope = _server.Services.CreateAsyncScope();
        var saves = new CloudSaveService(scope.ServiceProvider.GetRequiredService<EiroraDbContext>(), clock, NullLogger<CloudSaveService>.Instance);

        var answered = await saves.AnswerConsentAsync(accountId, Yes, Cancel);
        var kept = await saves.GetConsentAsync(accountId, Cancel);

        Assert.Equal(clock.GetUtcNow().AddTicks(-7), answered!.AnsweredAt);
        Assert.Equal(answered.AnsweredAt, kept.AnsweredAt);
        Assert.Equal(answered.AnsweredAt, (await api.GetConsentAsync(tokens.Access)).Consent!.AnsweredAt);
    }

    // Acceptance criterion 3.
    [Fact]
    public async Task AWithdrawalDeletesTheContributionPartAndTheLockedOnesAndNoneIsStoredAfterIt()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 0, keyEpoch: 1));
        var world = TestSaves.Upload(baseVersion: 1, keyEpoch: 2);
        await api.UploadCloudSaveAsync(tokens.Access, world);

        var (withdrawn, consent) = await api.AnswerConsentAsync(tokens.Access, No);
        var withPart = await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 2, keyEpoch: 2));

        Assert.Equal((HttpStatusCode.OK, ConsentAnswer.Declined), (withdrawn, consent!.Answer));
        Assert.Equal(HttpStatusCode.Conflict, withPart.Status);
        var save = (await api.GetCloudSaveAsync(tokens.Access)).Save!;
        Assert.Null(save.Contributions);
        Assert.Equal((2L, 2), (save.Version, save.KeyEpoch));
        Assert.Equal(world.World, save.World);
        Assert.Empty((await api.GetLockedContributionsAsync(tokens.Access)).Locked!);
        await using var db = fixture.CreateDbContext();
        Assert.Null((await db.CloudSaves.SingleAsync(s => s.AccountId == accountId, Cancel)).Contributions);
        Assert.False(await db.LockedContributions.AnyAsync(l => l.AccountId == accountId, Cancel));
    }

    // GDPR Article 7(1): the account shows what the player agreed to and when, also after a withdrawal.
    [Fact]
    public async Task EveryAnswerIsKeptInOrder()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;

        await api.AnswerConsentAsync(tokens.Access, No);
        await api.AnswerConsentAsync(tokens.Access, Yes);
        await api.AnswerConsentAsync(tokens.Access, No);

        await using var db = fixture.CreateDbContext();
        var answers = await db.ContributionConsents.Where(c => c.AccountId == accountId).OrderBy(c => c.Id).ToListAsync(Cancel);
        Assert.Equal([false, true, false], answers.Select(a => a.Given));
        Assert.All(answers, a => Assert.Equal(ContributionConsent.CurrentVersion, a.Version));
        Assert.True(answers.Zip(answers.Skip(1)).All(p => p.First.AnsweredAt <= p.Second.AnsweredAt));
        Assert.Equal(ConsentAnswer.Declined, (await api.GetConsentAsync(tokens.Access)).Consent!.Answer);
    }

    // The issue's migration: an account from before the question keeps its part until the player answers.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AContributionPartFromBeforeTheQuestionStaysUntilTheAnswerAndGoesWithADecline(bool given)
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        byte[] part = TestSaves.Contributions();
        await using (var db = fixture.CreateDbContext())
        {
            db.CloudSaves.Add(new StoredCloudSave { AccountId = accountId, Version = 3, KeyEpoch = 1, World = TestSaves.World(), Contributions = part, SavedAt = DateTimeOffset.UtcNow });
            db.LockedContributions.Add(new StoredLockedContributions { AccountId = accountId, KeyEpoch = 0, Version = 1, Contributions = [1], LockedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(Cancel);
        }

        var before = (await api.GetCloudSaveAsync(tokens.Access)).Save!;
        var upload = await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 3));
        await api.AnswerConsentAsync(tokens.Access, given ? Yes : No);
        var after = (await api.GetCloudSaveAsync(tokens.Access)).Save!;

        Assert.Equal(part, before.Contributions);
        Assert.Equal(HttpStatusCode.Conflict, upload.Status);
        Assert.Equal(given ? part : null, after.Contributions);
        Assert.Equal(given ? 1 : 0, (await api.GetLockedContributionsAsync(tokens.Access)).Locked!.Length);
    }

    [Fact]
    public async Task AWorldPartUploadWhileConsentIsGivenConflictsAndKeepsThePart()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var upload = TestSaves.Upload(baseVersion: 0);
        await api.UploadCloudSaveAsync(tokens.Access, upload);

        var (status, version) = await api.UploadCloudSaveAsync(tokens.Access, WorldOnly(baseVersion: 1));

        Assert.Equal((HttpStatusCode.Conflict, 1L), (status, version!.Version));
        Assert.Equal(upload.Contributions, (await api.GetCloudSaveAsync(tokens.Access)).Save!.Contributions);
    }

    // The epoch never goes down: a world part alone keeps it, and a part after a new consent cannot be of an older one.
    [Fact]
    public async Task AWorldPartAloneKeepsTheKeyEpochThatAPartAfterItMustNotGoBelow()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 0, keyEpoch: 3));
        await api.AnswerConsentAsync(tokens.Access, No);
        var alone = await api.UploadCloudSaveAsync(tokens.Access, WorldOnly(baseVersion: 1));
        await api.AnswerConsentAsync(tokens.Access, Yes);

        var older = await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 2, keyEpoch: 2));
        var newer = await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 2, keyEpoch: 4));

        Assert.Equal((HttpStatusCode.OK, 3), (alone.Status, alone.Version!.KeyEpoch));
        Assert.Equal(HttpStatusCode.Conflict, older.Status);
        Assert.Equal((HttpStatusCode.OK, 3L, 4), (newer.Status, newer.Version!.Version, newer.Version.KeyEpoch));
        Assert.Empty((await api.GetLockedContributionsAsync(tokens.Access)).Locked!); // There was no part to lock.
    }

    // The account's row is locked for both, so a part begun before a withdrawal is either stored and then deleted, or
    // refused: never stored after it.
    [Fact]
    public async Task UploadsAtTheSameTimeAsAWithdrawalNeverLeaveAPart()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 0));

        var uploads = Enumerable.Range(0, 6).Select(_ => api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 1))).ToList();
        var withdrawal = api.AnswerConsentAsync(tokens.Access, No);
        await Task.WhenAll([.. uploads, withdrawal]);

        Assert.Equal(HttpStatusCode.OK, (await withdrawal).Status);
        Assert.Null((await api.GetCloudSaveAsync(tokens.Access)).Save!.Contributions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ContributionConsent.CurrentVersion + 1)]
    public async Task AnAnswerToAQuestionTheServerDoesNotKnowIsRejected(int version)
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));

        var (status, _) = await api.AnswerConsentAsync(tokens.Access, new ContributionConsentAnswer(true, version));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ConsentAnswer.Unanswered, (await api.GetConsentAsync(tokens.Access)).Consent!.Answer);
    }

    [Fact]
    public async Task AnotherPlayersAnswerNeverReachesThisAccount()
    {
        using var api = _server.CreateApi();
        var mine = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var theirs = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var upload = TestSaves.Upload(baseVersion: 0);
        await api.UploadCloudSaveAsync(mine.Access, upload);

        await api.AnswerConsentAsync(theirs.Access, No);

        Assert.Equal(ConsentAnswer.Given, (await api.GetConsentAsync(mine.Access)).Consent!.Answer);
        Assert.Equal(upload.Contributions, (await api.GetCloudSaveAsync(mine.Access)).Save!.Contributions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task WithoutAValidTokenTheConsentIsNotReached(string accessToken)
    {
        using var api = _server.CreateApi();

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetConsentAsync(accessToken)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.AnswerConsentAsync(accessToken, Yes)).Status);
    }

    private static CloudSaveUpload WorldOnly(long baseVersion) => new(baseVersion, 0, TestSaves.World(), null);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
