using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents an HTTP header sent in a request or response.
/// </summary>
public sealed class HarHeader
{
    /// <summary>
    /// Gets or initializes the header name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the header value.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; init; } = null!;

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
