using System.Net;
using Eirora.Contracts;
using Eirora.Server.SignIn;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.EntityFrameworkCore.Models;

namespace Eirora.Server.Tests;

/// <summary>Deleting the account leaves nothing of it on the server and revokes the app at Apple (ACC-05, 5.1.1(v)).</summary>
public sealed class DeletionTests(ServerFixture fixture)
{
    private readonly ServerFactory _server = fixture.Server;

    [Fact]
    public async Task ADeletedAccountLeavesNoAccountDataOnTheServer()
    {
        using var api = _server.CreateApi();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(google);
        var refreshed = await api.RefreshAsync(tokens.Refresh);
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;

        var status = await api.DeleteAccountAsync(refreshed.Access, _server.Providers.SignIn(LoginProviders.Google, google.Subject).ToExternalLogin());

        Assert.Equal(HttpStatusCode.NoContent, status);
        await using var db = fixture.CreateDbContext();
        var subject = accountId.ToString();
        Assert.False(await db.Accounts.AnyAsync(a => a.Id == accountId, TestContext.Current.CancellationToken));
        Assert.False(await db.AccountLogins.AnyAsync(l => l.AccountId == accountId || l.Subject == google.Subject, TestContext.Current.CancellationToken));
        Assert.False(await db.Set<OpenIddictEntityFrameworkCoreToken<Guid>>().AnyAsync(t => t.Subject == subject, TestContext.Current.CancellationToken));
        Assert.False(await db.Set<OpenIddictEntityFrameworkCoreAuthorization<Guid>>().AnyAsync(a => a.Subject == subject, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheDeletedAccountsTokensStopWorkingAtOnce()
    {
        using var api = _server.CreateApi();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(google);

        await api.DeleteAccountAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Google, google.Subject).ToExternalLogin());

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAccountAsync(tokens.Access)).Status);
        Assert.Equal("invalid_grant", (await api.RefreshAsync(tokens.Refresh)).Error);
    }

    [Fact]
    public async Task SigningInAfterDeletionStartsANewAccount()
    {
        using var api = _server.CreateApi();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(google);
        var deleted = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        await api.DeleteAccountAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Google, google.Subject).ToExternalLogin());

        var again = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject));

        Assert.True(again.AccountCreated);
        Assert.NotEqual(deleted, (await api.GetAccountAsync(again.Access)).Account!.Id);
    }

    [Fact]
    public async Task DeletionIsConfirmedWithALoginOfThisAccount()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var someoneElse = _server.Providers.SignIn(LoginProviders.Google);
        await api.SignInAsync(someoneElse);

        var status = await api.DeleteAccountAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Google, someoneElse.Subject).ToExternalLogin());

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAccountAsync(tokens.Access)).Status);
    }

    [Fact]
    public async Task AnAccountWithAnAppleLoginIsConfirmedWithAppleAndItsAuthorizationCode()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var google = _server.Providers.SignIn(LoginProviders.Google);
        await api.LinkAsync(tokens.Access, google.ToExternalLogin(), _server.Providers.SignIn(LoginProviders.Apple, apple.Subject).ToExternalLogin());

        var withGoogle = await api.DeleteAccountAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Google, google.Subject).ToExternalLogin());
        var withoutCode = await api.DeleteAccountAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Apple, apple.Subject).ToExternalLogin());

        Assert.Equal(HttpStatusCode.BadRequest, withGoogle);
        Assert.Equal(HttpStatusCode.BadRequest, withoutCode);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAccountAsync(tokens.Access)).Status);
    }

    [Fact]
    public async Task DeletingAnAccountWithAnAppleLoginRevokesTheAppAtApple()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var code = "apple-code-" + Guid.NewGuid().ToString("N");

        var status = await api.DeleteAccountAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Apple, apple.Subject).ToExternalLogin(code));

        Assert.Equal(HttpStatusCode.NoContent, status);
        var exchange = Assert.Single(_server.Apple.Requests, r => r.Path == "/auth/token" && r.Form["code"] == code).Form;
        Assert.Equal(TestProviders.AppleAudience, exchange["client_id"]);
        Assert.Equal("authorization_code", exchange["grant_type"]);
        await AssertAppleClientSecretAsync(exchange["client_secret"]);
        Assert.Contains(_server.Apple.Requests, r => r.Path == "/auth/revoke"
            && r.Form["token"] == AppleStub.RefreshToken
            && r.Form["token_type_hint"] == "refresh_token"
            && r.Form["client_secret"] == exchange["client_secret"]);
    }

    [Theory]
    [InlineData(AppleStub.UnreachablePrefix)]
    [InlineData(AppleStub.HangingPrefix)]
    [InlineData(AppleStub.GarbledPrefix)]
    public async Task TheAccountIsDeletedEvenWhenAppleFails(string failure)
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var accountId = (await api.GetAccountAsync(tokens.Access)).Account!.Id;
        var code = failure + Guid.NewGuid().ToString("N");

        var status = await api.DeleteAccountAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Apple, apple.Subject).ToExternalLogin(code));

        Assert.Equal(HttpStatusCode.NoContent, status);
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Accounts.AnyAsync(a => a.Id == accountId, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(code, string.Join('\n', _server.Logs), StringComparison.Ordinal);
    }

    private async Task AssertAppleClientSecretAsync(string clientSecret)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(clientSecret, new TokenValidationParameters
        {
            IssuerSigningKey = new ECDsaSecurityKey(_server.Apple.PublicKey),
            ValidIssuer = AppleStub.TeamId,
            ValidAudience = AppleIdClient.Audience,
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        var token = (JsonWebToken)result.SecurityToken;
        Assert.Equal("ES256", token.Alg);
        Assert.Equal(AppleStub.KeyId, token.Kid);
        Assert.Equal(TestProviders.AppleAudience, token.Subject);
    }
}
