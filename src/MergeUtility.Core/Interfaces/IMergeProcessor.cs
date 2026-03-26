using MergeUtility.Core.Models;

namespace MergeUtility.Core.Interfaces;

public interface IMergeProcessor
{
    Task<ProcessingRecord> ProcessAsync(PdfFileInfo file, CancellationToken ct);
    Task<List<ProcessingRecord>> ProcessGroupAsync(string identifier, IReadOnlyList<PdfFileInfo> files, CancellationToken ct);
}
