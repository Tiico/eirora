using System.Net;
using System.Net.Http.Json;
using Eirora.Contracts;
using Eirora.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eirora.Server.Tests;

public sealed class ServerEndpointTests(ServerFixture fixture)
{
    [Fact]
    public async Task HealthEndpointReportsHealthy()
    {
        using var client = fixture.Server.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Every host prunes the shared database as it starts, so a host that a test starts for itself would delete the rows
    // that other tests, running at the same time, have made expired on purpose.
    [Fact]
    public void TheTestServersDoNotPruneTheSharedDatabaseInTheBackground()
    {
        using var other = fixture.Server.WithWebHostBuilder(_ => { });

        foreach (var server in new[] { fixture.Server, other })
        {
            var background = server.Services.GetServices<IHostedService>().Select(service => service.GetType().Name);
            Assert.DoesNotContain(background, name => name.EndsWith("Pruning", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task InfoEndpointReportsTheSharedSimulationRulesVersion()
    {
        using var client = fixture.Server.CreateClient();

        var info = await client.GetFromJsonAsync<ServerInfo>(new Uri("/api/info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.NotNull(info);
        Assert.Equal(RulesVersion.Current, info.RulesVersion);
    }
}
