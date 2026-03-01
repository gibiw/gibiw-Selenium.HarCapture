using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents a single HTTP request/response pair captured in the HAR file.
/// </summary>
public sealed class HarEntry
{
    /// <summary>
    /// Gets or initializes the date and time when the request was started.
    /// </summary>
    [JsonPropertyName("startedDateTime")]
    public DateTimeOffset StartedDateTime { get; init; }

    /// <summary>
    /// Gets or initializes the total elapsed time of the request in milliseconds.
    /// </summary>
    [JsonPropertyName("time")]
    public double Time { get; init; }

    /// <summary>
    /// Gets or initializes the HTTP request details.
    /// </summary>
    [JsonPropertyName("request")]
    public HarRequest Request { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the HTTP response details.
    /// </summary>
    [JsonPropertyName("response")]
    public HarResponse Response { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the cache usage information.
    /// </summary>
    [JsonPropertyName("cache")]
    public HarCache Cache { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the detailed timing breakdown for the request.
    /// </summary>
    [JsonPropertyName("timings")]
    public HarTimings Timings { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the reference to the parent page (if applicable).
    /// </summary>
    [JsonPropertyName("pageref")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PageRef { get; init; }

    /// <summary>
    /// Gets or initializes the IP address of the server that was connected to.
    /// </summary>
    [JsonPropertyName("serverIPAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServerIPAddress { get; init; }

    /// <summary>
    /// Gets or initializes the unique identifier of the connection used.
    /// </summary>
    [JsonPropertyName("connection")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Connection { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }

    /// <summary>
    /// Gets or initializes the resource type for Chrome DevTools compatibility.
    /// Set to <c>"websocket"</c> for WebSocket entries so Chrome Network tab Socket filter works.
    /// Null for regular HTTP entries (omitted from JSON).
    /// </summary>
    [JsonPropertyName("_resourceType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceType { get; init; }

    /// <summary>
    /// Gets or initializes WebSocket messages captured during this connection.
    /// Uses the Chrome DevTools custom <c>_webSocketMessages</c> field for compatibility.
    /// Only present for WebSocket upgrade (101) entries; null for regular HTTP entries.
    /// </summary>
    [JsonPropertyName("_webSocketMessages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IList<HarWebSocketMessage>? WebSocketMessages { get; init; }

    /// <summary>
    /// Gets or initializes the actual bytes transferred for the request body.
    /// Populated from CDP postData length. Omitted when 0 (unknown/none).
    /// </summary>
    [JsonPropertyName("_requestBodySize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long RequestBodySize { get; init; }

    /// <summary>
    /// Gets or initializes the actual bytes transferred for the response body (on-wire compressed size).
    /// Populated from CDP Network.Response.encodedDataLength. Omitted when 0 (unknown/none).
    /// </summary>
    [JsonPropertyName("_responseBodySize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long ResponseBodySize { get; init; }

    /// <summary>
    /// Gets or initializes the initiator of this network request.
    /// Vendor extension field (_initiator) populated from CDP requestWillBeSent event.
    /// Null (and omitted from JSON) when initiator data is unavailable.
    /// </summary>
    [JsonPropertyName("_initiator")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HarInitiator? Initiator { get; init; }

    /// <summary>
    /// Gets or initializes TLS security details for HTTPS responses.
    /// Vendor extension field (_securityDetails) populated from CDP Network.SecurityDetails.
    /// Null (and omitted from JSON) for HTTP responses or when SecurityDetails is not available.
    /// </summary>
    [JsonPropertyName("_securityDetails")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HarSecurityDetails? SecurityDetails { get; init; }
}
