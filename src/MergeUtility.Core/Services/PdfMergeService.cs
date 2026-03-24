using MergeUtility.Core.Interfaces;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace MergeUtility.Core.Services;

public class PdfMergeService : IPdfMergeService
{
    public async Task<string> MergeAsync(string basePath, string appendPath, string outputPath, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            using var outputDoc = new PdfDocument();

            using var baseDoc = PdfReader.Open(basePath, PdfDocumentOpenMode.Import);
            foreach (var page in baseDoc.Pages)
                outputDoc.AddPage(page);

            using var appendDoc = PdfReader.Open(appendPath, PdfDocumentOpenMode.Import);
            foreach (var page in appendDoc.Pages)
                outputDoc.AddPage(page);

            outputDoc.Save(outputPath);
        }, ct);

        return outputPath;
    }
}
