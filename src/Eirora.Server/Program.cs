using Eirora.Contracts;
using Eirora.Server.Accounts;
using Eirora.Server.CloudSaves;
using Eirora.Server.Data;
using Eirora.Server.KeyTransfers;
using Eirora.Server.SignIn;
using Eirora.Simulation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// `migrate` applies the database migrations and exits; it runs as its own step before a new version starts (ADR-0008).
// It issues no tokens, so it is only given the connection string (deploy/compose.yaml).
var migrating = args is ["migrate"];

// Secrets arrive as one file per key (Docker Compose secrets), e.g. SignIn__Apple__PrivateKey (ADR-0008).
builder.Configuration.AddKeyPerFile(builder.Configuration["SecretsDirectory"] ?? "/run/secrets", optional: true);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<EiroraDbContext>(options =>
{
    // Password authentication only: GSS would need Kerberos libraries the chiseled image does not have, and Npgsql would
    // otherwise try GSS encryption first on every connection.
    var connectionString = new NpgsqlConnectionStringBuilder(builder.Configuration.GetConnectionString("Eirora"))
    {
        GssEncryptionMode = GssEncryptionMode.Disable,
    };
    options.UseNpgsql(connectionString.ConnectionString);
    options.UseOpenIddict<Guid>();
});

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<EiroraDbContext>().ReplaceDefaultEntities<Guid>())
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("connect/token").SetRevocationEndpointUris("connect/revoke");
        options.AllowCustomFlow(SignInGrant.GrantType).AllowRefreshTokenFlow();

        // The app is the only client and a public one: a client id would not be a secret, so none is required.
        options.AcceptAnonymousClients();
        options.RegisterScopes(Scopes.OfflineAccess);
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15)).SetRefreshTokenLifetime(TimeSpan.FromDays(60));
        AddTokenKeys(options, builder.Configuration, requireKeys: builder.Environment.IsProduction() && !migrating);
        options.UseAspNetCore().EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();

        // Access tokens of a deleted account or a revoked sign-in stop working at once, not when they expire.
        options.EnableTokenEntryValidation();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

builder.Services.AddSingleton(ExternalProviders.FromConfiguration(builder.Configuration.GetSection("SignIn:Providers")));
builder.Services.AddScoped<ExternalTokenValidator>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CloudSaveService>();
builder.Services.AddScoped<KeyTransferService>();
builder.Services.AddOptions<AppleOptions>()
    .Bind(builder.Configuration.GetSection("SignIn:Apple"))
    .Validate(apple => apple.Timeout > TimeSpan.Zero, "SignIn:Apple:Timeout must be greater than zero.")
    .Validate(apple => !apple.IsConfigured || apple.HasReadableKey(), "SignIn:Apple:PrivateKey is not an EC private key in PEM.")
    .ValidateOnStart();
builder.Services.AddOptions<WebSignInOptions>().Bind(builder.Configuration.GetSection("SignIn:Web"));
builder.Services.AddHttpClient<AppleIdClient>((services, http) =>
{
    http.BaseAddress = new Uri(AppleIdClient.Audience + "/");
    http.Timeout = services.GetRequiredService<IOptions<AppleOptions>>().Value.Timeout;
});
builder.Services.AddHostedService<TokenPruning>();
builder.Services.AddHostedService<LockedContributionPruning>();
builder.Services.AddHostedService<KeyTransferPruning>();

// Behind Traefik, which terminates TLS; the server is only reachable through it (deploy/compose.yaml).
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustAll"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (migrating)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<EiroraDbContext>().Database.MigrateAsync();
    return;
}

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapGet("/api/info", () => new ServerInfo(RulesVersion.Current));
app.MapTokenEndpoint();
app.MapAccountEndpoints();
app.MapCloudSaveEndpoints();
app.MapKeyTransferEndpoints();
app.MapAppleWebCallback();
app.MapDeletionPage();

await app.RunAsync();

// The server in production signs and encrypts tokens with keys from secrets; migrate, which issues no tokens, and other
// environments get throwaway keys, so tokens end with the process.
static void AddTokenKeys(OpenIddictServerBuilder options, IConfiguration configuration, bool requireKeys)
{
    var signingKey = configuration["SignIn:TokenSigningKey"];
    var encryptionKey = configuration["SignIn:TokenEncryptionKey"];
    if (!string.IsNullOrEmpty(signingKey) && !string.IsNullOrEmpty(encryptionKey))
    {
        var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportFromPem(signingKey);
        options.AddSigningKey(new RsaSecurityKey(rsa));
        options.AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(encryptionKey)));
        return;
    }

    if (requireKeys)
    {
        throw new InvalidOperationException("SignIn:TokenSigningKey and SignIn:TokenEncryptionKey are required in production.");
    }

    options.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
}
