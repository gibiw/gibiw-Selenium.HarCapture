using System;
using System.Collections.Generic;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Internal static validator for <see cref="CaptureOptions"/>.
/// Validates conflict rules and field constraints at <c>StartAsync()</c> time.
/// Never validates at property setters — all existing tests must pass unchanged.
/// </summary>
internal static class CaptureOptionsValidator
{
    /// <summary>
    /// Validates the provided <paramref name="options"/> and throws a single
    /// <see cref="ArgumentException"/> listing ALL violations if any are found.
    /// </summary>
    /// <param name="options">The <see cref="CaptureOptions"/> to validate.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when one or more validation rules are violated.
    /// The message lists every violation so the caller can fix them all at once.
    /// </exception>
    public static void ValidateAndThrow(CaptureOptions options)
    {
        var errors = new List<string>();

        // ── Cross-field conflict rules ────────────────────────────────────────

        if (options.EnableCompression && options.ForceSeleniumNetworkApi)
        {
            errors.Add(
                "EnableCompression and ForceSeleniumNetworkApi cannot both be true — " +
                "INetwork API does not retrieve response bodies, so compression has no effect.");
        }

        if (options.ResponseBodyScope == ResponseBodyScope.None && options.MaxResponseBodySize > 0)
        {
            errors.Add(
                $"ResponseBodyScope.None combined with MaxResponseBodySize = {options.MaxResponseBodySize} is contradictory — " +
                "no bodies are retrieved so the size limit has no effect.");
        }

        // ── Field-level rules ─────────────────────────────────────────────────

        if (options.MaxResponseBodySize < 0)
        {
            errors.Add(
                $"MaxResponseBodySize must be >= 0, but was {options.MaxResponseBodySize}.");
        }

        if (options.MaxWebSocketFramesPerConnection < 0)
        {
            errors.Add(
                $"MaxWebSocketFramesPerConnection must be >= 0, but was {options.MaxWebSocketFramesPerConnection}.");
        }

        if (options.MaxOutputFileSize < 0)
        {
            errors.Add(
                $"MaxOutputFileSize must be >= 0, but was {options.MaxOutputFileSize}.");
        }

        if (options.MaxOutputFileSize > 0 && options.OutputFilePath == null)
        {
            errors.Add(
                "MaxOutputFileSize requires OutputFilePath to be set (streaming mode only). " +
                "Set OutputFilePath before configuring MaxOutputFileSize.");
        }

        if (options.CreatorName != null && options.CreatorName.Length == 0)
        {
            errors.Add(
                "CreatorName must not be an empty string. " +
                "Set to null to use the default, or provide a non-empty name.");
        }

        if (options.UrlIncludePatterns != null)
        {
            for (int i = 0; i < options.UrlIncludePatterns.Count; i++)
            {
                if (string.IsNullOrEmpty(options.UrlIncludePatterns[i]))
                {
                    errors.Add($"UrlIncludePatterns[{i}] must not be null or empty.");
                }
            }
        }

        if (options.UrlExcludePatterns != null)
        {
            for (int i = 0; i < options.UrlExcludePatterns.Count; i++)
            {
                if (string.IsNullOrEmpty(options.UrlExcludePatterns[i]))
                {
                    errors.Add($"UrlExcludePatterns[{i}] must not be null or empty.");
                }
            }
        }

        // ── Throw single exception with all violations ─────────────────────────

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                $"CaptureOptions validation failed with {errors.Count} error(s):\n" +
                string.Join("\n", errors));
        }
    }
}
