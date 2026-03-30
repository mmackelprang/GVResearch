using Microsoft.Extensions.Logging;

namespace GvResearch.Softphone;

/// <summary>
/// Simple file logger that overwrites the log file on each run.
/// All log entries are appended with timestamps.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public FileLoggerProvider(string filePath)
    {
        // Overwrite on each run so the file always has the latest session
        _writer = new StreamWriter(filePath, append: false) { AutoFlush = true };
        _writer.WriteLine($"=== GVResearch Softphone log — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        _writer.WriteLine();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal void WriteEntry(string category, LogLevel level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level,-12}] {category}: {message}";
        lock (_lock)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}

internal sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var msg = formatter(state, exception);
        if (exception is not null)
            msg += Environment.NewLine + exception;

        provider.WriteEntry(category, logLevel, msg);
    }
}
