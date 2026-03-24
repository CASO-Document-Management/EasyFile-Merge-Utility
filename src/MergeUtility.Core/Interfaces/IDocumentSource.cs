namespace MergeUtility.Core.Interfaces;

public interface IDocumentSource
{
    Task<Stream> DownloadAsync(int docId, CancellationToken ct);
    Task ReplaceAsync(int docId, string cabinetName, string mergedFilePath, string comments, CancellationToken ct);
}
