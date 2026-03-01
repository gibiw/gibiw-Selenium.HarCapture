using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using Selenium.HarCapture.Capture.Internal;
using Selenium.HarCapture.Capture.Strategies;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Serialization;

namespace Selenium.HarCapture.Capture;

/// <summary>
/// Public orchestrator for HAR capture sessions.
/// Manages capture lifecycle (Start/Stop), multi-page captures (NewPage), and snapshot retrieval (GetHar).
/// </summary>
/// <remarks>
/// This is the main class users interact with to capture network traffic.
/// It coordinates between a capture strategy (CDP or INetwork) and the HAR data model.
/// Thread-safe for concurrent operations using internal locking.
/// </remarks>
public sealed class HarCaptureSession : IDisposable, IAsyncDisposable
{
    private readonly CaptureOptions _options;
    private readonly UrlPatternMatcher _urlMatcher;
    private readonly FileLogger? _logger;
    private readonly object _lock = new object();
    private readonly string? _browserName;
    private readonly string? _browserVersion;
    private INetworkCaptureStrategy? _strategy;
    private HarStreamWriter? _streamWriter;
    private Har _har = null!;
    private string? _currentPageRef;
    private string? _finalOutputFilePath;
    private bool _isCapturing;
    private bool _disposed;
    private volatile bool _isPaused;

    /// <summary>
    /// Gets a value indicating whether capture is currently active.
    /// </summary>
    public bool IsCapturing => _isCapturing;

    /// <summary>
    /// Gets a value indicating whether capture is currently paused.
    /// When paused, new entries from the network are dropped rather than recorded.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Fires after each HAR entry has been written, outside the internal lock.
    /// The event argument contains the total entry count, current page reference, and entry URL.
    /// </summary>
    /// <remarks>
    /// Handlers may safely call <see cref="GetHar"/> from within this event — the event is raised
    /// outside the internal lock to prevent deadlocks.
    /// </remarks>
    public event EventHandler<HarCaptureProgress>? EntryWritten;

    /// <summary>
    /// Gets the name of the active capture strategy (e.g., "CDP", "INetwork").
    /// Returns null if no strategy is configured.
    /// </summary>
    public string? ActiveStrategyName => _strategy?.StrategyName;

    /// <summary>
    /// Gets the file logger for diagnostic logging.
    /// Used internally by HarCapture for StopAndSave logging.
    /// </summary>
    internal FileLogger? Logger => _logger;

    /// <summary>
    /// Gets whether the session is in streaming mode (writing entries to file incrementally).
    /// </summary>
    internal bool IsStreamingMode => _streamWriter != null;

    /// <summary>
    /// Gets the configured output file path, if any.
    /// </summary>
    internal string? OutputFilePath => _options.OutputFilePath;

    /// <summary>
    /// Gets the actual output file path after capture is stopped.
    /// When compression is enabled, this returns the .gz path instead of the original.
    /// Falls back to <see cref="OutputFilePath"/> if capture hasn't been stopped yet.
    /// </summary>
    internal string? FinalOutputFilePath => _finalOutputFilePath ?? _options.OutputFilePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="HarCaptureSession"/> class without a strategy.
    /// </summary>
    /// <param name="options">Configuration options for capture behavior. If null, default options are used.</param>
    /// <remarks>
    /// This constructor is primarily for testing or scenarios where the strategy will be set later.
    /// For production use, strategy creation will be available in Phase 3/4.
    /// </remarks>
    public HarCaptureSession(CaptureOptions? options = null)
    {
        _options = options ?? new CaptureOptions();
        _urlMatcher = new UrlPatternMatcher(_options.UrlIncludePatterns, _options.UrlExcludePatterns);
        _logger = FileLogger.Create(_options.LogFilePath);
    }

