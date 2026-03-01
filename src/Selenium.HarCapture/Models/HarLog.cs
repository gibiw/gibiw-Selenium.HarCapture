using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents the log object containing all captured HTTP traffic data.
/// </summary>
public sealed class HarLog
{
    /// <summary>
    /// Gets or initializes the HAR format version (should be "1.2").
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the creator application information.
    /// </summary>
    [JsonPropertyName("creator")]
    public HarCreator Creator { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the browser information.
    /// </summary>
    [JsonPropertyName("browser")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HarBrowser? Browser { get; init; }

    /// <summary>
    /// Gets or initializes the list of pages (page objects).
    /// </summary>
    [JsonPropertyName("pages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<HarPage>? Pages { get; init; }

    /// <summary>
    /// Gets or initializes the list of all captured HTTP requests and responses.
    /// </summary>
    [JsonPropertyName("entries")]
    public IList<HarEntry> Entries { get; init; } = null!;

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }

    /// <summary>
    /// Gets or initializes optional user-provided key-value metadata embedded in the HAR file.
    /// Serialized as "_custom" in the HAR log object — a HAR spec extension field.
    /// </summary>
    /// <remarks>
    /// Values must be JSON-primitive-compatible (string, int, long, double, bool).
    /// After a JSON round-trip, values will be deserialized as <see cref="System.Text.Json.JsonElement"/>.
    /// Use this to embed context like environment name, transaction IDs, or build numbers.
    /// </remarks>
    [JsonPropertyName("_custom")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, object>? Custom { get; init; }
}
