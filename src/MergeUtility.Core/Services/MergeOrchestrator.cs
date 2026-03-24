using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.Core.Services;

public class MergeOrchestrator : IMergeOrchestrator
{
    private readonly IFileScanner _fileScanner;
    private readonly IMergeProcessor _mergeProcessor;
    private readonly IReportGenerator _reportGenerator;
    private readonly ProcessedFileLog _processedFileLog;
    private readonly MergeOptions _options;
    private readonly ILogger<MergeOrchestrator> _logger;

    public MergeOrchestrator(
        IFileScanner fileScanner,
        IMergeProcessor mergeProcessor,
        IReportGenerator reportGenerator,
        ProcessedFileLog processedFileLog,
        IOptions<MergeOptions> options,
        ILogger<MergeOrchestrator> logger)
    {
        _fileScanner = fileScanner;
        _mergeProcessor = mergeProcessor;
        _reportGenerator = reportGenerator;
        _processedFileLog = processedFileLog;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var summary = new RunSummary { StartedAt = DateTime.UtcNow };

        SweepTempDirectory();

        _logger.LogInformation("Starting merge run. Source: {Source}", _options.SourceDirectory);

        var files = _fileScanner.Scan().ToList();
        summary.TotalFilesFound = files.Count;
        _logger.LogInformation("Found {Count} file(s) to process", files.Count);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            if (_processedFileLog.IsProcessed(file.FileName))
            {
                _logger.LogInformation("[Skipped] {File} — already processed", file.FileName);
                summary.Skipped++;
                continue;
            }

            var record = await _mergeProcessor.ProcessAsync(file, ct);
            summary.Tally(record.Status);

            var docIdStr = record.TargetDocId.HasValue ? record.TargetDocId.ToString() : "N/A";
            _logger.LogInformation("[{Status}] {File} → DocId: {DocId} ({Duration}ms)",
                record.Status, record.FileName, docIdStr, record.DurationMs);

            if (record.Status == MergeStatus.Success)
                await _processedFileLog.MarkProcessedAsync(file.FileName);
            else if (record.ErrorMessage != null)
                _logger.LogWarning("  Error: {Message}", record.ErrorMessage);

            await _reportGenerator.WriteRecordAsync(record);
        }

        summary.CompletedAt = DateTime.UtcNow;
        await _reportGenerator.WriteSummaryAsync(summary);

        _logger.LogInformation(
            "Run complete. Total: {Total} | Skipped: {Skipped} | Success: {Success} | Failed: {Failed}",
            summary.TotalFilesFound,
            summary.Skipped,
            summary.Success,
            summary.TotalProcessed - summary.Success);
    }

    private void SweepTempDirectory()
    {
        var tempDir = _options.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(tempDir)) return;

        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
            return;
        }

        var staleFiles = Directory.EnumerateFiles(tempDir, "*.pdf").ToList();
        foreach (var stale in staleFiles)
        {
            try { File.Delete(stale); }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not delete stale temp file {File}: {Error}", stale, ex.Message);
            }
        }

        if (staleFiles.Count > 0)
            _logger.LogInformation("Swept {Count} stale temp file(s)", staleFiles.Count);
    }
}
