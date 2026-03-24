using FluentAssertions;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Models.Configuration;
using MergeUtility.Core.Services;

namespace MergeUtility.Tests;

public class FileScannerTests : IDisposable
{
    private readonly string _tempDir;

    public FileScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileScannerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    private FileScanner CreateScanner(string pattern = "*_LF*.pdf")
    {
        var options = Options.Create(new MergeOptions
        {
            SourceDirectory = _tempDir,
            FilePattern = pattern
        });
        return new FileScanner(options);
    }

    [Fact]
    public void Scan_EmptyDirectory_ReturnsEmptyList()
    {
        var scanner = CreateScanner();
        scanner.Scan().Should().BeEmpty();
    }

    [Fact]
    public void Scan_OnlyPdfFiles_ReturnsAllMatching()
    {
        File.WriteAllText(Path.Combine(_tempDir, "doc1_LF001.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "doc2_LF001.pdf"), "");

        var scanner = CreateScanner();
        scanner.Scan().Should().HaveCount(2);
    }

    [Fact]
    public void Scan_MixedFileTypes_ReturnsOnlyMatchingPdfs()
    {
        File.WriteAllText(Path.Combine(_tempDir, "doc1_LF001.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "doc1.docx"), "");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "");
        File.WriteAllText(Path.Combine(_tempDir, "regular.pdf"), "");

        var scanner = CreateScanner();
        var results = scanner.Scan().ToList();

        results.Should().HaveCount(1);
        results[0].FileName.Should().Be("doc1_LF001.pdf");
    }

    [Fact]
    public void Scan_Subdirectory_IsIncluded()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllText(Path.Combine(subDir.FullName, "doc1_LF001.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "doc2_LF001.pdf"), "");

        var scanner = CreateScanner();
        var results = scanner.Scan().ToList();

        results.Should().HaveCount(2);
    }

    [Fact]
    public void Scan_PopulatesFileProperties()
    {
        var filePath = Path.Combine(_tempDir, "ABC123_LF001.pdf");
        File.WriteAllText(filePath, "test content");

        var scanner = CreateScanner();
        var result = scanner.Scan().Single();

        result.FileName.Should().Be("ABC123_LF001.pdf");
        result.FullPath.Should().Be(filePath);
        result.SizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Scan_ReturnsFilesOrderedByIdentifierThenLfNumber()
    {
        // Create files deliberately out of order
        File.WriteAllText(Path.Combine(_tempDir, "doc_LF003.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "doc_LF001.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "doc_LF002.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "alpha_LF002.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "alpha_LF001.pdf"), "");

        var scanner = CreateScanner();
        var results = scanner.Scan().Select(f => f.FileName).ToList();

        results.Should().ContainInOrder(
            "alpha_LF001.pdf",
            "alpha_LF002.pdf",
            "doc_LF001.pdf",
            "doc_LF002.pdf",
            "doc_LF003.pdf");
    }

    [Fact]
    public void Scan_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        var options = Options.Create(new MergeOptions
        {
            SourceDirectory = @"C:\does\not\exist",
            FilePattern = "*_LF*.pdf"
        });
        var scanner = new FileScanner(options);

        var act = () => scanner.Scan().ToList();
        act.Should().Throw<DirectoryNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
