namespace Selenium.HarCapture;

/// <summary>
/// Pre-built regex patterns for common PII types.
/// Use with <see cref="Capture.CaptureOptions.WithSensitiveBodyPatterns"/> to redact sensitive data from response bodies.
/// </summary>
/// <remarks>
/// All patterns are linear-time (no nested quantifiers) to avoid catastrophic backtracking.
/// Patterns are designed to be conservative: they may produce false negatives but avoid false positives.
/// The match timeout (100ms) applied by <see cref="Capture.Internal.SensitiveDataRedactor"/> provides
/// an additional safety layer against ReDoS attacks.
/// </remarks>
/// <example>
/// <code>
/// var capture = driver.StartHarCapture(o => o
///     .WithSensitiveBodyPatterns(
///         HarPiiPatterns.Email,
///         HarPiiPatterns.CreditCard,
///         HarPiiPatterns.Ssn));
/// </code>
/// </example>
public static class HarPiiPatterns
{
    /// <summary>
    /// Matches Visa, Mastercard, Amex, and Discover card numbers (16 or 13 digits, without spaces or dashes).
    /// Example: <c>4111111111111111</c>, <c>5500000000000004</c>, <c>371449635398431</c>.
    /// </summary>
    public const string CreditCard =
        @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|6(?:011|5[0-9]{2})[0-9]{12})\b";

    /// <summary>
    /// Matches common email address formats.
    /// Example: <c>user@example.com</c>, <c>first.last+tag@sub.domain.org</c>.
    /// </summary>
    public const string Email =
        @"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b";

    /// <summary>
    /// Matches US Social Security Numbers in NNN-NN-NNNN format.
    /// Example: <c>123-45-6789</c>.
    /// </summary>
    public const string Ssn =
        @"\b\d{3}-\d{2}-\d{4}\b";

    /// <summary>
    /// Matches US phone numbers in common formats (10 digits, with optional country code, spaces, dashes, or parentheses).
    /// Example: <c>(555) 123-4567</c>, <c>555-123-4567</c>, <c>+1 555 123 4567</c>.
    /// </summary>
    public const string Phone =
        @"\b(?:\+1[\s\-]?)?\(?\d{3}\)?[\s\-]?\d{3}[\s\-]?\d{4}\b";

    /// <summary>
    /// Matches IPv4 addresses in dotted-decimal notation.
    /// Example: <c>192.168.1.1</c>, <c>10.0.0.255</c>.
    /// </summary>
    public const string IpAddress =
        @"\b(?:(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}(?:25[0-5]|2[0-4]\d|[01]?\d\d?)\b";
}
