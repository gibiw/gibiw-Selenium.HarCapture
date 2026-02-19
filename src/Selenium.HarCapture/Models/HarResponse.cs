using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents an HTTP response captured in the HAR file.
/// </summary>
public sealed class HarResponse
{
    /// <summary>
    /// Gets or initializes the HTTP status code.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; init; }

    /// <summary>
    /// Gets or initializes the HTTP status text (e.g., "OK", "Not Found").
    /// </summary>
    [JsonPropertyName("statusText")]
    public string StatusText { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the HTTP version (e.g., "HTTP/1.1").
    /// </summary>
    [JsonPropertyName("httpVersion")]
    public string HttpVersion { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the list of cookies received with the response.
    /// </summary>
    [JsonPropertyName("cookies")]
    public IList<HarCookie> Cookies { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the list of response headers.
    /// </summary>
    [JsonPropertyName("headers")]
    public IList<HarHeader> Headers { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the response body content details.
    /// </summary>
    [JsonPropertyName("content")]
    public HarContent Content { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the redirect URL (if the response is a redirect).
    /// </summary>
    [JsonPropertyName("redirectURL")]
    public string RedirectURL { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the total size of response headers in bytes.
    /// -1 means the size is not available.
    /// </summary>
    [JsonPropertyName("headersSize")]
    public long HeadersSize { get; init; }

    /// <summary>
    /// Gets or initializes the size of the response body in bytes.
    /// -1 means the size is not available.
    /// </summary>
    [JsonPropertyName("bodySize")]
    public long BodySize { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
