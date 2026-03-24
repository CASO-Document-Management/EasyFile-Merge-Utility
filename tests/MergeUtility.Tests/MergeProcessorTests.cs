using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;
using MergeUtility.Core.Models.Configuration;
using MergeUtility.Core.Services;

namespace MergeUtility.Tests;

public class MergeProcessorTests : IDisposable
{
    private readonly Mock<IIdentifierExtractor> _extractor = new();
    private readonly Mock<ICabinetService> _cabinet = new();
    private readonly Mock<IDocumentSource> _document = new();
    private readonly Mock<IPdfMergeService> _pdfMerge = new();
    private readonly string _tempDir;
    private readonly string _sourcePdfPath;

    public MergeProcessorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ProcessorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create a dummy source PDF file
        _sourcePdfPath = Path.Combine(_tempDir, "ABC123_LF001.pdf");
        File.WriteAllText(_sourcePdfPath, "dummy");
    }

    private MergeProcessor CreateProcessor()
    {
        var options = Options.Create(new MergeOptions
        {
            CabinetName = "TEST_CAB",
            SearchFieldName = "DocumentKey",
            WorkingDirectory = _tempDir
        });
        return new MergeProcessor(
            _extractor.Object,
            _cabinet.Object,
            _document.Object,
            _pdfMerge.Object,
            options,
            NullLogger<MergeProcessor>.Instance);
    }

    private PdfFileInfo MakeFile(string name = "ABC123_LF001.pdf") =>
        new() { FileName = name, FullPath = _sourcePdfPath, SizeBytes = 100 };

    private static DataResultResponse MakeSearchResult(int docId, string keyValue)
    {
        var json = $"{{\"DOC_ID\":{docId},\"DocumentKey\":\"{keyValue}\"}}";
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        return new DataResultResponse { Data = [element], TotalCount = 1 };
    }

    [Fact]
    public async Task ProcessAsync_NoIdentifier_ReturnsNoIdentifierStatus()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns((string?)null);

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.NoIdentifier);
        _cabinet.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_NoMatch_ReturnsNoMatchStatus()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataResultResponse { Data = [], TotalCount = 0 });

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.NoMatch);
    }

    [Fact]
    public async Task ProcessAsync_MultipleMatches_ReturnsMultipleMatchesStatus()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");

        var json1 = "{\"DOC_ID\":1,\"DocumentKey\":\"ABC123\"}";
        var json2 = "{\"DOC_ID\":2,\"DocumentKey\":\"ABC123\"}";
        var result1 = new DataResultResponse
        {
            Data = [JsonSerializer.Deserialize<JsonElement>(json1), JsonSerializer.Deserialize<JsonElement>(json2)],
            TotalCount = 2
        };
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result1);

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.MultipleMatches);
    }

    [Fact]
    public async Task ProcessAsync_DownloadFails_ReturnsDownloadFailedStatus()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.DownloadFailed);
        result.ErrorMessage.Should().Be("Connection failed");
    }

    [Fact]
    public async Task ProcessAsync_MergeFails_ReturnsMergeFailedStatus()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _pdfMerge.Setup(x => x.MergeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("PDF open failed"));

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.MergeFailed);
        result.ErrorMessage.Should().Contain("PDF open failed");
    }

    [Fact]
    public async Task ProcessAsync_ReplaceFails_ReturnsReplaceFailedStatus()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _pdfMerge.Setup(x => x.MergeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string b, string a, string o, CancellationToken _) => { File.WriteAllText(o, "merged"); return o; });
        _document.Setup(x => x.ReplaceAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Replace failed"));

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.ReplaceFailed);
    }

    [Fact]
    public async Task ProcessAsync_Success_ReturnsSuccessWithDocId()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _pdfMerge.Setup(x => x.MergeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string b, string a, string o, CancellationToken _) => { File.WriteAllText(o, "merged"); return o; });
        _document.Setup(x => x.ReplaceAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.Success);
        result.TargetDocId.Should().Be(42);
        result.Identifier.Should().Be("ABC123");
    }

    [Fact]
    public async Task ProcessAsync_TempFilesCleanedUpOnFailure()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("fail"));

        await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        // Only the source file should remain; no temp base_ files
        var tempFiles = Directory.GetFiles(_tempDir, "base_*.pdf");
        tempFiles.Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
