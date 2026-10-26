using Microsoft.Extensions.Options;

namespace Eirora.Server.SignIn;

/// <summary>
/// Whether Sign in with Apple is on. Its settings are spread over three sections, and it is on when any of them is set:
/// all of them are then required, which <see cref="AppleOptionsValidation"/> checks at startup. With none of them, the
/// server runs with Google only, as it does until the product owner has an Apple Developer account.
/// </summary>
/// <remarks>
/// An empty value counts as not set, since Compose passes an unset variable as an empty one and the deployment's empty
/// placeholder for the key file as an empty key (deploy/README.md).
/// </remarks>
public static class AppleSignIn
{
    public const string Audiences = "SignIn:Providers:apple:Audiences";
    public const string ServicesId = "SignIn:Web:AppleServicesId";
    public const string TeamId = "SignIn:Apple:TeamId";
    public const string KeyId = "SignIn:Apple:KeyId";
    public const string PrivateKey = "SignIn:Apple:PrivateKey";

    private static readonly string[] s_settings = [Audiences, ServicesId, TeamId, KeyId, PrivateKey];

    public static bool IsEnabled(IConfiguration configuration) => s_settings.Any(setting => IsSet(configuration, setting));

    /// <summary>The settings Sign in with Apple needs that are not set.</summary>
    public static IReadOnlyList<string> Missing(IConfiguration configuration) =>
        [.. s_settings.Where(setting => !IsSet(configuration, setting))];

    /// <summary>The settings Sign in with Apple needs, as a sentence: "A, B and C".</summary>
    public static string Settings => string.Join(", ", s_settings[..^1]) + " and " + s_settings[^1];

    /// <summary>The client ids Apple's ID tokens may be issued to, without empty ones.</summary>
    public static IReadOnlyList<string> AudiencesOf(IConfiguration configuration) =>
        [.. configuration.GetSection(Audiences).GetChildren().Select(child => child.Value).OfType<string>().Where(value => value.Length > 0)];

    private static bool IsSet(IConfiguration configuration, string setting) =>
        setting == Audiences ? AudiencesOf(configuration).Count > 0 : !string.IsNullOrEmpty(configuration[setting]);
}

/// <summary>
/// Checks Sign in with Apple's configuration at startup, so that a broken one stops the server instead of the first
/// sign-in or account deletion with Apple.
/// </summary>
public sealed class AppleOptionsValidation(IConfiguration configuration) : IValidateOptions<AppleOptions>
{
    public ValidateOptionsResult Validate(string? name, AppleOptions options)
    {
        var failures = new List<string>();
        if (options.Timeout <= TimeSpan.Zero)
        {
            failures.Add("SignIn:Apple:Timeout must be greater than zero.");
        }

        if (AppleSignIn.IsEnabled(configuration))
        {
            if (AppleSignIn.Missing(configuration) is { Count: > 0 } missing)
            {
                failures.Add(
                    $"Sign in with Apple is partly configured: set all of {AppleSignIn.Settings} to turn it on, or none of " +
                    $"them to turn it off. Missing: {string.Join(", ", missing)}.");
            }
            else
            {
                if (!options.HasReadableKey())
                {
                    failures.Add("SignIn:Apple:PrivateKey is not an EC private key in PEM.");
                }

                if (!AppleSignIn.AudiencesOf(configuration).Contains(configuration[AppleSignIn.ServicesId], StringComparer.Ordinal))
                {
                    failures.Add($"{AppleSignIn.ServicesId} must be one of {AppleSignIn.Audiences}.");
                }
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
