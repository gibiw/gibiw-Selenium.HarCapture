using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents the initiator of a network request.
/// Vendor extension field (_initiator) for Chrome DevTools compatibility.
/// Populated from CDP requestWillBeSent event initiator data.
/// </summary>
public sealed class HarInitiator
{
    /// <summary>
    /// Gets or initializes the type of initiator.
    /// Common values: "parser", "script", "preload", "other".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "other";

    /// <summary>
    /// Gets or initializes the URL of the script that initiated the request.
    /// Present when Type is "script". Null for non-script initiators.
    /// </summary>
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    /// <summary>
    /// Gets or initializes the line number in the initiating script.
    /// Present when Type is "script". Null for non-script initiators.
    /// </summary>
    [JsonPropertyName("lineNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? LineNumber { get; init; }
}
