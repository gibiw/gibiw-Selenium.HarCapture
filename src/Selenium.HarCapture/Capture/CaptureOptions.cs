using System.Collections.Generic;

namespace Selenium.HarCapture.Capture;

/// <summary>
/// Configuration options for HAR capture sessions.
/// Controls what data is captured, how URLs are filtered, and other capture behavior.
/// </summary>
public sealed class CaptureOptions
{
    /// <summary>
    /// Gets or sets the types of traffic data to capture.
    /// Default is <see cref="CaptureType.AllText"/> (headers, cookies, text content, timings).
    /// </summary>
    public CaptureType CaptureTypes { get; set; } = CaptureType.AllText;

    /// <summary>
    /// Gets or sets the name of the creator tool to include in the HAR file metadata.
    /// Default is "Selenium.HarCapture".
    /// </summary>
    public string CreatorName { get; set; } = "Selenium.HarCapture";

    /// <summary>
    /// Gets or sets whether to force the use of Selenium's INetwork API even if CDP is available.
    /// Default is false (CDP will be used if available).
    /// </summary>
    /// <remarks>
    /// Set to true to explicitly use INetwork API for cross-browser compatibility testing.
    /// Note that INetwork lacks detailed timings and response body capture.
    /// </remarks>
    public bool ForceSeleniumNetworkApi { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum size in bytes for response bodies to capture.
    /// Default is 0 (unlimited). Set a positive value to limit captured body size and reduce memory usage.
    /// </summary>
    /// <remarks>
    /// When set to a positive value, response bodies larger than this limit will be truncated.
    /// A value of 0 means no limit (all response bodies are captured in full).
    /// </remarks>
    public long MaxResponseBodySize { get; set; } = 0;

    /// <summary>
    /// Gets or sets URL patterns to include for capture (glob patterns like "https://api.example.com/**").
    /// Default is null (all URLs are included).
    /// </summary>
    /// <remarks>
    /// When null, all URLs are captured (no filtering).
    /// When set, only URLs matching at least one pattern are captured.
    /// Exclude patterns take precedence over include patterns.
    /// </remarks>
    public IReadOnlyList<string>? UrlIncludePatterns { get; set; }

    /// <summary>
    /// Gets or sets URL patterns to exclude from capture (glob patterns like "**/*.png").
    /// Default is null (nothing is explicitly excluded).
    /// </summary>
    /// <remarks>
    /// When null, no URLs are explicitly excluded.
    /// When set, URLs matching any pattern are excluded from capture.
    /// Exclude patterns take precedence over include patterns.
    /// </remarks>
    public IReadOnlyList<string>? UrlExcludePatterns { get; set; }

    /// <summary>
    /// Sets the types of traffic data to capture.
    /// </summary>
    /// <param name="types">The capture types to configure.</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithCaptureTypes(CaptureType types)
    {
        CaptureTypes = types;
        return this;
    }

    /// <summary>
    /// Sets the maximum size in bytes for response bodies to capture.
    /// </summary>
    /// <param name="maxSize">The maximum response body size in bytes. Use 0 for unlimited.</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithMaxResponseBodySize(long maxSize)
    {
        MaxResponseBodySize = maxSize;
        return this;
    }

    /// <summary>
    /// Sets URL patterns to include for capture.
    /// </summary>
    /// <param name="patterns">Glob patterns for URLs to include (e.g., "https://api.example.com/**").</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithUrlIncludePatterns(params string[] patterns)
    {
        UrlIncludePatterns = patterns;
        return this;
    }

    /// <summary>
    /// Sets URL patterns to exclude from capture.
    /// </summary>
    /// <param name="patterns">Glob patterns for URLs to exclude (e.g., "**/*.png").</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithUrlExcludePatterns(params string[] patterns)
    {
        UrlExcludePatterns = patterns;
        return this;
    }

    /// <summary>
    /// Sets the creator name to include in the HAR file metadata.
    /// </summary>
    /// <param name="name">The creator tool name.</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithCreatorName(string name)
    {
        CreatorName = name;
        return this;
    }

    /// <summary>
    /// Gets or sets the output file path for streaming HAR capture.
    /// When set, entries are written incrementally to the file instead of being accumulated in memory.
    /// Default is null (in-memory mode).
    /// </summary>
    /// <remarks>
    /// When set, entries are serialized directly to the file as they arrive, keeping the file
    /// always valid JSON. This eliminates OOM issues for large captures. Use parameterless
    /// <c>StopAndSave()</c> / <c>StopAndSaveAsync()</c> when this option is configured.
    /// </remarks>
    public string? OutputFilePath { get; set; }

    /// <summary>
    /// Gets or sets the file path for diagnostic log output.
    /// Default is null (logging is disabled).
    /// </summary>
    /// <remarks>
    /// When set, diagnostic messages are written to the specified file instead of Debug.WriteLine.
    /// The file is opened in append mode. Set to null to disable file logging.
    /// </remarks>
    public string? LogFilePath { get; set; }

    /// <summary>
    /// Sets the output file path for streaming HAR capture.
    /// Entries will be written incrementally to the file, keeping it always valid.
    /// </summary>
    /// <param name="path">The file path to write HAR data to.</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithOutputFile(string path)
    {
        OutputFilePath = path;
        return this;
    }

    /// <summary>
    /// Forces the use of Selenium's INetwork API even if CDP is available.
    /// </summary>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions ForceSeleniumNetwork()
    {
        ForceSeleniumNetworkApi = true;
        return this;
    }

    /// <summary>
    /// Sets the file path for diagnostic log output.
    /// </summary>
    /// <param name="path">The file path to write logs to.</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithLogFile(string path)
    {
        LogFilePath = path;
        return this;
    }
}
