using System;
using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents an HTTP cookie sent or received in a request/response.
/// </summary>
public sealed class HarCookie
{
    /// <summary>
    /// Gets or initializes the cookie name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the cookie value.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the cookie path.
    /// </summary>
    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    /// <summary>
    /// Gets or initializes the cookie domain.
    /// </summary>
    [JsonPropertyName("domain")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Domain { get; init; }

    /// <summary>
    /// Gets or initializes the cookie expiration date and time.
    /// </summary>
    [JsonPropertyName("expires")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Expires { get; init; }

    /// <summary>
    /// Gets or initializes whether the cookie is HTTP-only.
    /// </summary>
    [JsonPropertyName("httpOnly")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HttpOnly { get; init; }

    /// <summary>
    /// Gets or initializes whether the cookie is secure (HTTPS only).
    /// </summary>
    [JsonPropertyName("secure")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Secure { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
