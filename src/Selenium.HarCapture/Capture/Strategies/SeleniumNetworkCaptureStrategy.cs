using System;
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
    private INetwork? _network;
    private CaptureOptions _options = null!;
    private readonly RequestResponseCorrelator _correlator = new();
    private UrlPatternMatcher? _urlMatcher;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeleniumNetworkCaptureStrategy"/> class.
    /// </summary>
    /// <param name="driver">The WebDriver instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
    internal SeleniumNetworkCaptureStrategy(IWebDriver driver)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
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
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_network != null)
        {
            try
            {
                await _network.StopMonitoring().ConfigureAwait(false);
            }
            catch
            {
                // Session may already be closed, ignore
            }

            // Unsubscribe from events
            _network.NetworkRequestSent -= OnNetworkRequestSent;
            _network.NetworkResponseReceived -= OnNetworkResponseReceived;
        }

        _correlator.Clear();
    }

    /// <summary>
    /// Handles INetwork NetworkRequestSent event.
    /// Fires when a request is about to be sent on the network.
    /// </summary>
    private void OnNetworkRequestSent(object? sender, NetworkRequestSentEventArgs e)
    {
        try
        {
            // URL filtering
            if (_urlMatcher != null && !_urlMatcher.ShouldCapture(e.RequestUrl ?? ""))
            {
                return;
            }

            // Build HAR request
            var harRequest = BuildHarRequest(e);

            // Record request in correlator
            _correlator.OnRequestSent(e.RequestId ?? "", harRequest, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            // Don't let exceptions in event handlers crash the capture
            System.Diagnostics.Debug.WriteLine($"[INetwork] Error in OnNetworkRequestSent: {ex.Message}");
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
            // Build HAR response
            var harResponse = BuildHarResponse(e);

            // INetwork lacks detailed timing data, use simple timings
            var harTimings = new HarTimings
            {
                Send = 0,
                Wait = 0,
                Receive = 0
            };

            // Correlate response with request
            var entry = _correlator.OnResponseReceived(e.RequestId ?? "", harResponse, harTimings, 0);

            if (entry != null)
            {
                EntryCompleted?.Invoke(entry, e.RequestId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[INetwork] Error in OnNetworkResponseReceived: {ex.Message}");
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

            // Try to stop monitoring
            try
            {
                _network.StopMonitoring().GetAwaiter().GetResult();
            }
            catch
            {
                // Session may already be closed, ignore
            }
        }

        _correlator.Clear();
    }
}
