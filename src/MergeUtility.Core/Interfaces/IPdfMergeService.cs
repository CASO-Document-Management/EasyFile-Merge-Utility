namespace MergeUtility.Core.Interfaces;

public interface IPdfMergeService
{
    Task<string> MergeAsync(string basePath, string appendPath, string outputPath, CancellationToken ct);
    Task<string> MergeAsync(string basePath, IReadOnlyList<string> appendPaths, string outputPath, CancellationToken ct);
}
