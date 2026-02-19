using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents an HTTP request captured in the HAR file.
/// </summary>
public sealed class HarRequest
{
    /// <summary>
    /// Gets or initializes the HTTP method (GET, POST, etc.).
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the absolute URL of the request.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the HTTP version (e.g., "HTTP/1.1").
    /// </summary>
    [JsonPropertyName("httpVersion")]
    public string HttpVersion { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the list of cookies sent with the request.
    /// </summary>
    [JsonPropertyName("cookies")]
    public IList<HarCookie> Cookies { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the list of request headers.
    /// </summary>
    [JsonPropertyName("headers")]
    public IList<HarHeader> Headers { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the list of query string parameters.
    /// </summary>
    [JsonPropertyName("queryString")]
    public IList<HarQueryString> QueryString { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the POST data details (if applicable).
    /// </summary>
    [JsonPropertyName("postData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HarPostData? PostData { get; init; }

    /// <summary>
    /// Gets or initializes the total size of request headers in bytes.
    /// -1 means the size is not available.
    /// </summary>
    [JsonPropertyName("headersSize")]
    public long HeadersSize { get; init; }

    /// <summary>
    /// Gets or initializes the size of the request body in bytes.
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
