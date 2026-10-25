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

    /// <summary>Links <paramref name="login"/>, confirmed with a sign-in of the account, or with none at all.</summary>
    public async Task<HttpStatusCode> LinkAsync(string accessToken, ExternalLogin login, ExternalLogin? confirmation) =>
        await SendAsync(Authorized(HttpMethod.Post, "/api/account/logins", accessToken, new { login, confirmation }));

    /// <summary>Removes the login with <paramref name="provider"/>, confirmed with a sign-in of the account, or with none.</summary>
    public async Task<HttpStatusCode> RemoveLoginAsync(string accessToken, string provider, ExternalLogin? confirmation)
    {
        var request = Authorized(HttpMethod.Post, $"/api/account/logins/{provider}/removal", accessToken);
        request.Content = JsonContent.Create(confirmation);
        return await SendAsync(request);
    }

    /// <summary>Deletes the account; cancelling <paramref name="hangUp"/> drops the connection like a player who closes the app.</summary>
    public async Task<HttpStatusCode> DeleteAccountAsync(string accessToken, ExternalLogin confirmation, CancellationToken? hangUp = null) =>
        await SendAsync(Authorized(HttpMethod.Post, "/api/account/deletion", accessToken, confirmation), hangUp);

    public async Task<(HttpStatusCode Status, CloudSave? Save)> GetCloudSaveAsync(string accessToken)
    {
        using var request = Authorized(HttpMethod.Get, "/api/account/cloud-save", accessToken);
        using var response = await http.SendAsync(request, Cancel);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CloudSave>(Cancel) : null);
    }

    /// <summary>Uploads <paramref name="upload"/>, a <see cref="CloudSaveUpload"/> or any other body; the version comes back with 200 and 409.</summary>
    public async Task<(HttpStatusCode Status, CloudSaveVersion? Version)> UploadCloudSaveAsync(string accessToken, object upload)
    {
        using var request = Authorized(HttpMethod.Put, "/api/account/cloud-save", accessToken, upload);
        using var response = await http.SendAsync(request, Cancel);
        return (response.StatusCode, response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict
            ? await response.Content.ReadFromJsonAsync<CloudSaveVersion>(Cancel)
            : null);
    }

    /// <summary>Signs in and consents to storing the contribution part, as a player who said yes to the question (TASK-060).</summary>
    public async Task<TokenResponse> SignInWithConsentAsync(PlayerLogin login)
    {
        var tokens = await SignInAsync(login);
        var (status, _) = await AnswerConsentAsync(tokens.Access, new ContributionConsentAnswer(true, ContributionConsent.CurrentVersion));
        return status == HttpStatusCode.OK ? tokens : throw new InvalidOperationException($"No consent: {status}");
    }

    public async Task<(HttpStatusCode Status, ContributionConsent? Consent)> GetConsentAsync(string accessToken)
    {
        using var request = Authorized(HttpMethod.Get, "/api/account/contribution-consent", accessToken);
        using var response = await http.SendAsync(request, Cancel);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ContributionConsent>(Cancel) : null);
    }

    /// <summary>Answers the question about the contribution part with <paramref name="answer"/>, a <see cref="ContributionConsentAnswer"/> or any other body.</summary>
    public async Task<(HttpStatusCode Status, ContributionConsent? Consent)> AnswerConsentAsync(string accessToken, object answer)
    {
        using var request = Authorized(HttpMethod.Put, "/api/account/contribution-consent", accessToken, answer);
        using var response = await http.SendAsync(request, Cancel);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ContributionConsent>(Cancel) : null);
    }

    public async Task<(HttpStatusCode Status, LockedContributions[]? Locked)> GetLockedContributionsAsync(string accessToken)
    {
        using var request = Authorized(HttpMethod.Get, "/api/account/cloud-save/locked", accessToken);
        using var response = await http.SendAsync(request, Cancel);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<LockedContributions[]>(Cancel) : null);
    }

    public async Task<(HttpStatusCode Status, KeyTransfer? Transfer)> CreateKeyTransferAsync(string accessToken, object request)
    {
        using var httpRequest = Authorized(HttpMethod.Post, "/api/account/key-transfers", accessToken, request);
        using var response = await http.SendAsync(httpRequest, Cancel);
        return (response.StatusCode, response.StatusCode == HttpStatusCode.Created ? await response.Content.ReadFromJsonAsync<KeyTransfer>(Cancel) : null);
    }

    public async Task<(HttpStatusCode Status, KeyTransfer? Transfer)> GetKeyTransferAsync(string accessToken, string id)
    {
        using var request = Authorized(HttpMethod.Get, "/api/account/key-transfers/" + Uri.EscapeDataString(id), accessToken);
        using var response = await http.SendAsync(request, Cancel);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<KeyTransfer>(Cancel) : null);
    }

    public async Task<HttpStatusCode> DeliverKeyTransferAsync(string accessToken, string id, object delivery) =>
        await SendAsync(Authorized(HttpMethod.Put, "/api/account/key-transfers/" + Uri.EscapeDataString(id), accessToken, delivery));

    public async Task<HttpStatusCode> DeleteKeyTransferAsync(string accessToken, string id) =>
        await SendAsync(Authorized(HttpMethod.Delete, "/api/account/key-transfers/" + Uri.EscapeDataString(id), accessToken));

    public async Task<HttpStatusCode> DeleteLockedContributionsAsync(string accessToken, int keyEpoch) =>
        await SendAsync(Authorized(HttpMethod.Delete, $"/api/account/cloud-save/locked/{keyEpoch}", accessToken));

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

    private async Task<HttpStatusCode> SendAsync(HttpRequestMessage request, CancellationToken? cancellationToken = null)
    {
        using (request)
        {
            using var response = await http.SendAsync(request, cancellationToken ?? Cancel);
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
