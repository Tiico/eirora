using System.Data.Common;
using System.Net;
using Eirora.Contracts;
using Eirora.Server.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

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

        Assert.Equal(HttpStatusCode.NoContent, await api.LinkAsync(tokens.Access, google.ToExternalLogin(), _server.Providers.Again(apple)));
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

        var status = await api.LinkAsync(tokens.Access, _server.Providers.Again(other), _server.Providers.Again(apple));

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task AnAccountHasAtMostOneLoginPerProvider()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);

        var status = await api.LinkAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Apple).ToExternalLogin(), _server.Providers.Again(apple));

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
            tokens.Access, new ExternalLogin(google.Provider, google.IdToken, "fedcba9876543210fedcba9876543210"), _server.Providers.Again(apple));

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task AStolenRefreshTokenCannotRemoveTheOwnersLogins()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var owner = await api.SignInAsync(apple);
        await api.LinkAsync(owner.Access, _server.Providers.SignIn(LoginProviders.Google).ToExternalLogin(), _server.Providers.Again(apple));
        var stolen = await api.RefreshAsync(owner.Refresh);
        var thief = _server.Providers.SignIn(LoginProviders.Google);
        await api.SignInAsync(thief);

        Assert.Equal(HttpStatusCode.BadRequest, await api.RemoveLoginAsync(stolen.Access, LoginProviders.Google, confirmation: null));
        Assert.Equal(HttpStatusCode.Forbidden, await api.RemoveLoginAsync(stolen.Access, LoginProviders.Google, _server.Providers.Again(thief)));
        Assert.Equal(HttpStatusCode.Forbidden, await api.RemoveLoginAsync(stolen.Access, LoginProviders.Apple, _server.Providers.Again(thief)));
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
        Assert.Equal(HttpStatusCode.Forbidden, await api.LinkAsync(stolen.Access, _server.Providers.Again(thief), _server.Providers.Again(thiefsOtherAccount)));
        Assert.Equal(HttpStatusCode.Forbidden, await api.LinkAsync(stolen.Access, _server.Providers.Again(thief), _server.Providers.Again(thief)));
        Assert.Equal([LoginProviders.Google], (await api.GetAccountAsync(stolen.Access)).Account!.Logins.Select(l => l.Provider));
    }

    [Fact]
    public async Task TheLastLoginCannotBeRemoved()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);

        Assert.Equal(HttpStatusCode.Conflict, await api.RemoveLoginAsync(tokens.Access, LoginProviders.Apple, _server.Providers.Again(apple)));
        Assert.Equal(HttpStatusCode.NotFound, await api.RemoveLoginAsync(tokens.Access, LoginProviders.Google, _server.Providers.Again(apple)));
    }

    [Fact]
    public async Task ARemovedLoginNoLongerReachesTheAccount()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var google = _server.Providers.SignIn(LoginProviders.Google);
        await api.LinkAsync(tokens.Access, google.ToExternalLogin(), _server.Providers.Again(apple));

        Assert.Equal(HttpStatusCode.NoContent, await api.RemoveLoginAsync(tokens.Access, LoginProviders.Google, _server.Providers.Again(apple)));
        var viaGoogle = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject));

        Assert.True(viaGoogle.AccountCreated);
    }

    [Fact]
    public async Task TwoRemovalsAtOnceLeaveTheAccountALogin()
    {
        var gate = new LoginDeletionGate();
        using var server = _server.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<EiroraDbContext>(options => options.AddInterceptors(gate))));
        using var api = new Api(server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }));
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var google = _server.Providers.SignIn(LoginProviders.Google);
        await api.LinkAsync(tokens.Access, google.ToExternalLogin(), _server.Providers.Again(apple));

        var statuses = await Task.WhenAll(
            api.RemoveLoginAsync(tokens.Access, LoginProviders.Apple, _server.Providers.Again(google)),
            api.RemoveLoginAsync(tokens.Access, LoginProviders.Google, _server.Providers.Again(apple)));

        Assert.Single(statuses, HttpStatusCode.NoContent);
        Assert.Single((await api.GetAccountAsync(tokens.Access)).Account!.Logins);
    }

    /// <summary>
    /// Holds the first deletion of a login until a second one arrives, so that two removals interleave the way two devices
    /// can: both have read the account's logins before either deletes. A removal that waits for the other gets through
    /// after <see cref="Timeout"/>.
    /// </summary>
    private sealed class LoginDeletionGate : DbCommandInterceptor
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        private readonly TaskCompletionSource _second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await PassAsync(command);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await PassAsync(command);
            return result;
        }

        private async Task PassAsync(DbCommand command)
        {
            if (!command.CommandText.Contains("DELETE FROM \"AccountLogins\"", StringComparison.Ordinal))
            {
                return;
            }

            switch (Interlocked.Increment(ref _arrived))
            {
                case 1:
                    await Task.WhenAny(_second.Task, Task.Delay(Timeout));
                    break;
                case 2:
                    _second.SetResult();
                    break;
            }
        }
    }
}
