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
                continue;
            }

            _logger.LogInformation("Processing group '{Identifier}' ({Count} file(s))",
                identifier, groupFiles.Count);

            var groupRecords = new List<ProcessingRecord>();
            var groupSuccess = true;

            foreach (var file in groupFiles)
            {
                ct.ThrowIfCancellationRequested();

                if (_processedFileLog.IsProcessed(file.FileName))
                {
                    _logger.LogInformation("[Skipped] {File} — already processed", file.FileName);
                    summary.Skipped++;
                    continue;
                }

                var record = await _mergeProcessor.ProcessAsync(file, ct);
                groupRecords.Add(record);
                summary.Tally(record.Status);

                var docIdStr = record.TargetDocId.HasValue ? record.TargetDocId.ToString() : "N/A";
                _logger.LogInformation("[{Status}] {File} → DocId: {DocId} ({Duration}ms)",
                    record.Status, record.FileName, docIdStr, record.DurationMs);

                if (record.ErrorMessage != null)
                    _logger.LogWarning("  Error: {Message}", record.ErrorMessage);

                await _reportGenerator.WriteRecordAsync(record);

                if (record.Status != MergeStatus.Success)
                {
                    groupSuccess = false;
                    _logger.LogWarning("Group '{Identifier}' failed at {File} — skipping remaining files in group",
                        identifier, file.FileName);
                    break;
                }
            }

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
