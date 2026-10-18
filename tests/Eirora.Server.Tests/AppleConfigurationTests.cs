using Eirora.Server.SignIn;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Eirora.Server.Tests;

/// <summary>A broken Sign in with Apple configuration stops the server at startup, not the first account deletion.</summary>
public sealed class AppleConfigurationTests(ServerFixture fixture)
{
    [Theory]
    [InlineData("Timeout", "00:00:00")]
    [InlineData("Timeout", "-00:00:01")]
    [InlineData("PrivateKey", "-----BEGIN PRIVATE KEY-----\nbm90IGEga2V5\n-----END PRIVATE KEY-----")]
    public void TheServerDoesNotStartWithABrokenAppleSetting(string setting, string value)
    {
        using var server = fixture.Server.WithWebHostBuilder(builder => builder.UseSetting("SignIn:Apple:" + setting, value));

        var error = Record.Exception(() => server.CreateClient());

        var invalid = Assert.IsType<OptionsValidationException>(error?.GetBaseException());
        Assert.Equal(typeof(AppleOptions), invalid.OptionsType);
    }
}
