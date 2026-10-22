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

    private HttpRequestMessage Request(HttpMethod method)
    {
        var request = new HttpRequestMessage(method, new Uri("/api/account/cloud-save", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
