using System.Collections.Concurrent;
using Eirora.Server.SignIn;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eirora.Server.Tests;

/// <summary>A broken Sign in with Apple configuration stops the server at startup, not the first account deletion.</summary>
public sealed class AppleConfigurationTests(ServerFixture fixture)
{
    [Theory]
    [InlineData("Timeout", "00:00:00", "SignIn:Apple:Timeout must be greater than zero.")]
    [InlineData("Timeout", "-00:00:01", "SignIn:Apple:Timeout must be greater than zero.")]
    [InlineData("PrivateKey", "-----BEGIN PRIVATE KEY-----\nbm90IGEga2V5\n-----END PRIVATE KEY-----", "SignIn:Apple:PrivateKey is not an EC private key in PEM.")]
    public void TheServerDoesNotStartWithABrokenAppleSetting(string setting, string value, string expectedFailure)
    {
        var logs = new ConcurrentQueue<string>();
        using var server = fixture.Server.WithWebHostBuilder(builder => builder
            .UseSetting("SignIn:Apple:" + setting, value)
            .ConfigureLogging(logging => logging.AddProvider(new ServerFactory.CapturingLoggerProvider(logs))));

        // What CreateClient throws is not the failure itself: the host starts on another thread, and when it fails and is
        // disposed there, the factory's own use of it can throw ObjectDisposedException first. The host logs the real
        // failure before it goes down, so the log tells why the server did not start.
        Assert.NotNull(Record.Exception(() => server.CreateClient()));

        Assert.Contains(logs, line =>
            line.Contains("Hosting failed to start", StringComparison.Ordinal)
            && line.Contains(nameof(OptionsValidationException), StringComparison.Ordinal)
            && line.Contains(expectedFailure, StringComparison.Ordinal));
    }
}
