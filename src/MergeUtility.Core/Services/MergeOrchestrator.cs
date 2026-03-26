using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.Core.Services;

public class MergeOrchestrator : IMergeOrchestrator
{
    private readonly IFileScanner _fileScanner;
    private readonly IIdentifierExtractor _extractor;
    private readonly IMergeProcessor _mergeProcessor;
    private readonly IReportGenerator _reportGenerator;
    private readonly ProcessedFileLog _processedFileLog;
    private readonly MergeOptions _options;
    private readonly ILogger<MergeOrchestrator> _logger;

    public MergeOrchestrator(
        IFileScanner fileScanner,
        IIdentifierExtractor extractor,
        IMergeProcessor mergeProcessor,
        IReportGenerator reportGenerator,
        ProcessedFileLog processedFileLog,
        IOptions<MergeOptions> options,
        ILogger<MergeOrchestrator> logger)
    {
        _fileScanner = fileScanner;
        _extractor = extractor;
        _mergeProcessor = mergeProcessor;
        _reportGenerator = reportGenerator;
        _processedFileLog = processedFileLog;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var summary = new RunSummary { StartedAt = DateTime.UtcNow };
        var groupSummaries = new List<(string Identifier, int? DocId, int FileCount, MergeStatus Status, string? Error)>();

        SweepTempDirectory();

        _logger.LogInformation("Starting merge run. Source: {Source}", _options.SourceDirectory);

        var files = _fileScanner.Scan().ToList();
        summary.TotalFilesFound = files.Count;
        _logger.LogInformation("Found {Count} file(s) to process", files.Count);

        // Group files by extracted identifier — files sharing a key value form one atomic unit
        var tagged = files.Select(f => (File: f, Id: _extractor.Extract(f.FileName))).ToList();

        // Process files with no extractable identifier individually
        foreach (var (file, _) in tagged.Where(x => x.Id == null))
        {
            ct.ThrowIfCancellationRequested();
            var record = new ProcessingRecord
            {
                Timestamp = DateTime.UtcNow,
                FileName = file.FileName,
                Status = MergeStatus.NoIdentifier
            };
            summary.Tally(record.Status);
            _logger.LogInformation("[{Status}] {File} → DocId: N/A (0ms)", record.Status, record.FileName);
            await _reportGenerator.WriteRecordAsync(record);
        }

        // Group files with valid identifiers
        var groups = tagged
            .Where(x => x.Id != null)
            .GroupBy(x => x.Id!)
            .ToList();

        _logger.LogInformation("Grouped into {GroupCount} document group(s)", groups.Count);

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();

            var identifier = group.Key;
            var groupFiles = group.Select(g => g.File).ToList();

            // Skip entire group if all files are already processed
            if (groupFiles.All(f => _processedFileLog.IsProcessed(f.FileName)))
            {
                foreach (var f in groupFiles)
                {
                    _logger.LogInformation("[Skipped] {File} — already processed", f.FileName);
                    summary.Skipped++;
                }
                groupSummaries.Add((identifier, null, groupFiles.Count, MergeStatus.Success, "Skipped"));
                continue;
            }

            _logger.LogInformation("Processing group '{Identifier}' ({Count} file(s))",
                identifier, groupFiles.Count);

            var groupRecords = await _mergeProcessor.ProcessGroupAsync(identifier, groupFiles, ct);
            var groupSuccess = groupRecords.All(r => r.Status == MergeStatus.Success);
            var firstRecord = groupRecords[0];

            foreach (var record in groupRecords)
            {
                summary.Tally(record.Status);

                var docIdStr = record.TargetDocId.HasValue ? record.TargetDocId.ToString() : "N/A";
                _logger.LogInformation("[{Status}] {File} → DocId: {DocId} ({Duration}ms)",
                    record.Status, record.FileName, docIdStr, record.DurationMs);

                if (record.ErrorMessage != null)
                    _logger.LogWarning("  Error: {Message}", record.ErrorMessage);

                await _reportGenerator.WriteRecordAsync(record);
            }

            if (!groupSuccess)
                _logger.LogWarning("Group '{Identifier}' failed", identifier);

            groupSummaries.Add((identifier, firstRecord.TargetDocId, groupFiles.Count,
                firstRecord.Status, firstRecord.ErrorMessage));

            // Only mark files as processed if the entire group succeeded
            if (groupSuccess)
            {
                foreach (var record in groupRecords)
                    await _processedFileLog.MarkProcessedAsync(record.FileName);
            }
        }

        summary.CompletedAt = DateTime.UtcNow;
        await _reportGenerator.WriteSummaryAsync(summary);

        _logger.LogInformation(
            "Run complete. Total: {Total} | Skipped: {Skipped} | Success: {Success} | Failed: {Failed}",
            summary.TotalFilesFound,
            summary.Skipped,
            summary.Success,
            summary.TotalProcessed - summary.Success);

        await WriteSummaryFileAsync(summary, groupSummaries);
    }

    private async Task WriteSummaryFileAsync(
        RunSummary summary,
        List<(string Identifier, int? DocId, int FileCount, MergeStatus Status, string? Error)> groupSummaries)
    {
        try
        {
            var path = Path.Combine(_options.SourceDirectory,
                $"merge-summary-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

            using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync($"EasyFile Merge Summary — {summary.StartedAt:u}");
            await writer.WriteLineAsync($"Source: {_options.SourceDirectory}");
            await writer.WriteLineAsync($"Cabinet: {_options.CabinetName}");
            await writer.WriteLineAsync();

            await writer.WriteLineAsync(
                $"{"Document",-30} {"DocId",-10} {"Pages",-7} {"Status",-15} {"Error"}");
            await writer.WriteLineAsync(
                $"{"".PadRight(30, '-')}  {"".PadRight(8, '-')}  {"".PadRight(5, '-')}  {"".PadRight(13, '-')}  {"".PadRight(30, '-')}");

            foreach (var (identifier, docId, fileCount, status, error) in groupSummaries)
            {
                var docIdStr = docId.HasValue ? docId.Value.ToString() : "N/A";
                var errorStr = error ?? "";
                await writer.WriteLineAsync(
                    $"{identifier,-30} {docIdStr,-10} {fileCount,-7} {status,-15} {errorStr}");
            }

            await writer.WriteLineAsync();
            var failed = summary.TotalProcessed - summary.Success;
            await writer.WriteLineAsync(
                $"Total: {summary.TotalFilesFound} files | {groupSummaries.Count} groups | " +
                $"Success: {summary.Success} | Failed: {failed} | Skipped: {summary.Skipped}");

            _logger.LogInformation("Summary written to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write summary file to source directory");
        }
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
