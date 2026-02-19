using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents POST data sent with the request.
/// </summary>
public sealed class HarPostData
{
    /// <summary>
    /// Gets or initializes the MIME type of the posted data.
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the list of posted parameters (for form data).
    /// </summary>
    [JsonPropertyName("params")]
    public IList<HarParam> Params { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the plain text of the posted data.
    /// </summary>
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
