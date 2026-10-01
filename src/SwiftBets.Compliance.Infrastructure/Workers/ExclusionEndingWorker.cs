using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwiftBets.Compliance.Application;

namespace SwiftBets.Compliance.Infrastructure.Workers;

/// <summary>Once a minute, announces cooling-off and self-exclusion periods that have run out. Safe in every replica.</summary>
public sealed partial class ExclusionEndingWorker(ComplianceHandler handler, TimeProvider time, ILogger<ExclusionEndingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                if (await handler.AnnounceEndedExclusionsAsync(stoppingToken) is > 0 and var count)
                {
                    LogAnnounced(count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Announced {Count} ended exclusions")]
    private partial void LogAnnounced(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Announcing ended exclusions failed; retrying next minute")]
    private partial void LogFailed(Exception ex);
}
