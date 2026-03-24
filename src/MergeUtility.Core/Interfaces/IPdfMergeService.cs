namespace MergeUtility.Core.Interfaces;

public interface IPdfMergeService
{
    Task<string> MergeAsync(string basePath, string appendPath, string outputPath, CancellationToken ct);
}
