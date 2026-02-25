using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Serialization;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Writes HAR entries incrementally to a file stream using seek-back technique.
/// The file is always valid JSON — after each entry the footer (closing brackets, pages, browser)
/// is written. On the next entry, the writer seeks back to overwrite the footer with the new entry
/// followed by an updated footer.
/// </summary>
/// <remarks>
/// This eliminates OOM issues caused by serializing a large Har object in memory.
/// Each entry is serialized directly to the file stream via <see cref="JsonSerializer.Serialize{T}(Stream, T, JsonSerializerOptions)"/>.
/// Thread-safe via internal locking.
/// </remarks>
internal sealed class HarStreamWriter : IDisposable
{
    private readonly FileStream _stream;
    private readonly JsonSerializerOptions _options;
    private readonly List<HarPage> _pages;
    private readonly FileLogger? _logger;
    private readonly object _lock = new();
    private HarBrowser? _browser;
    private string? _comment;
    private long _footerStartPos;
    private int _entryCount;
    private bool _completed;
    private bool _disposed;

    /// <summary>
    /// Gets the number of entries written so far.
    /// </summary>
    public int Count => _entryCount;

    /// <summary>
    /// Initializes a new <see cref="HarStreamWriter"/> and writes the HAR header to the file.
    /// The file is immediately valid JSON after construction.
    /// </summary>
    /// <param name="filePath">Path to the output HAR file.</param>
    /// <param name="version">HAR format version (e.g., "1.2").</param>
    /// <param name="creator">Creator metadata.</param>
    /// <param name="browser">Optional browser metadata.</param>
    /// <param name="comment">Optional top-level comment.</param>
    /// <param name="initialPages">Optional initial pages to include.</param>
    /// <param name="logger">Optional file logger for diagnostics.</param>
    public HarStreamWriter(
        string filePath,
        string version,
        HarCreator creator,
        HarBrowser? browser = null,
        string? comment = null,
        IList<HarPage>? initialPages = null,
        FileLogger? logger = null)
    {
        _options = HarSerializer.CreateOptions(writeIndented: false);
        _browser = browser;
        _comment = comment;
        _pages = initialPages != null ? new List<HarPage>(initialPages) : new List<HarPage>();
        _logger = logger;

        _stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, bufferSize: 65536);

        // Write header: {"log":{"version":"...","creator":{...},"entries":[
        var header = new StringBuilder();
        header.Append("{\"log\":{\"version\":");
        header.Append(JsonSerializer.Serialize(version, _options));
        header.Append(",\"creator\":");
        header.Append(JsonSerializer.Serialize(creator, _options));
        header.Append(",\"entries\":[");

        var headerBytes = Encoding.UTF8.GetBytes(header.ToString());
        _stream.Write(headerBytes, 0, headerBytes.Length);

        _footerStartPos = _stream.Position;

        WriteFooter();
        _stream.Flush();

        _logger?.Log("HarStreamWriter", $"Initialized: {filePath}");
    }

    /// <summary>
    /// Writes a single HAR entry to the stream. The file remains valid JSON after this call.
    /// </summary>
    /// <param name="entry">The HAR entry to write.</param>
    public void WriteEntry(HarEntry entry)
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            _stream.Position = _footerStartPos;

            if (_entryCount > 0)
            {
                _stream.WriteByte((byte)',');
            }

            JsonSerializer.Serialize(_stream, entry, _options);

            _footerStartPos = _stream.Position;
            _entryCount++;

            WriteFooter();
            _stream.Flush();
        }
    }

    /// <summary>
    /// Adds a page to the HAR and rewrites the footer to include it.
    /// </summary>
    /// <param name="page">The page to add.</param>
    public void AddPage(HarPage page)
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            _pages.Add(page);

            _stream.Position = _footerStartPos;
            WriteFooter();
            _stream.Flush();
        }
    }

    /// <summary>
    /// Marks the stream as completed. The file is already valid; this just flushes final state.
    /// </summary>
    public void Complete()
    {
        lock (_lock)
        {
            if (_completed || _disposed)
                return;

            _completed = true;
            _stream.Flush();
            _logger?.Log("HarStreamWriter", $"Completed: {_entryCount} entries");
        }
    }

    /// <summary>
    /// Writes the closing footer after the entries array.
    /// Must be called with _lock held and _stream.Position at _footerStartPos.
    /// </summary>
    private void WriteFooter()
    {
        var footer = new StringBuilder();
        footer.Append(']');

        if (_pages.Count > 0)
        {
            footer.Append(",\"pages\":");
            footer.Append(JsonSerializer.Serialize(_pages, _options));
        }

        if (_browser != null)
        {
            footer.Append(",\"browser\":");
            footer.Append(JsonSerializer.Serialize(_browser, _options));
        }

        if (_comment != null)
        {
            footer.Append(",\"comment\":");
            footer.Append(JsonSerializer.Serialize(_comment, _options));
        }

        footer.Append("}}");

        var footerBytes = Encoding.UTF8.GetBytes(footer.ToString());
        _stream.Write(footerBytes, 0, footerBytes.Length);
        _stream.SetLength(_stream.Position);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HarStreamWriter));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        if (!_completed)
            Complete();

        _stream.Dispose();
        _disposed = true;
    }
}
