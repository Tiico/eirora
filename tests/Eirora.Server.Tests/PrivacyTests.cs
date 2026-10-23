using Eirora.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Eirora.Server.Tests;

/// <summary>What the server writes down about a player: nothing in the logs (NFR-10), and a schema that matches the code.</summary>
public sealed class PrivacyTests(ServerFixture fixture)
{
    private readonly ServerFactory _server = fixture.Server;

    [Fact]
    public async Task LogsNeverContainTokensNoncesOrIdentifiers()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInWithConsentAsync(apple);
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id.ToString();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var linkConfirmation = _server.Providers.Again(apple);
        await api.LinkAsync(tokens.Access, google.ToExternalLogin(), linkConfirmation);
        var removalConfirmation = _server.Providers.Again(google);
        await api.RemoveLoginAsync(tokens.Access, LoginProviders.Google, removalConfirmation);
        await api.SignInAsync(apple);
        var upload = TestSaves.Upload(baseVersion: 0, keyEpoch: 1);
        await api.UploadCloudSaveAsync(tokens.Access, upload);
        await api.UploadCloudSaveAsync(tokens.Access, TestSaves.Upload(baseVersion: 1, keyEpoch: 2));
        await api.AnswerConsentAsync(tokens.Access, new ContributionConsentAnswer(false, ContributionConsent.CurrentVersion));
        var code = AppleStub.UnreachablePrefix + Guid.NewGuid().ToString("N");
        var confirmation = _server.Providers.Again(apple, code);
        await api.DeleteAccountAsync(tokens.Access, confirmation);

        string[] secrets =
        [
            apple.Subject, apple.Nonce, apple.IdToken, google.Subject, google.Nonce, google.IdToken,
            linkConfirmation.IdToken, removalConfirmation.IdToken, confirmation.IdToken, code,
            tokens.Access, tokens.Refresh, accountId,
            Convert.ToBase64String(upload.Contributions!)[..32], Convert.ToHexString(upload.Contributions!)[..32],
        ];
        var logs = string.Join('\n', _server.Logs);
        Assert.All(secrets, secret => Assert.DoesNotContain(secret, logs, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheMigrationsMatchTheModel()
    {
        await using var db = fixture.CreateDbContext();

        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }
}
