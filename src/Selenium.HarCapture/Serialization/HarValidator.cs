using System;
using System.Collections.Generic;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Serialization;

/// <summary>
/// Validates a <see cref="Har"/> object graph for structural correctness per HAR 1.2.
/// Never throws for validation findings — returns a <see cref="HarValidationResult"/>.
/// </summary>
public static class HarValidator
{
    /// <summary>
    /// Validates the given <paramref name="har"/> object and returns all findings.
    /// </summary>
    /// <param name="har">The HAR object to validate. Must not be null.</param>
    /// <param name="mode">Validation strictness. Defaults to <see cref="HarValidationMode.Standard"/>.</param>
    /// <returns>A <see cref="HarValidationResult"/> with all errors and warnings found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="har"/> is null — programming error.</exception>
    public static HarValidationResult Validate(Har har, HarValidationMode mode = HarValidationMode.Standard)
    {
        if (har is null) throw new ArgumentNullException(nameof(har));

        var errors = new List<HarValidationError>();
        ValidateLog(har.Log, errors, mode);
        return new HarValidationResult(errors);
    }

    // ── Private validators ────────────────────────────────────────────────────────

    private static void ValidateLog(HarLog? log, List<HarValidationError> errors, HarValidationMode mode)
    {
        if (log is null)
        {
            errors.Add(Error("log", "log is required"));
            return;
        }

        if (string.IsNullOrEmpty(log.Version))
            errors.Add(Error("log.version", "version is required and must not be empty"));

        if (log.Creator is null)
            errors.Add(Error("log.creator", "creator is required"));
        else
            ValidateCreator(log.Creator, errors);

        // Pages: absent is OK in Standard/Lenient; Warning in Strict
        if (log.Pages is null && mode == HarValidationMode.Strict)
            errors.Add(Warning("log.pages", "pages array is absent; consider including page timing data"));

        if (log.Entries is null)
        {
            errors.Add(Error("log.entries", "entries is required"));
            return;
        }

        for (int i = 0; i < log.Entries.Count; i++)
            ValidateEntry(log.Entries[i], i, errors, mode);
    }

    private static void ValidateCreator(HarCreator creator, List<HarValidationError> errors)
    {
        if (string.IsNullOrEmpty(creator.Name))
            errors.Add(Error("log.creator.name", "creator.name is required and must not be empty"));

        if (string.IsNullOrEmpty(creator.Version))
            errors.Add(Error("log.creator.version", "creator.version is required and must not be empty"));
    }

    private static void ValidateEntry(HarEntry? entry, int index, List<HarValidationError> errors, HarValidationMode mode)
    {
        var prefix = $"log.entries[{index}]";

        if (entry is null)
        {
            errors.Add(Error(prefix, "entry is required"));
            return;
        }

        if (entry.Request is null)
            errors.Add(Error($"{prefix}.request", "request is required"));
        else
            ValidateRequest(entry.Request, $"{prefix}.request", errors, mode);

        if (entry.Response is null)
            errors.Add(Error($"{prefix}.response", "response is required"));
        else
            ValidateResponse(entry.Response, $"{prefix}.response", errors, mode);

        if (entry.Cache is null)
            errors.Add(Error($"{prefix}.cache", "cache is required (use empty object if no cache info)"));

        if (entry.Timings is null)
            errors.Add(Error($"{prefix}.timings", "timings is required"));
        else
            ValidateTimings(entry.Timings, $"{prefix}.timings", errors, mode);
    }

    private static void ValidateRequest(HarRequest request, string prefix, List<HarValidationError> errors, HarValidationMode mode)
    {
        if (string.IsNullOrEmpty(request.Method))
            errors.Add(Error($"{prefix}.method", "method is required and must not be empty"));

        if (string.IsNullOrEmpty(request.Url))
            errors.Add(Error($"{prefix}.url", "url is required and must not be empty"));

        if (string.IsNullOrEmpty(request.HttpVersion))
            errors.Add(Error($"{prefix}.httpVersion", "httpVersion is required and must not be empty"));

        if (request.Cookies is null)
            errors.Add(Error($"{prefix}.cookies", "cookies array is required (use empty array if none)"));

        if (request.Headers is null)
            errors.Add(Error($"{prefix}.headers", "headers array is required (use empty array if none)"));

        if (request.QueryString is null)
            errors.Add(Error($"{prefix}.queryString", "queryString array is required (use empty array if none)"));

        // Size fields: -1 is OK in Standard/Lenient; Error in Strict
        if (mode == HarValidationMode.Strict)
        {
            if (request.HeadersSize == -1)
                errors.Add(Error($"{prefix}.headersSize", "-1 sentinel is not allowed in Strict mode"));
            if (request.BodySize == -1)
                errors.Add(Error($"{prefix}.bodySize", "-1 sentinel is not allowed in Strict mode"));
        }
    }

