using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MergeUtility.Core.Interfaces;

namespace MergeUtility.Console;

public class MergeWorker : BackgroundService
{
    private readonly IMergeOrchestrator _orchestrator;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<MergeWorker> _logger;

    public MergeWorker(
        IMergeOrchestrator orchestrator,
        IHostApplicationLifetime lifetime,
        ILogger<MergeWorker> logger)
    {
        _orchestrator = orchestrator;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _orchestrator.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Merge run cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Unhandled exception in merge worker");
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
