using System.Net;
using Eirora.Contracts;

namespace Eirora.Server.Tests;

/// <summary>Signing in with Apple or Google creates the account the first time and reaches it after that (D-72).</summary>
public sealed class SignInTests(ServerFixture fixture)
{
    private readonly ServerFactory _server = fixture.Server;

    [Theory]
    [InlineData(LoginProviders.Apple)]
    [InlineData(LoginProviders.Google)]
    public async Task FirstSignInCreatesTheAccountWithThatLogin(string provider)
    {
        using var api = _server.CreateApi();

        var tokens = await api.SignInAsync(_server.Providers.SignIn(provider));
        var (status, account) = await api.GetAccountAsync(tokens.Access);

        Assert.True(tokens.AccountCreated);
        Assert.NotNull(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([provider], account!.Logins.Select(l => l.Provider));
    }

    [Fact]
    public async Task SigningInAgainWithTheSameLoginReachesTheSameAccount()
    {
        using var api = _server.CreateApi();
        var first = _server.Providers.SignIn(LoginProviders.Apple);
        var firstAccount = (await api.GetAccountAsync((await api.SignInAsync(first)).Access)).Account!;

        var again = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple, first.Subject));
        var (_, account) = await api.GetAccountAsync(again.Access);

        Assert.False(again.AccountCreated);
        Assert.Equal(firstAccount.Id, account!.Id);
    }

    [Fact]
    public async Task TheSameSubjectWithAnotherProviderIsAnotherPlayer()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var appleAccount = (await api.GetAccountAsync((await api.SignInAsync(apple)).Access)).Account!;

        var google = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google, apple.Subject));
        var (_, googleAccount) = await api.GetAccountAsync(google.Access);

        Assert.True(google.AccountCreated);
        Assert.NotEqual(appleAccount.Id, googleAccount!.Id);
    }

    public static TheoryData<string> Forgeries => ["wrong audience", "wrong key", "expired", "no nonce", "unknown provider"];

    [Theory]
    [MemberData(nameof(Forgeries))]
    public async Task ATokenTheProviderDidNotIssueToTheAppIsRejected(string forgery)
    {
        using var api = _server.CreateApi();
        var providers = _server.Providers;
        var subject = "player-" + Guid.NewGuid().ToString("N");
        const string Nonce = "0123456789abcdef0123456789abcdef";
        var nonceHash = TestProviders.HashHex(Nonce);
        var login = forgery switch
        {
            "wrong audience" => new PlayerLogin(LoginProviders.Apple, subject, Nonce, providers.IdToken(LoginProviders.Apple, subject, nonceHash, audience: "com.someone.else")),
            "wrong key" => new PlayerLogin(LoginProviders.Apple, subject, Nonce, providers.IdToken(LoginProviders.Apple, subject, nonceHash, signingKey: TestProviders.NewKey("apple"))),
            "expired" => new PlayerLogin(LoginProviders.Google, subject, Nonce, providers.IdToken(LoginProviders.Google, subject, nonceHash, expires: DateTime.UtcNow.AddMinutes(-5))),
            "no nonce" => new PlayerLogin(LoginProviders.Google, subject, Nonce, providers.IdToken(LoginProviders.Google, subject, nonceHash: null)),
            _ => new PlayerLogin("facebook", subject, Nonce, providers.IdToken(LoginProviders.Google, subject, nonceHash)),
        };

        var tokens = await api.SignInAsync(login);

        Assert.Equal(HttpStatusCode.BadRequest, tokens.Status);
        Assert.Equal("invalid_grant", tokens.Error);
        Assert.Null(tokens.AccessToken);
    }

    [Fact]
    public async Task ATokenIsOnlyAcceptedWithTheNonceItWasRequestedWith()
    {
        using var api = _server.CreateApi();

        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple), nonce: "fedcba9876543210fedcba9876543210");

        Assert.Equal("invalid_grant", tokens.Error);
    }

    [Fact]
    public async Task AnInterceptedIdTokenCannotBeUsedASecondTime()
    {
        using var api = _server.CreateApi();
        var login = _server.Providers.SignIn(LoginProviders.Google);
        await api.SignInAsync(login);

        var replay = await api.SignInAsync(login);

        Assert.Equal("invalid_grant", replay.Error);
    }

    [Fact]
    public async Task RefreshingGivesNewTokensForTheSameAccount()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));
        var account = (await api.GetAccountAsync(tokens.Access)).Account!;

        var refreshed = await api.RefreshAsync(tokens.Refresh);

        Assert.Equal(HttpStatusCode.OK, refreshed.Status);
        Assert.NotEqual(tokens.Refresh, refreshed.Refresh);
        Assert.Equal(account.Id, (await api.GetAccountAsync(refreshed.Access)).Account!.Id);
    }

    [Fact]
    public async Task SigningOutRevokesTheRefreshToken()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));

        Assert.Equal(HttpStatusCode.OK, await api.RevokeAsync(tokens.Refresh));
        var refreshed = await api.RefreshAsync(tokens.Refresh);

        Assert.Equal("invalid_grant", refreshed.Error);
    }

    [Fact]
    public async Task AnUnknownGrantTypeIsRejected()
    {
        using var api = _server.CreateApi();
        var login = _server.Providers.SignIn(LoginProviders.Apple);

        var tokens = await api.TokenAsync(new() { ["grant_type"] = "password", ["username"] = login.Subject, ["password"] = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, tokens.Status);
        Assert.Null(tokens.AccessToken);
    }
}
