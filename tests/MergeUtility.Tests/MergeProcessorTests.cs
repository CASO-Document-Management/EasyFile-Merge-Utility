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

    private void SetupCheckout()
    {
        _document.Setup(x => x.CheckoutAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupUndoCheckout()
    {
        _document.Setup(x => x.UndoCheckoutAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
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
    public async Task ProcessAsync_CheckoutFails_ReturnsCheckoutFailedStatus()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        _document.Setup(x => x.CheckoutAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Document already checked out"));

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.CheckoutFailed);
        result.ErrorMessage.Should().Contain("already checked out");
    }

    [Fact]
    public async Task ProcessAsync_DownloadFails_ReturnsDownloadFailedAndUndoesCheckout()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        SetupCheckout();
        SetupUndoCheckout();
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.DownloadFailed);
        result.ErrorMessage.Should().Be("Connection failed");
        _document.Verify(x => x.UndoCheckoutAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_MergeFails_ReturnsMergeFailedAndUndoesCheckout()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        SetupCheckout();
        SetupUndoCheckout();
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _pdfMerge.Setup(x => x.MergeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("PDF open failed"));

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.MergeFailed);
        result.ErrorMessage.Should().Contain("PDF open failed");
        _document.Verify(x => x.UndoCheckoutAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_CheckinFails_ReturnsCheckinFailedAndUndoesCheckout()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        SetupCheckout();
        SetupUndoCheckout();
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _pdfMerge.Setup(x => x.MergeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string b, string a, string o, CancellationToken _) => { File.WriteAllText(o, "merged"); return o; });
        _document.Setup(x => x.CheckinAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Checkin failed"));

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.CheckinFailed);
        _document.Verify(x => x.UndoCheckoutAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_Success_ReturnsSuccessWithDocId()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        SetupCheckout();
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _pdfMerge.Setup(x => x.MergeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string b, string a, string o, CancellationToken _) => { File.WriteAllText(o, "merged"); return o; });
        _document.Setup(x => x.CheckinAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        result.Status.Should().Be(MergeStatus.Success);
        result.TargetDocId.Should().Be(42);
        result.Identifier.Should().Be("ABC123");
        _document.Verify(x => x.UndoCheckoutAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_TempFilesCleanedUpOnFailure()
    {
        _extractor.Setup(x => x.Extract(It.IsAny<string>())).Returns("ABC123");
        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        SetupCheckout();
        SetupUndoCheckout();
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("fail"));

        await CreateProcessor().ProcessAsync(MakeFile(), CancellationToken.None);

        // Only the source file should remain; no temp base_ files
        var tempFiles = Directory.GetFiles(_tempDir, "base_*.pdf");
        tempFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessGroupAsync_Success_SingleCheckoutAndCheckin()
    {
        var files = new PdfFileInfo[]
        {
            new() { FileName = "ABC123_LF001.pdf", FullPath = _sourcePdfPath, SizeBytes = 100 },
            new() { FileName = "ABC123_LF002.pdf", FullPath = _sourcePdfPath, SizeBytes = 100 },
            new() { FileName = "ABC123_LF003.pdf", FullPath = _sourcePdfPath, SizeBytes = 100 },
        };

        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSearchResult(42, "ABC123"));
        SetupCheckout();
        _document.Setup(x => x.DownloadAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _pdfMerge.Setup(x => x.MergeAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string b, IReadOnlyList<string> a, string o, CancellationToken _) => { File.WriteAllText(o, "merged"); return o; });
        _document.Setup(x => x.CheckinAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var results = await CreateProcessor().ProcessGroupAsync("ABC123", files, CancellationToken.None);

        results.Should().HaveCount(3);
        results.Should().OnlyContain(r => r.Status == MergeStatus.Success);
        results.Should().OnlyContain(r => r.TargetDocId == 42);

        // Checkout and checkin called exactly once each
        _document.Verify(x => x.CheckoutAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        _document.Verify(x => x.CheckinAsync(42, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _document.Verify(x => x.DownloadAsync(42, It.IsAny<CancellationToken>()), Times.Once);

        // Batch merge called with all 3 file paths
        _pdfMerge.Verify(x => x.MergeAsync(It.IsAny<string>(),
            It.Is<IReadOnlyList<string>>(list => list.Count == 3),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessGroupAsync_NoMatch_AllRecordsGetNoMatchStatus()
    {
        var files = new PdfFileInfo[]
        {
            new() { FileName = "ABC123_LF001.pdf", FullPath = _sourcePdfPath, SizeBytes = 100 },
            new() { FileName = "ABC123_LF002.pdf", FullPath = _sourcePdfPath, SizeBytes = 100 },
        };

        _cabinet.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataResultResponse { Data = [], TotalCount = 0 });

        var results = await CreateProcessor().ProcessGroupAsync("ABC123", files, CancellationToken.None);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Status == MergeStatus.NoMatch);
        _document.Verify(x => x.CheckoutAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
