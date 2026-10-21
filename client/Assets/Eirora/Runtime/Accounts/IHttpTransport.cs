using System.Threading.Tasks;

namespace Eirora.Client.Accounts
{
    /// <summary>
    /// HTTPS to the server and to the providers' token endpoints: UnityWebRequest in the game, a fake server in tests.
    /// </summary>
    public interface IHttpTransport
    {
        /// <summary>Sends the call; never throws for network failures, which answer with status 0.</summary>
        Task<HttpReply> SendAsync(HttpCall call);
    }

    public sealed class HttpCall
    {
        public const string Form = "application/x-www-form-urlencoded";
        public const string Json = "application/json";

        public HttpCall(string method, string url, string? body = null, string? contentType = null, string? bearer = null)
        {
            Method = method;
            Url = url;
            Body = body;
            ContentType = contentType;
            Bearer = bearer;
        }

        public string Method { get; }

        public string Url { get; }

        public string? Body { get; }

        public string? ContentType { get; }

        /// <summary>The access token for the Authorization header, if any.</summary>
        public string? Bearer { get; }
    }

    public sealed class HttpReply
    {
        public HttpReply(int status, string body = "")
        {
            Status = status;
            Body = body;
        }

        /// <summary>The HTTP status, or 0 when the server could not be reached.</summary>
        public int Status { get; }

        public string Body { get; }

        public bool Reached => Status != 0;

        public bool Succeeded => Status >= 200 && Status < 300;
    }
}
