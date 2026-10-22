namespace Eirora.Server.CloudSaves;

/// <summary>Deletes locked contribution parts once they have been kept for 90 days, every few hours (ADR-0008).</summary>
public sealed partial class LockedContributionPruning(IServiceScopeFactory scopes, TimeProvider time, ILogger<LockedContributionPruning> logger)
    : BackgroundService
{
    private static readonly TimeSpan s_interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(s_interval, time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CloudSaveService>().PruneLockedAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pruning locked contributions failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
