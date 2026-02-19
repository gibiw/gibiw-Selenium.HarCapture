using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents a parameter in posted data (either form field or file upload).
/// </summary>
public sealed class HarParam
{
    /// <summary>
    /// Gets or initializes the parameter name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the parameter value.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; init; }

    /// <summary>
    /// Gets or initializes the file name for file upload parameters.
    /// </summary>
    [JsonPropertyName("fileName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileName { get; init; }

    /// <summary>
    /// Gets or initializes the content type for file upload parameters.
    /// </summary>
    [JsonPropertyName("contentType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
