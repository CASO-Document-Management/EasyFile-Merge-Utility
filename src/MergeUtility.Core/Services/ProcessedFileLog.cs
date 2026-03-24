using Microsoft.Extensions.Options;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.Core.Services;

public class ProcessedFileLog
{
    private readonly string _logPath;
    private readonly HashSet<string> _processedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ProcessedFileLog(IOptions<MergeOptions> options)
    {
        _logPath = Path.Combine(options.Value.WorkingDirectory, "processed-files.txt");
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_logPath)) return;

        foreach (var line in File.ReadLines(_logPath))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                _processedFiles.Add(trimmed);
        }
    }

    public bool IsProcessed(string fileName) => _processedFiles.Contains(fileName);

    public async Task MarkProcessedAsync(string fileName)
    {
        await _writeLock.WaitAsync();
        try
        {
            if (_processedFiles.Add(fileName))
                await File.AppendAllTextAsync(_logPath, fileName + Environment.NewLine);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
