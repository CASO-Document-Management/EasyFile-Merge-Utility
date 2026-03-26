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
    private readonly Mock<IIdentifierExtractor> _extractor = new();
    private readonly Mock<IMergeProcessor> _processor = new();
    private readonly Mock<IReportGenerator> _report = new();
    private readonly string _tempDir;
    private readonly string _processedLogPath;

    public MergeOrchestratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"OrchestratorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _processedLogPath = Path.Combine(_tempDir, "processed-files.txt");

        // Default: extract identifier by stripping _LF###.pdf suffix
        _extractor.Setup(x => x.Extract(It.IsAny<string>()))
            .Returns((string fn) =>
            {
                var idx = fn.IndexOf("_LF", StringComparison.OrdinalIgnoreCase);
                return idx > 0 ? fn[..idx] : null;
            });
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
            _extractor.Object,
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
    public async Task RunAsync_DifferentGroups_TalliedIndependently()
    {
        // Three files, each with a different identifier → three separate groups
        var files = new[]
        {
            MakeFile("file1_LF001.pdf"),
            MakeFile("file2_LF001.pdf"),
            MakeFile("file3_LF001.pdf"),
        };
        _scanner.Setup(x => x.Scan()).Returns(files);

        _processor.Setup(x => x.ProcessGroupAsync("file1", It.IsAny<IReadOnlyList<PdfFileInfo>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingRecord> { new() { Status = MergeStatus.Success, FileName = "file1_LF001.pdf" } });
        _processor.Setup(x => x.ProcessGroupAsync("file2", It.IsAny<IReadOnlyList<PdfFileInfo>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingRecord> { new() { Status = MergeStatus.NoMatch, FileName = "file2_LF001.pdf" } });
        _processor.Setup(x => x.ProcessGroupAsync("file3", It.IsAny<IReadOnlyList<PdfFileInfo>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingRecord> { new() { Status = MergeStatus.DownloadFailed, FileName = "file3_LF001.pdf" } });

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
    public async Task RunAsync_AlreadyProcessedGroup_SkipsEntireGroup()
    {
        var files = new[]
        {
            MakeFile("doc_LF001.pdf"),
            MakeFile("doc_LF002.pdf"),
        };
        _scanner.Setup(x => x.Scan()).Returns(files);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        var (orchestrator, _) = Create(alreadyProcessed: ["doc_LF001.pdf", "doc_LF002.pdf"]);
        await orchestrator.RunAsync(CancellationToken.None);

        _processor.Verify(x => x.ProcessGroupAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PdfFileInfo>>(), It.IsAny<CancellationToken>()), Times.Never);
        _report.Verify(x => x.WriteSummaryAsync(
            It.Is<RunSummary>(s => s.Skipped == 2 && s.TotalFilesFound == 2)), Times.Once);
    }

    [Fact]
    public async Task RunAsync_GroupSuccess_AllFilesMarkedProcessed()
    {
        // Two files with same identifier → one group
        var files = new[]
        {
            MakeFile("doc_LF001.pdf"),
            MakeFile("doc_LF002.pdf"),
        };
        _scanner.Setup(x => x.Scan()).Returns(files);

        _processor.Setup(x => x.ProcessGroupAsync("doc", It.IsAny<IReadOnlyList<PdfFileInfo>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingRecord>
            {
                new() { Status = MergeStatus.Success, FileName = "doc_LF001.pdf" },
                new() { Status = MergeStatus.Success, FileName = "doc_LF002.pdf" },
            });

        _report.Setup(x => x.WriteRecordAsync(It.IsAny<ProcessingRecord>())).Returns(Task.CompletedTask);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        var (orchestrator, log) = Create();
        await orchestrator.RunAsync(CancellationToken.None);

        log.IsProcessed("doc_LF001.pdf").Should().BeTrue();
        log.IsProcessed("doc_LF002.pdf").Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_GroupFailure_NoFilesMarkedProcessed()
    {
        // Two files with same identifier, group fails
        var files = new[]
        {
            MakeFile("doc_LF001.pdf"),
            MakeFile("doc_LF002.pdf"),
        };
        _scanner.Setup(x => x.Scan()).Returns(files);

        _processor.Setup(x => x.ProcessGroupAsync("doc", It.IsAny<IReadOnlyList<PdfFileInfo>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingRecord>
            {
                new() { Status = MergeStatus.MergeFailed, FileName = "doc_LF001.pdf", ErrorMessage = "PDF error" },
                new() { Status = MergeStatus.MergeFailed, FileName = "doc_LF002.pdf", ErrorMessage = "PDF error" },
            });

        _report.Setup(x => x.WriteRecordAsync(It.IsAny<ProcessingRecord>())).Returns(Task.CompletedTask);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        var (orchestrator, log) = Create();
        await orchestrator.RunAsync(CancellationToken.None);

        // Neither file should be marked processed since the group failed
        log.IsProcessed("doc_LF001.pdf").Should().BeFalse();
        log.IsProcessed("doc_LF002.pdf").Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_GroupProcessedAsOneUnit()
    {
        // Three files with same identifier — ProcessGroupAsync called once with all files
        var files = new[]
        {
            MakeFile("doc_LF001.pdf"),
            MakeFile("doc_LF002.pdf"),
            MakeFile("doc_LF003.pdf"),
        };
        _scanner.Setup(x => x.Scan()).Returns(files);

        _processor.Setup(x => x.ProcessGroupAsync("doc", It.IsAny<IReadOnlyList<PdfFileInfo>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, IReadOnlyList<PdfFileInfo> f, CancellationToken _) =>
                f.Select(fi => new ProcessingRecord { Status = MergeStatus.Success, FileName = fi.FileName }).ToList());

        _report.Setup(x => x.WriteRecordAsync(It.IsAny<ProcessingRecord>())).Returns(Task.CompletedTask);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        var (orchestrator, _) = Create();
        await orchestrator.RunAsync(CancellationToken.None);

        // ProcessGroupAsync called exactly once with all 3 files
        _processor.Verify(x => x.ProcessGroupAsync("doc",
            It.Is<IReadOnlyList<PdfFileInfo>>(list => list.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_MixedGroups_FailedGroupDoesNotAffectOther()
    {
        var files = new[]
        {
            MakeFile("alpha_LF001.pdf"),  // group "alpha"
            MakeFile("beta_LF001.pdf"),   // group "beta"
        };
        _scanner.Setup(x => x.Scan()).Returns(files);

        // alpha fails, beta succeeds
        _processor.Setup(x => x.ProcessGroupAsync("alpha", It.IsAny<IReadOnlyList<PdfFileInfo>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingRecord> { new() { Status = MergeStatus.DownloadFailed, FileName = "alpha_LF001.pdf" } });
        _processor.Setup(x => x.ProcessGroupAsync("beta", It.IsAny<IReadOnlyList<PdfFileInfo>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingRecord> { new() { Status = MergeStatus.Success, FileName = "beta_LF001.pdf" } });

        _report.Setup(x => x.WriteRecordAsync(It.IsAny<ProcessingRecord>())).Returns(Task.CompletedTask);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        var (orchestrator, log) = Create();
        await orchestrator.RunAsync(CancellationToken.None);

        log.IsProcessed("alpha_LF001.pdf").Should().BeFalse();
        log.IsProcessed("beta_LF001.pdf").Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_NoIdentifierFiles_ReportedAsNoIdentifier()
    {
        _extractor.Setup(x => x.Extract("badfile.pdf")).Returns((string?)null);

        var files = new[] { MakeFile("badfile.pdf") };
        _scanner.Setup(x => x.Scan()).Returns(files);
        _report.Setup(x => x.WriteRecordAsync(It.IsAny<ProcessingRecord>())).Returns(Task.CompletedTask);
        _report.Setup(x => x.WriteSummaryAsync(It.IsAny<RunSummary>())).Returns(Task.CompletedTask);

        var (orchestrator, _) = Create();
        await orchestrator.RunAsync(CancellationToken.None);

        _report.Verify(x => x.WriteRecordAsync(
            It.Is<ProcessingRecord>(r => r.Status == MergeStatus.NoIdentifier)), Times.Once);
        _processor.VerifyNoOtherCalls();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
