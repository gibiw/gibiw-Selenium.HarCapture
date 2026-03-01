using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Provides redaction of sensitive data in HAR entries at capture time.
/// Supports case-insensitive matching for headers and cookies, wildcard patterns for query parameters,
/// and regex-based body redaction with ReDoS protection (100ms matchTimeout, 512 KB size gate).
/// </summary>
internal sealed class SensitiveDataRedactor
{
    private const string RedactedValue = "[REDACTED]";

    // Body redaction constants (ReDoS protection)
    private static readonly TimeSpan BodyMatchTimeout = TimeSpan.FromMilliseconds(100);
    private const int MaxBodySizeForRedaction = 512 * 1024; // 512 KB (char count)

    private readonly HashSet<string> _sensitiveHeaders;
    private readonly HashSet<string> _sensitiveCookies;
    private readonly Regex? _queryParamPattern;
    private readonly Regex[]? _bodyPatterns;
    private readonly bool _hasRedactions;

    // Thread-safe audit counters (accessed concurrently by body worker tasks)
    private int _bodyRedactionCount;
    private int _wsRedactionCount;
    private int _bodySkippedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveDataRedactor"/> class.
    /// </summary>
    /// <param name="sensitiveHeaders">Case-insensitive header names to redact (e.g., "Authorization").</param>
    /// <param name="sensitiveCookies">Case-insensitive cookie names to redact (e.g., "session_id").</param>
    /// <param name="sensitiveQueryParams">Wildcard patterns for query parameter names (e.g., "api_*", "token").</param>
    /// <param name="sensitiveBodyPatterns">Regex patterns for body text redaction. Each pattern is compiled with a 100ms matchTimeout for ReDoS protection.</param>
    public SensitiveDataRedactor(
        IReadOnlyList<string>? sensitiveHeaders,
        IReadOnlyList<string>? sensitiveCookies,
        IReadOnlyList<string>? sensitiveQueryParams,
        IReadOnlyList<string>? sensitiveBodyPatterns = null)
    {
        _sensitiveHeaders = sensitiveHeaders != null && sensitiveHeaders.Count > 0
            ? new HashSet<string>(sensitiveHeaders, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _sensitiveCookies = sensitiveCookies != null && sensitiveCookies.Count > 0
            ? new HashSet<string>(sensitiveCookies, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _queryParamPattern = BuildQueryParamPattern(sensitiveQueryParams);
        _bodyPatterns = BuildBodyPatterns(sensitiveBodyPatterns);

        _hasRedactions = _sensitiveHeaders.Count > 0
                      || _sensitiveCookies.Count > 0
                      || _queryParamPattern != null
                      || _bodyPatterns != null;
    }

    /// <summary>
    /// Gets a value indicating whether any redaction rules are configured.
    /// Used as a fast path to skip redaction when disabled.
    /// </summary>
    public bool HasRedactions => _hasRedactions;

    /// <summary>
    /// Gets a value indicating whether body pattern redaction is configured.
    /// Used as a fast path to skip body redaction when no patterns are set.
    /// </summary>
    public bool HasBodyPatterns => _bodyPatterns != null;

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

    /// <summary>
    /// Redacts sensitive patterns from a body text string using regex replacement.
    /// Skips bodies exceeding 512 KB to prevent ReDoS on large untrusted input.
    /// Each pattern is applied with a 100ms matchTimeout; timed-out patterns are skipped and logged.
    /// </summary>
    /// <param name="bodyText">The body text to redact.</param>
    /// <param name="replacementCount">The total number of substitutions made across all patterns.</param>
    /// <param name="logger">Optional logger for diagnostics (skip events, timeouts).</param>
    /// <param name="requestId">Request identifier for log correlation.</param>
    /// <returns>The redacted body text, or the original text if no patterns matched or body was skipped.</returns>
    public string RedactBody(string bodyText, out int replacementCount, FileLogger? logger, string requestId)
    {
        replacementCount = 0;

        if (_bodyPatterns == null || string.IsNullOrEmpty(bodyText))
            return bodyText;

        if (bodyText.Length > MaxBodySizeForRedaction)
        {
            logger?.Log("Redaction", $"Body skipped (size={bodyText.Length} chars): id={requestId}");
            Interlocked.Increment(ref _bodySkippedCount);
            return bodyText;
        }

        var result = bodyText;
        foreach (var pattern in _bodyPatterns)
        {
            try
            {
                int patternCount = 0;
                result = pattern.Replace(result, _ =>
                {
                    patternCount++;
                    return RedactedValue;
                });
                Interlocked.Add(ref _bodyRedactionCount, patternCount);
                replacementCount += patternCount;
            }
            catch (RegexMatchTimeoutException)
            {
                logger?.Log("Redaction", $"Timeout matching body pattern: id={requestId}");
                Interlocked.Increment(ref _bodySkippedCount);
            }
        }

        return result;
    }

    /// <summary>
    /// Records body redaction substitutions in the audit counter (thread-safe).
    /// </summary>
    /// <param name="count">The number of substitutions to record.</param>
    public void RecordBodyRedaction(int count) => Interlocked.Add(ref _bodyRedactionCount, count);

    /// <summary>
    /// Records WebSocket payload redaction substitutions in the audit counter (thread-safe).
    /// </summary>
    /// <param name="count">The number of substitutions to record.</param>
    public void RecordWsRedaction(int count) => Interlocked.Add(ref _wsRedactionCount, count);

    /// <summary>
    /// Records a skipped body in the audit counter (thread-safe).
    /// Bodies are skipped when they exceed the 512 KB size gate or when a pattern times out.
    /// </summary>
    public void RecordBodySkipped() => Interlocked.Increment(ref _bodySkippedCount);

    /// <summary>
    /// Logs aggregate redaction counts to the provided logger.
    /// Call at session stop time to emit the audit trail. No-op when logger is null.
    /// </summary>
    /// <param name="logger">The logger to write audit data to.</param>
    public void LogAudit(FileLogger? logger)
    {
        if (logger == null) return;
        logger.Log("Redaction",
            $"Audit: bodyRedactions={_bodyRedactionCount}, wsRedactions={_wsRedactionCount}, bodiesSkipped={_bodySkippedCount}");
    }

    private static Regex[]? BuildBodyPatterns(IReadOnlyList<string>? patterns)
    {
        if (patterns == null || patterns.Count == 0) return null;
        var result = new Regex[patterns.Count];
        for (int i = 0; i < patterns.Count; i++)
        {
            result[i] = new Regex(patterns[i], RegexOptions.Compiled, BodyMatchTimeout);
        }
        return result;
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
