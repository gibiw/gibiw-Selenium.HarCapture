using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents the root HAR (HTTP Archive) object.
/// </summary>
public sealed class Har
{
    /// <summary>
    /// Gets or initializes the log object containing all captured HTTP traffic data.
    /// </summary>
    [JsonPropertyName("log")]
    public HarLog Log { get; init; } = null!;
}
