using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// <see cref="IHttpTransport"/> with UnityWebRequest, which uses the platform's own network stack and certificate store.
    /// Called on the main thread; the reply arrives there too.
    /// </summary>
    public sealed class UnityHttpTransport : IHttpTransport
    {
        private const int TimeoutSeconds = 20;

        public Task<HttpReply> SendAsync(HttpCall call)
        {
            var request = new UnityWebRequest(call.Url, call.Method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };
            if (call.Body is not null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(call.Body));
                request.SetRequestHeader("Content-Type", call.ContentType ?? HttpCall.Json);
            }

            if (call.Bearer is not null)
            {
                request.SetRequestHeader("Authorization", "Bearer " + call.Bearer);
            }

            request.SetRequestHeader("Accept", HttpCall.Json);
            var reply = new TaskCompletionSource<HttpReply>();
            request.SendWebRequest().completed += _ =>
            {
                reply.SetResult(request.result == UnityWebRequest.Result.ConnectionError
                    ? new HttpReply(0)
                    : new HttpReply((int)request.responseCode, request.downloadHandler.text ?? string.Empty));
                request.Dispose();
            };
            return reply.Task;
        }
    }
}
