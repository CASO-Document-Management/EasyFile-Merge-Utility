namespace MergeUtility.Core.Models;

public class PdfFileInfo
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}
