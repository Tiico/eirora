using System.Collections.Concurrent;
using Eirora.Server.CloudSaves;
using Eirora.Server.KeyTransfers;
using Eirora.Server.SignIn;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eirora.Server.Tests;

/// <summary>
/// The server against the test database, with stand-ins for Apple and Google, every log line captured and no pruning
/// in the background. Without <paramref name="apple"/>, the server is configured with Google only, as when the product
/// owner has no Apple Developer account.
/// </summary>
/// <remarks>
/// The pruning services delete on the real clock, once as the server starts and then every so often, across every
/// account. In a database the tests share, that would be another actor deleting at a moment no test controls: a test
/// that lets a transfer run out would see it vanish whenever the startup pruning was slow enough to come after it.
/// Tests prune by calling the pruning itself.
/// </remarks>
public sealed class ServerFactory(string connectionString, bool apple = true) : WebApplicationFactory<Program>
{
    private static readonly Type[] s_pruning = [typeof(TokenPruning), typeof(LockedContributionPruning), typeof(KeyTransferPruning)];

    public TestProviders Providers { get; } = new();

    public AppleStub Apple { get; } = new();

    public ConcurrentQueue<string> Logs { get; } = new();

    /// <summary>A client over HTTPS, which OpenIddict requires.</summary>
    public Api CreateApi() => new(CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Eirora", connectionString);
        builder.UseSetting("SignIn:Providers:google:Audiences:0", TestProviders.GoogleAudience);
        builder.UseSetting("SignIn:Web:GoogleClientId", TestProviders.GoogleAudience);
        builder.UseSetting("SignIn:Apple:Timeout", "00:00:01");
        if (apple)
        {
            builder.UseSetting("SignIn:Providers:apple:Audiences:0", TestProviders.AppleAudience);
            builder.UseSetting("SignIn:Providers:apple:Audiences:1", TestProviders.AppleServicesId);
            builder.UseSetting("SignIn:Apple:TeamId", AppleStub.TeamId);
            builder.UseSetting("SignIn:Apple:KeyId", AppleStub.KeyId);
            builder.UseSetting("SignIn:Apple:PrivateKey", Apple.PrivateKeyPem);
            builder.UseSetting("SignIn:Web:AppleServicesId", TestProviders.AppleServicesId);
        }

        builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(Logs)));
        builder.ConfigureTestServices(services =>
        {
            // Every host prunes the shared database as it starts, which would delete rows that tests have made expired on
            // purpose, so the tests that cover the pruning run it themselves.
            foreach (var pruning in services.Where(d => d.ImplementationType is { } type && s_pruning.Contains(type)).ToList())
            {
                services.Remove(pruning);
            }

            // The providers the server registered from its configuration, each with a stand-in's signing keys in place of the
            // real provider's discovery document.
            var registered = (ExternalProviders)services.Single(d => d.ServiceType == typeof(ExternalProviders)).ImplementationInstance!;
            services.RemoveAll<ExternalProviders>();
            foreach (var pruning in services.Where(d => d.ServiceType == typeof(IHostedService) && s_pruning.Contains(d.ImplementationType)).ToList())
            {
                services.Remove(pruning);
            }

            services.AddSingleton(Providers.ToExternalProviders(registered.Names));
            services.AddHttpClient<AppleIdClient>().ConfigurePrimaryHttpMessageHandler(Apple.CreateHandler);
        });
    }

    internal sealed class CapturingLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, logs);

        public void Dispose()
        {
        }

        private sealed class Logger(string category, ConcurrentQueue<string> logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                logs.Enqueue($"{logLevel} {category}: {formatter(state, exception)} {exception}");
        }
    }
}
