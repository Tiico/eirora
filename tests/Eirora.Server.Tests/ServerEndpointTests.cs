using System.Net;
using System.Net.Http.Json;
using Eirora.Contracts;
using Eirora.Simulation;

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

    [Fact]
    public async Task InfoEndpointReportsTheSharedSimulationRulesVersion()
    {
        using var client = fixture.Server.CreateClient();

        var info = await client.GetFromJsonAsync<ServerInfo>(new Uri("/api/info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.NotNull(info);
        Assert.Equal(RulesVersion.Current, info.RulesVersion);
    }
}
