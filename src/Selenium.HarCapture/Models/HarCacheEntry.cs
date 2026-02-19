using System;
using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents detailed cache entry information before or after a request.
/// </summary>
public sealed class HarCacheEntry
{
    /// <summary>
    /// Gets or initializes the expiration date and time of the cache entry.
    /// </summary>
    [JsonPropertyName("expires")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Expires { get; init; }

    /// <summary>
    /// Gets or initializes the last time the cache entry was accessed.
    /// </summary>
    [JsonPropertyName("lastAccess")]
    public DateTimeOffset LastAccess { get; init; }

    /// <summary>
    /// Gets or initializes the ETag identifier for the cached resource.
    /// </summary>
    [JsonPropertyName("eTag")]
    public string ETag { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the number of times the cache entry has been accessed.
    /// </summary>
    [JsonPropertyName("hitCount")]
    public int HitCount { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
