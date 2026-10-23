using System.Net;
using System.Text;
using Eirora.Contracts;
using Eirora.Server.CloudSaves;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Eirora.Server.Tests;

/// <summary>
/// The account's cloud save (ADR-0008, TASK-023): stored as uploaded, versioned so that two devices merge instead of
/// overwriting each other (D-99), checked for format and size but never read, and reached only by its own account. The
/// players here have consented to the contribution part; <see cref="ContributionConsentTests"/> is about the consent.
/// </summary>
public sealed class CloudSaveTests(ServerFixture fixture)
{
    private readonly ServerFactory _server = fixture.Server;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnAccountWithoutAnUploadHasNoCloudSave()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));

        var (status, save) = await api.GetCloudSaveAsync(tokens.Access);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Null(save);
    }

    [Fact]
    public async Task TheFirstUploadBecomesVersionOneAndDownloadsAsItWasUploaded()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var upload = TestSaves.Upload(baseVersion: 0);
        var before = DateTimeOffset.UtcNow;

        var (status, version) = await api.UploadCloudSaveAsync(tokens.Access, upload);
        var (_, save) = await api.GetCloudSaveAsync(tokens.Access);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal((1, 1), (version!.Version, version.KeyEpoch));
        Assert.Equal((1, 1), (save!.Version, save.KeyEpoch));
        Assert.Equal(upload.World, save.World);
        Assert.Equal(upload.Contributions, save.Contributions);
        Assert.InRange(save.SavedAt, before.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task EachUploadBasedOnTheCurrentVersionReplacesItWithTheNext()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 0));
        var second = TestSaves.Upload(baseVersion: 1);

        var (status, version) = await api.UploadCloudSaveAsync(tokens.Access, second);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2, version!.Version);
        Assert.Equal(second.Contributions, (await api.GetCloudSaveAsync(tokens.Access)).Save!.Contributions);
    }

    // Another device uploaded since this one's base, so this one merges the journals instead of overwriting (D-99).
    [Theory]
    [InlineData(0)] // A new phone's first upload, while the account already has a cloud save.
    [InlineData(1)]
    [InlineData(3)] // A version the server never had.
    public async Task AnUploadBasedOnAnotherVersionConflictsAndSaysWhichIsCurrent(long baseVersion)
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 0));
        var current = TestSaves.Upload(baseVersion: 1);
        await api.UploadCloudSaveAsync(tokens.Access, current);

        var (status, version) = await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion));

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal((2, 1), (version!.Version, version.KeyEpoch));
        Assert.Equal(current.Contributions, (await api.GetCloudSaveAsync(tokens.Access)).Save!.Contributions);
    }

    [Fact]
    public async Task OfUploadsAtOnceFromTheSameVersionOnlyOneIsStored()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var uploads = Enumerable.Range(0, 6).Select(_ => TestSaves.Upload(baseVersion: 0)).ToList();

        var results = await Task.WhenAll(uploads.Select(u => api.UploadCloudSaveAsync(tokens.Access, u)));

        var stored = Assert.Single(results, r => r.Status == HttpStatusCode.OK);
        Assert.All(results.Where(r => r.Status != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.Status));
        Assert.Equal(1, stored.Version!.Version);
        var save = (await api.GetCloudSaveAsync(tokens.Access)).Save!;
        Assert.Equal(1, save.Version);
        Assert.Single(uploads, u => u.Contributions!.SequenceEqual(save.Contributions));
    }

    [Fact]
    public async Task AnotherPlayersTokenOnlyEverReachesThatPlayersOwnCloudSave()
    {
        using var api = _server.CreateApi();
        var mine = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Apple));
        var theirs = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Apple));
        var myUpload = TestSaves.Upload(baseVersion: 0);
        await api.UploadCloudSaveAsync(mine.Access, myUpload);

        var (reachedStatus, _) = await api.GetCloudSaveAsync(theirs.Access);
        var (uploadStatus, _) = await api.UploadCloudSaveAsync(theirs.Access, TestSaves.Upload(baseVersion: 1));

        Assert.Equal(HttpStatusCode.NotFound, reachedStatus);
        Assert.Equal(HttpStatusCode.Conflict, uploadStatus);
        var mySave = (await api.GetCloudSaveAsync(mine.Access)).Save!;
        Assert.Equal(1, mySave.Version);
        Assert.Equal(myUpload.Contributions, mySave.Contributions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task WithoutAValidTokenTheCloudSaveIsNotReached(string accessToken)
    {
        using var api = _server.CreateApi();

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetCloudSaveAsync(accessToken)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.UploadCloudSaveAsync(accessToken, TestSaves.Upload(0))).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetLockedContributionsAsync(accessToken)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, await api.DeleteLockedContributionsAsync(accessToken, 1));
    }

    [Fact]
    public async Task TheContributionPartIsStoredAsTheBytesThatWereUploaded()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        var upload = TestSaves.Upload(baseVersion: 0);

        await api.UploadCloudSaveAsync(tokens.Access, upload);

        await using var db = fixture.CreateDbContext();
        var row = await db.CloudSaves.SingleAsync(s => s.AccountId == accountId, Cancel);
        Assert.Equal(upload.Contributions, row.Contributions);
        Assert.Equal(upload.World, row.World);
    }

    public static TheoryData<string> MalformedUploads => new()
    {
        "no world part",
        "no contribution part",
        "empty world part",
        "empty contribution part",
        "negative base version",
        "key epoch 0",
        "not a save",
        "damaged world part",
        "contribution part as the world part",
        "whole save as the world part",
        "world part with an edited snapshot",
        "world part of a newer format",
        "map too large for its text",
    };

    [Theory]
    [MemberData(nameof(MalformedUploads))]
    public async Task AnUploadWhoseWorldPartDoesNotLoadIsRejectedAndNothingIsStored(string malformation)
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));

        var (status, _) = await api.UploadCloudSaveAsync(tokens.Access, Malformed(malformation));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetCloudSaveAsync(tokens.Access)).Status);
    }

    [Fact]
    public async Task AResealedButUnchangedWorldPartIsAccepted()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var world = TestSaves.World();
        var resealed = TestSaves.Resealed(world, (_, text) => text);

        var (status, _) = await api.UploadCloudSaveAsync(tokens.Access, new CloudSaveUpload(0, 1, resealed, TestSaves.Contributions()));

        Assert.Equal(world, resealed);
        Assert.Equal(HttpStatusCode.OK, status);
    }

    // A player can craft any world part and seal it with a matching hash; the server must only ever say no.
    [Fact]
    public async Task ACraftedWorldPartIsRejectedOrAcceptedButNeverBreaksTheServer()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var world = TestSaves.World();
        var random = new Random(38);
        long version = 0;

        for (int i = 0; i < 150; i++)
        {
            var crafted = TestSaves.Resealed(world, (_, text) => Mutated(text, random));
            var (status, current) = await api.UploadCloudSaveAsync(tokens.Access, new CloudSaveUpload(version, 1, crafted, TestSaves.Contributions(64)));

            Assert.True(status is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"Mutation {i} answered {status}.");
            version = current?.Version ?? version;
        }
    }

    [Theory]
    [InlineData(CloudSaveUpload.MaxWorldBytes + 1, 64)]
    [InlineData(1024, CloudSaveUpload.MaxContributionBytes + 1)]
    public async Task AnUploadOverItsLimitsIsRejectedAsTooLarge(int worldBytes, int contributionBytes)
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));

        var (status, _) = await api.UploadCloudSaveAsync(tokens.Access, new CloudSaveUpload(0, 1, new byte[worldBytes], new byte[contributionBytes]));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, status);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetCloudSaveAsync(tokens.Access)).Status);
    }

    [Fact]
    public async Task AContributionPartAtItsLimitIsStored()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var upload = TestSaves.Upload(baseVersion: 0, contributions: TestSaves.Contributions(CloudSaveUpload.MaxContributionBytes));

        var (status, _) = await api.UploadCloudSaveAsync(tokens.Access, upload);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(upload.Contributions, (await api.GetCloudSaveAsync(tokens.Access)).Save!.Contributions);
    }

    // A new phone without the key begins a new epoch; the old part stays in case the key turns up (ADR-0008).
    [Fact]
    public async Task ANewKeyEpochLocksTheContributionPartItReplaces()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var first = TestSaves.Upload(baseVersion: 0, keyEpoch: 1);
        await api.UploadCloudSaveAsync(tokens.Access, first);
        var newEpoch = TestSaves.Upload(baseVersion: 1, keyEpoch: 2);

        var (status, version) = await api.UploadCloudSaveAsync(tokens.Access, newEpoch);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal((2, 2), (version!.Version, version.KeyEpoch));
        Assert.Equal(newEpoch.Contributions, (await api.GetCloudSaveAsync(tokens.Access)).Save!.Contributions);
        var locked = Assert.Single((await api.GetLockedContributionsAsync(tokens.Access)).Locked!);
        Assert.Equal((1, 1L), (locked.KeyEpoch, locked.Version));
        Assert.Equal(first.Contributions, locked.Contributions);
        Assert.Equal(locked.LockedAt.AddDays(LockedContributions.RetentionDays), locked.DeletedAt);
    }

    [Fact]
    public async Task AnUploadOfAnOlderKeyEpochConflicts()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 0, keyEpoch: 1));
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 1, keyEpoch: 2));

        var (status, version) = await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 2, keyEpoch: 1));

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal((2, 2), (version!.Version, version.KeyEpoch));
    }

    [Fact]
    public async Task TheSameKeyEpochLocksNothing()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 0, keyEpoch: 3));

        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 1, keyEpoch: 3));

        Assert.Empty((await api.GetLockedContributionsAsync(tokens.Access)).Locked!);
    }

    [Fact]
    public async Task APlayerCanDeleteALockedContributionPartEarly()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var someoneElse = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 0, keyEpoch: 1));
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 1, keyEpoch: 2));

        var byAnother = await api.DeleteLockedContributionsAsync(someoneElse.Access, 1);
        var deleted = await api.DeleteLockedContributionsAsync(tokens.Access, 1);
        var again = await api.DeleteLockedContributionsAsync(tokens.Access, 1);

        Assert.Equal(HttpStatusCode.NotFound, byAnother);
        Assert.Equal(HttpStatusCode.NoContent, deleted);
        Assert.Equal(HttpStatusCode.NotFound, again);
        Assert.Empty((await api.GetLockedContributionsAsync(tokens.Access)).Locked!);
        Assert.Equal(2, (await api.GetCloudSaveAsync(tokens.Access)).Save!.Version);
    }

    [Fact]
    public async Task ALockedContributionPartIsDeletedAfterNinetyDays()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInWithConsentAsync(_server.Providers.SignIn(LoginProviders.Google));
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        var now = DateTimeOffset.UtcNow;
        await using (var db = fixture.CreateDbContext())
        {
            db.LockedContributions.AddRange(
                new StoredLockedContributions { AccountId = accountId, KeyEpoch = 1, Version = 4, Contributions = [1], LockedAt = now.AddDays(-90.01) },
                new StoredLockedContributions { AccountId = accountId, KeyEpoch = 2, Version = 9, Contributions = [2], LockedAt = now.AddDays(-89.99) });
            await db.SaveChangesAsync(Cancel);
        }

        await using (var scope = _server.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<CloudSaveService>().PruneLockedAsync(Cancel);
        }

        var kept = Assert.Single((await api.GetLockedContributionsAsync(tokens.Access)).Locked!);
        Assert.Equal(2, kept.KeyEpoch);
    }

    private static object Malformed(string malformation)
    {
        var save = TestSaves.NewSave();
        var (world, contributions) = save.ToParts();
        var encrypted = TestSaves.Contributions();
        return malformation switch
        {
            "no world part" => new { baseVersion = 0, keyEpoch = 1, contributions = encrypted },
            "no contribution part" => new { baseVersion = 0, keyEpoch = 1, world },
            "empty world part" => new CloudSaveUpload(0, 1, [], encrypted),
            "empty contribution part" => new CloudSaveUpload(0, 1, world, []),
            "negative base version" => new CloudSaveUpload(-1, 1, world, encrypted),
            "key epoch 0" => new CloudSaveUpload(0, 0, world, encrypted),
            "not a save" => new CloudSaveUpload(0, 1, Encoding.UTF8.GetBytes("hello"), encrypted),
            "damaged world part" => new CloudSaveUpload(0, 1, [.. world[..^1], (byte)(world[^1] ^ 1)], encrypted),
            "contribution part as the world part" => new CloudSaveUpload(0, 1, contributions, encrypted),
            "whole save as the world part" => new CloudSaveUpload(0, 1, save.ToBytes(), encrypted),
            "world part with an edited snapshot" => new CloudSaveUpload(
                0,
                1,
                TestSaves.Resealed(world, (name, text) => name == "latest" ? text + "unreconciled 3 Steps\n" : text),
                encrypted),
            "world part of a newer format" => new CloudSaveUpload(
                0,
                1,
                TestSaves.Resealed(world, (_, text) => text, format: 99),
                encrypted),
            "map too large for its text" => new CloudSaveUpload(
                0,
                1,
                TestSaves.Resealed(world, (name, text) => name == "map" ? text.Replace("\nradius 24\n", "\nradius 30000\n", StringComparison.Ordinal) : text),
                encrypted),
            _ => throw new ArgumentOutOfRangeException(nameof(malformation), malformation, null),
        };
    }

    /// <summary>The text with a few characters replaced, removed or repeated, mostly digits and spaces that parsers read.</summary>
    private static string Mutated(string text, Random random)
    {
        const string Replacements = "0123456789 -\nxX";
        var mutated = new StringBuilder(text);
        for (int edits = random.Next(1, 4); edits > 0 && mutated.Length > 0; edits--)
        {
            int at = random.Next(mutated.Length);
            switch (random.Next(3))
            {
                case 0:
                    mutated[at] = Replacements[random.Next(Replacements.Length)];
                    break;
                case 1:
                    mutated.Remove(at, 1);
                    break;
                default:
                    mutated.Insert(at, Replacements[random.Next(Replacements.Length)]);
                    break;
            }
        }

        return mutated.ToString();
    }
}
