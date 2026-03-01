namespace Selenium.HarCapture.Serialization;

/// <summary>
/// Controls how strictly the HAR validator interprets the HAR 1.2 specification.
/// </summary>
public enum HarValidationMode
{
    /// <summary>
    /// Strict conformance: rejects -1 sentinels for size/timing fields and flags absent pages as a warning.
    /// </summary>
    Strict,

    /// <summary>
    /// Standard mode (default): accepts -1 sentinels, underscore extension fields, and absent pages array.
    /// This is the mode in which all library-produced HAR files are valid.
    /// </summary>
    Standard,

    /// <summary>
    /// Lenient mode: like Standard but suppresses warnings for extension fields and missing optional fields.
    /// </summary>
    Lenient
}
