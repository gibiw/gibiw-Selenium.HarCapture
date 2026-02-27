using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Provides redaction of sensitive data in HAR entries at capture time.
/// Supports case-insensitive matching for headers and cookies, wildcard patterns for query parameters.
/// </summary>
internal sealed class SensitiveDataRedactor
{
    private const string RedactedValue = "[REDACTED]";

    private readonly HashSet<string> _sensitiveHeaders;
    private readonly HashSet<string> _sensitiveCookies;
    private readonly Regex? _queryParamPattern;
    private readonly bool _hasRedactions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveDataRedactor"/> class.
    /// </summary>
    /// <param name="sensitiveHeaders">Case-insensitive header names to redact (e.g., "Authorization").</param>
    /// <param name="sensitiveCookies">Case-insensitive cookie names to redact (e.g., "session_id").</param>
    /// <param name="sensitiveQueryParams">Wildcard patterns for query parameter names (e.g., "api_*", "token").</param>
    public SensitiveDataRedactor(
        IReadOnlyList<string>? sensitiveHeaders,
        IReadOnlyList<string>? sensitiveCookies,
        IReadOnlyList<string>? sensitiveQueryParams)
    {
        _sensitiveHeaders = sensitiveHeaders != null && sensitiveHeaders.Count > 0
            ? new HashSet<string>(sensitiveHeaders, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _sensitiveCookies = sensitiveCookies != null && sensitiveCookies.Count > 0
            ? new HashSet<string>(sensitiveCookies, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _queryParamPattern = BuildQueryParamPattern(sensitiveQueryParams);

        _hasRedactions = _sensitiveHeaders.Count > 0
                      || _sensitiveCookies.Count > 0
                      || _queryParamPattern != null;
    }

    /// <summary>
    /// Gets a value indicating whether any redaction rules are configured.
    /// Used as a fast path to skip redaction when disabled.
    /// </summary>
    public bool HasRedactions => _hasRedactions;

    /// <summary>
    /// Redacts sensitive header values, replacing them with "[REDACTED]".
    /// </summary>
    /// <param name="headers">The original headers list.</param>
    /// <returns>A new list with redacted header values.</returns>
    public List<HarHeader> RedactHeaders(List<HarHeader> headers)
    {
        if (headers.Count == 0 || _sensitiveHeaders.Count == 0)
            return headers;

        var result = new List<HarHeader>(headers.Count);
        foreach (var header in headers)
        {
            if (_sensitiveHeaders.Contains(header.Name))
            {
                result.Add(new HarHeader
                {
                    Name = header.Name,
                    Value = RedactedValue,
                    Comment = header.Comment
                });
            }
            else
            {
                result.Add(header);
            }
        }
        return result;
    }

    /// <summary>
    /// Redacts sensitive cookie values, replacing them with "[REDACTED]".
    /// </summary>
    /// <param name="cookies">The original cookies list.</param>
    /// <returns>A new list with redacted cookie values.</returns>
    public List<HarCookie> RedactCookies(List<HarCookie> cookies)
    {
        if (cookies.Count == 0 || _sensitiveCookies.Count == 0)
            return cookies;

        var result = new List<HarCookie>(cookies.Count);
        foreach (var cookie in cookies)
        {
            if (_sensitiveCookies.Contains(cookie.Name))
            {
                result.Add(new HarCookie
                {
                    Name = cookie.Name,
                    Value = RedactedValue,
                    Path = cookie.Path,
                    Domain = cookie.Domain,
                    Expires = cookie.Expires,
                    HttpOnly = cookie.HttpOnly,
                    Secure = cookie.Secure,
                    Comment = cookie.Comment
                });
            }
            else
            {
                result.Add(cookie);
            }
        }
        return result;
    }

    /// <summary>
    /// Redacts sensitive query string parameter values, replacing them with "[REDACTED]".
    /// </summary>
    /// <param name="queryParams">The original query parameters list.</param>
    /// <returns>A new list with redacted query parameter values.</returns>
    public List<HarQueryString> RedactQueryString(List<HarQueryString> queryParams)
    {
        if (queryParams.Count == 0 || _queryParamPattern == null)
            return queryParams;

        var result = new List<HarQueryString>(queryParams.Count);
        foreach (var param in queryParams)
        {
            if (_queryParamPattern.IsMatch(param.Name))
            {
                result.Add(new HarQueryString
                {
                    Name = param.Name,
                    Value = RedactedValue,
                    Comment = param.Comment
                });
            }
            else
            {
                result.Add(param);
            }
        }
        return result;
    }

    /// <summary>
    /// Redacts sensitive query parameter values in a URL string.
    /// </summary>
    /// <param name="url">The original URL.</param>
    /// <returns>The URL with redacted query parameter values.</returns>
    public string RedactUrl(string url)
    {
        if (string.IsNullOrEmpty(url) || _queryParamPattern == null)
            return url;

        // Try to parse URL and redact query string
        if (!TryParseUrl(url, out var baseUrl, out var queryString))
            return url;

        if (string.IsNullOrEmpty(queryString))
            return url;

        var redactedQuery = RedactQueryStringInUrl(queryString);
        return baseUrl + "?" + redactedQuery;
    }

    private static Regex? BuildQueryParamPattern(IReadOnlyList<string>? patterns)
    {
        if (patterns == null || patterns.Count == 0)
            return null;

        // Convert wildcard patterns to regex patterns
        var regexPatterns = patterns.Select(pattern =>
        {
            var escaped = Regex.Escape(pattern);
            var withWildcards = escaped.Replace("\\*", ".*").Replace("\\?", ".");
            return "^" + withWildcards + "$";
        });

        var combinedPattern = string.Join("|", regexPatterns);
        return new Regex(combinedPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static bool TryParseUrl(string url, out string baseUrl, out string queryString)
    {
        baseUrl = url;
        queryString = string.Empty;

        try
        {
            var questionMarkIndex = url.IndexOf('?');
            if (questionMarkIndex < 0)
                return true; // No query string

            baseUrl = url.Substring(0, questionMarkIndex);
            queryString = url.Substring(questionMarkIndex + 1);

            // Remove fragment if present
            var hashIndex = queryString.IndexOf('#');
            if (hashIndex >= 0)
                queryString = queryString.Substring(0, hashIndex);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string RedactQueryStringInUrl(string queryString)
    {
        if (_queryParamPattern == null)
            return queryString;

        var parameters = queryString.Split('&');
        var redactedParams = new List<string>(parameters.Length);

        foreach (var param in parameters)
        {
            var equalsIndex = param.IndexOf('=');
            if (equalsIndex < 0)
            {
                // No value, keep as-is
                redactedParams.Add(param);
                continue;
            }

            var name = param.Substring(0, equalsIndex);
            var value = param.Substring(equalsIndex + 1);

            if (_queryParamPattern.IsMatch(name))
            {
                redactedParams.Add(name + "=" + RedactedValue);
            }
            else
            {
                redactedParams.Add(param);
            }
        }

        return string.Join("&", redactedParams);
    }
}
