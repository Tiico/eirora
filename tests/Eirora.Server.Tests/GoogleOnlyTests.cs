using System.Net;
using System.Net.Http.Json;
using Eirora.Contracts;
using Eirora.Server.Accounts;
using Eirora.Server.SignIn;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Eirora.Server.Tests;

/// <summary>
/// A server with Google only and no Apple configuration at all, as the product owner runs it without an Apple Developer
/// account: Sign in with Apple is off everywhere, and nothing ever calls Apple.
/// </summary>
public sealed class GoogleOnlyTests(ServerFixture fixture)
{
    private readonly ServerFactory _server = fixture.GoogleOnlyServer;
    private readonly ServerFactory _withApple = fixture.Server;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APlayerSignsInWithGoogle()
    {
        using var api = _server.CreateApi();

        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));

        Assert.Equal(HttpStatusCode.OK, tokens.Status);
        Assert.True(tokens.AccountCreated);
    }

    [Fact]
    public async Task AppleIsNotAProviderTheServerAccepts()
    {
        using var api = _server.CreateApi();

        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Apple));

        Assert.Equal("invalid_grant", tokens.Error);
        Assert.DoesNotContain(LoginProviders.Apple, _server.Services.GetRequiredService<ExternalProviders>().Names);
    }

    [Fact]
    public async Task ApplesWebCallbackIsNotThere()
    {
        using var client = _server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        using var response = await client.PostAsync(
            new Uri(AppleWebCallback.Path, UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "c.0.abc", ["state"] = "s1" }),
            Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheDeletionPageOffersGoogleOnly()
    {
        using var client = _server.CreateClient();

        using var response = await client.GetAsync(new Uri(DeletionPage.Path, UriKind.Relative), Cancel);
        var html = await response.Content.ReadAsStringAsync(Cancel);
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Radera ditt konto i Eirora", html, StringComparison.Ordinal);
        Assert.Contains("https://accounts.google.com/gsi/client", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Apple", html, StringComparison.Ordinal);
        Assert.DoesNotContain("apple", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apple", policy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://accounts.google.com/gsi/", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDeletionPageWithAppleStillOffersBoth()
    {
        using var client = _withApple.CreateClient();

        using var response = await client.GetAsync(new Uri(DeletionPage.Path, UriKind.Relative), Cancel);
        var html = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Contains("appleid.auth.js", html, StringComparison.Ordinal);
        Assert.Contains("data-apple-services-id=\"" + TestProviders.AppleServicesId + "\"", html, StringComparison.Ordinal);
        Assert.Contains("https://appleid.apple.com", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAccountIsDeletedInTheAppWithoutCallingApple()
    {
        using var api = _server.CreateApi();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(google);

        var status = await api.DeleteAccountAsync(tokens.Access, _server.Providers.Again(google));

        Assert.Equal(HttpStatusCode.NoContent, status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAccountAsync(tokens.Access)).Status);
        Assert.Empty(_server.Apple.Requests);
    }

    [Fact]
    public async Task AnAccountIsDeletedOnTheWebWithGoogle()
    {
        using var api = _server.CreateApi();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(google);

        var status = await DeleteOnTheWebAsync(_server.Providers.Again(google));

        Assert.Equal(HttpStatusCode.NoContent, status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAccountAsync(tokens.Access)).Status);
        Assert.Empty(_server.Apple.Requests);
    }

    [Fact]
    public async Task AnAccountWithAnAppleLoginFromBeforeIsDeletedWithGoogle()
    {
        // The account got its Apple login while Apple was on; with Apple off, Google's confirmation is all there can be.
        using var withApple = _withApple.CreateApi();
        var apple = _withApple.Providers.SignIn(LoginProviders.Apple);
        var appleTokens = await withApple.SignInAsync(apple);
        var google = _withApple.Providers.SignIn(LoginProviders.Google);
        await withApple.LinkAsync(appleTokens.Access, google.ToExternalLogin(), _withApple.Providers.Again(apple));
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject));

        var status = await api.DeleteAccountAsync(tokens.Access, _server.Providers.Again(google));

        Assert.Equal(HttpStatusCode.NoContent, status);
        Assert.True((await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject))).AccountCreated);
        Assert.Empty(_server.Apple.Requests);
    }

    [Fact]
    public async Task EmptyAppleSettingsTurnAppleOff()
    {
        // Compose passes an unset variable as an empty one, and an empty secret file as an empty key.
        using var server = _server.WithWebHostBuilder(builder => builder
            .UseSetting("SignIn:Providers:apple:Audiences:0", "")
            .UseSetting("SignIn:Providers:apple:Audiences:1", "")
            .UseSetting("SignIn:Web:AppleServicesId", "")
            .UseSetting("SignIn:Apple:TeamId", "")
            .UseSetting("SignIn:Apple:KeyId", "")
            .UseSetting("SignIn:Apple:PrivateKey", ""));
        using var client = server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative), Cancel);
        using var callback = await client.PostAsync(new Uri(AppleWebCallback.Path, UriKind.Relative), new FormUrlEncodedContent([]), Cancel);

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, callback.StatusCode);
        Assert.Equal([LoginProviders.Google], server.Services.GetRequiredService<ExternalProviders>().Names);
    }

    private async Task<HttpStatusCode> DeleteOnTheWebAsync(ExternalLogin confirmation)
    {
        using var client = _server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync(new Uri(DeletionPage.Path, UriKind.Relative), confirmation, Cancel);
        return response.StatusCode;
    }
}
