namespace Selenium.HarCapture.Serialization;

/// <summary>
/// Describes a single validation finding (error or warning) within a HAR object graph.
/// </summary>
public sealed class HarValidationError
{
    /// <summary>Dot-path to the offending field, e.g. "log.entries[0].request.url".</summary>
    public string Field { get; }

    /// <summary>Human-readable description of the problem.</summary>
    public string Message { get; }

    /// <summary>Whether this finding is an error (invalidating) or a warning (informational).</summary>
    public HarValidationSeverity Severity { get; }

    /// <summary>Initialises a new validation finding.</summary>
    public HarValidationError(string field, string message, HarValidationSeverity severity)
    {
        Field = field;
        Message = message;
        Severity = severity;
    }

    /// <inheritdoc/>
    public override string ToString() => $"[{Severity}] {Field}: {Message}";
}
