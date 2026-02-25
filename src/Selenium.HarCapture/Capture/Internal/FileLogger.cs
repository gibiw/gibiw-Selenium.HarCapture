using System;
using System.IO;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Thread-safe file logger for diagnostic output.
/// Replaces Debug.WriteLine with configurable file-based logging.
/// </summary>
internal sealed class FileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    private FileLogger(StreamWriter writer)
    {
        _writer = writer;
    }

    /// <summary>
    /// Creates a FileLogger for the given path, or returns null if path is null.
    /// </summary>
    /// <param name="path">The file path to write logs to, or null for no logging.</param>
    /// <returns>A FileLogger instance, or null if path is null.</returns>
    public static FileLogger? Create(string? path)
        => path == null ? null : new FileLogger(new StreamWriter(path, append: true));

    /// <summary>
    /// Writes a timestamped log line to the file.
    /// </summary>
    /// <param name="category">Log category (e.g., "CDP", "INetwork", "HarCapture").</param>
    /// <param name="message">The log message.</param>
    public void Log(string category, string message)
    {
        var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{category}] {message}";
        lock (_lock)
        {
            _writer.WriteLine(line);
            _writer.Flush();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _writer.Dispose();
}
