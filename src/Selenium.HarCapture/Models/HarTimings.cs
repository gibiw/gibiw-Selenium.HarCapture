using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents detailed timing breakdown for an HTTP request.
/// All values are in milliseconds. -1 indicates timing is not applicable or not available.
/// </summary>
public sealed class HarTimings
{
    /// <summary>
    /// Gets or initializes the time spent in a queue waiting for a network connection.
    /// -1 means the timing is not applicable or not available.
    /// </summary>
    [JsonPropertyName("blocked")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Blocked { get; init; }

    /// <summary>
    /// Gets or initializes the DNS resolution time.
    /// -1 means the timing is not applicable or not available.
    /// </summary>
    [JsonPropertyName("dns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Dns { get; init; }

    /// <summary>
    /// Gets or initializes the time required to create a TCP connection.
    /// -1 means the timing is not applicable or not available.
    /// </summary>
    [JsonPropertyName("connect")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Connect { get; init; }

    /// <summary>
    /// Gets or initializes the time required to send the HTTP request.
    /// </summary>
    [JsonPropertyName("send")]
    public double Send { get; init; }

    /// <summary>
    /// Gets or initializes the time spent waiting for a response from the server.
    /// </summary>
    [JsonPropertyName("wait")]
    public double Wait { get; init; }

    /// <summary>
    /// Gets or initializes the time required to read the entire response.
    /// </summary>
    [JsonPropertyName("receive")]
    public double Receive { get; init; }

    /// <summary>
    /// Gets or initializes the time required for SSL/TLS negotiation.
    /// -1 means the timing is not applicable or not available.
    /// </summary>
    [JsonPropertyName("ssl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Ssl { get; init; }

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
