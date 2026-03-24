using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MergeUtility.Console;

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly Lock _writeLock = new();
    private StreamWriter? _writer;
    private string? _currentDate;

    public FileLoggerProvider(string logDirectory, int retainDays = 30)
    {
        _logDirectory = logDirectory;
        PurgeOldLogs(retainDays);
    }

    private void PurgeOldLogs(int retainDays)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-retainDays);
            foreach (var file in Directory.EnumerateFiles(_logDirectory, "merge-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { /* best-effort cleanup */ }
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    internal void WriteEntry(string message)
    {
        lock (_writeLock)
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            if (_writer is null || _currentDate != today)
            {
                _writer?.Dispose();
                var path = Path.Combine(_logDirectory, $"merge-{today}.log");
                _writer = new StreamWriter(path, append: true) { AutoFlush = true };
                _currentDate = today;
            }

            _writer.WriteLine(message);
        }
    }

    public void Dispose()
    {
        lock (_writeLock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}

internal sealed class FileLogger : ILogger
{
    private readonly string _category;
    private readonly FileLoggerProvider _provider;

    public FileLogger(string category, FileLoggerProvider provider)
    {
        _category = category;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var level = logLevel switch
        {
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRI",
            _ => logLevel.ToString()[..3].ToUpperInvariant()
        };

        var shortCategory = _category.Contains('.')
            ? _category[(_category.LastIndexOf('.') + 1)..]
            : _category;

        var message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {shortCategory}: {formatter(state, exception)}";
        if (exception is not null)
            message += Environment.NewLine + exception;

        _provider.WriteEntry(message);
    }
}
