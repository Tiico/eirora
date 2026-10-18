using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace Eirora.Server.Tests;

/// <summary>Sign in with Apple's REST API: records the requests and answers like Apple.</summary>
/// <remarks>
/// An authorization code starting with one of the failure prefixes makes the token exchange fail that way, so that tests
/// sharing the stub can each choose how Apple behaves.
/// </remarks>
public sealed class AppleStub
{
    public const string TeamId = "TEAM123456";
    public const string KeyId = "KEY1234567";
    public const string RefreshToken = "apple-refresh-token";

    /// <summary>Apple cannot be reached.</summary>
    public const string UnreachablePrefix = "unreachable-";

    /// <summary>Apple never answers.</summary>
    public const string HangingPrefix = "hanging-";

    /// <summary>Apple answers with something that is not its JSON.</summary>
    public const string GarbledPrefix = "garbled-";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _reached = new(StringComparer.Ordinal);

    public string PrivateKeyPem => _key.ExportPkcs8PrivateKeyPem();

    public ECDsa PublicKey => _key;

    public ConcurrentQueue<(string Path, Dictionary<string, string> Form)> Requests { get; } = new();

    /// <summary>Completes once a request with the authorization code has reached Apple.</summary>
    public Task Reached(string authorizationCode) => Arrival(authorizationCode).Task;

    public HttpMessageHandler CreateHandler() => new Handler(this);

    private TaskCompletionSource Arrival(string authorizationCode) =>
        _reached.GetOrAdd(authorizationCode, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    private sealed class Handler(AppleStub stub) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var form = body.Split('&').Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1]));
            stub.Requests.Enqueue((request.RequestUri!.AbsolutePath, form));
            var code = form.GetValueOrDefault("code", "");
            stub.Arrival(code).TrySetResult();
            if (code.StartsWith(UnreachablePrefix, StringComparison.Ordinal))
            {
                throw new HttpRequestException("No route to appleid.apple.com.");
            }

            if (code.StartsWith(HangingPrefix, StringComparison.Ordinal))
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (code.StartsWith(GarbledPrefix, StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html>Service Unavailable</html>", System.Text.Encoding.UTF8, "application/json"),
                };
            }

            return request.RequestUri.AbsolutePath switch
            {
                "/auth/token" => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"access_token":"apple-access-token","token_type":"Bearer","expires_in":3600,"refresh_token":"{{RefreshToken}}","id_token":"x"}""",
                        System.Text.Encoding.UTF8,
                        "application/json"),
                },
                "/auth/revoke" => new HttpResponseMessage(HttpStatusCode.OK),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }
    }
}
