using MergeUtility.Core.Models;

namespace MergeUtility.Core.Interfaces;

public interface IMergeProcessor
{
    Task<ProcessingRecord> ProcessAsync(PdfFileInfo file, CancellationToken ct);
}
