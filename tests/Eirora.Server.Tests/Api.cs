using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Eirora.Contracts;

namespace Eirora.Server.Tests;

/// <summary>The server's HTTP API as the client calls it.</summary>
public sealed class Api(HttpClient http) : IDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async Task<TokenResponse> SignInAsync(PlayerLogin login, string? nonce = null) => await TokenAsync(new()
    {
        ["grant_type"] = SignInGrant.GrantType,
        [SignInGrant.ProviderParameter] = login.Provider,
        [SignInGrant.IdTokenParameter] = login.IdToken,
        [SignInGrant.NonceParameter] = nonce ?? login.Nonce,
        ["scope"] = SignInGrant.OfflineAccessScope,
    });

    public async Task<TokenResponse> RefreshAsync(string refreshToken) => await TokenAsync(new()
    {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = refreshToken,
    });

    public async Task<HttpStatusCode> RevokeAsync(string token)
    {
        using var response = await http.PostAsync(
            new Uri("/connect/revoke", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token, ["token_type_hint"] = "refresh_token" }),
            Cancel);
        return response.StatusCode;
    }

    public async Task<(HttpStatusCode Status, AccountInfo? Account)> GetAccountAsync(string accessToken)
    {
        using var request = Authorized(HttpMethod.Get, "/api/account", accessToken);
        using var response = await http.SendAsync(request, Cancel);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AccountInfo>(Cancel) : null);
    }

    public async Task<HttpStatusCode> LinkAsync(string accessToken, ExternalLogin login) =>
        await SendAsync(Authorized(HttpMethod.Post, "/api/account/logins", accessToken, login));

    public async Task<HttpStatusCode> UnlinkAsync(string accessToken, string provider) =>
        await SendAsync(Authorized(HttpMethod.Delete, $"/api/account/logins/{provider}", accessToken));

    public async Task<HttpStatusCode> DeleteAccountAsync(string accessToken, ExternalLogin confirmation) =>
        await SendAsync(Authorized(HttpMethod.Post, "/api/account/deletion", accessToken, confirmation));

    public void Dispose() => http.Dispose();

    public async Task<TokenResponse> TokenAsync(Dictionary<string, string> form)
    {
        using var response = await http.PostAsync(new Uri("/connect/token", UriKind.Relative), new FormUrlEncodedContent(form), Cancel);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        var root = json.RootElement;
        return new TokenResponse(
            response.StatusCode,
            String(root, "access_token"),
            String(root, "refresh_token"),
            root.TryGetProperty(SignInGrant.AccountCreatedParameter, out var created) ? created.GetBoolean() : null,
            String(root, "error"));
    }

    private async Task<HttpStatusCode> SendAsync(HttpRequestMessage request)
    {
        using (request)
        {
            using var response = await http.SendAsync(request, Cancel);
            return response.StatusCode;
        }
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static string? String(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.GetString() : null;
}

public sealed record TokenResponse(HttpStatusCode Status, string? AccessToken, string? RefreshToken, bool? AccountCreated, string? Error)
{
    public string Access => AccessToken ?? throw new InvalidOperationException($"No access token: {Status} {Error}");

    public string Refresh => RefreshToken ?? throw new InvalidOperationException($"No refresh token: {Status} {Error}");
}
