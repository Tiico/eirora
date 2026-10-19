using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Eirora.Contracts;
using Eirora.Server.SignIn;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Eirora.Server.Tests;

/// <summary>
/// Stand-ins for Apple and Google: each has its own signing key, and issues ID tokens the way the real providers do,
/// with the SHA-256 of the client's nonce as lowercase hex.
/// </summary>
public sealed class TestProviders
{
    public const string AppleAudience = "se.eirora.app";
    public const string GoogleAudience = "eirora-test.apps.googleusercontent.com";

    private static readonly JsonWebTokenHandler s_handler = new();

    private readonly Dictionary<string, Provider> _providers = new(StringComparer.Ordinal)
    {
        [LoginProviders.Apple] = new("https://appleid.apple.com", AppleAudience, NewKey("apple")),
        [LoginProviders.Google] = new("https://accounts.google.com", GoogleAudience, NewKey("google")),
    };

    public ExternalProviders ToExternalProviders() => new(_providers.Select(p => new ExternalProvider(
        p.Key,
        [p.Value.Issuer],
        [p.Value.Audience],
        new StaticConfigurationManager<OpenIdConnectConfiguration>(Configuration(p.Value.Key)))));

    /// <summary>A fresh sign-in for a player, by default a new one.</summary>
    public PlayerLogin SignIn(string provider, string? subject = null)
    {
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        subject ??= "player-" + Guid.NewGuid().ToString("N");
        return new PlayerLogin(provider, subject, nonce, IdToken(provider, subject, HashHex(nonce)));
    }

    /// <summary>
    /// A fresh sign-in with the same login, as the client sends it to confirm changing the account, with Apple's
    /// authorization code when deleting.
    /// </summary>
    public ExternalLogin Again(PlayerLogin login, string? authorizationCode = null) =>
        SignIn(login.Provider, login.Subject).ToExternalLogin(authorizationCode);

    public string IdToken(
        string provider,
        string subject,
        string? nonceHash,
        string? audience = null,
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        var source = _providers[provider];
        var now = DateTime.UtcNow;
        var claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = subject };
        if (nonceHash is not null)
        {
            claims["nonce"] = nonceHash;
        }

        var expiresAt = expires ?? now.AddMinutes(10);
        return s_handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = source.Issuer,
            Audience = audience ?? source.Audience,
            Claims = claims,
            IssuedAt = expiresAt.AddMinutes(-10),
            NotBefore = expiresAt.AddMinutes(-10),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(signingKey ?? source.Key, SecurityAlgorithms.RsaSha256),
        });
    }

    public static string HashHex(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static RsaSecurityKey NewKey(string keyId) => new(RSA.Create(2048)) { KeyId = keyId };

    private static OpenIdConnectConfiguration Configuration(SecurityKey key)
    {
        var configuration = new OpenIdConnectConfiguration();
        configuration.SigningKeys.Add(key);
        return configuration;
    }

    private sealed record Provider(string Issuer, string Audience, RsaSecurityKey Key);
}

/// <summary>A sign-in as the client sends it: the provider's ID token and the nonce it was requested with.</summary>
public sealed record PlayerLogin(string Provider, string Subject, string Nonce, string IdToken)
{
    public ExternalLogin ToExternalLogin(string? authorizationCode = null) => new(Provider, IdToken, Nonce, authorizationCode);
}
