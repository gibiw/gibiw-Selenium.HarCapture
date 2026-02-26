using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents a single WebSocket message (frame) captured during a WebSocket connection.
/// Follows the Chrome DevTools HAR extension format for compatibility with Chrome HAR viewers.
/// </summary>
public sealed class HarWebSocketMessage
{
    /// <summary>
    /// Gets or initializes the direction of the message: "send" or "receive".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the timestamp of the message in epoch seconds.
    /// </summary>
    [JsonPropertyName("time")]
    public double Time { get; init; }

    /// <summary>
    /// Gets or initializes the WebSocket frame opcode (1 = text, 2 = binary).
    /// </summary>
    [JsonPropertyName("opcode")]
    public int Opcode { get; init; }

    /// <summary>
    /// Gets or initializes the payload data of the message.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; init; } = null!;
}
