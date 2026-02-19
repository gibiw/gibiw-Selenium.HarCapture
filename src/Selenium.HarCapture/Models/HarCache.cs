using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents cache usage information for a request.
/// </summary>
public sealed class HarCache
{
    /// <summary>
    /// Gets or initializes the cache state before the request was made.
    /// </summary>
    [JsonPropertyName("beforeRequest")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HarCacheEntry? BeforeRequest { get; init; }

    /// <summary>
    /// Gets or initializes the cache state after the request was made.
    /// </summary>
    [JsonPropertyName("afterRequest")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HarCacheEntry? AfterRequest { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
