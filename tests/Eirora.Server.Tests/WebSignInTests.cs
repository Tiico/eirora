using System.Net;
using System.Net.Http.Json;
using Eirora.Contracts;
using Eirora.Server.Accounts;
using Eirora.Server.SignIn;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Eirora.Server.Tests;

/// <summary>
/// Sign-in and deletion on the web: Apple's answer for the Android app, and the deletion page Google Play requires
/// (ADR-0008, ACC-05).
/// </summary>
public sealed class WebSignInTests(ServerFixture fixture)
{
    private readonly ServerFactory _server = fixture.Server;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApplesAnswerIsHandedToTheAndroidAppOnly()
    {
        using var client = NonRedirectingClient();

        using var response = await client.PostAsync(
            new Uri(AppleWebCallback.Path, UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = "c.0.abc",
                ["id_token"] = "eyJ.header.sig",
                ["state"] = "s+1/2",
                ["user"] = "{\"email\":\"someone@example.com\"}",
            }),
            Cancel);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith("intent://apple?", location, StringComparison.Ordinal);
        Assert.EndsWith("#Intent;scheme=eirora-signin;package=com.eirora.game;end", location, StringComparison.Ordinal);
        Assert.Contains("code=c.0.abc", location, StringComparison.Ordinal);
        Assert.Contains("id_token=eyJ.header.sig", location, StringComparison.Ordinal);
        Assert.Contains("state=s%2B1%2F2", location, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACancelledAppleSignInReachesTheAppAsAnError()
    {
        using var client = NonRedirectingClient();

        using var response = await client.PostAsync(
            new Uri(AppleWebCallback.Path, UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["error"] = "user_cancelled_authorize", ["state"] = "s1" }),
            Cancel);

        Assert.StartsWith("intent://apple?error=user_cancelled_authorize&state=s1#Intent;", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDeletionPageSaysWhatIsDeletedAndHowLongBackupsKeepIt()
    {
        using var client = _server.CreateClient();

        using var response = await client.GetAsync(new Uri(DeletionPage.Path, UriKind.Relative), Cancel);
        var html = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Radera ditt konto i Eirora", html, StringComparison.Ordinal);
        Assert.Contains("30 dagar", html, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task TheDeletionPageScriptIsServed()
    {
        using var client = _server.CreateClient();

        using var response = await client.GetAsync(new Uri("/account/delete.js", UriKind.Relative), Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/account/delete", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAccountIsDeletedOnTheWebWithAFreshSignIn()
    {
        using var api = _server.CreateApi();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(google);

        var status = await DeleteOnTheWebAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject).ToExternalLogin());

        Assert.Equal(HttpStatusCode.NoContent, status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAccountAsync(tokens.Access)).Status);
        Assert.True((await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject))).AccountCreated);
    }

    [Fact]
    public async Task OnTheWebAnAccountWithAnAppleLoginIsConfirmedWithApple()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var google = _server.Providers.SignIn(LoginProviders.Google);
        await api.LinkAsync(tokens.Access, google.ToExternalLogin());

        var withGoogle = await DeleteOnTheWebAsync(_server.Providers.SignIn(LoginProviders.Google, google.Subject).ToExternalLogin());
        var code = "apple-code-" + Guid.NewGuid().ToString("N");
        var withApple = await DeleteOnTheWebAsync(_server.Providers.SignIn(LoginProviders.Apple, apple.Subject).ToExternalLogin(code));

        Assert.Equal(HttpStatusCode.Forbidden, withGoogle);
        Assert.Equal(HttpStatusCode.NoContent, withApple);
        Assert.Contains(_server.Apple.Requests, r => r.Path == "/auth/token" && r.Form["code"] == code);
    }

    [Fact]
    public async Task OnTheWebALoginWithoutAnAccountDeletesNothing()
    {
        var status = await DeleteOnTheWebAsync(_server.Providers.SignIn(LoginProviders.Google).ToExternalLogin());

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task OnTheWebAnUnverifiedSignInDeletesNothing()
    {
        using var api = _server.CreateApi();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(google);
        var confirmation = _server.Providers.SignIn(LoginProviders.Google, google.Subject);

        var status = await DeleteOnTheWebAsync(new ExternalLogin(LoginProviders.Google, confirmation.IdToken, "another-nonce-0123456789"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(HttpStatusCode.OK, (await api.GetAccountAsync(tokens.Access)).Status);
    }

    [Fact]
    public async Task NothingFromTheWebSignInsIsLogged()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        await api.SignInAsync(apple);
        var confirmation = _server.Providers.SignIn(LoginProviders.Apple, apple.Subject);
        var code = "apple-code-" + Guid.NewGuid().ToString("N");
        using var client = NonRedirectingClient();
        using var callback = await client.PostAsync(
            new Uri(AppleWebCallback.Path, UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = code, ["id_token"] = confirmation.IdToken, ["state"] = "s1" }),
            Cancel);

        await DeleteOnTheWebAsync(confirmation.ToExternalLogin(code));

        var logs = string.Join('\n', _server.Logs);
        Assert.All(
            [apple.Subject, confirmation.IdToken, confirmation.Nonce, code],
            secret => Assert.DoesNotContain(secret, logs, StringComparison.Ordinal));
    }

    private async Task<HttpStatusCode> DeleteOnTheWebAsync(ExternalLogin confirmation)
    {
        using var client = _server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync(new Uri(DeletionPage.Path, UriKind.Relative), confirmation, Cancel);
        return response.StatusCode;
    }

    private HttpClient NonRedirectingClient() =>
        _server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
}
