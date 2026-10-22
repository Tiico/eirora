namespace Eirora.Server.KeyTransfers;

/// <summary>Deletes key transfers whose ten minutes have run out, every quarter of an hour (ADR-0008).</summary>
public sealed partial class KeyTransferPruning(IServiceScopeFactory scopes, TimeProvider time, ILogger<KeyTransferPruning> logger) : BackgroundService
{
    private static readonly TimeSpan s_interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(s_interval, time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<KeyTransferService>().PruneAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFailed(logger, e);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pruning key transfers failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
