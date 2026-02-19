using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents the browser information used during the capture session.
/// </summary>
public sealed class HarBrowser
{
    /// <summary>
    /// Gets or initializes the name of the browser.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the version of the browser.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = null!;

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
