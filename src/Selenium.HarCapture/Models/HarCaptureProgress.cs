using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Carries progress information about a single HAR entry that was just written.
/// Delivered via <see cref="Selenium.HarCapture.Capture.HarCaptureSession.EntryWritten"/>.
/// </summary>
public sealed class HarCaptureProgress
{
    /// <summary>
    /// Total number of entries written to the HAR so far (including this one).
    /// </summary>
    [JsonPropertyName("entryCount")]
    public int EntryCount { get; init; }

    /// <summary>
    /// The page reference of the page this entry belongs to, or null if no page was set.
    /// </summary>
    [JsonPropertyName("currentPageRef")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrentPageRef { get; init; }

    /// <summary>
    /// The URL of the entry that was just written.
    /// </summary>
    [JsonPropertyName("entryUrl")]
    public string EntryUrl { get; init; } = null!;
}