    private static void ValidateResponse(HarResponse response, string prefix, List<HarValidationError> errors, HarValidationMode mode)
    {
        if (response.Status < 0)
            errors.Add(Error($"{prefix}.status", $"status must be >= 0, got {response.Status}"));

        if (string.IsNullOrEmpty(response.StatusText))
            errors.Add(Error($"{prefix}.statusText", "statusText is required"));

        if (string.IsNullOrEmpty(response.HttpVersion))
            errors.Add(Error($"{prefix}.httpVersion", "httpVersion is required and must not be empty"));

        if (response.Cookies is null)
            errors.Add(Error($"{prefix}.cookies", "cookies array is required (use empty array if none)"));

        if (response.Headers is null)
            errors.Add(Error($"{prefix}.headers", "headers array is required (use empty array if none)"));

        if (response.Content is null)
            errors.Add(Error($"{prefix}.content", "content is required"));
        else
            ValidateContent(response.Content, $"{prefix}.content", errors, mode);

        if (response.RedirectURL is null)
            errors.Add(Error($"{prefix}.redirectURL", "redirectURL is required (use empty string if none)"));

        // Size fields: -1 OK in Standard/Lenient; Error in Strict
        if (mode == HarValidationMode.Strict)
        {
            if (response.HeadersSize == -1)
                errors.Add(Error($"{prefix}.headersSize", "-1 sentinel is not allowed in Strict mode"));
            if (response.BodySize == -1)
                errors.Add(Error($"{prefix}.bodySize", "-1 sentinel is not allowed in Strict mode"));
        }
    }

    private static void ValidateContent(HarContent content, string prefix, List<HarValidationError> errors, HarValidationMode mode)
    {
        if (string.IsNullOrEmpty(content.MimeType))
            errors.Add(Error($"{prefix}.mimeType", "mimeType is required and must not be empty"));

        // content.size: -1 OK in Standard/Lenient; Error in Strict
        if (mode == HarValidationMode.Strict && content.Size == -1)
            errors.Add(Error($"{prefix}.size", "-1 sentinel is not allowed in Strict mode"));
    }

    private static void ValidateTimings(HarTimings timings, string prefix, List<HarValidationError> errors, HarValidationMode mode)
    {
        // Required fields: send, wait, receive. -1 means "unknown" in all modes. Values < -1 always invalid.
        ValidateRequiredTiming(timings.Send, $"{prefix}.send", errors);
        ValidateRequiredTiming(timings.Wait, $"{prefix}.wait", errors);
        ValidateRequiredTiming(timings.Receive, $"{prefix}.receive", errors);

        // Optional fields: blocked, dns, connect, ssl. null OK in all modes.
        // -1 OK in Standard/Lenient; Error in Strict.
        if (timings.Blocked.HasValue)
            ValidateOptionalTiming(timings.Blocked.Value, $"{prefix}.blocked", errors, mode);

        if (timings.Dns.HasValue)
            ValidateOptionalTiming(timings.Dns.Value, $"{prefix}.dns", errors, mode);

        if (timings.Connect.HasValue)
            ValidateOptionalTiming(timings.Connect.Value, $"{prefix}.connect", errors, mode);

        if (timings.Ssl.HasValue)
            ValidateOptionalTiming(timings.Ssl.Value, $"{prefix}.ssl", errors, mode);
    }

    private static void ValidateRequiredTiming(double value, string field, List<HarValidationError> errors)
    {
        // -1 is valid sentinel for "unknown". Values < -1 are always invalid.
        if (value < -1.0)
            errors.Add(Error(field, $"timing value must be >= -1, got {value}"));
    }

    private static void ValidateOptionalTiming(double value, string field, List<HarValidationError> errors, HarValidationMode mode)
    {
        if (value < -1.0)
        {
            errors.Add(Error(field, $"timing value must be >= -1, got {value}"));
            return;
        }

        // -1 sentinel: Error in Strict, accepted in Standard/Lenient
        if (value == -1.0 && mode == HarValidationMode.Strict)
            errors.Add(Error(field, "-1 sentinel is not allowed in Strict mode; use null for not-applicable timing"));
    }

    // ── Factory helpers ───────────────────────────────────────────────────────────

    private static HarValidationError Error(string field, string message) =>
        new HarValidationError(field, message, HarValidationSeverity.Error);

    private static HarValidationError Warning(string field, string message) =>
        new HarValidationError(field, message, HarValidationSeverity.Warning);
}
