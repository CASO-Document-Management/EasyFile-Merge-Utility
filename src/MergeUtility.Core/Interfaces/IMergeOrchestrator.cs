namespace MergeUtility.Core.Interfaces;

public interface IMergeOrchestrator
{
    Task RunAsync(CancellationToken ct);
}
