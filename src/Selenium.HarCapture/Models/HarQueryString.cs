using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents a query string parameter in the request URL.
/// </summary>
public sealed class HarQueryString
{
    /// <summary>
    /// Gets or initializes the query parameter name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the query parameter value.
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
