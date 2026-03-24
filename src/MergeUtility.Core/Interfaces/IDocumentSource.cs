namespace MergeUtility.Core.Interfaces;

public interface IDocumentSource
{
    Task<Stream> DownloadAsync(int docId, CancellationToken ct);
    Task CheckoutAsync(int docId, CancellationToken ct);
    Task CheckinAsync(int docId, string mergedFilePath, string comments, CancellationToken ct);
    Task UndoCheckoutAsync(int docId, CancellationToken ct);
}
