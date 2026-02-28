using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using Selenium.HarCapture.Capture.Internal;
using Selenium.HarCapture.Capture.Internal.Cdp;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Strategies;

/// <summary>
/// Captures network traffic using Chrome DevTools Protocol (CDP) Network domain.
/// Primary strategy for Chromium-based browsers (Chrome, Edge).
/// Provides detailed timings and response body capture.
/// Supports CDP versions V142-V144 via auto-detection.
/// </summary>
internal sealed class CdpNetworkCaptureStrategy : INetworkCaptureStrategy
{
    private readonly IWebDriver _driver;
    private readonly FileLogger? _logger;
    private DevToolsSession? _session;
    private ICdpNetworkAdapter? _adapter;
    private CaptureOptions _options = null!;
    private readonly RequestResponseCorrelator _correlator = new();
    private readonly ConcurrentDictionary<string, LinkedListNode<CacheEntry>> _bodyCache = new();
    private readonly LinkedList<CacheEntry> _lruList = new();
    private readonly object _cacheLock = new object();
    private const int MaxCacheEntries = 500;
    private Channel<BodyRetrievalRequest>? _bodyChannel;
    private Task[]? _bodyWorkers;
    private const int BodyWorkerCount = 3;
    private UrlPatternMatcher? _urlMatcher;
    private MimeTypeMatcher? _mimeMatcher;
    private SensitiveDataRedactor? _redactor;
    private WebSocketFrameAccumulator? _wsAccumulator;
    private volatile bool _stopping;
    private bool _disposed;

    // Page timing tracking (HAR-02)
    private double _firstRequestTimestamp;
    private double? _domContentLoadedTimestamp;
    private double? _loadTimestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="CdpNetworkCaptureStrategy"/> class.
    /// </summary>
    /// <param name="driver">The WebDriver instance. Must implement IDevTools (Chromium-based browsers).</param>
    /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
    internal CdpNetworkCaptureStrategy(IWebDriver driver, FileLogger? logger = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _logger = logger;
    }

    /// <inheritdoc />
    public string StrategyName => "CDP";

    /// <inheritdoc />
    public bool SupportsDetailedTimings => true;

    /// <inheritdoc />
    public bool SupportsResponseBody => true;

    /// <inheritdoc />
    public double? LastDomContentLoadedTimestamp => _domContentLoadedTimestamp;

    /// <inheritdoc />
    public double? LastLoadTimestamp => _loadTimestamp;

    /// <inheritdoc />
    public event Action<HarEntry, string>? EntryCompleted;

    /// <inheritdoc />
    public async Task StartAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        _logger?.Log("CDP", "StartAsync: creating session and adapter");

        // Verify driver supports CDP
        var devTools = _driver as IDevTools;
        if (devTools == null)
        {
            throw new InvalidOperationException(
                "Driver does not support Chrome DevTools Protocol. Use a Chromium-based browser (Chrome, Edge).");
        }

        // Create CDP session and auto-detect version
        _session = devTools.GetDevToolsSession();
        _adapter = CdpAdapterFactory.Create(_session, _logger);

        // Initialize URL matcher for filtering
        _urlMatcher = new UrlPatternMatcher(options.UrlIncludePatterns, options.UrlExcludePatterns);

        // Initialize MIME type matcher for body retrieval filtering
        _mimeMatcher = MimeTypeMatcher.FromScope(options.ResponseBodyScope, options.ResponseBodyMimeFilter);

        // Initialize redactor for sensitive data
        _redactor = new SensitiveDataRedactor(
            options.SensitiveHeaders,
            options.SensitiveCookies,
            options.SensitiveQueryParams);

        // Create bounded channel + worker tasks for body retrieval
        _bodyChannel = Channel.CreateBounded<BodyRetrievalRequest>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
        _bodyWorkers = new Task[BodyWorkerCount];
        for (int i = 0; i < BodyWorkerCount; i++)
        {
            _bodyWorkers[i] = BodyWorkerLoop(_bodyChannel.Reader, cancellationToken);
        }

        // Subscribe to adapter events BEFORE enabling (critical order)
        _adapter.RequestWillBeSent += OnRequestWillBeSent;
        _adapter.ResponseReceived += OnResponseReceived;
        _adapter.LoadingFinished += OnLoadingFinished;
        _adapter.LoadingFailed += OnLoadingFailed;

