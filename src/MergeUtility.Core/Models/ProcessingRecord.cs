namespace MergeUtility.Core.Models;

public record ProcessingRecord
{
    public DateTime Timestamp { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string? Identifier { get; init; }
    public int? TargetDocId { get; init; }
    public MergeStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public long DurationMs { get; init; }
}
