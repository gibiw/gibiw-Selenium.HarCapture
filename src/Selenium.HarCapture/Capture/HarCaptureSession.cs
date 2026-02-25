using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
public sealed class HarCaptureSession : IDisposable
{
    private readonly CaptureOptions _options;
    private readonly UrlPatternMatcher _urlMatcher;
    private readonly FileLogger? _logger;
    private readonly object _lock = new object();
    private INetworkCaptureStrategy? _strategy;
    private HarStreamWriter? _streamWriter;
    private Har _har = null!;
    private string? _currentPageRef;
    private bool _isCapturing;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether capture is currently active.
    /// </summary>
    public bool IsCapturing => _isCapturing;

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
    }

    /// <summary>
    /// Asynchronously starts network traffic capture.
    /// </summary>
    /// <param name="initialPageRef">Optional page reference ID for the initial page. If provided, creates the first page in the HAR.</param>
    /// <param name="initialPageTitle">Optional page title for the initial page. Used only if initialPageRef is provided.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the session has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when capture is already started or no strategy is configured.</exception>
    public async Task StartAsync(string? initialPageRef = null, string? initialPageTitle = null)
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
                _har.Log.Pages, _logger);
            _logger?.Log("HarCapture", $"Streaming mode: {_options.OutputFilePath}");
        }

        if (initialPageRef != null)
        {
            _logger?.Log("HarCapture", $"Initial page: ref={initialPageRef}, title={initialPageTitle}");
        }

        await _strategy.StartAsync(_options).ConfigureAwait(false);
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
    public async Task<Har> StopAsync()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HarCaptureSession));
        }

        if (!_isCapturing)
        {
            throw new InvalidOperationException("Capture is not started.");
        }

        _logger?.Log("HarCapture", "StopAsync called");
        await _strategy!.StopAsync().ConfigureAwait(false);
        _isCapturing = false;
        _strategy.EntryCompleted -= OnEntryCompleted;

        if (_streamWriter != null)
        {
            _streamWriter.Complete();
            _logger?.Log("HarCapture", $"Streaming completed: {_streamWriter.Count} entries, {_har.Log.Pages?.Count ?? 0} pages");
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
                        Comment = _har.Log.Comment
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
                        Comment = _har.Log.Comment
                    }
                };
            }

            _currentPageRef = pageRef;
        }
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
                        Comment = _har.Log.Comment
                    }
                };
            }

            // Deep clone via JSON round-trip
            var json = HarSerializer.Serialize(_har, writeIndented: false);
            return HarSerializer.Deserialize(json);
        }
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
                    Pages = pages,
                    Entries = new List<HarEntry>()
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
                    Comment = entry.Comment
                };
            }

            if (_streamWriter != null)
            {
                _streamWriter.WriteEntry(entryToAdd);
                _logger?.Log("HarCapture", $"Entry streamed: pageRef={_currentPageRef ?? "(none)"}, total={_streamWriter.Count}");
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
                        Comment = _har.Log.Comment
                    }
                };

                _logger?.Log("HarCapture", $"Entry added: pageRef={_currentPageRef ?? "(none)"}, totalEntries={entries.Count}");
            }
        }
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
