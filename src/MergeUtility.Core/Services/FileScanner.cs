using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.Core.Services;

public class FileScanner : IFileScanner
{
    private static readonly Regex LfNumberRegex = new(@"_LF(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly MergeOptions _options;

    public FileScanner(IOptions<MergeOptions> options)
    {
        _options = options.Value;
    }

    public IEnumerable<PdfFileInfo> Scan()
    {
        if (!Directory.Exists(_options.SourceDirectory))
            throw new DirectoryNotFoundException($"Source directory not found: {_options.SourceDirectory}");

        return Directory.EnumerateFiles(_options.SourceDirectory, _options.FilePattern, SearchOption.AllDirectories)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new PdfFileInfo
                {
                    FileName = info.Name,
                    FullPath = info.FullName,
                    SizeBytes = info.Length
                };
            })
            .OrderBy(f => LfNumberRegex.Replace(f.FileName, ""), StringComparer.OrdinalIgnoreCase)
            .ThenBy(f =>
            {
                var m = LfNumberRegex.Match(f.FileName);
                return m.Success ? int.Parse(m.Groups[1].Value) : 0;
            });
    }
}
