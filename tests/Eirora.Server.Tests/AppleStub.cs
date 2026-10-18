using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace Eirora.Server.Tests;

/// <summary>Sign in with Apple's REST API: records the requests and answers like Apple.</summary>
public sealed class AppleStub
{
    public const string TeamId = "TEAM123456";
    public const string KeyId = "KEY1234567";
    public const string RefreshToken = "apple-refresh-token";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string PrivateKeyPem => _key.ExportPkcs8PrivateKeyPem();

    public ECDsa PublicKey => _key;

    public ConcurrentQueue<(string Path, Dictionary<string, string> Form)> Requests { get; } = new();

    public HttpMessageHandler CreateHandler() => new Handler(this);

    private sealed class Handler(AppleStub stub) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var form = body.Split('&').Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1]));
            stub.Requests.Enqueue((request.RequestUri!.AbsolutePath, form));
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
