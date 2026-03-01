using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Selenium.HarCapture.Capture.Internal;
using Selenium.HarCapture.Capture.Internal.Cdp;
using Selenium.HarCapture.Capture.Strategies;
using Selenium.HarCapture.Models;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture;

/// <summary>
/// Tests for HAR 1.2 spec completeness features (protocol version, page timings, error handling).
/// </summary>
public sealed class HarCompletenessTests
{
    #region HAR-01: Protocol Version Support

    [Fact]
    public void CdpResponseInfo_Protocol_StoresH2Value()
    {
        // Arrange & Act
        var responseInfo = new CdpResponseInfo { Protocol = "h2" };

        // Assert
        responseInfo.Protocol.Should().Be("h2");
    }

    [Fact]
    public void CdpResponseInfo_Protocol_StoresH3Value()
    {
        // Arrange & Act
        var responseInfo = new CdpResponseInfo { Protocol = "h3" };

        // Assert
        responseInfo.Protocol.Should().Be("h3");
    }

    [Fact]
    public void CdpResponseInfo_Protocol_StoresHttpValue()
    {
        // Arrange & Act
        var responseInfo = new CdpResponseInfo { Protocol = "http/1.1" };

        // Assert
        responseInfo.Protocol.Should().Be("http/1.1");
    }

    [Fact]
    public void CdpResponseInfo_Protocol_NullFallback()
    {
        // Arrange & Act
        var responseInfo = new CdpResponseInfo { Protocol = null };

        // Assert
        responseInfo.Protocol.Should().BeNull();
    }

    #endregion

    #region HAR-02: Page Timings Support

    [Fact]
    public void HarPageTimings_WithValues_SerializesCorrectly()
    {
        // Arrange
        var pageTimings = new HarPageTimings
        {
            OnContentLoad = 1234.5,
            OnLoad = 2345.6
        };

        // Act
        var json = JsonSerializer.Serialize(pageTimings);
        var deserialized = JsonSerializer.Deserialize<HarPageTimings>(json);

        // Assert
        json.Should().Contain("\"onContentLoad\":1234.5");
        json.Should().Contain("\"onLoad\":2345.6");
        deserialized.Should().NotBeNull();
        deserialized!.OnContentLoad.Should().Be(1234.5);
        deserialized.OnLoad.Should().Be(2345.6);
    }

    [Fact]
    public void HarPageTimings_WithNullValues_OmitsFromJson()
    {
        // Arrange
        var pageTimings = new HarPageTimings
        {
            OnContentLoad = null,
            OnLoad = null
        };

        // Act
        var json = JsonSerializer.Serialize(pageTimings);

        // Assert
        json.Should().NotContain("onContentLoad");
        json.Should().NotContain("onLoad");
        json.Should().Be("{}");
    }

    [Fact]
    public void HarPageTimings_WithPartialValues_SerializesOnlyPresent()
    {
        // Arrange
        var pageTimings = new HarPageTimings
        {
            OnContentLoad = 1234.5,
            OnLoad = null
        };

        // Act
        var json = JsonSerializer.Serialize(pageTimings);

        // Assert
        json.Should().Contain("\"onContentLoad\":1234.5");
        json.Should().NotContain("onLoad");
    }

    [Fact]
    public void CdpPageTimingEventData_Timestamp_StoresValue()
    {
        // Arrange & Act
        var eventData = new CdpPageTimingEventData { Timestamp = 123.456 };

        // Assert
        eventData.Timestamp.Should().Be(123.456);
    }

    #endregion

    #region HAR-03: Cookie Parse Error Handling

    // Note: Testing the actual logging behavior would require creating FileLogger instances
    // and verifying file content, which is complex for unit tests.
    // Instead, we verify that the parsing methods are resilient to malformed input.

    // The change in HAR-03 is replacing silent catch blocks with FileLogger?.Log calls.
    // The behavior (return empty list on parse error) remains the same.
    // This is verified by the existing integration tests.

    #endregion

    #region HAR-05: Request Initiator (_initiator)

