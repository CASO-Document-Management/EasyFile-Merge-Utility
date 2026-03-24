using MergeUtility.Core.Models;

namespace MergeUtility.Core.Interfaces;

public interface IReportGenerator
{
    Task WriteRecordAsync(ProcessingRecord record);
    Task WriteSummaryAsync(RunSummary summary);
}
