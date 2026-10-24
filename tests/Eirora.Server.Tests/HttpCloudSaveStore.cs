using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Eirora.CloudSaving;
using Eirora.Contracts;

namespace Eirora.Server.Tests;

/// <summary>
/// The cloud save API over an HTTP client with a bearer token, as a headless device reaches the real server: the same
/// statuses the Unity client's store answers with.
/// </summary>
public sealed class HttpCloudSaveStore(HttpClient http, string accessToken) : ICloudSaveStore
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async Task<StoreReply<CloudSave>> GetAsync()
    {
        using var request = Request(HttpMethod.Get);
        try
        {
            using var response = await http.SendAsync(request, Cancel);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => new StoreReply<CloudSave>(StoreStatus.Ok, await response.Content.ReadFromJsonAsync<CloudSave>(Cancel)),
                HttpStatusCode.NotFound => new StoreReply<CloudSave>(StoreStatus.NotFound),
                HttpStatusCode.Unauthorized => new StoreReply<CloudSave>(StoreStatus.SignedOut),
                _ => new StoreReply<CloudSave>(StoreStatus.Failed),
            };
        }
        catch (HttpRequestException)
        {
            return new StoreReply<CloudSave>(StoreStatus.Offline);
        }
    }

    public async Task<StoreReply<CloudSaveVersion>> UploadAsync(CloudSaveUpload upload)
    {
        using var request = Request(HttpMethod.Put);
        request.Content = JsonContent.Create(upload);
        try
        {
            using var response = await http.SendAsync(request, Cancel);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => new StoreReply<CloudSaveVersion>(StoreStatus.Ok, await response.Content.ReadFromJsonAsync<CloudSaveVersion>(Cancel)),
                HttpStatusCode.Conflict => new StoreReply<CloudSaveVersion>(StoreStatus.Conflict, await response.Content.ReadFromJsonAsync<CloudSaveVersion>(Cancel)),
                HttpStatusCode.Unauthorized => new StoreReply<CloudSaveVersion>(StoreStatus.SignedOut),
                HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge => new StoreReply<CloudSaveVersion>(StoreStatus.Rejected),
                _ => new StoreReply<CloudSaveVersion>(StoreStatus.Failed),
            };
        }
        catch (HttpRequestException)
        {
            return new StoreReply<CloudSaveVersion>(StoreStatus.Offline);
        }
    }

    public async Task<StoreReply<ContributionConsent>> GetConsentAsync()
    {
        using var request = Request(HttpMethod.Get, "/api/account/contribution-consent");
        return await ConsentAsync(request);
    }

    public async Task<StoreReply<ContributionConsent>> AnswerConsentAsync(ContributionConsentAnswer answer)
    {
        using var request = Request(HttpMethod.Put, "/api/account/contribution-consent");
        request.Content = JsonContent.Create(answer);
        return await ConsentAsync(request);
    }

    private async Task<StoreReply<ContributionConsent>> ConsentAsync(HttpRequestMessage request)
    {
        try
        {
            using var response = await http.SendAsync(request, Cancel);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => new StoreReply<ContributionConsent>(StoreStatus.Ok, await response.Content.ReadFromJsonAsync<ContributionConsent>(Cancel)),
                HttpStatusCode.Unauthorized => new StoreReply<ContributionConsent>(StoreStatus.SignedOut),
                _ => new StoreReply<ContributionConsent>(StoreStatus.Failed),
            };
        }
        catch (HttpRequestException)
        {
            return new StoreReply<ContributionConsent>(StoreStatus.Offline);
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string path = "/api/account/cloud-save")
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
