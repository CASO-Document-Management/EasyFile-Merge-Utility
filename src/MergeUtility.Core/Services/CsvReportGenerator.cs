using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;
using MergeUtility.Core.Models.Configuration;
using System.Globalization;

namespace MergeUtility.Core.Services;

public class CsvReportGenerator : IReportGenerator, IAsyncDisposable
{
    private readonly string _reportPath;
    private StreamWriter? _writer;
    private CsvWriter? _csv;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _headerWritten;

    public CsvReportGenerator(IOptions<MergeOptions> options)
    {
        _reportPath = Path.Combine(options.Value.WorkingDirectory,
            $"merge-report-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
    }

    public async Task WriteRecordAsync(ProcessingRecord record)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureInitializedAsync();
            _csv!.WriteRecord(record);
            await _csv.NextRecordAsync();
            await _writer!.FlushAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task WriteSummaryAsync(RunSummary summary)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureInitializedAsync();

            await _writer!.WriteLineAsync();
            await _writer.WriteLineAsync("# Run Summary");
            await _writer.WriteLineAsync($"# Started: {summary.StartedAt:O}");
            await _writer.WriteLineAsync($"# Completed: {summary.CompletedAt:O}");
            await _writer.WriteLineAsync($"# Total Files Found: {summary.TotalFilesFound}");
            await _writer.WriteLineAsync($"# Skipped (already processed): {summary.Skipped}");
            await _writer.WriteLineAsync($"# Success: {summary.Success}");
            await _writer.WriteLineAsync($"# No Identifier: {summary.NoIdentifier}");
            await _writer.WriteLineAsync($"# No Match: {summary.NoMatch}");
            await _writer.WriteLineAsync($"# Multiple Matches: {summary.MultipleMatches}");
            await _writer.WriteLineAsync($"# Download Failed: {summary.DownloadFailed}");
            await _writer.WriteLineAsync($"# Merge Failed: {summary.MergeFailed}");
            await _writer.WriteLineAsync($"# Checkout Failed: {summary.CheckoutFailed}");
            await _writer.WriteLineAsync($"# Checkin Failed: {summary.CheckinFailed}");
            await _writer.WriteLineAsync($"# Error: {summary.Error}");
            await _writer.FlushAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_writer != null) return;

        _writer = new StreamWriter(_reportPath, append: false, System.Text.Encoding.UTF8);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };
        _csv = new CsvWriter(_writer, config);

        if (!_headerWritten)
        {
            _csv.WriteHeader<ProcessingRecord>();
            await _csv.NextRecordAsync();
            _headerWritten = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_csv != null) await _csv.DisposeAsync();
        if (_writer != null) await _writer.DisposeAsync();
    }
}
