using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Shared HTTP parsing utilities used by both CDP and INetwork capture strategies.
/// </summary>
internal static class HttpParsingHelper
{
    /// <summary>
    /// Parses query string parameters from a URL.
    /// </summary>
    internal static List<HarQueryString> ParseQueryString(string url)
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
    /// Parses cookies from a Cookie header value.
    /// </summary>
    internal static List<HarCookie> ParseCookiesFromHeader(string? cookieHeader, FileLogger? logger = null, string tag = "")
    {
        var result = new List<HarCookie>();

        if (string.IsNullOrEmpty(cookieHeader))
        {
            return result;
        }

        try
        {
            // Cookie header format: "name1=value1; name2=value2"
            var pairs = cookieHeader!.Split(';');
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
        catch (Exception ex)
        {
            logger?.Log(tag, $"ParseCookiesFromHeader failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Parses Set-Cookie headers from response headers.
    /// Accepts IEnumerable&lt;KeyValuePair&lt;string, string&gt;&gt; to work with both
    /// IDictionary and IReadOnlyDictionary.
    /// </summary>
    internal static List<HarCookie> ParseSetCookieHeaders(IEnumerable<KeyValuePair<string, string>>? headers, FileLogger? logger = null, string tag = "")
    {
        var result = new List<HarCookie>();

        if (headers == null)
        {
            return result;
        }

        try
        {
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
        catch (Exception ex)
        {
            logger?.Log(tag, $"ParseSetCookieHeaders failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Extracts the MIME type from the Content-Type header in a headers collection.
    /// </summary>
    internal static string ExtractMimeType(IEnumerable<KeyValuePair<string, string>>? headers, string defaultMimeType = "application/octet-stream")
    {
        if (headers == null) return defaultMimeType;

        foreach (var kvp in headers)
        {
            if (kvp.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(kvp.Value))
            {
                // Content-Type may include charset/boundary, extract just the MIME type
                return kvp.Value.Split(';')[0].Trim();
            }
        }

        return defaultMimeType;
    }
}
