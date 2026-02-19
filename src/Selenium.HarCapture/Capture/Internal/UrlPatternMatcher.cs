using System.Collections.Generic;
using System.Linq;
using DotNet.Globbing;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Internal utility for matching URLs against glob patterns for include/exclude filtering.
/// Supports standard glob patterns like "https://api.example.com/**" or "**/*.png".
/// </summary>
internal sealed class UrlPatternMatcher
{
    private readonly Glob[]? _includeGlobs;
    private readonly Glob[]? _excludeGlobs;

    /// <summary>
    /// Gets a static instance that captures all URLs (no filtering).
    /// </summary>
    public static UrlPatternMatcher CaptureAll { get; } = new UrlPatternMatcher(null, null);

    /// <summary>
    /// Initializes a new instance of the <see cref="UrlPatternMatcher"/> class.
    /// </summary>
    /// <param name="includePatterns">Glob patterns for URLs to include (null means include all).</param>
    /// <param name="excludePatterns">Glob patterns for URLs to exclude (null means exclude none).</param>
    public UrlPatternMatcher(IReadOnlyList<string>? includePatterns, IReadOnlyList<string>? excludePatterns)
    {
        _includeGlobs = includePatterns?.Select(p => Glob.Parse(p)).ToArray();
        _excludeGlobs = excludePatterns?.Select(p => Glob.Parse(p)).ToArray();
    }

    /// <summary>
    /// Determines whether the specified URL should be captured based on include/exclude patterns.
    /// </summary>
    /// <param name="url">The URL to evaluate.</param>
    /// <returns>
    /// true if the URL should be captured; false if it should be excluded.
    /// Exclude patterns take precedence over include patterns.
    /// </returns>
    public bool ShouldCapture(string url)
    {
        // Exclude patterns take precedence - if URL matches any exclude pattern, reject it
        if (_excludeGlobs != null)
        {
            foreach (var glob in _excludeGlobs)
            {
                if (glob.IsMatch(url))
                {
                    return false;
                }
            }
        }

        // If include patterns are specified, URL must match at least one
        if (_includeGlobs != null)
        {
            foreach (var glob in _includeGlobs)
            {
                if (glob.IsMatch(url))
                {
                    return true;
                }
            }
            // Include patterns specified but none matched
            return false;
        }

        // No patterns specified (or only exclude patterns that didn't match) - capture all
        return true;
    }
}
