using System.Security.Cryptography;
using System.Text;
using Eirora.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Eirora.Server.SignIn;

/// <summary>
/// Verifies an Apple or Google ID token: signature, issuer, audience, lifetime, nonce, and that it has not been used before.
/// </summary>
/// <remarks>
/// The client gives the provider the SHA-256 of a random nonce as lowercase hex and sends the nonce itself, so a token
/// is only accepted together with the secret it was requested with. Each token is accepted once (ADR-0008, threat model).
/// Failures are logged by reason only, never with the token or the player's identifiers (NFR-10).
/// </remarks>
public sealed partial class ExternalTokenValidator(
    ExternalProviders providers,
    EiroraDbContext db,
    ILogger<ExternalTokenValidator> logger)
{
    private const int MinNonceLength = 16;
    private const int MaxNonceLength = 256;

    private static readonly JsonWebTokenHandler s_handler = new() { MapInboundClaims = false };

    public async Task<ExternalIdentity?> ValidateAsync(string? provider, string? idToken, string? nonce, CancellationToken cancellationToken)
    {
        if (!providers.TryGet(provider, out var source))
        {
            LogRejected(logger, "unknown provider");
            return null;
        }

        if (string.IsNullOrEmpty(idToken) || nonce is null || nonce.Length is < MinNonceLength or > MaxNonceLength)
        {
            LogRejected(logger, "missing token or nonce");
            return null;
        }

        var result = await s_handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ConfigurationManager = source.Configuration,
            ValidIssuers = source.Issuers,
            ValidAudiences = source.Audiences,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        });
        if (!result.IsValid)
        {
            LogRejected(logger, "invalid token");
            return null;
        }

        var token = (JsonWebToken)result.SecurityToken;
        if (!token.TryGetPayloadValue("nonce", out string? tokenNonce) || tokenNonce != HashHex(nonce))
        {
            LogRejected(logger, "nonce mismatch");
            return null;
        }

        if (string.IsNullOrEmpty(token.Subject))
        {
            LogRejected(logger, "no subject");
            return null;
        }

        if (!await TryRedeemAsync(idToken, token.ValidTo, cancellationToken))
        {
            LogRejected(logger, "token already used");
            return null;
        }

        return new ExternalIdentity(source.Name, token.Subject, token.Audiences.First(source.Audiences.Contains));
    }

    private async Task<bool> TryRedeemAsync(string idToken, DateTime validTo, CancellationToken cancellationToken)
    {
        var hash = HashHex(idToken);
        var expiresAt = new DateTimeOffset(DateTime.SpecifyKind(validTo, DateTimeKind.Utc));
        var inserted = await db.Database.ExecuteSqlAsync(
            $"""INSERT INTO "RedeemedIdTokens" ("Hash", "ExpiresAt") VALUES ({hash}, {expiresAt}) ON CONFLICT DO NOTHING""",
            cancellationToken);
        return inserted == 1;
    }

    private static string HashHex(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected a sign-in token: {Reason}")]
    private static partial void LogRejected(ILogger logger, string reason);
}
