using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents the creator application that generated the HAR file.
/// </summary>
public sealed class HarCreator
{
    /// <summary>
    /// Gets or initializes the name of the application that created the HAR file.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the version of the application that created the HAR file.
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