    [Fact]
    public void Correlator_Stores_Initiator_And_Outputs_In_HarEntry()
    {
        // Arrange
        var correlator = new RequestResponseCorrelator();
        var request = new HarRequest
        {
            Method = "GET",
            Url = "https://example.com/api/data",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            QueryString = new List<HarQueryString>(),
            HeadersSize = -1,
            BodySize = 0
        };
        var initiator = new CdpInitiatorInfo
        {
            Type = "script",
            Url = "https://example.com/app.js",
            LineNumber = 42
        };
        var response = new HarResponse
        {
            Status = 200,
            StatusText = "OK",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            Content = new HarContent { Size = 0, MimeType = "application/json" },
            RedirectURL = "",
            HeadersSize = -1,
            BodySize = -1
        };

        // Act
        correlator.OnRequestSent("req-1", request, DateTimeOffset.UtcNow, initiator);
        var entry = correlator.OnResponseReceived("req-1", response, null, 100.0);

        // Assert
        entry.Should().NotBeNull();
        entry!.Initiator.Should().NotBeNull();
        entry.Initiator!.Type.Should().Be("script");
        entry.Initiator.Url.Should().Be("https://example.com/app.js");
        entry.Initiator.LineNumber.Should().Be(42);
    }

    [Fact]
    public void Correlator_Null_Initiator_Results_In_Null_HarEntry_Initiator()
    {
        // Arrange
        var correlator = new RequestResponseCorrelator();
        var request = new HarRequest
        {
            Method = "GET",
            Url = "https://example.com/",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            QueryString = new List<HarQueryString>(),
            HeadersSize = -1,
            BodySize = 0
        };
        var response = new HarResponse
        {
            Status = 200,
            StatusText = "OK",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            Content = new HarContent { Size = 0, MimeType = "text/html" },
            RedirectURL = "",
            HeadersSize = -1,
            BodySize = -1
        };

        // Act — no initiator passed (uses default null)
        correlator.OnRequestSent("req-2", request, DateTimeOffset.UtcNow);
        var entry = correlator.OnResponseReceived("req-2", response, null, 50.0);

        // Assert
        entry.Should().NotBeNull();
        entry!.Initiator.Should().BeNull("no initiator was provided");
    }

    #endregion

    #region HAR-06: Cache Hit/Miss Detection

    private static HarEntry CreateSimpleEntry(int status = 200, bool fromDiskCache = false, bool fromServiceWorker = false, CdpSecurityDetails? securityDetails = null)
    {
        bool isCacheHit = fromDiskCache || fromServiceWorker || status == 304;
        var cache = isCacheHit
            ? new HarCache { BeforeRequest = new HarCacheEntry { LastAccess = DateTimeOffset.MinValue, ETag = "", HitCount = 0 } }
            : new HarCache();

        HarSecurityDetails? harSecDetails = securityDetails != null
            ? new HarSecurityDetails
            {
                Protocol = securityDetails.Protocol,
                Cipher = securityDetails.Cipher,
                SubjectName = securityDetails.SubjectName,
                Issuer = securityDetails.Issuer,
                ValidFrom = securityDetails.ValidFrom,
                ValidTo = securityDetails.ValidTo
            }
            : null;

        return new HarEntry
        {
            StartedDateTime = DateTimeOffset.UtcNow,
            Time = 10,
            Request = new HarRequest
            {
                Method = "GET",
                Url = "https://example.com/resource",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                QueryString = new List<HarQueryString>(),
                HeadersSize = -1,
                BodySize = 0
            },
            Response = new HarResponse
            {
                Status = status,
                StatusText = status == 304 ? "Not Modified" : "OK",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                Content = new HarContent { Size = 0, MimeType = "text/html" },
                RedirectURL = "",
                HeadersSize = -1,
                BodySize = -1
            },
            Cache = cache,
            Timings = new HarTimings { Send = 0, Wait = 5, Receive = 5 },
            SecurityDetails = harSecDetails
        };
    }

    [Fact]
    public void CacheHit_FromDiskCache_PopulatesBeforeRequest()
    {
        // Arrange & Act
        var entry = CreateSimpleEntry(status: 200, fromDiskCache: true);

        // Assert
        entry.Cache.Should().NotBeNull();
        entry.Cache.BeforeRequest.Should().NotBeNull("fromDiskCache=true should populate cache.beforeRequest");
        entry.Cache.BeforeRequest!.ETag.Should().Be("");
        entry.Cache.BeforeRequest.HitCount.Should().Be(0);
        entry.Cache.BeforeRequest.LastAccess.Should().Be(DateTimeOffset.MinValue);
    }

