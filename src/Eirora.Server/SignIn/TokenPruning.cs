using Eirora.Server.Data;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Eirora.Server.SignIn;

/// <summary>Removes ended token entries and expired redeemed provider tokens every few hours.</summary>
public sealed partial class TokenPruning(IServiceScopeFactory scopes, TimeProvider time, ILogger<TokenPruning> logger) : BackgroundService
{
    private static readonly TimeSpan s_interval = TimeSpan.FromHours(6);

    // Entries are kept a while after they end, so that reuse of a redeemed refresh token is still detected.
    private static readonly TimeSpan s_retention = TimeSpan.FromDays(14);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(s_interval, time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var services = scope.ServiceProvider;
                var now = time.GetUtcNow();
                await services.GetRequiredService<IOpenIddictTokenManager>().PruneAsync(now - s_retention, stoppingToken);
                await services.GetRequiredService<IOpenIddictAuthorizationManager>().PruneAsync(now - s_retention, stoppingToken);
                await services.GetRequiredService<EiroraDbContext>().RedeemedIdTokens
                    .Where(t => t.ExpiresAt < now)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pruning tokens failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
