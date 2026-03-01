using System.Collections.Generic;
using System;

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
    /// Gets or sets the browser name to use in HAR metadata.
    /// When set, overrides auto-detected browser name from WebDriver capabilities.
    /// Default is null (auto-detect from driver).
    /// </summary>
    public string? BrowserName { get; set; }

    /// <summary>
    /// Gets or sets the browser version to use in HAR metadata.
    /// When set, overrides auto-detected browser version from WebDriver capabilities.
    /// Default is null (auto-detect from driver).
    /// </summary>
    public string? BrowserVersion { get; set; }

    /// <summary>
    /// Gets or sets which response bodies to retrieve via CDP.
    /// Limiting body retrieval reduces CDP WebSocket contention and improves navigation speed.
    /// Default is <see cref="ResponseBodyScope.All"/> (backward compatible — retrieve all bodies).
    /// </summary>
    public ResponseBodyScope ResponseBodyScope { get; set; } = ResponseBodyScope.All;

    /// <summary>
    /// Gets or sets additional MIME types for body retrieval, additive to <see cref="ResponseBodyScope"/> preset.
    /// For example, with <see cref="Capture.ResponseBodyScope.PagesAndApi"/> scope and filter ["image/png"],
    /// both HTML/JSON and PNG bodies are retrieved.
    /// Default is null (no extra types).
    /// </summary>
    public IReadOnlyList<string>? ResponseBodyMimeFilter { get; set; }

    /// <summary>
    /// Gets or sets whether to enable gzip compression for the output file.
    /// When true in streaming mode (WithOutputFile), the HAR file is compressed to .gz format
    /// at finalization time after all entries are written.
    /// Default is false (no compression).
    /// </summary>
    public bool EnableCompression { get; set; } = false;

    /// <summary>
    /// Gets or sets the header names to redact in HAR output (case-insensitive).
    /// When set, matching header values are replaced with "[REDACTED]" at capture time.
    /// Default is null (no headers are redacted).
    /// </summary>
    /// <remarks>
    /// Use this to prevent sensitive headers like "Authorization" or "X-Api-Key" from appearing in HAR files.
    /// Redaction happens before data reaches storage, ensuring sensitive values never persist.
    /// </remarks>
    public IReadOnlyList<string>? SensitiveHeaders { get; set; }

    /// <summary>
    /// Gets or sets the cookie names to redact in HAR output (case-insensitive).
    /// When set, matching cookie values are replaced with "[REDACTED]" at capture time.
    /// Default is null (no cookies are redacted).
    /// </summary>
    /// <remarks>
    /// Use this to prevent sensitive cookies like "session_id" or "auth_token" from appearing in HAR files.
    /// Redaction happens before data reaches storage, ensuring sensitive values never persist.
    /// </remarks>
    public IReadOnlyList<string>? SensitiveCookies { get; set; }

    /// <summary>
    /// Gets or sets the query parameter patterns to redact in HAR output (case-insensitive).
    /// Supports wildcard patterns: "*" matches any characters, "?" matches a single character.
    /// When set, matching query parameter values are replaced with "[REDACTED]" at capture time.
    /// Default is null (no query parameters are redacted).
    /// </summary>
    /// <remarks>
    /// Use this to prevent sensitive query parameters like "api_key", "token", or "password" from appearing in HAR files.
    /// Examples: "api_*" matches "api_key", "api_secret"; "token" matches exactly "token".
    /// Redaction happens before data reaches storage, ensuring sensitive values never persist.
    /// </remarks>
    public IReadOnlyList<string>? SensitiveQueryParams { get; set; }

    /// <summary>
    /// Gets or sets regex patterns for redacting sensitive content from response/request bodies.
    /// When set, matching content is replaced with "[REDACTED]" at capture time.
    /// Each pattern is applied with a 100ms timeout and a 512 KB body size gate for ReDoS protection.
    /// Default is null (no body content is redacted).
    /// </summary>
    /// <remarks>
    /// Use <see cref="HarPiiPatterns"/> for built-in presets (credit cards, emails, SSNs, phone numbers, IPv4 addresses).
    /// Patterns are linear-time by design. Avoid nested quantifiers to prevent catastrophic backtracking.
    /// Bodies exceeding 512 KB are skipped entirely. Base64-encoded bodies are not redacted.
    /// </remarks>
    public IReadOnlyList<string>? SensitiveBodyPatterns { get; set; }

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

    /// <summary>
    /// Sets the browser name and version to use in HAR metadata, overriding auto-detection.
    /// </summary>
    /// <param name="name">The browser name (e.g., "Chrome", "Firefox").</param>
    /// <param name="version">The browser version string.</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithBrowser(string name, string version)
    {
        BrowserName = name;
        BrowserVersion = version;
        return this;
    }

    /// <summary>
    /// Enables WebSocket frame capture by adding the <see cref="CaptureType.WebSocket"/> flag.
    /// Requires CDP strategy (Chrome/Edge). INetwork strategy silently ignores this flag.
    /// </summary>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithWebSocketCapture()
    {
        CaptureTypes |= CaptureType.WebSocket;
        return this;
    }

    /// <summary>
    /// Sets which response bodies to retrieve via CDP.
    /// </summary>
    /// <param name="scope">The body retrieval scope.</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithResponseBodyScope(ResponseBodyScope scope)
    {
        ResponseBodyScope = scope;
        return this;
    }

    /// <summary>
    /// Sets additional MIME types for body retrieval, additive to <see cref="ResponseBodyScope"/> preset.
    /// </summary>
    /// <param name="mimeTypes">Extra MIME types to retrieve (e.g., "image/png", "image/svg+xml").</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithResponseBodyMimeFilter(params string[] mimeTypes)
    {
        ResponseBodyMimeFilter = mimeTypes;
        return this;
    }

    /// <summary>
    /// Enables gzip compression for the output file.
    /// In streaming mode, compression happens at finalization after all entries are written.
    /// The output file path will have .gz appended if it doesn't already end with .gz.
    /// </summary>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithCompression()
    {
        EnableCompression = true;
        return this;
    }

    /// <summary>
    /// Sets header names to redact in HAR output (case-insensitive).
    /// Matching header values are replaced with "[REDACTED]" at capture time.
    /// </summary>
    /// <param name="headerNames">Header names to redact (e.g., "Authorization", "X-Api-Key").</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithSensitiveHeaders(params string[] headerNames)
    {
        SensitiveHeaders = headerNames;
        return this;
    }

    /// <summary>
    /// Sets cookie names to redact in HAR output (case-insensitive).
    /// Matching cookie values are replaced with "[REDACTED]" at capture time.
    /// </summary>
    /// <param name="cookieNames">Cookie names to redact (e.g., "session_id", "auth_token").</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithSensitiveCookies(params string[] cookieNames)
    {
        SensitiveCookies = cookieNames;
        return this;
    }

    /// <summary>
    /// Sets query parameter patterns to redact in HAR output (case-insensitive).
    /// Supports wildcard patterns: "*" matches any characters, "?" matches a single character.
    /// Matching query parameter values are replaced with "[REDACTED]" at capture time.
    /// </summary>
    /// <param name="paramPatterns">Query parameter patterns to redact (e.g., "api_*", "token", "pass*").</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithSensitiveQueryParams(params string[] paramPatterns)
    {
        SensitiveQueryParams = paramPatterns;
        return this;
    }

    /// <summary>
    /// Gets or sets the maximum number of WebSocket frames to keep per connection.
    /// When set to a positive value, oldest frames are dropped when the limit is reached (oldest-first eviction).
    /// Default is 0 (unlimited — all frames are kept).
    /// </summary>
    /// <remarks>
    /// Use this to cap memory usage for long-lived WebSocket connections that generate many frames.
    /// A value of 0 means unlimited (all frames retained). Negative values are invalid and will be
    /// rejected at <c>StartAsync()</c> time by <see cref="Internal.CaptureOptionsValidator"/>.
    /// </remarks>
    public int MaxWebSocketFramesPerConnection { get; set; } = 0;

    /// <summary>
    /// Sets regex patterns for redacting sensitive content from response/request bodies.
    /// Matching content is replaced with "[REDACTED]" at capture time.
    /// Each pattern runs with a 100ms timeout and a 512 KB body size gate for ReDoS protection.
    /// </summary>
    /// <param name="patterns">
    /// Regex patterns to match and redact (e.g., <see cref="HarPiiPatterns.Email"/>, <see cref="HarPiiPatterns.CreditCard"/>).
    /// Patterns must be valid .NET regex strings. Avoid nested quantifiers to prevent backtracking.
    /// </param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithSensitiveBodyPatterns(params string[] patterns)
    {
        SensitiveBodyPatterns = patterns;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of WebSocket frames to keep per connection.
    /// When the limit is reached, the oldest frames are dropped (oldest-first eviction).
    /// </summary>
    /// <param name="maxFrames">Maximum frames per connection. Use 0 for unlimited. Negative values are invalid.</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithMaxWebSocketFramesPerConnection(int maxFrames)
    {
        MaxWebSocketFramesPerConnection = maxFrames;
        return this;
    }

    /// <summary>
    /// Gets or sets the maximum size in bytes for the output HAR file in streaming mode.
    /// When the file exceeds this limit, streaming is aborted and no further entries are written.
    /// Default is 0 (unlimited). Requires <see cref="OutputFilePath"/> to be set.
    /// </summary>
    /// <remarks>
    /// When the limit is exceeded, the file remains valid JSON — the last entry that pushed past
    /// the limit is fully written with a valid footer. Subsequent entries are silently dropped.
    /// <see cref="StopAsync"/> returns cleanly without throwing.
    /// A value of 0 means no limit (all entries are written). Negative values are invalid.
    /// </remarks>
    public long MaxOutputFileSize { get; set; } = 0;

    /// <summary>
    /// Gets or sets user-provided key-value metadata to embed in the HAR file under the "_custom" key.
    /// Default is null (no custom metadata).
    /// </summary>
    /// <remarks>
    /// Values must be JSON-primitive-compatible (string, int, long, double, bool).
    /// Use <see cref="WithCustomMetadata"/> for fluent configuration.
    /// </remarks>
    public IDictionary<string, object>? CustomMetadata { get; set; }

    /// <summary>
    /// Sets the maximum size in bytes for the output HAR file in streaming mode.
    /// When the file exceeds this limit, streaming is aborted cleanly.
    /// </summary>
    /// <param name="bytes">Maximum file size in bytes. Use 0 for unlimited. Negative values are invalid.</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithMaxOutputFileSize(long bytes)
    {
        MaxOutputFileSize = bytes;
        return this;
    }

    /// <summary>
    /// Adds a key-value entry to the custom metadata dictionary embedded in the HAR file.
    /// Multiple calls accumulate entries; calling with the same key overwrites the previous value.
    /// </summary>
    /// <param name="key">The metadata key (e.g., "env", "transactionId").</param>
    /// <param name="value">The metadata value. Must be JSON-primitive-compatible (string, int, long, double, bool).</param>
    /// <returns>The current instance for method chaining.</returns>
    public CaptureOptions WithCustomMetadata(string key, object value)
    {
        CustomMetadata ??= new Dictionary<string, object>();
        ((Dictionary<string, object>)CustomMetadata)[key] = value;
        return this;
    }
}
