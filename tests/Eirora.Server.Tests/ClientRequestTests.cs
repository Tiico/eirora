using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Eirora.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Eirora.Server.Tests;

/// <summary>
/// The contract with the client (ADR-0008): the requests in <c>src/Eirora.Contracts/Requests~</c> are byte for byte what
/// the Unity client sends (its own <c>ClientRequestTests</c>), and here they reach the real server, with real tokens in
/// place of the placeholders. Either side drifting from the other fails one of the two.
/// </summary>
public sealed class ClientRequestTests(ServerFixture fixture)
{
    private readonly ServerFactory _server = fixture.Server;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheClientsSignInIsAccepted()
    {
        var google = _server.Providers.SignIn(LoginProviders.Google);

        var (status, body) = await SendAsync("sign-in.http", new() { ["ID_TOKEN"] = google.IdToken, ["NONCE"] = google.Nonce });

        Assert.Equal(HttpStatusCode.OK, status);
        AssertReadsAsTheClientExpects("token-response.json", body);
    }

    [Fact]
    public async Task TheClientsRefreshIsAccepted()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));

        var (status, body) = await SendAsync("refresh.http", new() { ["REFRESH_TOKEN"] = tokens.Refresh });

        Assert.Equal(HttpStatusCode.OK, status);
        AssertReadsAsTheClientExpects("token-response.json", body, except: SignInGrant.AccountCreatedParameter);
    }

    [Fact]
    public async Task TheClientsSignOutRevokesTheRefreshToken()
    {
        using var api = _server.CreateApi();
        var tokens = await api.SignInAsync(_server.Providers.SignIn(LoginProviders.Google));

        var (status, _) = await SendAsync("sign-out.http", new() { ["REFRESH_TOKEN"] = tokens.Refresh });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("invalid_grant", (await api.RefreshAsync(tokens.Refresh)).Error);
    }

    [Fact]
    public async Task TheAccountReadsAsTheClientExpects()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        await api.LinkAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Google).ToExternalLogin(), _server.Providers.Again(apple));

        var (status, body) = await SendAsync("account.http", new() { ["ACCESS_TOKEN"] = tokens.Access });

        Assert.Equal(HttpStatusCode.OK, status);
        AssertReadsAsTheClientExpects("account-response.json", body);
    }

    [Fact]
    public async Task TheClientsLinkIsAccepted()
    {
        using var api = _server.CreateApi();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(google);
        var confirmation = _server.Providers.SignIn(LoginProviders.Google, google.Subject);
        var apple = _server.Providers.SignIn(LoginProviders.Apple);

        var (status, _) = await SendAsync("link.http", new()
        {
            ["ACCESS_TOKEN"] = tokens.Access,
            ["LOGIN_ID_TOKEN"] = apple.IdToken,
            ["LOGIN_NONCE"] = apple.Nonce,
            ["CONFIRMATION_ID_TOKEN"] = confirmation.IdToken,
            ["CONFIRMATION_NONCE"] = confirmation.Nonce,
        });

        Assert.Equal(HttpStatusCode.NoContent, status);
        Assert.Equal(
            [LoginProviders.Google, LoginProviders.Apple],
            (await api.GetAccountAsync(tokens.Access)).Account!.Logins.Select(l => l.Provider));
    }

    [Fact]
    public async Task TheClientsLoginRemovalIsAccepted()
    {
        using var api = _server.CreateApi();
        var google = _server.Providers.SignIn(LoginProviders.Google);
        var tokens = await api.SignInAsync(google);
        await api.LinkAsync(tokens.Access, _server.Providers.SignIn(LoginProviders.Apple).ToExternalLogin(), _server.Providers.Again(google));
        var confirmation = _server.Providers.SignIn(LoginProviders.Google, google.Subject);

        var (status, _) = await SendAsync("login-removal.http", new()
        {
            ["ACCESS_TOKEN"] = tokens.Access,
            ["CONFIRMATION_ID_TOKEN"] = confirmation.IdToken,
            ["CONFIRMATION_NONCE"] = confirmation.Nonce,
        });

        Assert.Equal(HttpStatusCode.NoContent, status);
        Assert.Equal([LoginProviders.Google], (await api.GetAccountAsync(tokens.Access)).Account!.Logins.Select(l => l.Provider));
    }

    [Fact]
    public async Task TheClientsDeletionIsAccepted()
    {
        using var api = _server.CreateApi();
        var apple = _server.Providers.SignIn(LoginProviders.Apple);
        var tokens = await api.SignInAsync(apple);
        var confirmation = _server.Providers.SignIn(LoginProviders.Apple, apple.Subject);
        var code = "apple-code-" + Guid.NewGuid().ToString("N");

        var (status, _) = await SendAsync("deletion.http", new()
        {
            ["ACCESS_TOKEN"] = tokens.Access,
            ["CONFIRMATION_ID_TOKEN"] = confirmation.IdToken,
            ["CONFIRMATION_NONCE"] = confirmation.Nonce,
            ["AUTHORIZATION_CODE"] = code,
        });

        Assert.Equal(HttpStatusCode.NoContent, status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.GetAccountAsync(tokens.Access)).Status);
        Assert.Contains(_server.Apple.Requests, r => r.Path == "/auth/revoke" && r.Form["token"] == AppleStub.RefreshToken);
        Assert.Contains(_server.Apple.Requests, r => r.Path == "/auth/token" && r.Form["code"] == code);
    }

    /// <summary>Sends a fixture's request with each <c>__NAME__</c> replaced by its value, encoded for the body.</summary>
    private async Task<(HttpStatusCode Status, string Body)> SendAsync(string name, Dictionary<string, string> values)
    {
        var lines = Fixture(name).Replace("\r", "", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        var requestLine = lines[0].Split(' ');
        var headers = lines.Skip(1).TakeWhile(l => l.Length > 0).Select(l => l.Split(": ", 2)).ToDictionary(h => h[0], h => h[1]);
        var body = lines.Skip(1 + headers.Count + 1).SingleOrDefault();
        var contentType = headers.GetValueOrDefault("Content-Type");

        using var request = new HttpRequestMessage(new HttpMethod(requestLine[0]), new Uri(requestLine[1], UriKind.Relative));
        if (headers.TryGetValue("Authorization", out var authorization))
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(Fill(authorization, values, Uri.EscapeDataString));
        }

        if (body is not null)
        {
            Func<string, string> encode = contentType == "application/json"
                ? value => JsonEncodedText.Encode(value).ToString()
                : Uri.EscapeDataString;
            request.Content = new StringContent(Fill(body, values, encode), Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType!);
        }

        using var client = _server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.SendAsync(request, Cancel);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Cancel));
    }

    private static string Fill(string text, Dictionary<string, string> values, Func<string, string> encode)
    {
        foreach (var (name, value) in values)
        {
            text = text.Replace($"__{name}__", encode(value), StringComparison.Ordinal);
        }

        Assert.DoesNotContain("__", text, StringComparison.Ordinal);
        return text;
    }

    /// <summary>
    /// Every property the client reads in the fixture is in the server's answer, with the same kind of value; the server
    /// may send more.
    /// </summary>
    private static void AssertReadsAsTheClientExpects(string name, string actual, string? except = null)
    {
        using var expected = JsonDocument.Parse(Fixture(name));
        using var answer = JsonDocument.Parse(actual);
        AssertContains(expected.RootElement, answer.RootElement, "$", except);
    }

    private static void AssertContains(JsonElement expected, JsonElement actual, string path, string? except)
    {
        Assert.True(Kind(expected) == Kind(actual), $"{path} is {actual.ValueKind} on the server, {expected.ValueKind} for the client.");
        if (expected.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in expected.EnumerateObject().Where(p => p.Name != except))
            {
                Assert.True(actual.TryGetProperty(property.Name, out var value), $"The server does not send {path}.{property.Name}.");
                AssertContains(property.Value, value, $"{path}.{property.Name}", except);
            }
        }
        else if (expected.ValueKind == JsonValueKind.Array && expected.GetArrayLength() > 0)
        {
            Assert.NotEqual(0, actual.GetArrayLength());
            AssertContains(expected[0], actual[0], $"{path}[0]", except);
        }
    }

    private static JsonValueKind Kind(JsonElement element) =>
        element.ValueKind is JsonValueKind.True or JsonValueKind.False ? JsonValueKind.True : element.ValueKind;

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Requests", name));
}