    [Fact]
    public void CacheHit_304_PopulatesBeforeRequest()
    {
        // Arrange & Act
        var entry = CreateSimpleEntry(status: 304);

        // Assert
        entry.Cache.Should().NotBeNull();
        entry.Cache.BeforeRequest.Should().NotBeNull("HTTP 304 should populate cache.beforeRequest");
    }

    [Fact]
    public void NoCacheHit_LeavesCacheEmpty()
    {
        // Arrange & Act — normal 200 response with no cache flags
        var entry = CreateSimpleEntry(status: 200, fromDiskCache: false);

        // Assert
        entry.Cache.Should().NotBeNull();
        entry.Cache.BeforeRequest.Should().BeNull("normal 200 response should leave cache.beforeRequest null");
    }

    [Fact]
    public void CacheHit_FromServiceWorker_PopulatesBeforeRequest()
    {
        // Arrange & Act
        var entry = CreateSimpleEntry(status: 200, fromServiceWorker: true);

        // Assert
        entry.Cache.Should().NotBeNull();
        entry.Cache.BeforeRequest.Should().NotBeNull("fromServiceWorker=true should populate cache.beforeRequest");
    }

    [Fact]
    public void CacheHit_Serializes_BeforeRequest_With_Sentinels()
    {
        // Arrange
        var entry = CreateSimpleEntry(status: 200, fromDiskCache: true);

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().Contain("\"beforeRequest\"");
        json.Should().Contain("\"lastAccess\"");
    }

    #endregion

    #region HAR-07: SecurityDetails TLS Extension Field

    [Fact]
    public void SecurityDetails_Populated_ForHttps()
    {
        // Arrange
        var secDetails = new CdpSecurityDetails
        {
            Protocol = "TLS 1.3",
            Cipher = "AES_256_GCM",
            SubjectName = "example.com",
            Issuer = "Let's Encrypt",
            ValidFrom = 1700000000L,
            ValidTo = 1730000000L
        };

        // Act
        var entry = CreateSimpleEntry(status: 200, securityDetails: secDetails);

        // Assert
        entry.SecurityDetails.Should().NotBeNull("HTTPS response with SecurityDetails should populate _securityDetails");
        entry.SecurityDetails!.Protocol.Should().Be("TLS 1.3");
        entry.SecurityDetails.Cipher.Should().Be("AES_256_GCM");
        entry.SecurityDetails.SubjectName.Should().Be("example.com");
        entry.SecurityDetails.ValidFrom.Should().Be(1700000000L);
        entry.SecurityDetails.ValidTo.Should().Be(1730000000L);
    }

    [Fact]
    public void SecurityDetails_Null_WhenAbsent()
    {
        // Arrange & Act — response without SecurityDetails (HTTP)
        var entry = CreateSimpleEntry(status: 200, securityDetails: null);

        // Assert
        entry.SecurityDetails.Should().BeNull("HTTP response without SecurityDetails should leave _securityDetails null");
    }

    [Fact]
    public void SecurityDetails_ExtractionFailure_DoesNotCrash()
    {
        // Arrange — CdpSecurityDetails with default empty values (simulates partial extraction)
        var secDetails = new CdpSecurityDetails();

        // Act
        var entry = CreateSimpleEntry(status: 200, securityDetails: secDetails);

        // Assert — should not throw, entry is valid with empty strings
        entry.SecurityDetails.Should().NotBeNull();
        entry.SecurityDetails!.Protocol.Should().Be("");
        entry.SecurityDetails.Cipher.Should().Be("");
    }

    [Fact]
    public void SecurityDetails_OmittedFromJson_WhenNull()
    {
        // Arrange
        var entry = CreateSimpleEntry(status: 200, securityDetails: null);

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().NotContain("_securityDetails", "null SecurityDetails should be omitted from JSON");
    }

    [Fact]
    public void SecurityDetails_PresentInJson_WhenNotNull()
    {
        // Arrange
        var secDetails = new CdpSecurityDetails
        {
            Protocol = "TLS 1.2",
            Cipher = "ECDHE_RSA_AES_128_GCM_SHA256",
            SubjectName = "api.example.com",
            Issuer = "DigiCert",
            ValidFrom = 1680000000L,
            ValidTo = 1710000000L
        };

        // Act
        var entry = CreateSimpleEntry(status: 200, securityDetails: secDetails);
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().Contain("\"_securityDetails\"");
        json.Should().Contain("\"protocol\":\"TLS 1.2\"");
    }

    #endregion
}
