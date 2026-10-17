using System.Net.Http.Json;
using Eirora.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Eirora.Server.Tests;

public class StatusEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Status_reports_the_api_version()
    {
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<ServerStatus>(new Uri("/status", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.NotNull(status);
        Assert.Equal(ServerStatus.CurrentApiVersion, status.ApiVersion);
    }
}
