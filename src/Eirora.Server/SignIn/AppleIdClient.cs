using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Eirora.Server.SignIn;

/// <summary>The app's Sign in with Apple key, section <c>SignIn:Apple</c>.</summary>
public sealed class AppleOptions
{
    public string TeamId { get; set; } = "";

    public string KeyId { get; set; } = "";

    /// <summary>The private key (.p8) as PEM; in production a secret file (ADR-0008).</summary>
    public string PrivateKey { get; set; } = "";

    public bool IsConfigured => TeamId.Length > 0 && KeyId.Length > 0 && PrivateKey.Length > 0;
}

/// <summary>
/// Calls Sign in with Apple's REST API to revoke the app's access to an Apple ID when its account is deleted
/// (App Store Review Guideline 5.1.1(v)).
/// </summary>
/// <remarks>
/// Revoking needs an Apple token, so the deletion request carries an authorization code from a fresh Sign in with Apple,
/// which is exchanged and the resulting refresh token revoked at once. The server never stores Apple's tokens.
/// </remarks>
public sealed partial class AppleIdClient(HttpClient http, IOptions<AppleOptions> options, TimeProvider time, ILogger<AppleIdClient> logger)
{
    public const string Audience = "https://appleid.apple.com";

    private static readonly TimeSpan s_clientSecretLifetime = TimeSpan.FromMinutes(5);
    private static readonly JsonWebTokenHandler s_handler = new();

    /// <summary>Revokes the app's access for the Apple ID the code belongs to; returns whether Apple confirmed it.</summary>
    public async Task<bool> RevokeAsync(string authorizationCode, string clientId, CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            LogNotConfigured(logger);
            return false;
        }

        var clientSecret = CreateClientSecret(clientId);
        using var exchange = await http.PostAsync(
            new Uri("auth/token", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = authorizationCode,
                ["grant_type"] = "authorization_code",
            }),
            cancellationToken);
        if (!exchange.IsSuccessStatusCode)
        {
            LogFailed(logger, "token exchange", (int)exchange.StatusCode);
            return false;
        }

        var tokens = await exchange.Content.ReadFromJsonAsync<AppleTokens>(cancellationToken);
        var (token, hint) = tokens?.RefreshToken is { Length: > 0 } refresh
            ? (refresh, "refresh_token")
            : (tokens?.AccessToken ?? "", "access_token");
        using var revoke = await http.PostAsync(
            new Uri("auth/revoke", UriKind.Relative),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["token"] = token,
                ["token_type_hint"] = hint,
            }),
            cancellationToken);
        if (!revoke.IsSuccessStatusCode)
        {
            LogFailed(logger, "revocation", (int)revoke.StatusCode);
            return false;
        }

        return true;
    }

    /// <summary>The ES256-signed JWT Apple requires as client secret, valid for a few minutes.</summary>
    private string CreateClientSecret(string clientId)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(options.Value.PrivateKey);
        var now = time.GetUtcNow().UtcDateTime;
        return s_handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Value.TeamId,
            Audience = Audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, clientId)]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now + s_clientSecretLifetime,
            SigningCredentials = new SigningCredentials(
                // The key is disposed after signing, so its signature provider must not be cached.
                new ECDsaSecurityKey(key)
                {
                    KeyId = options.Value.KeyId,
                    CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
                },
                SecurityAlgorithms.EcdsaSha256),
        });
    }

    private sealed record AppleTokens(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign in with Apple is not configured; cannot revoke the app's access to an Apple ID")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign in with Apple {Step} failed with status {Status}")]
    private static partial void LogFailed(ILogger logger, string step, int status);
}
