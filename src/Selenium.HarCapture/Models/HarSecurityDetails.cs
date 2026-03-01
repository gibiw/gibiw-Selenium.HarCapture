using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Models;

/// <summary>
/// TLS security details for an HTTPS entry.
/// Vendor extension field (_securityDetails) populated from CDP Network.SecurityDetails.
/// </summary>
public sealed class HarSecurityDetails
{
    /// <summary>
    /// Gets or initializes the TLS protocol version (e.g., "TLS 1.3").
    /// </summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = "";

    /// <summary>
    /// Gets or initializes the cipher suite used (e.g., "AES_256_GCM").
    /// </summary>
    [JsonPropertyName("cipher")]
    public string Cipher { get; init; } = "";

    /// <summary>
    /// Gets or initializes the certificate subject name (e.g., "example.com").
    /// </summary>
    [JsonPropertyName("subjectName")]
    public string SubjectName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the certificate issuer (e.g., "Let's Encrypt").
    /// </summary>
    [JsonPropertyName("issuer")]
    public string Issuer { get; init; } = "";

    /// <summary>
    /// Gets or initializes the certificate validity start time as a Unix timestamp.
    /// </summary>
    [JsonPropertyName("validFrom")]
    public long ValidFrom { get; init; }

    /// <summary>
    /// Gets or initializes the certificate validity end time as a Unix timestamp.
    /// </summary>
    [JsonPropertyName("validTo")]
    public long ValidTo { get; init; }
}