    /// <summary>
    /// Initializes a new instance with automatic strategy selection based on driver capabilities.
    /// Uses CDP if available, falls back to INetwork if CDP session creation fails.
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture network traffic from.</param>
    /// <param name="options">Configuration options for capture behavior. If null, default options are used.</param>
    /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the driver does not support network capture (no IDevTools).</exception>
    public HarCaptureSession(IWebDriver driver, CaptureOptions? options = null)
    {
        if (driver == null)
        {
            throw new ArgumentNullException(nameof(driver));
        }

        _options = options ?? new CaptureOptions();
        _urlMatcher = new UrlPatternMatcher(_options.UrlIncludePatterns, _options.UrlExcludePatterns);
        _logger = FileLogger.Create(_options.LogFilePath);

        // Extract browser info: override takes precedence over auto-detection
        if (_options.BrowserName != null)
        {
            _browserName = _options.BrowserName;
            _browserVersion = _options.BrowserVersion;
        }
        else
        {
            var (name, version) = BrowserCapabilityExtractor.Extract(driver);
            _browserName = name;
            _browserVersion = version;
        }

        _strategy = StrategyFactory.Create(driver, _options, _logger);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HarCaptureSession"/> class with a specific strategy.
    /// </summary>
    /// <param name="strategy">The network capture strategy to use.</param>
    /// <param name="options">Configuration options for capture behavior. If null, default options are used.</param>
    /// <remarks>
    /// This constructor is internal for use by factory methods and unit tests (via InternalsVisibleTo).
    /// </remarks>
    internal HarCaptureSession(INetworkCaptureStrategy strategy, CaptureOptions? options = null)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _options = options ?? new CaptureOptions();
        _urlMatcher = new UrlPatternMatcher(_options.UrlIncludePatterns, _options.UrlExcludePatterns);
        _logger = FileLogger.Create(_options.LogFilePath);

        if (_options.BrowserName != null)
        {
            _browserName = _options.BrowserName;
            _browserVersion = _options.BrowserVersion;
        }
    }

