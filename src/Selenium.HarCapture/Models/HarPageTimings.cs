using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents timing information for a page load.
/// </summary>
public sealed class HarPageTimings
{
    /// <summary>
    /// Gets or initializes the time in milliseconds when the DOMContentLoaded event fired.
    /// -1 means the timing is not applicable or not available.
    /// </summary>
    [JsonPropertyName("onContentLoad")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OnContentLoad { get; init; }

    /// <summary>
    /// Gets or initializes the time in milliseconds when the load event fired.
    /// -1 means the timing is not applicable or not available.
    /// </summary>
    [JsonPropertyName("onLoad")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OnLoad { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
