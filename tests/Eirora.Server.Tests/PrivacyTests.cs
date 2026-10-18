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
        var tokens = await api.SignInAsync(apple);
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id.ToString();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var linkConfirmation = _server.Providers.SignIn(LoginProviders.Apple, apple.Subject);
        await api.LinkAsync(tokens.Access, google.ToExternalLogin(), linkConfirmation.ToExternalLogin());
        var removalConfirmation = _server.Providers.SignIn(LoginProviders.Google, google.Subject);
        await api.UnlinkAsync(tokens.Access, LoginProviders.Google, removalConfirmation.ToExternalLogin());
        await api.SignInAsync(apple);
        var confirmation = _server.Providers.SignIn(LoginProviders.Apple, apple.Subject);
        var code = AppleStub.UnreachablePrefix + Guid.NewGuid().ToString("N");
        await api.DeleteAccountAsync(tokens.Access, confirmation.ToExternalLogin(code));

        string[] secrets =
        [
            apple.Subject, apple.Nonce, apple.IdToken, google.Subject, google.Nonce, google.IdToken,
            linkConfirmation.IdToken, removalConfirmation.IdToken, confirmation.IdToken, code,
            tokens.Access, tokens.Refresh, accountId,
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