    /// <summary>
    /// Asynchronously starts network traffic capture.
    /// </summary>
    /// <param name="initialPageRef">Optional page reference ID for the initial page. If provided, creates the first page in the HAR.</param>
    /// <param name="initialPageTitle">Optional page title for the initial page. Used only if initialPageRef is provided.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the session has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is already started or no strategy is configured.</exception>
    public async Task StartAsync(string? initialPageRef = null, string? initialPageTitle = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HarCaptureSession));
        }

        if (_isCapturing)
        {
            throw new InvalidOperationException("Capture is already started.");
        }

        if (_strategy == null)
        {
            throw new InvalidOperationException("No capture strategy configured. Use the constructor that accepts IWebDriver for automatic strategy selection.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        CaptureOptionsValidator.ValidateAndThrow(_options);

        _logger?.Log("HarCapture", $"StartAsync: strategy={_strategy.StrategyName}, captureTypes={_options.CaptureTypes}, maxBodySize={_options.MaxResponseBodySize}");
        _logger?.Log("HarCapture", $"URL filtering: include={_options.UrlIncludePatterns?.Count ?? 0}, exclude={_options.UrlExcludePatterns?.Count ?? 0}");

        _strategy.EntryCompleted += OnEntryCompleted;
        InitializeHar(initialPageRef, initialPageTitle);

        if (_options.OutputFilePath != null)
        {
            var dir = Path.GetDirectoryName(_options.OutputFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _streamWriter = new HarStreamWriter(
                _options.OutputFilePath,
                _har.Log.Version, _har.Log.Creator,
                _har.Log.Browser, _har.Log.Comment,
                _har.Log.Pages, _logger,
                _options.CustomMetadata, _options.MaxOutputFileSize);
            _logger?.Log("HarCapture", $"Streaming mode: {_options.OutputFilePath}");
        }

        if (initialPageRef != null)
        {
            _logger?.Log("HarCapture", $"Initial page: ref={initialPageRef}, title={initialPageTitle}");
        }

        await _strategy.StartAsync(_options, cancellationToken).ConfigureAwait(false);
        _isCapturing = true;
        _logger?.Log("HarCapture", "Capture started");
    }

    /// <summary>
    /// Synchronously starts network traffic capture.
    /// </summary>
    /// <param name="initialPageRef">Optional page reference ID for the initial page. If provided, creates the first page in the HAR.</param>
    /// <param name="initialPageTitle">Optional page title for the initial page. Used only if initialPageRef is provided.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the session has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is already started or no strategy is configured.</exception>
    public void Start(string? initialPageRef = null, string? initialPageTitle = null)
    {
        StartAsync(initialPageRef, initialPageTitle).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously stops network traffic capture and returns the final HAR object.
    /// </summary>
    /// <returns>A task that represents the asynchronous stop operation. The task result contains the final HAR object.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the session has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    public async Task<Har> StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HarCaptureSession));
        }

        if (!_isCapturing)
        {
            throw new InvalidOperationException("Capture is not started.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger?.Log("HarCapture", "StopAsync called");

        // Read timing values BEFORE StopAsync — CdpNetworkCaptureStrategy.StopAsync resets them to null
        var domContentLoadedMs = _strategy!.LastDomContentLoadedTimestamp;
        var loadMs = _strategy!.LastLoadTimestamp;

        await _strategy!.StopAsync(cancellationToken).ConfigureAwait(false);
        _isCapturing = false;
        _strategy.EntryCompleted -= OnEntryCompleted;

        ApplyPageTimings(domContentLoadedMs, loadMs);

        if (_streamWriter != null)
        {
            // Complete signals no more writes, DisposeAsync drains remaining entries
            _streamWriter.Complete();
            await _streamWriter.DisposeAsync().ConfigureAwait(false);
            _logger?.Log("HarCapture", $"Streaming completed: {_streamWriter.Count} entries, {_har.Log.Pages?.Count ?? 0} pages");

            // Log truncation notice (read IsTruncated before nulling the writer)
            if (_streamWriter.IsTruncated)
            {
                _logger?.Log("HarCapture", $"Output file was truncated — MaxOutputFileSize limit exceeded after {_streamWriter.Count} entries");
            }

            _streamWriter = null;

            // Post-finalization compression: compress the uncompressed HAR file to .gz
            if (_options.EnableCompression && _options.OutputFilePath != null)
            {
                var sourcePath = _options.OutputFilePath;
                var compressedPath = sourcePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    ? sourcePath
                    : sourcePath + ".gz";

                _logger?.Log("HarCapture", $"Compressing: {sourcePath} -> {compressedPath}");

                cancellationToken.ThrowIfCancellationRequested();

                using (var inputStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: 65536, useAsync: true))
                using (var outputStream = new FileStream(compressedPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true))
                using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    // netstandard2.0 CopyToAsync doesn't have CancellationToken overload — use default buffer size
                    await inputStream.CopyToAsync(gzipStream, 81920).ConfigureAwait(false);
                    // No explicit Flush on GZipStream — Dispose handles footer writing (research pitfall #1)
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Delete uncompressed original if we created a new .gz file
                if (!string.Equals(sourcePath, compressedPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(sourcePath);
                    _logger?.Log("HarCapture", $"Deleted uncompressed file: {sourcePath}");
                }

                _finalOutputFilePath = compressedPath;
                var compressedSize = new FileInfo(compressedPath).Length;
                _logger?.Log("HarCapture", $"Compression completed: {compressedPath} ({compressedSize} bytes)");
            }
        }
        else
        {
            _logger?.Log("HarCapture", $"Capture stopped: {_har.Log.Entries?.Count ?? 0} entries, {_har.Log.Pages?.Count ?? 0} pages");
        }

        // No clone needed - capture is stopped, return the final state
        return _har;
    }

    /// <summary>
    /// Synchronously stops network traffic capture and returns the final HAR object.
    /// </summary>
    /// <returns>The final HAR object.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the session has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    public Har Stop()
    {
        return StopAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates a new page in the HAR capture.
    /// Subsequent entries will be associated with this page via their PageRef property.
    /// </summary>
    /// <param name="pageRef">Unique identifier for the page. This will be used in entry PageRef fields.</param>
    /// <param name="pageTitle">Human-readable title for the page.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the session has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    /// <exception cref="ArgumentNullException">Thrown when pageRef or pageTitle is null.</exception>
    public void NewPage(string pageRef, string pageTitle)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HarCaptureSession));
        }

        if (!_isCapturing)
        {
            throw new InvalidOperationException("Capture is not started.");
        }

        if (pageRef == null)
        {
            throw new ArgumentNullException(nameof(pageRef));
        }

        if (pageTitle == null)
        {
            throw new ArgumentNullException(nameof(pageTitle));
        }

        lock (_lock)
        {
            _logger?.Log("HarCapture", $"NewPage: ref={pageRef}, title={pageTitle}, entriesSoFar={_har.Log.Entries?.Count ?? 0}");

            // Create new page
            var page = new HarPage
            {
                Id = pageRef,
                Title = pageTitle,
                StartedDateTime = DateTimeOffset.UtcNow,
                PageTimings = new HarPageTimings()
            };

            // Build new pages list from existing
            var pages = new List<HarPage>(_har.Log.Pages ?? (IEnumerable<HarPage>)Array.Empty<HarPage>())
            {
                page
            };

            if (_streamWriter != null)
            {
                // Streaming mode: write page to file, keep entries empty in memory
                _streamWriter.AddPage(page);

                _har = new Har
                {
                    Log = new HarLog
                    {
                        Version = _har.Log.Version,
                        Creator = _har.Log.Creator,
                        Browser = _har.Log.Browser,
                        Pages = pages,
                        Entries = new List<HarEntry>(),
                        Comment = _har.Log.Comment,
                        Custom = _har.Log.Custom
                    }
                };
            }
            else
            {
                // In-memory mode: keep existing entries
                var entries = new List<HarEntry>(_har.Log.Entries ?? (IEnumerable<HarEntry>)Array.Empty<HarEntry>());

                _har = new Har
                {
                    Log = new HarLog
                    {
                        Version = _har.Log.Version,
                        Creator = _har.Log.Creator,
                        Browser = _har.Log.Browser,
                        Pages = pages,
                        Entries = entries,
                        Comment = _har.Log.Comment,
                        Custom = _har.Log.Custom
                    }
                };
            }

            _currentPageRef = pageRef;
        }
    }

    /// <summary>
    /// Pauses capture. Entries that arrive while paused are dropped (not queued).
    /// Idempotent — calling Pause() multiple times does not throw.
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
        _logger?.Log("HarCapture", "Capture paused — new entries will be dropped");
    }

    /// <summary>
    /// Resumes capture after a pause. Entries that arrive after this call are recorded normally.
    /// Idempotent — calling Resume() multiple times (or without a prior Pause()) does not throw.
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
        _logger?.Log("HarCapture", "Capture resumed");
    }

    /// <summary>
    /// Gets a deep-cloned snapshot of the current HAR data while capture continues.
    /// </summary>
    /// <returns>A deep-cloned HAR object that is independent of the live capture.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the session has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is not started.</exception>
    /// <remarks>
    /// The returned HAR is a complete copy. Modifying it will not affect the ongoing capture.
    /// Uses JSON round-trip serialization for deep cloning.
    /// </remarks>
    public Har GetHar()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HarCaptureSession));
        }

        if (!_isCapturing)
        {
            throw new InvalidOperationException("Capture is not started.");
        }

        lock (_lock)
        {
            if (_streamWriter != null)
            {
                // Streaming mode: return metadata only (entries are in the file)
                return new Har
                {
                    Log = new HarLog
                    {
                        Version = _har.Log.Version,
                        Creator = _har.Log.Creator,
                        Browser = _har.Log.Browser,
                        Pages = _har.Log.Pages != null ? new List<HarPage>(_har.Log.Pages) : null,
                        Entries = new List<HarEntry>(),
                        Comment = _har.Log.Comment,
                        Custom = _har.Log.Custom
                    }
                };
            }

            // Deep clone via JSON round-trip
            var json = HarSerializer.Serialize(_har, writeIndented: false);
            return HarSerializer.Deserialize(json);
        }
    }

    /// <summary>
    /// Writes CDP page timing values into the last HAR page's PageTimings.
    /// Called from StopAsync after the strategy has been stopped.
    /// </summary>
    /// <param name="domContentLoadedMs">DOMContentLoaded time in milliseconds, or null if not available.</param>
    /// <param name="loadMs">Load event time in milliseconds, or null if not available.</param>
    private void ApplyPageTimings(double? domContentLoadedMs, double? loadMs)
    {
        if (_har.Log.Pages == null || _har.Log.Pages.Count == 0)
            return;
        if (domContentLoadedMs == null && loadMs == null)
            return;

        var pages = _har.Log.Pages;
        int lastIdx = pages.Count - 1;
        var existingPage = pages[lastIdx];

        var updatedPage = new HarPage
        {
            Id = existingPage.Id,
            Title = existingPage.Title,
            StartedDateTime = existingPage.StartedDateTime,
            Comment = existingPage.Comment,
            PageTimings = new HarPageTimings
            {
                OnContentLoad = domContentLoadedMs,
                OnLoad = loadMs,
                Comment = existingPage.PageTimings?.Comment
            }
        };

        pages[lastIdx] = updatedPage;
        _logger?.Log("HarCapture", $"PageTimings written: onContentLoad={domContentLoadedMs:F1}ms, onLoad={loadMs:F1}ms");
    }

    /// <summary>
    /// Initializes the HAR object with creator metadata and optional initial page.
    /// </summary>
    /// <param name="initialPageRef">Optional page reference ID for the initial page.</param>
    /// <param name="initialPageTitle">Optional page title for the initial page.</param>
    private void InitializeHar(string? initialPageRef, string? initialPageTitle)
    {
        lock (_lock)
        {
            // Create initial pages list
            List<HarPage>? pages = null;
            if (initialPageRef != null && initialPageTitle != null)
            {
                pages = new List<HarPage>
                {
                    new HarPage
                    {
                        Id = initialPageRef,
                        Title = initialPageTitle,
                        StartedDateTime = DateTimeOffset.UtcNow,
                        PageTimings = new HarPageTimings()
                    }
                };
            }

            // Create browser metadata if available
            HarBrowser? browser = null;
            if (_browserName != null)
            {
                browser = new HarBrowser
                {
                    Name = _browserName,
                    Version = _browserVersion ?? ""
                };
            }

            // Initialize HAR
            _har = new Har
            {
                Log = new HarLog
                {
                    Version = "1.2",
                    Creator = new HarCreator
                    {
                        Name = _options.CreatorName,
                        Version = typeof(HarCaptureSession).Assembly.GetName().Version?.ToString() ?? "1.0.0"
                    },
                    Browser = browser,
                    Pages = pages,
                    Entries = new List<HarEntry>(),
                    Custom = _options.CustomMetadata != null
                        ? new Dictionary<string, object>(_options.CustomMetadata)
                        : null
                }
            };

            _currentPageRef = initialPageRef;
        }
    }

    /// <summary>
    /// Event handler for completed entries from the capture strategy.
    /// Filters by URL pattern and adds to the HAR with current page reference.
    /// </summary>
    /// <param name="entry">The completed HAR entry.</param>
    /// <param name="requestId">The internal request ID (not used here).</param>
    private void OnEntryCompleted(HarEntry entry, string requestId)
    {
        _logger?.Log("HarCapture", $"EntryCompleted: {entry.Request.Method} {entry.Request.Url} → {entry.Response.Status}");

        // Filter by URL pattern
        if (!_urlMatcher.ShouldCapture(entry.Request.Url))
        {
            _logger?.Log("HarCapture", $"Entry filtered out by URL pattern: {entry.Request.Url}");
            return;
        }

        // Drop entry if capture is paused (checked outside lock — volatile read is sufficient)
        if (_isPaused)
        {
            _logger?.Log("HarCapture", $"Entry dropped (paused): {entry.Request.Url}");
            return;
        }

        HarCaptureProgress? progress = null;

        lock (_lock)
        {
            // Create new entry with PageRef set if we have a current page
            var entryToAdd = entry;
            if (_currentPageRef != null)
            {
                entryToAdd = new HarEntry
                {
                    StartedDateTime = entry.StartedDateTime,
                    Time = entry.Time,
                    Request = entry.Request,
                    Response = entry.Response,
                    Cache = entry.Cache,
                    Timings = entry.Timings,
                    PageRef = _currentPageRef,
                    ServerIPAddress = entry.ServerIPAddress,
                    Connection = entry.Connection,
                    Comment = entry.Comment,
                    ResourceType = entry.ResourceType,
                    WebSocketMessages = entry.WebSocketMessages,
                    RequestBodySize = entry.RequestBodySize,
                    ResponseBodySize = entry.ResponseBodySize,
                    Initiator = entry.Initiator,
                    SecurityDetails = entry.SecurityDetails
                };
            }

            if (_streamWriter != null)
            {
                _streamWriter.WriteEntry(entryToAdd);
                _logger?.Log("HarCapture", $"Entry streamed: pageRef={_currentPageRef ?? "(none)"}, total={_streamWriter.Count}");
                progress = new HarCaptureProgress
                {
                    EntryCount = _streamWriter.Count,
                    CurrentPageRef = _currentPageRef,
                    EntryUrl = entryToAdd.Request.Url
                };
            }
            else
            {
                // In-memory mode: accumulate entries in _har
                var entries = new List<HarEntry>(_har.Log.Entries ?? (IEnumerable<HarEntry>)Array.Empty<HarEntry>())
                {
                    entryToAdd
                };

                _har = new Har
                {
                    Log = new HarLog
                    {
                        Version = _har.Log.Version,
                        Creator = _har.Log.Creator,
                        Browser = _har.Log.Browser,
                        Pages = _har.Log.Pages,
                        Entries = entries,
                        Comment = _har.Log.Comment,
                        Custom = _har.Log.Custom
                    }
                };

                _logger?.Log("HarCapture", $"Entry added: pageRef={_currentPageRef ?? "(none)"}, totalEntries={entries.Count}");
                progress = new HarCaptureProgress
                {
                    EntryCount = entries.Count,
                    CurrentPageRef = _currentPageRef,
                    EntryUrl = entryToAdd.Request.Url
                };
            }
        }

        // Fire EntryWritten OUTSIDE the lock to prevent deadlock when handlers call GetHar()
        EntryWritten?.Invoke(this, progress);
    }

    /// <summary>
    /// Asynchronously releases all resources used by the <see cref="HarCaptureSession"/>.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _logger?.Log("HarCapture", "Session async disposal started");

        if (_streamWriter != null)
        {
            await _streamWriter.DisposeAsync().ConfigureAwait(false);
            _streamWriter = null;
        }

        if (_strategy != null)
        {
            _strategy.EntryCompleted -= OnEntryCompleted;
            _strategy.Dispose();
            _strategy = null;
        }

        _logger?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="HarCaptureSession"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger?.Log("HarCapture", "Session disposed");

        _streamWriter?.Dispose();
        _streamWriter = null;

        if (_strategy != null)
        {
            _strategy.EntryCompleted -= OnEntryCompleted;
            _strategy.Dispose();
            _strategy = null;
        }

        _logger?.Dispose();
        _disposed = true;
    }
}
