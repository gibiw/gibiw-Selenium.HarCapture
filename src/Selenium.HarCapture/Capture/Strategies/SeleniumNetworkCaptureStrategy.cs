using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using Selenium.HarCapture.Capture.Internal;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Strategies;

/// <summary>
/// Captures network traffic using Selenium's INetwork API (event-based).
/// Fallback strategy for non-Chromium browsers or when CDP is unavailable.
/// Does not provide detailed timings but supports request and response body capture.
/// </summary>
internal sealed class SeleniumNetworkCaptureStrategy : INetworkCaptureStrategy
{
    private readonly IWebDriver _driver;
    private readonly FileLogger? _logger;
    private INetwork? _network;
    private CaptureOptions _options = null!;
    private readonly RequestResponseCorrelator _correlator = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _requestTimestamps = new();
    private UrlPatternMatcher? _urlMatcher;
    private SensitiveDataRedactor? _redactor;
    private MimeTypeMatcher _mimeMatcher = MimeTypeMatcher.CaptureAll;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeleniumNetworkCaptureStrategy"/> class.
    /// </summary>
    /// <param name="driver">The WebDriver instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
    internal SeleniumNetworkCaptureStrategy(IWebDriver driver, FileLogger? logger = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _logger = logger;
    }

    /// <inheritdoc />
    public string StrategyName => "INetwork";

    /// <inheritdoc />
    public bool SupportsDetailedTimings => false;

    /// <inheritdoc />
    public bool SupportsResponseBody => true;

    /// <inheritdoc />
    public double? LastDomContentLoadedTimestamp => null;

    /// <inheritdoc />
    public double? LastLoadTimestamp => null;

    /// <inheritdoc />
    public event Action<HarEntry, string>? EntryCompleted;

    /// <inheritdoc />
    public async Task StartAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        cancellationToken.ThrowIfCancellationRequested();

        // Initialize URL matcher for filtering
        _urlMatcher = new UrlPatternMatcher(options.UrlIncludePatterns, options.UrlExcludePatterns);

        // Initialize redactor for sensitive data
        _redactor = new SensitiveDataRedactor(
            options.SensitiveHeaders,
            options.SensitiveCookies,
            options.SensitiveQueryParams);

        // Initialize MIME type matcher for body filtering (HAR-04 — parity with CDP strategy)
        _mimeMatcher = MimeTypeMatcher.FromScope(options.ResponseBodyScope, options.ResponseBodyMimeFilter);

        // Get INetwork from driver
        _network = _driver.Manage().Network;

        // Subscribe to events BEFORE starting monitoring (critical order)
        _network.NetworkRequestSent += OnNetworkRequestSent;
        _network.NetworkResponseReceived += OnNetworkResponseReceived;

