using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Eirora.CloudSaving;
using Eirora.Contracts;

namespace Eirora.Server.Tests;

/// <summary>The key transfer API over an HTTP client with a bearer token, as a headless device reaches the real server.</summary>
public sealed class HttpKeyTransferStore(HttpClient http, string accessToken) : IKeyTransferStore
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public Task<StoreReply<KeyTransfer>> CreateAsync(byte[] receiverPublicKey) =>
        SendAsync(HttpMethod.Post, string.Empty, new KeyTransferRequest(receiverPublicKey), HttpStatusCode.Created);

    public Task<StoreReply<KeyTransfer>> TakeAsync(string id) => SendAsync(HttpMethod.Get, "/" + Uri.EscapeDataString(id), null, HttpStatusCode.OK);

    public Task<StoreReply<KeyTransfer>> DeliverAsync(string id, byte[] sealedKey) =>
        SendAsync(HttpMethod.Put, "/" + Uri.EscapeDataString(id), new KeyTransferDelivery(sealedKey), HttpStatusCode.NoContent);

    public Task<StoreReply<KeyTransfer>> CancelAsync(string id) => SendAsync(HttpMethod.Delete, "/" + Uri.EscapeDataString(id), null, HttpStatusCode.NoContent);

    private async Task<StoreReply<KeyTransfer>> SendAsync(HttpMethod method, string path, object? body, HttpStatusCode success)
    {
        using var request = new HttpRequestMessage(method, new Uri("/api/account/key-transfers" + path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        try
        {
            using var response = await http.SendAsync(request, Cancel);
            if (response.StatusCode == success)
            {
                var transfer = success == HttpStatusCode.NoContent ? null : await response.Content.ReadFromJsonAsync<KeyTransfer>(Cancel);
                return new StoreReply<KeyTransfer>(StoreStatus.Ok, transfer);
            }

            return response.StatusCode switch
            {
                HttpStatusCode.NotFound => new StoreReply<KeyTransfer>(StoreStatus.NotFound),
                HttpStatusCode.Conflict => new StoreReply<KeyTransfer>(StoreStatus.Conflict),
                HttpStatusCode.Unauthorized => new StoreReply<KeyTransfer>(StoreStatus.SignedOut),
                HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge => new StoreReply<KeyTransfer>(StoreStatus.Rejected),
                _ => new StoreReply<KeyTransfer>(StoreStatus.Failed),
            };
        }
        catch (HttpRequestException)
        {
            return new StoreReply<KeyTransfer>(StoreStatus.Offline);
        }
    }
}
