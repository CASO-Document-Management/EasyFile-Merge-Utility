using Microsoft.Extensions.Options;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.Core.Services;

public class FileScanner : IFileScanner
{
    private readonly MergeOptions _options;

    public FileScanner(IOptions<MergeOptions> options)
    {
        _options = options.Value;
    }

    public IEnumerable<PdfFileInfo> Scan()
    {
        if (!Directory.Exists(_options.SourceDirectory))
            throw new DirectoryNotFoundException($"Source directory not found: {_options.SourceDirectory}");

        return Directory.EnumerateFiles(_options.SourceDirectory, _options.FilePattern, SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new PdfFileInfo
                {
                    FileName = info.Name,
                    FullPath = info.FullName,
                    SizeBytes = info.Length
                };
            });
    }
}
