using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
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
    public event Action<HarEntry, string>? EntryCompleted;

    /// <inheritdoc />
    public async Task StartAsync(CaptureOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize URL matcher for filtering
        _urlMatcher = new UrlPatternMatcher(options.UrlIncludePatterns, options.UrlExcludePatterns);

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
    public async Task StopAsync()
    {
        if (_network != null)
        {
            try
            {
                var stopTask = _network.StopMonitoring();
                var completed = await Task.WhenAny(stopTask, Task.Delay(StopTimeoutMs)).ConfigureAwait(false);
                if (completed == stopTask)
                {
                    await stopTask.ConfigureAwait(false);
                    _logger?.Log("INetwork", "StopAsync: monitoring stopped");
                }
                else
                {
                    _logger?.Log("INetwork", $"StopAsync: StopMonitoring timed out after {StopTimeoutMs / 1000}s, forcing stop");
                }
            }
            catch (Exception ex)
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
                EntryCompleted?.Invoke(entry, e.RequestId);
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
                cookies = ParseCookiesFromHeader(cookieHeader.Value);
            }
        }

        // Query string
        var queryString = ParseQueryString(e.RequestUrl ?? "");

        // Post data
        HarPostData? postData = null;
        if ((captureTypes & CaptureType.RequestContent) != 0 && !string.IsNullOrEmpty(e.RequestPostData))
        {
            postData = new HarPostData
            {
                MimeType = "application/octet-stream",
                Params = new List<HarParam>(),
                Text = e.RequestPostData
            };
        }

        long bodySize = postData?.Text?.Length ?? -1;

        return new HarRequest
        {
            Method = e.RequestMethod ?? "GET",
            Url = e.RequestUrl ?? "",
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
            cookies = ParseSetCookieHeaders(e.ResponseHeaders);
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
            bodyText = e.ResponseBody;
            bodySize = bodyText.Length;

            // Check MaxResponseBodySize limit
            if (_options.MaxResponseBodySize > 0 && bodySize > _options.MaxResponseBodySize)
            {
                bodyText = bodyText.Substring(0, (int)_options.MaxResponseBodySize);
                bodySize = _options.MaxResponseBodySize;
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

    /// <summary>
    /// Parses query string from URL.
    /// </summary>
    private List<HarQueryString> ParseQueryString(string url)
    {
        var result = new List<HarQueryString>();

        try
        {
            var uri = new Uri(url);
            var query = uri.Query;

            if (string.IsNullOrEmpty(query) || query == "?")
            {
                return result;
            }

            // Remove leading '?'
            query = query.TrimStart('?');

            // Parse key=value pairs
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var parts = pair.Split(new[] { '=' }, 2);
                var name = HttpUtility.UrlDecode(parts[0]);
                var value = parts.Length > 1 ? HttpUtility.UrlDecode(parts[1]) : "";

                result.Add(new HarQueryString { Name = name, Value = value });
            }
        }
        catch
        {
            // Invalid URL, return empty list
        }

        return result;
    }

    /// <summary>
    /// Parses cookies from Cookie header value.
    /// </summary>
    private List<HarCookie> ParseCookiesFromHeader(string? cookieHeader)
    {
        var result = new List<HarCookie>();

        if (string.IsNullOrEmpty(cookieHeader))
        {
            return result;
        }

        try
        {
            // Cookie header format: "name1=value1; name2=value2"
            var pairs = cookieHeader.Split(';');
            foreach (var pair in pairs)
            {
                var trimmed = pair.Trim();
                var parts = trimmed.Split(new[] { '=' }, 2);

                if (parts.Length == 2)
                {
                    result.Add(new HarCookie
                    {
                        Name = parts[0].Trim(),
                        Value = parts[1].Trim()
                    });
                }
            }
        }
        catch
        {
            // Invalid cookie header, return empty list
        }

        return result;
    }

    /// <summary>
    /// Parses Set-Cookie headers from response headers dictionary.
    /// </summary>
    private List<HarCookie> ParseSetCookieHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        var result = new List<HarCookie>();

        if (headers == null)
        {
            return result;
        }

        try
        {
            // INetwork uses IReadOnlyDictionary<string, string> for headers
            foreach (var kvp in headers)
            {
                if (kvp.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
                {
                    var value = kvp.Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        // Simplified parsing: just extract name=value
                        var parts = value!.Split(';')[0].Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            result.Add(new HarCookie
                            {
                                Name = parts[0].Trim(),
                                Value = parts[1].Trim()
                            });
                        }
                    }
                }
            }
        }
        catch
        {
            // Invalid Set-Cookie header, return empty list
        }

        return result;
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
