using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;
using MergeUtility.Core.Models.Configuration;
using MergeUtility.Core.Services;

namespace MergeUtility.Tests;

public class MergeOrchestratorTests : IDisposable
{
    private readonly Mock<IFileScanner> _scanner = new();
    private readonly Mock<IMergeProcessor> _processor = new();
    private readonly Mock<IReportGenerator> _report = new();
    private readonly string _tempDir;
    private readonly string _processedLogPath;

    public MergeOrchestratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"OrchestratorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _processedLogPath = Path.Combine(_tempDir, "processed-files.txt");
    }

    private (MergeOrchestrator orchestrator, ProcessedFileLog log) Create(string[]? alreadyProcessed = null)
    {
        if (alreadyProcessed != null)
            File.WriteAllLines(_processedLogPath, alreadyProcessed);

        var options = Options.Create(new MergeOptions
        {
            WorkingDirectory = _tempDir
        });

        var log = new ProcessedFileLog(options);
        var orchestrator = new MergeOrchestrator(
            _scanner.Object,
            _processor.Object,
            _report.Object,
            log,
            options,
            NullLogger<MergeOrchestrator>.Instance);

        return (orchestrator, log);
    }

    private static PdfFileInfo MakeFile(string name) =>
        new() { FileName = name, FullPath = $"/fake/{name}", SizeBytes = 100 };

    [Fact]
    public async Task RunAsync_ZeroFiles_SummaryHasZeroCount()
    {
        _scanner.Setup(x => x.Scan()).Returns([]);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        var (orchestrator, _) = Create();
        await orchestrator.RunAsync(CancellationToken.None);

        _report.Verify(x => x.WriteSummaryAsync(
            It.Is<RunSummary>(s => s.TotalFilesFound == 0)), Times.Once);
        _processor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunAsync_MixedResults_TalliesCorrectly()
    {
        var files = new[]
        {
            MakeFile("file1_LF001.pdf"),
            MakeFile("file2_LF001.pdf"),
            MakeFile("file3_LF001.pdf"),
        };
        _scanner.Setup(x => x.Scan()).Returns(files);

        _processor.SetupSequence(x => x.ProcessAsync(It.IsAny<PdfFileInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessingRecord { Status = MergeStatus.Success, FileName = "file1_LF001.pdf" })
            .ReturnsAsync(new ProcessingRecord { Status = MergeStatus.NoMatch, FileName = "file2_LF001.pdf" })
            .ReturnsAsync(new ProcessingRecord { Status = MergeStatus.DownloadFailed, FileName = "file3_LF001.pdf" });

        _report.Setup(x => x.WriteRecordAsync(It.IsAny<ProcessingRecord>())).Returns(Task.CompletedTask);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        var (orchestrator, _) = Create();
        await orchestrator.RunAsync(CancellationToken.None);

        _report.Verify(x => x.WriteSummaryAsync(It.Is<RunSummary>(s =>
            s.TotalFilesFound == 3 &&
            s.Success == 1 &&
            s.NoMatch == 1 &&
            s.DownloadFailed == 1
        )), Times.Once);
    }

    [Fact]
    public async Task RunAsync_AlreadyProcessedFile_SkipsProcessAsync()
    {
        var files = new[]
        {
            MakeFile("file1_LF001.pdf"),
            MakeFile("file2_LF001.pdf"),
        };
        _scanner.Setup(x => x.Scan()).Returns(files);
        _processor.Setup(x => x.ProcessAsync(It.IsAny<PdfFileInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessingRecord { Status = MergeStatus.Success });
        _report.Setup(x => x.WriteRecordAsync(It.IsAny<ProcessingRecord>())).Returns(Task.CompletedTask);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        // file1 is already processed
        var (orchestrator, _) = Create(alreadyProcessed: ["file1_LF001.pdf"]);
        await orchestrator.RunAsync(CancellationToken.None);

        // ProcessAsync should only be called for file2
        _processor.Verify(x => x.ProcessAsync(
            It.Is<PdfFileInfo>(f => f.FileName == "file2_LF001.pdf"),
            It.IsAny<CancellationToken>()), Times.Once);
        _processor.Verify(x => x.ProcessAsync(
            It.Is<PdfFileInfo>(f => f.FileName == "file1_LF001.pdf"),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_SkippedFilesCountedInSummary()
    {
        var files = new[] { MakeFile("file1_LF001.pdf") };
        _scanner.Setup(x => x.Scan()).Returns(files);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        var (orchestrator, _) = Create(alreadyProcessed: ["file1_LF001.pdf"]);
        await orchestrator.RunAsync(CancellationToken.None);

        _report.Verify(x => x.WriteSummaryAsync(
            It.Is<RunSummary>(s => s.Skipped == 1 && s.TotalFilesFound == 1)), Times.Once);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