        // Subscribe to WebSocket events if WebSocket capture is enabled
        if ((options.CaptureTypes & CaptureType.WebSocket) != 0)
        {
            _wsAccumulator = new WebSocketFrameAccumulator();
            _adapter.WebSocketCreated += OnWebSocketCreated;
            _adapter.WebSocketWillSendHandshakeRequest += OnWebSocketWillSendHandshakeRequest;
            _adapter.WebSocketHandshakeResponseReceived += OnWebSocketHandshakeResponseReceived;
            _adapter.WebSocketFrameSent += OnWebSocketFrameSent;
            _adapter.WebSocketFrameReceived += OnWebSocketFrameReceived;
            _adapter.WebSocketClosed += OnWebSocketClosed;
            _logger?.Log("CDP", "WebSocket capture enabled");
        }

        // Enable Network domain
        await _adapter.EnableNetworkAsync().ConfigureAwait(false);
        _logger?.Log("CDP", "Network domain enabled, capture ready");

        // Enable Page domain for page timing events (HAR-02)
        await _adapter.EnablePageAsync().ConfigureAwait(false);
        _adapter.DomContentEventFired += OnDomContentEventFired;
        _adapter.LoadEventFired += OnLoadEventFired;
        _logger?.Log("CDP", "Page domain enabled for timing events");
    }

    private const int StopTimeoutMs = 10_000;
    private const int DisposeTimeoutMs = 5_000;

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_adapter != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Set stopping flag FIRST to prevent event handlers from accessing disposed resources
            _stopping = true;

            // Unsubscribe from events FIRST to prevent new entries during drain
            _adapter.RequestWillBeSent -= OnRequestWillBeSent;
            _adapter.ResponseReceived -= OnResponseReceived;
            _adapter.LoadingFinished -= OnLoadingFinished;
            _adapter.LoadingFailed -= OnLoadingFailed;

            // Unsubscribe from WebSocket events
            if (_wsAccumulator != null)
            {
                _adapter.WebSocketCreated -= OnWebSocketCreated;
                _adapter.WebSocketWillSendHandshakeRequest -= OnWebSocketWillSendHandshakeRequest;
                _adapter.WebSocketHandshakeResponseReceived -= OnWebSocketHandshakeResponseReceived;
                _adapter.WebSocketFrameSent -= OnWebSocketFrameSent;
                _adapter.WebSocketFrameReceived -= OnWebSocketFrameReceived;
                _adapter.WebSocketClosed -= OnWebSocketClosed;
            }

            // Unsubscribe from Page events (HAR-02)
            _adapter.DomContentEventFired -= OnDomContentEventFired;
            _adapter.LoadEventFired -= OnLoadEventFired;

            // Complete channel and wait for workers to drain (with timeout)
            _bodyChannel?.Writer.TryComplete();
            if (_bodyWorkers != null)
            {
                _logger?.Log("CDP", $"StopAsync: waiting for {BodyWorkerCount} body workers to drain");

                // Create linked token source combining caller's token with 10-second timeout
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(10));

                var allWorkers = Task.WhenAll(_bodyWorkers);
                try
                {
                    var completed = await Task.WhenAny(allWorkers, Task.Delay(Timeout.Infinite, linkedCts.Token)).ConfigureAwait(false);
                    if (completed == allWorkers)
                    {
                        await allWorkers.ConfigureAwait(false);
                        _logger?.Log("CDP", "StopAsync: all body workers completed");
                    }
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Caller cancelled
                        throw;
                    }
                    else
                    {
                        // Timeout
                        _logger?.Log("CDP", $"StopAsync: body workers timed out after {StopTimeoutMs / 1000}s, proceeding without remaining bodies");
                    }
                }
            }

            try
            {
                var disableTask = _adapter.DisableNetworkAsync();
                var completed2 = await Task.WhenAny(disableTask, Task.Delay(DisposeTimeoutMs)).ConfigureAwait(false);
                if (completed2 == disableTask)
                {
                    await disableTask.ConfigureAwait(false);
                    _logger?.Log("CDP", "StopAsync: network domain disabled");
                }
                else
                {
                    _logger?.Log("CDP", $"StopAsync: DisableNetwork timed out after {DisposeTimeoutMs / 1000}s");
                }
            }
            catch (Exception ex)
            {
                _logger?.Log("CDP", $"StopAsync: DisableNetwork failed: {ex.Message}");
            }

            // Disable Page domain (best-effort, HAR-02)
            try
            {
                await _adapter.DisablePageAsync().ConfigureAwait(false);
            }
            catch
            {
                // Page domain disable failure is non-critical
            }

            // Reset page timing fields for next capture
            _firstRequestTimestamp = 0;
            _domContentLoadedTimestamp = null;
            _loadTimestamp = null;
        }

        // Flush all unclosed WebSocket connections
        if (_wsAccumulator != null)
        {
            var activeWsIds = _wsAccumulator.GetActiveRequestIds();
            if (activeWsIds.Count > 0)
            {
                _logger?.Log("CDP", $"StopAsync: flushing {activeWsIds.Count} unclosed WebSocket connections");
                foreach (var wsId in activeWsIds)
                {
                    FlushWebSocket(wsId);
                }
            }
        }

        _bodyChannel = null;
        _bodyWorkers = null;
        _correlator.Clear();
        ClearCache();
        _wsAccumulator?.Clear();
        _stopping = false;
    }

    /// <summary>
    /// Handles CDP requestWillBeSent event.
    /// Fires when a request is about to be sent on the network.
    /// </summary>
    private void OnRequestWillBeSent(CdpRequestWillBeSentData e)
    {
        try
        {
            if (_stopping) return;

            _logger?.Log("CDP", $"RequestWillBeSent: id={e.RequestId}, {e.Request.Method} {e.Request.Url}");

            // Track first request timestamp for page timing calculation (HAR-02)
            if (_firstRequestTimestamp == 0)
            {
                _firstRequestTimestamp = e.Timestamp;
            }

            // Suppress normal HTTP flow for WebSocket requests
            if (_wsAccumulator != null && _wsAccumulator.IsWebSocket(e.RequestId))
            {
                _logger?.Log("CDP", $"Request suppressed (WebSocket): id={e.RequestId}");
                return;
            }

            // URL filtering
            if (_urlMatcher != null && !_urlMatcher.ShouldCapture(e.Request.Url))
            {
                _logger?.Log("CDP", $"Request filtered out by URL pattern: {e.Request.Url}");
                return;
            }

            // Handle redirects: complete previous entry with redirect response
            if (e.RedirectResponse != null)
            {
                _logger?.Log("CDP", $"Redirect detected: id={e.RequestId}, completing redirect entry");
                CompleteRedirectEntry(e.RequestId, e.RedirectResponse, e.Timestamp);
            }

            // Build HAR request
            var harRequest = BuildHarRequest(e.Request);

            // Calculate started time from WallTime (CDP uses epoch seconds)
            var startedDateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(e.WallTime * 1000));

            // Record request in correlator
            _correlator.OnRequestSent(e.RequestId, harRequest, startedDateTime);
            _logger?.Log("CDP", $"Request recorded: id={e.RequestId}, pending={_correlator.PendingCount}");
        }
        catch (Exception ex)
        {
            // Don't let exceptions in event handlers crash the capture
            _logger?.Log("CDP", $"Error in OnRequestWillBeSent: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles CDP responseReceived event.
    /// Fires when HTTP response headers are received.
    /// </summary>
    private void OnResponseReceived(CdpResponseReceivedData e)
    {
        try
        {
            if (_stopping) return;

            _logger?.Log("CDP", $"ResponseReceived: id={e.RequestId}, status={e.Response.Status}, mime={e.Response.MimeType}");

            // Suppress normal HTTP flow for WebSocket requests
            if (_wsAccumulator != null && _wsAccumulator.IsWebSocket(e.RequestId))
            {
                _logger?.Log("CDP", $"Response suppressed (WebSocket): id={e.RequestId}");
                return;
            }

            // Build HAR response
            var harResponse = BuildHarResponse(e.Response);

            // Build HAR timings from CDP ResourceTiming
            HarTimings? harTimings = null;
            double totalTime = 0;

            if (e.Response.Timing != null)
            {
                var t = e.Response.Timing;
                harTimings = CdpTimingMapper.MapToHarTimings(
                    t.DnsStart, t.DnsEnd,
                    t.ConnectStart, t.ConnectEnd,
                    t.SslStart, t.SslEnd,
                    t.SendStart, t.SendEnd,
                    t.ReceiveHeadersEnd,
                    t.RequestTime,
                    e.Timestamp);

                // Calculate total time from timing components
                totalTime = (harTimings.Blocked ?? 0) + (harTimings.Dns ?? 0) + (harTimings.Connect ?? 0) +
                            harTimings.Send + harTimings.Wait + harTimings.Receive;

                _logger?.Log("CDP", $"Timings: dns={harTimings.Dns ?? 0:F1}, connect={harTimings.Connect ?? 0:F1}, send={harTimings.Send:F1}, wait={harTimings.Wait:F1}, receive={harTimings.Receive:F1}, total={totalTime:F1}ms");
            }

            // Correlate response with request
            var entry = _correlator.OnResponseReceived(e.RequestId, harResponse, harTimings, totalTime, e.Type);

            if (entry == null)
            {
                _logger?.Log("CDP", $"Correlation failed: no matching request for id={e.RequestId}");
            }
            else
            {
                // Determine if we should retrieve response body
                bool shouldGetBody = ShouldRetrieveResponseBody(e.Response.Status, e.Response.MimeType);

                if (!shouldGetBody)
                {
                    if (e.Response.Status == 304 || e.Response.Status == 204)
                    {
                        _logger?.Log("CDP", $"Body retrieval: skip (status={e.Response.Status})");
                    }
                    else
                    {
                        _logger?.Log("CDP", $"Body retrieval: skip (mime={e.Response.MimeType})");
                    }

                    // No body needed, fire EntryCompleted immediately
                    _logger?.Log("CDP", $"EntryCompleted fired (no body): id={e.RequestId}");
                    EntryCompleted?.Invoke(entry, e.RequestId);
                }
                else
                {
                    // Queue body retrieval to bounded channel — workers process concurrently.
                    var request = new BodyRetrievalRequest(e.RequestId, entry);
                    if (!_bodyChannel!.Writer.TryWrite(request))
                    {
                        // In Wait mode, TryWrite returns false only when channel is completed
                        // (i.e., during shutdown). Log for diagnostic purposes.
                        _logger?.Log("CDP", $"WARN: Body retrieval channel rejected write for id={e.RequestId}, channel may be closing");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Log("CDP", $"Error in OnResponseReceived: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles CDP loadingFinished event.
    /// Fires when network loading completes successfully.
    /// </summary>
    private void OnLoadingFinished(string requestId)
    {
        if (_stopping) return;

        // Response body retrieval happens in OnResponseReceived (immediately after headers received).
        // LoadingFinished is too late - the resource may have been dumped by the browser.
        // We only use this event for metadata (EncodedDataLength).
        // No action needed here.
    }

    /// <summary>
    /// Handles CDP loadingFailed event.
    /// Fires when network loading fails (timeout, connection refused, etc.).
    /// </summary>
    private void OnLoadingFailed(string requestId)
    {
        if (_stopping) return;

        _logger?.Log("CDP", $"LoadingFailed: id={requestId} (request dropped)");
        // Failed requests do not produce complete HAR entries.
        // Remove pending entry from correlator to free memory.
        // In the future, we could optionally create error entries.
        // For now, silently drop failed requests.
    }

    private void OnWebSocketCreated(CdpWebSocketCreatedData e)
    {
        try
        {
            if (_stopping) return;

            _logger?.Log("CDP", $"WebSocketCreated: id={e.RequestId}, url={e.Url}");
            _wsAccumulator?.OnCreated(e.RequestId, e.Url);
        }
        catch (Exception ex)
        {
            _logger?.Log("CDP", $"Error in OnWebSocketCreated: {ex.Message}");
        }
    }

    private void OnWebSocketWillSendHandshakeRequest(CdpWebSocketHandshakeRequestData e)
    {
        try
        {
            if (_stopping) return;

            _logger?.Log("CDP", $"WebSocketHandshakeRequest: id={e.RequestId}");
            _wsAccumulator?.OnHandshakeRequest(e.RequestId, e.Timestamp, e.WallTime, e.Headers);
        }
        catch (Exception ex)
        {
            _logger?.Log("CDP", $"Error in OnWebSocketWillSendHandshakeRequest: {ex.Message}");
        }
    }

    private void OnWebSocketHandshakeResponseReceived(CdpWebSocketHandshakeResponseData e)
    {
        try
        {
            if (_stopping) return;

            _logger?.Log("CDP", $"WebSocketHandshakeResponse: id={e.RequestId}, status={e.Status}");
            _wsAccumulator?.OnHandshakeResponse(e.RequestId, e.Timestamp, e.Status, e.StatusText, e.Headers);
        }
        catch (Exception ex)
        {
            _logger?.Log("CDP", $"Error in OnWebSocketHandshakeResponseReceived: {ex.Message}");
        }
    }

    private void OnWebSocketFrameSent(CdpWebSocketFrameData e)
    {
        try
        {
            if (_stopping) return;

            _wsAccumulator?.AddFrame(e.RequestId, "send", e.Timestamp, e.Opcode, e.PayloadData);
        }
        catch (Exception ex)
        {
            _logger?.Log("CDP", $"Error in OnWebSocketFrameSent: {ex.Message}");
        }
    }

    private void OnWebSocketFrameReceived(CdpWebSocketFrameData e)
    {
        try
        {
            if (_stopping) return;

            _wsAccumulator?.AddFrame(e.RequestId, "receive", e.Timestamp, e.Opcode, e.PayloadData);
        }
        catch (Exception ex)
        {
            _logger?.Log("CDP", $"Error in OnWebSocketFrameReceived: {ex.Message}");
        }
    }

    private void OnWebSocketClosed(CdpWebSocketClosedData e)
    {
        try
        {
            if (_stopping) return;

            _logger?.Log("CDP", $"WebSocketClosed: id={e.RequestId}");
            FlushWebSocket(e.RequestId);
        }
        catch (Exception ex)
        {
            _logger?.Log("CDP", $"Error in OnWebSocketClosed: {ex.Message}");
        }
    }

    private void OnDomContentEventFired(CdpPageTimingEventData e)
    {
        if (_stopping) return;
        if (_firstRequestTimestamp > 0)
        {
            _domContentLoadedTimestamp = (e.Timestamp - _firstRequestTimestamp) * 1000.0;
        }
        _logger?.Log("CDP", $"DomContentLoaded: offset={_domContentLoadedTimestamp:F1}ms");
    }

    private void OnLoadEventFired(CdpPageTimingEventData e)
    {
        if (_stopping) return;
        if (_firstRequestTimestamp > 0)
        {
            _loadTimestamp = (e.Timestamp - _firstRequestTimestamp) * 1000.0;
        }
        _logger?.Log("CDP", $"Load: offset={_loadTimestamp:F1}ms");
    }

    private void FlushWebSocket(string requestId)
    {
        var result = _wsAccumulator?.Flush(requestId);
        if (result == null)
            return;

        var (baseEntry, frames) = result.Value;

        // Create final entry with WebSocketMessages attached
        var finalEntry = new HarEntry
        {
            StartedDateTime = baseEntry.StartedDateTime,
            Time = baseEntry.Time,
            Request = baseEntry.Request,
            Response = baseEntry.Response,
            Cache = baseEntry.Cache,
            Timings = baseEntry.Timings,
            ResourceType = "websocket",
            WebSocketMessages = frames
        };

        _logger?.Log("CDP", $"WebSocket entry completed: id={requestId}, frames={frames.Count}");
        EntryCompleted?.Invoke(finalEntry, requestId);
    }

    /// <summary>
    /// Completes a redirect entry using the redirect response data.
    /// </summary>
    private void CompleteRedirectEntry(string requestId, CdpResponseInfo redirectResponse, double timestamp)
    {
        var harResponse = BuildHarResponse(redirectResponse);

        // Redirects don't have detailed timing data in the redirectResponse
        // Use simple timings
        var harTimings = new HarTimings
        {
            Send = 0,
            Wait = 0,
            Receive = 0
        };

        var entry = _correlator.OnResponseReceived(requestId, harResponse, harTimings, 0);
        if (entry != null)
        {
            EntryCompleted?.Invoke(entry, requestId);
        }
    }

    /// <summary>
    /// Retrieves the response body for a completed request and fires EntryCompleted with updated entry.
    /// Uses URL-based caching to avoid redundant CDP calls for the same resource across pages.
    /// </summary>
    private async Task RetrieveResponseBodyAsync(string requestId, HarEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            if (_stopping || _adapter == null)
            {
                EntryCompleted?.Invoke(entry, requestId);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            string? bodyText;
            bool base64Encoded;
            var url = entry.Request.Url;

            // Check body cache — same URL across pages returns identical content,
            // so we can skip the CDP call and reuse the cached body.
            if (TryGetCachedBody(url, out var cached))
            {
                bodyText = cached.Body;
                base64Encoded = cached.Base64Encoded;
                _logger?.Log("CDP", $"Body from cache: id={requestId}, size={bodyText?.Length ?? 0}");
            }
            else
            {
                (bodyText, base64Encoded) = await _adapter.GetResponseBodyAsync(requestId).ConfigureAwait(false);
                CacheBody(url, bodyText, base64Encoded);
                _logger?.Log("CDP", $"Body retrieved: id={requestId}, size={bodyText?.Length ?? 0}, base64={base64Encoded}");
            }

            // Check MaxResponseBodySize limit
            string? encoding = base64Encoded ? "base64" : null;
            long bodySize = bodyText?.Length ?? 0;

            if (_options.MaxResponseBodySize > 0 && bodySize > _options.MaxResponseBodySize)
            {
                _logger?.Log("CDP", $"Body truncated: id={requestId}, original={bodySize}, limit={_options.MaxResponseBodySize}");
                bodyText = bodyText?.Substring(0, (int)_options.MaxResponseBodySize);
                bodySize = _options.MaxResponseBodySize;
            }

            // Create updated entry with response body
            // HarEntry uses init-only properties, so we must create a new instance
            var updatedEntry = new HarEntry
            {
                StartedDateTime = entry.StartedDateTime,
                Time = entry.Time,
                Request = entry.Request,
                Response = new HarResponse
                {
                    Status = entry.Response.Status,
                    StatusText = entry.Response.StatusText,
                    HttpVersion = entry.Response.HttpVersion,
                    Headers = entry.Response.Headers,
                    Cookies = entry.Response.Cookies,
                    Content = new HarContent
                    {
                        Size = bodySize,
                        MimeType = entry.Response.Content.MimeType,
                        Text = bodyText,
                        Encoding = encoding
                    },
                    RedirectURL = entry.Response.RedirectURL,
                    HeadersSize = entry.Response.HeadersSize,
                    BodySize = bodySize
                },
                Cache = entry.Cache,
                Timings = entry.Timings,
                PageRef = entry.PageRef,
                ServerIPAddress = entry.ServerIPAddress,
                Connection = entry.Connection,
                Comment = entry.Comment,
                ResourceType = entry.ResourceType
            };

            EntryCompleted?.Invoke(updatedEntry, requestId);
        }
        catch (Exception ex)
        {
            // "No resource with given identifier found" is expected when resource was already dumped
            _logger?.Log("CDP", $"Body retrieval failed: id={requestId}, {ex.Message} — firing entry without body");

            // Fire EntryCompleted with entry without body content
            EntryCompleted?.Invoke(entry, requestId);
        }
    }

    /// <summary>
    /// Determines whether response body should be retrieved based on status code, MIME type, and capture options.
    /// </summary>
    private bool ShouldRetrieveResponseBody(long status, string? mimeType)
    {
        // Skip body for 304 (Not Modified) and 204 (No Content)
        if (status == 304 || status == 204)
        {
            return false;
        }

        // Check if response content capture is enabled
        var captureTypes = _options.CaptureTypes;
        bool wantsContent = (captureTypes & CaptureType.ResponseContent) != 0 ||
                            (captureTypes & CaptureType.ResponseBinaryContent) != 0;

        if (!wantsContent)
            return false;

        // Check MIME type filter
        if (_mimeMatcher != null && !_mimeMatcher.ShouldRetrieveBody(mimeType))
            return false;

        return true;
    }

    /// <summary>
    /// Builds a HAR request from CDP request data.
    /// </summary>
    private HarRequest BuildHarRequest(CdpRequestInfo cdpRequest)
    {
        var captureTypes = _options.CaptureTypes;

        // Headers
        var headers = new List<HarHeader>();
        if ((captureTypes & CaptureType.RequestHeaders) != 0 && cdpRequest.Headers != null)
        {
            headers = cdpRequest.Headers
                .Select(kvp => new HarHeader { Name = kvp.Key, Value = kvp.Value ?? "" })
                .ToList();
        }

        // Cookies (parse from Cookie header)
        var cookies = new List<HarCookie>();
        if ((captureTypes & CaptureType.RequestCookies) != 0)
        {
            var cookieHeader = headers.FirstOrDefault(h => h.Name.Equals("Cookie", StringComparison.OrdinalIgnoreCase));
            if (cookieHeader != null)
            {
                cookies = HttpParsingHelper.ParseCookiesFromHeader(cookieHeader.Value, _logger, "CDP");
            }
        }

        // Query string
        var queryString = HttpParsingHelper.ParseQueryString(cdpRequest.Url);

        // Apply redaction at capture time (RDCT-04)
        if (_redactor != null && _redactor.HasRedactions)
        {
            headers = _redactor.RedactHeaders(headers);
            cookies = _redactor.RedactCookies(cookies);
            queryString = _redactor.RedactQueryString(queryString);
        }

        // Post data
        HarPostData? postData = null;
        if ((captureTypes & CaptureType.RequestContent) != 0 && !string.IsNullOrEmpty(cdpRequest.PostData))
        {
            postData = new HarPostData
            {
                MimeType = "application/octet-stream",
                Params = new List<HarParam>(),
                Text = cdpRequest.PostData
            };
        }

        long bodySize = postData?.Text?.Length ?? 0;

        return new HarRequest
        {
            Method = cdpRequest.Method ?? "GET",
            Url = (_redactor != null && _redactor.HasRedactions)
                ? _redactor.RedactUrl(cdpRequest.Url ?? "")
                : (cdpRequest.Url ?? ""),
            HttpVersion = "HTTP/1.1",
            Headers = headers,
            Cookies = cookies,
            QueryString = queryString,
            PostData = postData,
            HeadersSize = -1,
            BodySize = bodySize
        };
    }

    /// <summary>
    /// Builds a HAR response from CDP response data.
    /// </summary>
    private HarResponse BuildHarResponse(CdpResponseInfo cdpResponse)
    {
        var captureTypes = _options.CaptureTypes;

        // Headers
        var headers = new List<HarHeader>();
        if ((captureTypes & CaptureType.ResponseHeaders) != 0 && cdpResponse.Headers != null)
        {
            headers = cdpResponse.Headers
                .Select(kvp => new HarHeader { Name = kvp.Key, Value = kvp.Value ?? "" })
                .ToList();
        }

        // Cookies (parse from Set-Cookie headers)
        var cookies = new List<HarCookie>();
        if ((captureTypes & CaptureType.ResponseCookies) != 0)
        {
            cookies = HttpParsingHelper.ParseSetCookieHeaders(cdpResponse.Headers, _logger, "CDP");
        }

        // Apply redaction at capture time (RDCT-04)
        if (_redactor != null && _redactor.HasRedactions)
        {
            headers = _redactor.RedactHeaders(headers);
            cookies = _redactor.RedactCookies(cookies);
        }

        // Content (initially without body text)
        var content = new HarContent
        {
            Size = -1,
            MimeType = cdpResponse.MimeType ?? "application/octet-stream"
        };

        return new HarResponse
        {
            Status = (int)cdpResponse.Status,
            StatusText = cdpResponse.StatusText ?? "",
            HttpVersion = cdpResponse.Protocol ?? "HTTP/1.1",
            Headers = headers,
            Cookies = cookies,
            Content = content,
            RedirectURL = "",
            HeadersSize = -1,
            BodySize = -1
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping = true;

        if (_adapter != null)
        {
            // Unsubscribe from events
            _adapter.RequestWillBeSent -= OnRequestWillBeSent;
            _adapter.ResponseReceived -= OnResponseReceived;
            _adapter.LoadingFinished -= OnLoadingFinished;
            _adapter.LoadingFailed -= OnLoadingFailed;

            // Unsubscribe from WebSocket events
            if (_wsAccumulator != null)
            {
                _adapter.WebSocketCreated -= OnWebSocketCreated;
                _adapter.WebSocketWillSendHandshakeRequest -= OnWebSocketWillSendHandshakeRequest;
                _adapter.WebSocketHandshakeResponseReceived -= OnWebSocketHandshakeResponseReceived;
                _adapter.WebSocketFrameSent -= OnWebSocketFrameSent;
                _adapter.WebSocketFrameReceived -= OnWebSocketFrameReceived;
                _adapter.WebSocketClosed -= OnWebSocketClosed;
            }

            // Try to disable Network domain with timeout to prevent hanging
            try
            {
                if (!_adapter.DisableNetworkAsync().Wait(DisposeTimeoutMs))
                {
                    _logger?.Log("CDP", $"Dispose: DisableNetwork timed out after {DisposeTimeoutMs / 1000}s");
                }
            }
            catch
            {
                // Session may already be closed, ignore
            }

            _adapter.Dispose();
        }

        // Do NOT dispose _session — it's a driver-cached singleton from GetDevToolsSession().
        // The driver owns its lifecycle; disposing it here kills the WebSocket for the entire driver
        // and causes "There is already one outstanding SendAsync call" race conditions.
        _bodyChannel?.Writer.TryComplete();
        _bodyChannel = null;
        _bodyWorkers = null;
        _correlator.Clear();
        ClearCache();
        _wsAccumulator?.Clear();
    }

    /// <summary>
    /// Attempts to retrieve a cached response body by URL, promoting it to MRU position on hit.
    /// Thread-safe: ConcurrentDictionary read + LinkedList mutation under lock.
    /// </summary>
    private bool TryGetCachedBody(string url, out (string? Body, bool Base64Encoded) result)
    {
        if (_bodyCache.TryGetValue(url, out var node))
        {
            lock (_cacheLock)
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
            }
            result = (node.Value.Body, node.Value.Base64Encoded);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Caches a response body with LRU eviction when at capacity.
    /// Thread-safe: All operations protected by lock.
    /// </summary>
    private void CacheBody(string url, string? body, bool base64Encoded)
    {
        lock (_cacheLock)
        {
            if (_bodyCache.TryGetValue(url, out var existingNode))
            {
                // Already cached, move to front
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            // Evict oldest if at capacity
            if (_bodyCache.Count >= MaxCacheEntries)
            {
                var oldest = _lruList.Last;
                if (oldest != null)
                {
                    _bodyCache.TryRemove(oldest.Value.Url, out _);
                    _lruList.RemoveLast();
                    _logger?.Log("CDP", $"Cache evicted LRU entry: {oldest.Value.Url}");
                }
            }

            // Add new entry at front
            var entry = new CacheEntry { Url = url, Body = body, Base64Encoded = base64Encoded };
            var node = new LinkedListNode<CacheEntry>(entry);
            _lruList.AddFirst(node);
            _bodyCache[url] = node;
        }
    }

    /// <summary>
    /// Clears both the ConcurrentDictionary and LinkedList under lock.
    /// Called during StopAsync and Dispose.
    /// </summary>
    private void ClearCache()
    {
        lock (_cacheLock)
        {
            _bodyCache.Clear();
            _lruList.Clear();
        }
    }

    /// <summary>
    /// Worker loop that reads body retrieval requests from the channel and processes them sequentially.
    /// Multiple workers run concurrently, providing bounded parallelism for CDP getResponseBody calls.
    /// </summary>
    private async Task BodyWorkerLoop(ChannelReader<BodyRetrievalRequest> reader, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var request))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await RetrieveResponseBodyAsync(request.RequestId, request.Entry, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.Log("CDP", "Body worker cancelled");
        }
        catch (Exception ex)
        {
            _logger?.Log("CDP", $"BodyWorkerLoop exited with error: {ex.Message}");
        }
    }

    /// <summary>
    /// Request to retrieve the response body for a specific request.
    /// </summary>
    private readonly struct BodyRetrievalRequest
    {
        public string RequestId { get; }
        public HarEntry Entry { get; }

        public BodyRetrievalRequest(string requestId, HarEntry entry)
        {
            RequestId = requestId;
            Entry = entry;
        }
    }

    /// <summary>
    /// Internal helper for tracking response body info with URL for LRU eviction.
    /// </summary>
    private sealed class CacheEntry
    {
        public string Url { get; set; } = "";
        public string? Body { get; set; }
        public bool Base64Encoded { get; set; }
    }
}
