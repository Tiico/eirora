using System.Net;
using Eirora.Contracts;

namespace Eirora.Server.Tests;

/// <summary>The signed-in player's own account: what it holds, its logins, and that no one else reaches it (ACC-01).</summary>
public sealed class AccountTests(ServerFixture fixture)
{
    private readonly ServerFactory _server = fixture.Server;

    [Fact]
    public async Task AnotherPlayersTokenOnlyEverReachesThatPlayersOwnAccount()
    {
        using var api = _server.CreateApi();
        var mine = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));
        var theirs = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));
        var myAccount = (await api.GetAccountAsync(mine.Access)).Account!;

        var (_, reached) = await api.GetAccountAsync(theirs.Access);

        Assert.NotEqual(myAccount.Id, reached!.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0.")]
    public async Task WithoutAValidTokenTheAccountIsNotReached(string accessToken)
    {
        using var api = _server.CreateApi();

        var (status, account) = await api.GetAccountAsync(accessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Null(account);
    }

    [Fact]
    public async Task TheAccountHoldsNoNameEmailOrProviderIdentifier()
    {
        using var api = _server.CreateApi();
        var login = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(login);

        var (_, account) = await api.GetAccountAsync(tokens.Access);

        Assert.Equal(LoginProviders.Google, Assert.Single(account!.Logins).Provider);
        Assert.DoesNotContain(login.Subject, System.Text.Json.JsonSerializer.Serialize(account));
    }

    [Fact]
    public async Task ASecondLinkedLoginReachesTheSameAccount()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));
        var account = (await api.GetAccountAsync(tokens.Access)).Account!;
        var google = _server.Providers.SignIn(LoginProviders.Google);

        Assert.Equal(HttpStatusCode.NoContent, await api.LinkAsync(tokens.Access, google.ToExternalLogin()));
        var viaGoogle = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject));

        Assert.False(viaGoogle.AccountCreated);
        Assert.Equal(account.Id, (await api.GetAccountAsync(viaGoogle.Access)).Account!.Id);
        Assert.Equal(
            [LoginProviders.Apple, LoginProviders.Google],
            (await api.GetAccountAsync(tokens.Access)).Account!.Logins.Select(l => l.Provider));
    }

    [Fact]
    public async Task ALoginOfAnotherAccountCannotBeLinked()
    {
        using var api = _server.CreateApi();
        var other = _server.Providers.SignIn(LoginProviders.Google);
        await api.SignInAsync(other);
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));

        var status = await api.LinkAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Google, other.Subject).ToExternalLogin());

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task AnAccountHasAtMostOneLoginPerProvider()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));

        var status = await api.LinkAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Apple).ToExternalLogin());

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task LinkingRequiresAValidSignIn()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));
        var google = _server.Providers.SignIn(LoginProviders.Google);

        var status = await api.LinkAsync(tokens.Access, new ExternalLogin(google.Provider, google.IdToken, "fedcba9876543210fedcba9876543210"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task TheLastLoginCannotBeRemoved()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));

        Assert.Equal(HttpStatusCode.Conflict, await api.UnlinkAsync(tokens.Access, LoginProviders.Apple));
        Assert.Equal(HttpStatusCode.NotFound, await api.UnlinkAsync(tokens.Access, LoginProviders.Google));
    }

    [Fact]
    public async Task ARemovedLoginNoLongerReachesTheAccount()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));
        var google = _server.Providers.SignIn(LoginProviders.Google);
        await api.LinkAsync(tokens.Access, google.ToExternalLogin());

        Assert.Equal(HttpStatusCode.NoContent, await api.UnlinkAsync(tokens.Access, LoginProviders.Google));
        var viaGoogle = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject));

        Assert.True(viaGoogle.AccountCreated);
    }

}
