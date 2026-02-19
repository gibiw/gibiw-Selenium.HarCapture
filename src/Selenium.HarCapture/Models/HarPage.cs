using System;
using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// Represents a page visited during the browser session.
/// </summary>
public sealed class HarPage
{
    /// <summary>
    /// Gets or initializes the date and time when the page load started.
    /// </summary>
    [JsonPropertyName("startedDateTime")]
    public DateTimeOffset StartedDateTime { get; init; }

    /// <summary>
    /// Gets or initializes the unique identifier of the page within the HAR file.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the page title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = null!;

    /// <summary>
    /// Gets or initializes the page timing information.
    /// </summary>
    [JsonPropertyName("pageTimings")]
    public HarPageTimings PageTimings { get; init; } = null!;

    /// <summary>
    /// Gets or initializes an optional comment.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; init; }
}
