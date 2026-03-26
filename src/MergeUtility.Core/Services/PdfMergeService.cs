using System.Diagnostics;
using MergeUtility.Core.Interfaces;

namespace MergeUtility.Core.Services;

public class PdfMergeService : IPdfMergeService
{
    public Task<string> MergeAsync(string basePath, string appendPath, string outputPath, CancellationToken ct)
        => MergeAsync(basePath, new[] { appendPath }, outputPath, ct);

    public async Task<string> MergeAsync(string basePath, IReadOnlyList<string> appendPaths, string outputPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = @"C:\Program Files\qpdf 12.3.2\bin\qpdf.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use ArgumentList to avoid shell quoting issues with Windows drive letters (E:\)
        // which qpdf misinterprets as page range specifiers
        psi.ArgumentList.Add("--empty");
        psi.ArgumentList.Add("--pages");
        psi.ArgumentList.Add(basePath);
        foreach (var path in appendPaths)
            psi.ArgumentList.Add(path);
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = psi };

        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0 && process.ExitCode != 3) // 3 = warnings (non-fatal)
        {
            throw new InvalidOperationException($"qpdf merge failed (exit {process.ExitCode}): {stderr}");
        }

        return outputPath;
    }
}
