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
/// in the background.
/// </summary>
/// <remarks>
/// The pruning services delete on the real clock, once as the server starts and then every so often, across every
/// account. In a database the tests share, that would be another actor deleting at a moment no test controls: a test
/// that lets a transfer run out would see it vanish whenever the startup pruning was slow enough to come after it.
/// Tests prune by calling the pruning itself.
/// </remarks>
public sealed class ServerFactory(string connectionString) : WebApplicationFactory<Program>
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
        builder.UseSetting("SignIn:Apple:TeamId", AppleStub.TeamId);
        builder.UseSetting("SignIn:Apple:KeyId", AppleStub.KeyId);
        builder.UseSetting("SignIn:Apple:PrivateKey", Apple.PrivateKeyPem);
        builder.UseSetting("SignIn:Apple:Timeout", "00:00:01");
        builder.UseSetting("SignIn:Web:AppleServicesId", TestProviders.AppleServicesId);
        builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(Logs)));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ExternalProviders>();
            foreach (var pruning in services.Where(d => d.ServiceType == typeof(IHostedService) && s_pruning.Contains(d.ImplementationType)).ToList())
            {
                services.Remove(pruning);
            }

            services.AddSingleton(Providers.ToExternalProviders());
            services.AddHttpClient<AppleIdClient>().ConfigurePrimaryHttpMessageHandler(Apple.CreateHandler);
        });
    }

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
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
