using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eirora.Server.Tests;

/// <summary>
/// A broken Sign in with Apple configuration stops the server at startup, not the first account deletion. Apple is either
/// configured in full or not at all; a server with none of its settings runs with Google only (<see cref="GoogleOnlyTests"/>).
/// </summary>
public sealed class AppleConfigurationTests(ServerFixture fixture)
{
    private const string PartlyConfigured = "Sign in with Apple is partly configured";
    private const string Audiences = "SignIn:Providers:apple:Audiences";

    [Theory]
    [InlineData("Timeout", "00:00:00", "SignIn:Apple:Timeout must be greater than zero.")]
    [InlineData("Timeout", "-00:00:01", "SignIn:Apple:Timeout must be greater than zero.")]
    [InlineData("PrivateKey", "-----BEGIN PRIVATE KEY-----\nbm90IGEga2V5\n-----END PRIVATE KEY-----", "SignIn:Apple:PrivateKey is not an EC private key in PEM.")]
    public void TheServerDoesNotStartWithABrokenAppleSetting(string setting, string value, string expectedFailure) =>
        Assert.Contains(expectedFailure, Failure(fixture.Server, new() { ["SignIn:Apple:" + setting] = value }), StringComparison.Ordinal);

    public static TheoryData<string> AppleSettings =>
    [
        Audiences,
        "SignIn:Web:AppleServicesId",
        "SignIn:Apple:TeamId",
        "SignIn:Apple:KeyId",
        "SignIn:Apple:PrivateKey",
    ];

    [Theory]
    [MemberData(nameof(AppleSettings))]
    public void TheServerDoesNotStartWithOneAppleSettingMissing(string missing)
    {
        var settings = missing == Audiences
            ? new Dictionary<string, string> { [Audiences + ":0"] = "", [Audiences + ":1"] = "" }
            : new Dictionary<string, string> { [missing] = "" };

        var failure = Failure(fixture.Server, settings);

        Assert.Contains(PartlyConfigured, failure, StringComparison.Ordinal);
        Assert.Contains($"Missing: {missing}.", failure, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AppleSettings))]
    public void TheServerWithGoogleOnlyDoesNotStartWithASingleAppleSetting(string setting)
    {
        var settings = setting switch
        {
            Audiences => new Dictionary<string, string> { [Audiences + ":0"] = TestProviders.AppleAudience },
            "SignIn:Apple:PrivateKey" => new Dictionary<string, string> { [setting] = fixture.Server.Apple.PrivateKeyPem },
            _ => new Dictionary<string, string> { [setting] = "value" },
        };

        var failure = Failure(fixture.GoogleOnlyServer, settings);

        Assert.Contains(PartlyConfigured, failure, StringComparison.Ordinal);
        var missing = failure[failure.IndexOf("Missing: ", StringComparison.Ordinal)..];
        Assert.All(AppleSettings, other => Assert.Equal(other != setting, missing.Contains(other, StringComparison.Ordinal)));
    }

    [Fact]
    public void TheServerDoesNotStartWhenTheServicesIdIsNotOneOfApplesAudiences() =>
        Assert.Contains(
            "SignIn:Web:AppleServicesId must be one of SignIn:Providers:apple:Audiences.",
            Failure(fixture.Server, new() { ["SignIn:Web:AppleServicesId"] = "se.eirora.app.other" }),
            StringComparison.Ordinal);

    [Fact]
    public void TheServerWithGoogleOnlyStillChecksTheAppleTimeout() =>
        Assert.Contains(
            "SignIn:Apple:Timeout must be greater than zero.",
            Failure(fixture.GoogleOnlyServer, new() { ["SignIn:Apple:Timeout"] = "00:00:00" }),
            StringComparison.Ordinal);

    /// <summary>The log line that says why the server did not start with <paramref name="settings"/>.</summary>
    private static string Failure(ServerFactory baseServer, Dictionary<string, string> settings)
    {
        var logs = new ConcurrentQueue<string>();
        using var server = baseServer.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureLogging(logging => logging.AddProvider(new ServerFactory.CapturingLoggerProvider(logs)));
        });

        // What CreateClient throws is not the failure itself: the host starts on another thread, and when it fails and is
        // disposed there, the factory's own use of it can throw ObjectDisposedException first. The host logs the real
        // failure before it goes down, so the log tells why the server did not start.
        Assert.NotNull(Record.Exception(() => server.CreateClient()));

        return Assert.Single(logs, line =>
            line.Contains("Hosting failed to start", StringComparison.Ordinal)
            && line.Contains(nameof(OptionsValidationException), StringComparison.Ordinal));
    }
}
