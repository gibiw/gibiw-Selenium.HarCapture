using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
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
    private readonly ConcurrentDictionary<string, ResponseBodyInfo> _responseBodies = new();
    private ConcurrentBag<Task> _pendingBodyTasks = new();
    private UrlPatternMatcher? _urlMatcher;
    private bool _disposed;

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
    public event Action<HarEntry, string>? EntryCompleted;

    /// <inheritdoc />
    public async Task StartAsync(CaptureOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Verify driver supports CDP
        var devTools = _driver as IDevTools;
        if (devTools == null)
        {
            throw new InvalidOperationException(
                "Driver does not support Chrome DevTools Protocol. Use a Chromium-based browser (Chrome, Edge).");
        }

        // Create CDP session and auto-detect version
        _session = devTools.GetDevToolsSession();
        _adapter = CdpAdapterFactory.Create(_session);

        // Initialize URL matcher for filtering
        _urlMatcher = new UrlPatternMatcher(options.UrlIncludePatterns, options.UrlExcludePatterns);

        // Subscribe to adapter events BEFORE enabling (critical order)
        _adapter.RequestWillBeSent += OnRequestWillBeSent;
        _adapter.ResponseReceived += OnResponseReceived;
        _adapter.LoadingFinished += OnLoadingFinished;
        _adapter.LoadingFailed += OnLoadingFailed;

        // Enable Network domain
        await _adapter.EnableNetworkAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_adapter != null)
        {
            // Unsubscribe from events FIRST to prevent new entries during drain
            _adapter.RequestWillBeSent -= OnRequestWillBeSent;
            _adapter.ResponseReceived -= OnResponseReceived;
            _adapter.LoadingFinished -= OnLoadingFinished;
            _adapter.LoadingFailed -= OnLoadingFailed;

            // Wait for all in-flight body retrievals to complete
            var tasks = _pendingBodyTasks.ToArray();
            if (tasks.Length > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            try
            {
                await _adapter.DisableNetworkAsync().ConfigureAwait(false);
            }
            catch
            {
                // Session may already be closed, ignore
            }
        }

        _pendingBodyTasks = new ConcurrentBag<Task>();
        _correlator.Clear();
        _responseBodies.Clear();
    }

    /// <summary>
    /// Handles CDP requestWillBeSent event.
    /// Fires when a request is about to be sent on the network.
    /// </summary>
    private void OnRequestWillBeSent(CdpRequestWillBeSentData e)
    {
        try
        {
            // URL filtering
            if (_urlMatcher != null && !_urlMatcher.ShouldCapture(e.Request.Url))
            {
                return;
            }

            // Handle redirects: complete previous entry with redirect response
            if (e.RedirectResponse != null)
            {
                CompleteRedirectEntry(e.RequestId, e.RedirectResponse, e.Timestamp);
            }

            // Build HAR request
            var harRequest = BuildHarRequest(e.Request);

            // Calculate started time from WallTime (CDP uses epoch seconds)
            var startedDateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(e.WallTime * 1000));

            // Record request in correlator
            _correlator.OnRequestSent(e.RequestId, harRequest, startedDateTime);
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
            }

            // Correlate response with request
            var entry = _correlator.OnResponseReceived(e.RequestId, harResponse, harTimings, totalTime);

            if (entry != null)
            {
                // Determine if we should retrieve response body
                bool shouldGetBody = ShouldRetrieveResponseBody(e.Response.Status);

                if (shouldGetBody)
                {
                    // Track async body retrieval task to ensure completion before StopAsync
                    var task = RetrieveResponseBodyAsync(e.RequestId, entry);
                    _pendingBodyTasks.Add(task);
                }
                else
                {
                    // No body needed, fire EntryCompleted immediately
                    EntryCompleted?.Invoke(entry, e.RequestId);
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
        // Failed requests do not produce complete HAR entries.
        // Remove pending entry from correlator to free memory.
        // In the future, we could optionally create error entries.
        // For now, silently drop failed requests.
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
    /// </summary>
    private async Task RetrieveResponseBodyAsync(string requestId, HarEntry entry)
    {
        try
        {
            if (_adapter == null)
            {
                EntryCompleted?.Invoke(entry, requestId);
                return;
            }

            var (body, base64Encoded) = await _adapter.GetResponseBodyAsync(requestId).ConfigureAwait(false);

            // Check MaxResponseBodySize limit
            string? bodyText = body;
            string? encoding = base64Encoded ? "base64" : null;
            long bodySize = body?.Length ?? 0;

            if (_options.MaxResponseBodySize > 0 && bodySize > _options.MaxResponseBodySize)
            {
                // Truncate body
                bodyText = body?.Substring(0, (int)_options.MaxResponseBodySize);
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
                Comment = entry.Comment
            };

            EntryCompleted?.Invoke(updatedEntry, requestId);
        }
        catch (Exception ex)
        {
            // "No resource with given identifier found" is expected when resource was already dumped
            _logger?.Log("CDP", $"Could not retrieve response body for {requestId}: {ex.Message}");

            // Fire EntryCompleted with entry without body content
            EntryCompleted?.Invoke(entry, requestId);
        }
    }

    /// <summary>
    /// Determines whether response body should be retrieved based on status code and capture options.
    /// </summary>
    private bool ShouldRetrieveResponseBody(long status)
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

        return wantsContent;
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
                cookies = ParseCookiesFromHeader(cookieHeader.Value);
            }
        }

        // Query string
        var queryString = ParseQueryString(cdpRequest.Url);

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
            Url = cdpRequest.Url ?? "",
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
            cookies = ParseSetCookieHeaders(cdpResponse.Headers);
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
    private List<HarCookie> ParseSetCookieHeaders(IDictionary<string, string>? headers)
    {
        var result = new List<HarCookie>();

        if (headers == null)
        {
            return result;
        }

        try
        {
            // CDP may return Set-Cookie as a single entry or multiple entries
            foreach (var kvp in headers)
            {
                if (kvp.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
                {
                    var value = kvp.Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        // Simplified parsing: just extract name=value
                        var parts = value.Split(';')[0].Split(new[] { '=' }, 2);
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

        if (_adapter != null)
        {
            // Unsubscribe from events
            _adapter.RequestWillBeSent -= OnRequestWillBeSent;
            _adapter.ResponseReceived -= OnResponseReceived;
            _adapter.LoadingFinished -= OnLoadingFinished;
            _adapter.LoadingFailed -= OnLoadingFailed;

            // Try to disable Network domain
            try
            {
                _adapter.DisableNetworkAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Session may already be closed, ignore
            }

            _adapter.Dispose();
        }

        _session?.Dispose();
        _pendingBodyTasks = new ConcurrentBag<Task>();
        _correlator.Clear();
        _responseBodies.Clear();
    }

    /// <summary>
    /// Internal helper for tracking response body info.
    /// </summary>
    private sealed class ResponseBodyInfo
    {
        public string? Body { get; set; }
        public bool Base64Encoded { get; set; }
    }
}
