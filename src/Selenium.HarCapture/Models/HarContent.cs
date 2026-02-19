using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents the response body content details.
/// </summary>
public sealed class HarContent
{
    /// <summary>
    /// Gets or initializes the size of the content in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>
    /// Gets or initializes the MIME type of the content.
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the number of bytes saved due to compression.
    /// </summary>
    [JsonPropertyName("compression")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Compression { get; init; }

    /// <summary>
    /// Gets or initializes the actual response body text.
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    /// <summary>
    /// Gets or initializes the encoding used for the text (e.g., "base64").
    /// </summary>
    [JsonPropertyName("encoding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Encoding { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
