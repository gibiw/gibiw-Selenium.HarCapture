using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
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
/// Thread-safe via internal Channel-based producer-consumer pattern.
/// </remarks>
internal sealed class HarStreamWriter : IDisposable, IAsyncDisposable
{
    private readonly FileStream _stream;
    private readonly JsonSerializerOptions _options;
    private readonly List<HarPage> _pages;
    private readonly FileLogger? _logger;
    private readonly Channel<WriteOperation> _channel;
    private readonly Task _consumerTask;
    private readonly CancellationTokenSource _cts;
    private HarBrowser? _browser;
    private string? _comment;
    private long _footerStartPos;
    private int _entryCount;
    private bool _completed;
    private bool _disposed;

    /// <summary>
    /// Gets the number of entries written so far.
    /// Note: This count reflects entries processed by the background consumer.
    /// Entries posted via WriteEntry may not be reflected immediately.
    /// </summary>
    public int Count => _entryCount;

    /// <summary>
    /// Waits for all queued operations to be processed by the background consumer.
    /// Posts a sentinel flush operation to the channel and awaits its completion,
    /// guaranteeing all preceding entries have been consumed.
    /// </summary>
    internal async Task WaitForConsumerAsync(TimeSpan? timeout = null)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite(WriteOperation.CreateFlush(tcs)))
        {
            return; // channel already completed, consumer has drained
        }

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(effectiveTimeout)).ConfigureAwait(false);
        if (completed != tcs.Task)
        {
            throw new TimeoutException("WaitForConsumerAsync timed out waiting for consumer to drain.");
        }
    }

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

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

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

        // Initialize channel and start background consumer
        _channel = Channel.CreateUnbounded<WriteOperation>(
            new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true,
                AllowSynchronousContinuations = false
            });
        _cts = new CancellationTokenSource();
        _consumerTask = Task.Run(() => ConsumeAsync(_cts.Token));
    }

    /// <summary>
    /// Writes a single HAR entry to the stream. The file remains valid JSON after this call.
    /// </summary>
    /// <param name="entry">The HAR entry to write.</param>
    public void WriteEntry(HarEntry entry)
    {
        ThrowIfDisposed();
        if (!_channel.Writer.TryWrite(WriteOperation.CreateEntry(entry)))
        {
            _logger?.Log("HarStreamWriter", "WriteEntry: channel completed, entry dropped");
        }
    }

    /// <summary>
    /// Adds a page to the HAR and rewrites the footer to include it.
    /// </summary>
    /// <param name="page">The page to add.</param>
    public void AddPage(HarPage page)
    {
        ThrowIfDisposed();
        _pages.Add(page);
        if (!_channel.Writer.TryWrite(WriteOperation.CreatePage(page)))
        {
            _logger?.Log("HarStreamWriter", "AddPage: channel completed, page dropped");
        }
    }

    /// <summary>
    /// Marks the stream as completed. The file is already valid; this just flushes final state.
    /// </summary>
    public void Complete()
    {
        if (_completed) return;
        _completed = true;
        _channel.Writer.TryComplete();
        _logger?.Log("HarStreamWriter", $"Complete: channel closed, {_entryCount} entries written so far");
    }

    /// <summary>
    /// Background consumer task that drains the channel and writes operations to the stream.
    /// </summary>
    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (_channel.Reader.TryRead(out var operation))
                {
                    try
                    {
                        ProcessOperation(operation);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log("HarStreamWriter", $"ProcessOperation failed: {ex.Message}");
                    }
                }
            }
            _logger?.Log("HarStreamWriter", "Consumer completed normally");
        }
        catch (OperationCanceledException)
        {
            // Drain remaining items after cancellation
            while (_channel.Reader.TryRead(out var operation))
            {
                try { ProcessOperation(operation); }
                catch { /* best effort during shutdown */ }
            }
            _logger?.Log("HarStreamWriter", "Consumer canceled, drained remaining items");
        }
    }

    /// <summary>
    /// Processes a single write operation (Entry or Page).
    /// </summary>
    private void ProcessOperation(WriteOperation operation)
    {
        switch (operation.Type)
        {
            case WriteOperation.OpType.Entry:
                WriteEntryToStream(operation.Entry!);
                break;
            case WriteOperation.OpType.Page:
                WritePageToStream();
                break;
            case WriteOperation.OpType.Flush:
                operation.FlushTcs?.TrySetResult(true);
                break;
        }
    }

    /// <summary>
    /// Writes an entry to the stream using seek-back technique.
    /// Called only by consumer task (no locking needed).
    /// </summary>
    private void WriteEntryToStream(HarEntry entry)
    {
        _stream.Position = _footerStartPos;
        if (_entryCount > 0) _stream.WriteByte((byte)',');
        JsonSerializer.Serialize(_stream, entry, _options);
        _footerStartPos = _stream.Position;
        _entryCount++;
        WriteFooter();
        _stream.Flush();
    }

    /// <summary>
    /// Rewrites the footer to include updated pages.
    /// Called only by consumer task (no locking needed).
    /// </summary>
    private void WritePageToStream()
    {
        _stream.Position = _footerStartPos;
        WriteFooter();
        _stream.Flush();
    }

    /// <summary>
    /// Writes the closing footer after the entries array.
    /// Must be called with _stream.Position at _footerStartPos.
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
        _disposed = true;

        // Complete channel and cancel consumer
        _channel.Writer.TryComplete();
        _cts.Cancel();

        // Best effort: wait briefly for consumer to drain
        try
        {
            if (!_consumerTask.Wait(TimeSpan.FromSeconds(5)))
            {
                _logger?.Log("HarStreamWriter", "Dispose: consumer did not drain within timeout, some entries may be lost");
            }
        }
        catch { /* swallow exceptions during sync disposal */ }

        _stream.Flush();
        _stream.Dispose();
        _cts.Dispose();

        _logger?.Log("HarStreamWriter", $"Dispose: sync disposal completed, {_entryCount} entries");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Phase 1: Signal no more writes
        _channel.Writer.TryComplete();

        // Phase 2: Wait for consumer to drain all items
        try
        {
            await _consumerTask.ConfigureAwait(false);
            _logger?.Log("HarStreamWriter", $"DisposeAsync: consumer drained, {_entryCount} entries written");
        }
        catch (OperationCanceledException) { /* normal */ }
        catch (Exception ex)
        {
            _logger?.Log("HarStreamWriter", $"DisposeAsync: consumer failed: {ex.Message}");
        }

        // Phase 3: Final flush and dispose resources
        _stream.Flush();
        _stream.Dispose();
        _cts.Dispose();

        GC.SuppressFinalize(this);
    }
}
