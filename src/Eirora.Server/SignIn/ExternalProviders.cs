using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Eirora.Server.SignIn;

/// <summary>Configuration of one sign-in provider, section <c>SignIn:Providers:{name}</c>.</summary>
public sealed class ExternalProviderOptions
{
    /// <summary>The provider's OpenID Connect discovery document, where its signing keys are found.</summary>
    public string MetadataAddress { get; set; } = "";

    public List<string> Issuers { get; set; } = [];

    /// <summary>The client ids the app signs in with (bundle id, services id, OAuth client ids); none disables the provider.</summary>
    public List<string> Audiences { get; set; } = [];
}

/// <summary>A provider whose ID tokens the server accepts.</summary>
public sealed record ExternalProvider(
    string Name,
    IReadOnlyList<string> Issuers,
    IReadOnlyList<string> Audiences,
    BaseConfigurationManager Configuration);

/// <summary>The enabled sign-in providers by name (one of <see cref="Contracts.LoginProviders"/>).</summary>
public sealed class ExternalProviders(IEnumerable<ExternalProvider> providers)
{
    private readonly Dictionary<string, ExternalProvider> _providers = providers.ToDictionary(p => p.Name, StringComparer.Ordinal);

    public bool TryGet(string? name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ExternalProvider? provider)
    {
        provider = null;
        return name is not null && _providers.TryGetValue(name, out provider);
    }

    /// <summary>Reads the providers from configuration; the signing keys are fetched and cached from each discovery document.</summary>
    public static ExternalProviders FromConfiguration(IConfiguration section)
    {
        var providers = new List<ExternalProvider>();
        foreach (var child in section.GetChildren())
        {
            var options = child.Get<ExternalProviderOptions>() ?? new ExternalProviderOptions();
            if (options.Audiences.Count == 0)
            {
                continue;
            }

            var configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
                options.MetadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true });
            providers.Add(new ExternalProvider(child.Key, options.Issuers, options.Audiences, configuration));
        }

        return new ExternalProviders(providers);
    }
}
