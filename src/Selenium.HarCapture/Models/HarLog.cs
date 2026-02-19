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
}
