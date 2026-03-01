namespace Selenium.HarCapture.Serialization;

/// <summary>
/// Indicates the severity of a HAR validation finding.
/// </summary>
public enum HarValidationSeverity
{
    /// <summary>A structural or value error that invalidates the HAR file.</summary>
    Error,

    /// <summary>A non-fatal issue that does not affect <see cref="HarValidationResult.IsValid"/>.</summary>
    Warning
}
