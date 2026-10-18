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
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var account = (await api.GetAccountAsync(tokens.Access)).Account!;
        var google = _server.Providers.SignIn(LoginProviders.Google);

        Assert.Equal(HttpStatusCode.NoContent, await api.LinkAsync(tokens.Access, google.ToExternalLogin(), Again(apple)));
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
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);

        var status = await api.LinkAsync(tokens.Access, Again(other), Again(apple));

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task AnAccountHasAtMostOneLoginPerProvider()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);

        var status = await api.LinkAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Apple).ToExternalLogin(), Again(apple));

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task LinkingRequiresAValidSignIn()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var google = _server.Providers.SignIn(LoginProviders.Google);

        var status = await api.LinkAsync(
            tokens.Access, new ExternalLogin(google.Provider, google.IdToken, "fedcba9876543210fedcba9876543210"), Again(apple));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task AStolenRefreshTokenCannotRemoveTheOwnersLogins()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var owner = await api.SignInAsync(apple);
        await api.LinkAsync(owner.Access, _server.Providers.SignIn(LoginProviders.Google).ToExternalLogin(), Again(apple));
        var stolen = await api.RefreshAsync(owner.Refresh);
        var thief = _server.Providers.SignIn(LoginProviders.Google);
        await api.SignInAsync(thief);

        Assert.Equal(HttpStatusCode.BadRequest, await api.UnlinkAsync(stolen.Access, LoginProviders.Google, confirmation: null));
        Assert.Equal(HttpStatusCode.Forbidden, await api.UnlinkAsync(stolen.Access, LoginProviders.Google, Again(thief)));
        Assert.Equal(HttpStatusCode.Forbidden, await api.UnlinkAsync(stolen.Access, LoginProviders.Apple, Again(thief)));
        Assert.Equal(
            [LoginProviders.Apple, LoginProviders.Google],
            (await api.GetAccountAsync(stolen.Access)).Account!.Logins.Select(l => l.Provider));
    }

    [Fact]
    public async Task LinkingRequiresAFreshSignInWithALoginOfTheAccount()
    {
        using var api = _server.CreateApi();
        var owner = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));
        var stolen = await api.RefreshAsync(owner.Refresh);
        var thief = _server.Providers.SignIn(LoginProviders.Apple);
        var thiefsOtherAccount = _server.Providers.SignIn(LoginProviders.Google);
        await api.SignInAsync(thiefsOtherAccount);

        Assert.Equal(HttpStatusCode.BadRequest, await api.LinkAsync(stolen.Access, thief.ToExternalLogin(), confirmation: null));
        Assert.Equal(HttpStatusCode.Forbidden, await api.LinkAsync(stolen.Access, Again(thief), Again(thiefsOtherAccount)));
        Assert.Equal(HttpStatusCode.Forbidden, await api.LinkAsync(stolen.Access, Again(thief), Again(thief)));
        Assert.Equal([LoginProviders.Google], (await api.GetAccountAsync(stolen.Access)).Account!.Logins.Select(l => l.Provider));
    }

    [Fact]
    public async Task TheLastLoginCannotBeRemoved()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);

        Assert.Equal(HttpStatusCode.Conflict, await api.UnlinkAsync(tokens.Access, LoginProviders.Apple, Again(apple)));
        Assert.Equal(HttpStatusCode.NotFound, await api.UnlinkAsync(tokens.Access, LoginProviders.Google, Again(apple)));
    }

    [Fact]
    public async Task ARemovedLoginNoLongerReachesTheAccount()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var google = _server.Providers.SignIn(LoginProviders.Google);
        await api.LinkAsync(tokens.Access, google.ToExternalLogin(), Again(apple));

        Assert.Equal(HttpStatusCode.NoContent, await api.UnlinkAsync(tokens.Access, LoginProviders.Google, Again(apple)));
        var viaGoogle = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject));

        Assert.True(viaGoogle.AccountCreated);
    }

    /// <summary>A fresh sign-in with the same login, as the client asks for before changing the account.</summary>
    private ExternalLogin Again(PlayerLogin login) => _server.Providers.SignIn(login.Provider, login.Subject).ToExternalLogin();
}