        // Start monitoring
        await _network.StartMonitoring().ConfigureAwait(false);
        _logger?.Log("INetwork", "StartAsync: network monitoring started");
    }

    private const int StopTimeoutMs = 10_000;
    private const int DisposeTimeoutMs = 5_000;

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_network != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Create linked token source combining caller's token with 10-second timeout
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(TimeSpan.FromSeconds(10));

                var stopTask = _network.StopMonitoring();
                try
                {
                    var completed = await Task.WhenAny(stopTask, Task.Delay(Timeout.Infinite, linkedCts.Token)).ConfigureAwait(false);
                    if (completed == stopTask)
                    {
                        await stopTask.ConfigureAwait(false);
                        _logger?.Log("INetwork", "StopAsync: monitoring stopped");
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
                        _logger?.Log("INetwork", $"StopAsync: StopMonitoring timed out after {StopTimeoutMs / 1000}s, forcing stop");
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger?.Log("INetwork", $"StopAsync: StopMonitoring failed: {ex.Message}");
            }

            // Unsubscribe from events
            _network.NetworkRequestSent -= OnNetworkRequestSent;
            _network.NetworkResponseReceived -= OnNetworkResponseReceived;
        }

        _correlator.Clear();
        _requestTimestamps.Clear();
    }

    /// <summary>
    /// Handles INetwork NetworkRequestSent event.
    /// Fires when a request is about to be sent on the network.
    /// </summary>
    private void OnNetworkRequestSent(object? sender, NetworkRequestSentEventArgs e)
    {
        try
        {
            var requestId = e.RequestId ?? "";
            _logger?.Log("INetwork", $"RequestSent: id={requestId}, {e.RequestMethod} {e.RequestUrl}");

            // URL filtering
            if (_urlMatcher != null && !_urlMatcher.ShouldCapture(e.RequestUrl ?? ""))
            {
                _logger?.Log("INetwork", $"Request filtered out by URL pattern: {e.RequestUrl}");
                return;
            }

            // Build HAR request
            var harRequest = BuildHarRequest(e);

            // Record timestamp for basic timing calculation
            var now = DateTimeOffset.UtcNow;
            _requestTimestamps[requestId] = now;

            // Record request in correlator
            _correlator.OnRequestSent(requestId, harRequest, now);
            _logger?.Log("INetwork", $"Request recorded: id={requestId}, pending={_correlator.PendingCount}");
        }
        catch (Exception ex)
        {
            // Don't let exceptions in event handlers crash the capture
            _logger?.Log("INetwork", $"Error in OnNetworkRequestSent: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles INetwork NetworkResponseReceived event.
    /// Fires when HTTP response is received.
    /// </summary>
    private void OnNetworkResponseReceived(object? sender, NetworkResponseReceivedEventArgs e)
    {
        try
        {
            var requestId = e.RequestId ?? "";
            _logger?.Log("INetwork", $"ResponseReceived: id={requestId}, status={e.ResponseStatusCode}");

            // Build HAR response
            var harResponse = BuildHarResponse(e);

            // Calculate basic timing from request/response timestamps
            double totalTime = 0;
            bool timestampFound = _requestTimestamps.TryRemove(requestId, out var requestTime);
            if (timestampFound)
            {
                totalTime = (DateTimeOffset.UtcNow - requestTime).TotalMilliseconds;
            }

            _logger?.Log("INetwork", $"Timing: totalTime={totalTime:F1}ms (timestamp {(timestampFound ? "found" : "not found")})");

            var harTimings = new HarTimings
            {
                Send = 0,
                Wait = totalTime,
                Receive = 0
            };

            // Correlate response with request
            var entry = _correlator.OnResponseReceived(requestId, harResponse, harTimings, totalTime);

            if (entry == null)
            {
                _logger?.Log("INetwork", $"Correlation failed: no matching request for id={requestId}");
            }
            else
            {
                var bodySize = harResponse.Content?.Size ?? -1;
                var truncated = _options.MaxResponseBodySize > 0 && (e.ResponseBody?.Length ?? 0) > _options.MaxResponseBodySize;
                _logger?.Log("INetwork", $"Body: size={bodySize}, truncated={( truncated ? "yes" : "no")}");
                _logger?.Log("INetwork", $"EntryCompleted fired: id={requestId}");
                EntryCompleted?.Invoke(entry, requestId);
            }
        }
        catch (Exception ex)
        {
            _logger?.Log("INetwork", $"Error in OnNetworkResponseReceived: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds a HAR request from INetwork request data.
    /// </summary>
    private HarRequest BuildHarRequest(NetworkRequestSentEventArgs e)
    {
        var captureTypes = _options.CaptureTypes;

        // Headers
        var headers = new List<HarHeader>();
        if ((captureTypes & CaptureType.RequestHeaders) != 0 && e.RequestHeaders != null)
        {
            headers = e.RequestHeaders
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
                cookies = HttpParsingHelper.ParseCookiesFromHeader(cookieHeader.Value, _logger, "INetwork");
            }
        }

        // Query string
        var queryString = HttpParsingHelper.ParseQueryString(e.RequestUrl ?? "");

        // Apply redaction at capture time (RDCT-04)
        if (_redactor != null && _redactor.HasRedactions)
        {
            headers = _redactor.RedactHeaders(headers);
            cookies = _redactor.RedactCookies(cookies);
            queryString = _redactor.RedactQueryString(queryString);
        }

        // Post data
        HarPostData? postData = null;
        if ((captureTypes & CaptureType.RequestContent) != 0 && !string.IsNullOrEmpty(e.RequestPostData))
        {
            postData = new HarPostData
            {
                MimeType = HttpParsingHelper.ExtractMimeType(e.RequestHeaders),
                Params = new List<HarParam>(),
                Text = e.RequestPostData
            };
        }

        long bodySize = postData?.Text?.Length ?? -1;

        return new HarRequest
        {
            Method = e.RequestMethod ?? "GET",
            Url = (_redactor != null && _redactor.HasRedactions)
                ? _redactor.RedactUrl(e.RequestUrl ?? "")
                : (e.RequestUrl ?? ""),
            // INetwork API does not expose protocol version; defaults to HTTP/1.1 (HAR-01 limitation)
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
    /// Builds a HAR response from INetwork response data.
    /// </summary>
    private HarResponse BuildHarResponse(NetworkResponseReceivedEventArgs e)
    {
        var captureTypes = _options.CaptureTypes;

        // Headers
        var headers = new List<HarHeader>();
        if ((captureTypes & CaptureType.ResponseHeaders) != 0 && e.ResponseHeaders != null)
        {
            headers = e.ResponseHeaders
                .Select(kvp => new HarHeader { Name = kvp.Key, Value = kvp.Value ?? "" })
                .ToList();
        }

        // Cookies (parse from Set-Cookie headers)
        var cookies = new List<HarCookie>();
        if ((captureTypes & CaptureType.ResponseCookies) != 0)
        {
            cookies = HttpParsingHelper.ParseSetCookieHeaders(e.ResponseHeaders, _logger, "INetwork");
        }

        // Apply redaction at capture time (RDCT-04)
        if (_redactor != null && _redactor.HasRedactions)
        {
            headers = _redactor.RedactHeaders(headers);
            cookies = _redactor.RedactCookies(cookies);
        }

        // Extract MIME type from Content-Type header
        string mimeType = "application/octet-stream";
        var contentTypeHeader = headers.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
        if (contentTypeHeader != null && !string.IsNullOrEmpty(contentTypeHeader.Value))
        {
            // Content-Type may include charset, extract just the MIME type
            var parts = contentTypeHeader.Value.Split(';');
            mimeType = parts[0].Trim();
        }

        // Response body
        string? bodyText = null;
        long bodySize = -1;
        bool wantsContent = (captureTypes & CaptureType.ResponseContent) != 0 ||
                            (captureTypes & CaptureType.ResponseBinaryContent) != 0;

        if (wantsContent && e.ResponseBody != null)
        {
            // HAR-04: Apply MIME filtering (parity with CDP strategy)
            if (!_mimeMatcher.ShouldRetrieveBody(mimeType))
            {
                _logger?.Log("INetwork", $"Body skipped by ResponseBodyScope filter: mimeType={mimeType}");
            }
            else
            {
                bodyText = e.ResponseBody;
                bodySize = bodyText.Length;

                // Check MaxResponseBodySize limit
                if (_options.MaxResponseBodySize > 0 && bodySize > _options.MaxResponseBodySize)
                {
                    bodyText = bodyText.Substring(0, (int)_options.MaxResponseBodySize);
                    bodySize = _options.MaxResponseBodySize;
                }
            }
        }

        var content = new HarContent
        {
            Size = bodySize,
            MimeType = mimeType,
            Text = bodyText
        };

        return new HarResponse
        {
            Status = (int)e.ResponseStatusCode,
            StatusText = "",
            HttpVersion = "HTTP/1.1",
            Headers = headers,
            Cookies = cookies,
            Content = content,
            RedirectURL = "",
            HeadersSize = -1,
            BodySize = bodySize
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

        if (_network != null)
        {
            // Unsubscribe from events
            _network.NetworkRequestSent -= OnNetworkRequestSent;
            _network.NetworkResponseReceived -= OnNetworkResponseReceived;

            // Try to stop monitoring with timeout to prevent hanging
            try
            {
                if (!_network.StopMonitoring().Wait(DisposeTimeoutMs))
                {
                    _logger?.Log("INetwork", $"Dispose: StopMonitoring timed out after {DisposeTimeoutMs / 1000}s");
                }
            }
            catch
            {
                // Session may already be closed, ignore
            }
        }

        _correlator.Clear();
        _requestTimestamps.Clear();
    }

}
