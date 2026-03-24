using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.Core.Services;

public class MergeProcessor : IMergeProcessor
{
    private readonly IIdentifierExtractor _extractor;
    private readonly ICabinetService _cabinetService;
    private readonly IDocumentSource _documentSource;
    private readonly IPdfMergeService _pdfMergeService;
    private readonly MergeOptions _options;
    private readonly ILogger<MergeProcessor> _logger;

    public MergeProcessor(
        IIdentifierExtractor extractor,
        ICabinetService cabinetService,
        IDocumentSource documentSource,
        IPdfMergeService pdfMergeService,
        IOptions<MergeOptions> options,
        ILogger<MergeProcessor> logger)
    {
        _extractor = extractor;
        _cabinetService = cabinetService;
        _documentSource = documentSource;
        _pdfMergeService = pdfMergeService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProcessingRecord> ProcessAsync(PdfFileInfo file, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var record = new ProcessingRecord { Timestamp = DateTime.UtcNow, FileName = file.FileName };
        string? tempBasePath = null;
        string? tempMergedPath = null;
        var opContext = MergeStatus.Error;

        try
        {
            var identifier = _extractor.Extract(file.FileName);
            if (identifier is null)
                return record with { Status = MergeStatus.NoIdentifier, DurationMs = sw.ElapsedMilliseconds };

            record = record with { Identifier = identifier };

            var searchResult = await _cabinetService.SearchAsync(
                _options.CabinetName, _options.SearchFieldName, identifier, ct);

            var exactMatches = searchResult.Data
                .Where(row => GetStringField(row, _options.SearchFieldName)
                    ?.Equals(identifier, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (exactMatches.Count == 0)
                return record with { Status = MergeStatus.NoMatch, DurationMs = sw.ElapsedMilliseconds };
            if (exactMatches.Count > 1)
                return record with { Status = MergeStatus.MultipleMatches, DurationMs = sw.ElapsedMilliseconds };

            var docId = exactMatches[0].GetProperty("DOC_ID").GetInt32();
            record = record with { TargetDocId = docId };

            opContext = MergeStatus.DownloadFailed;
            tempBasePath = Path.Combine(_options.WorkingDirectory, $"base_{docId}_{Guid.NewGuid():N}.pdf");
            Directory.CreateDirectory(_options.WorkingDirectory);

            await using (var downloadStream = await _documentSource.DownloadAsync(docId, ct))
            await using (var fs = File.Create(tempBasePath))
            {
                await downloadStream.CopyToAsync(fs, ct);
            }

            tempMergedPath = Path.Combine(_options.WorkingDirectory, $"merge_{docId}_{Guid.NewGuid():N}.pdf");

            opContext = MergeStatus.MergeFailed;
            await _pdfMergeService.MergeAsync(tempBasePath, file.FullPath, tempMergedPath, ct);

            opContext = MergeStatus.ReplaceFailed;
            await _documentSource.ReplaceAsync(docId, _options.CabinetName, tempMergedPath,
                $"Merged large format: {file.FileName}", ct);

            return record with { Status = MergeStatus.Success, DurationMs = sw.ElapsedMilliseconds };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error processing {File} at step {Step}", file.FileName, opContext);
            return record with { Status = opContext, ErrorMessage = ex.Message, DurationMs = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {File}", file.FileName);
            return record with { Status = opContext, ErrorMessage = ex.Message, DurationMs = sw.ElapsedMilliseconds };
        }
        finally
        {
            if (tempBasePath is not null && File.Exists(tempBasePath)) File.Delete(tempBasePath);
            if (tempMergedPath is not null && File.Exists(tempMergedPath)) File.Delete(tempMergedPath);
        }
    }

    private static string? GetStringField(JsonElement row, string fieldName)
    {
        if (row.TryGetProperty(fieldName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }
}
