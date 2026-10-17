using System.Net;
using System.Net.Http.Json;
using Eirora.Contracts;
using Eirora.Simulation;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Eirora.Server.Tests;

public sealed class ServerEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task HealthEndpointReportsHealthy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InfoEndpointReportsTheSharedSimulationRulesVersion()
    {
        using var client = factory.CreateClient();

        var info = await client.GetFromJsonAsync<ServerInfo>(new Uri("/api/info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.NotNull(info);
        Assert.Equal(RulesVersion.Current, info.RulesVersion);
    }
}
