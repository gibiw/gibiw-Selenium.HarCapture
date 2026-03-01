using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Selenium.HarCapture.Models;
using Xunit;

namespace Selenium.HarCapture.Tests.Models;

/// <summary>
/// Tests for HarEntry body size extension fields (_requestBodySize, _responseBodySize)
/// and initiator extension field (_initiator).
/// </summary>
public sealed class HarEntryTests
{
    private static HarEntry CreateBaseEntry(long requestBodySize = 0, long responseBodySize = 0)
    {
        return new HarEntry
        {
            StartedDateTime = DateTimeOffset.UtcNow,
            Time = 100,
            Request = new HarRequest
            {
                Method = "POST",
                Url = "https://example.com/api",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                QueryString = new List<HarQueryString>(),
                HeadersSize = -1,
                BodySize = requestBodySize
            },
            Response = new HarResponse
            {
                Status = 200,
                StatusText = "OK",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                Content = new HarContent { Size = responseBodySize, MimeType = "application/json" },
                RedirectURL = "",
                HeadersSize = -1,
                BodySize = responseBodySize
            },
            Cache = new HarCache(),
            Timings = new HarTimings { Send = 1, Wait = 50, Receive = 49 },
            RequestBodySize = requestBodySize,
            ResponseBodySize = responseBodySize
        };
    }

    [Fact]
    public void HarEntry_RequestBodySize_Serializes()
    {
        // Arrange
        var entry = CreateBaseEntry(requestBodySize: 1024);

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().Contain("\"_requestBodySize\":1024");
    }

    [Fact]
    public void HarEntry_ResponseBodySize_Serializes()
    {
        // Arrange
        var entry = CreateBaseEntry(responseBodySize: 2048);

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().Contain("\"_responseBodySize\":2048");
    }

    [Fact]
    public void HarEntry_BodySize_Zero_Omitted()
    {
        // Arrange — default RequestBodySize=0, ResponseBodySize=0
        var entry = new HarEntry
        {
            StartedDateTime = DateTimeOffset.UtcNow,
            Time = 100,
            Request = new HarRequest
            {
                Method = "GET",
                Url = "https://example.com/",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                QueryString = new List<HarQueryString>(),
                HeadersSize = -1,
                BodySize = 0
            },
            Response = new HarResponse
            {
                Status = 200,
                StatusText = "OK",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                Content = new HarContent { Size = 0, MimeType = "text/html" },
                RedirectURL = "",
                HeadersSize = -1,
                BodySize = 0
            },
            Cache = new HarCache(),
            Timings = new HarTimings { Send = 1, Wait = 50, Receive = 49 }
            // RequestBodySize and ResponseBodySize default to 0 — should be OMITTED from JSON
        };

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().NotContain("_requestBodySize", "zero value should be omitted via WhenWritingDefault");
        json.Should().NotContain("_responseBodySize", "zero value should be omitted via WhenWritingDefault");
    }

    [Fact]
    public void HarEntry_BodySize_RoundTrip()
    {
        // Arrange
        var entry = CreateBaseEntry(requestBodySize: 512, responseBodySize: 4096);

        // Act
        var json = JsonSerializer.Serialize(entry);
        var deserialized = JsonSerializer.Deserialize<HarEntry>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.RequestBodySize.Should().Be(512);
        deserialized.ResponseBodySize.Should().Be(4096);
    }

    // _initiator extension field tests

    [Fact]
    public void HarEntry_Initiator_Serializes()
    {
        // Arrange
        var entry = CreateBaseEntry();
        var entryWithInitiator = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = entry.Timings,
            Initiator = new HarInitiator
            {
                Type = "script",
                Url = "https://example.com/app.js",
                LineNumber = 42
            }
        };

        // Act
        var json = JsonSerializer.Serialize(entryWithInitiator);

        // Assert
        json.Should().Contain("\"_initiator\"");
        json.Should().Contain("\"type\":\"script\"");
        json.Should().Contain("\"url\":\"https://example.com/app.js\"");
        json.Should().Contain("\"lineNumber\":42");
    }

    [Fact]
    public void HarEntry_Initiator_Null_Omitted()
    {
        // Arrange — Initiator defaults to null
        var entry = CreateBaseEntry();

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().NotContain("_initiator", "null initiator should be omitted via WhenWritingNull");
    }

    [Fact]
    public void HarEntry_Initiator_RoundTrip()
    {
        // Arrange
        var entry = CreateBaseEntry();
        var entryWithInitiator = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = entry.Timings,
            Initiator = new HarInitiator
            {
                Type = "script",
                Url = "https://example.com/app.js",
                LineNumber = 42
            }
        };

        // Act
        var json = JsonSerializer.Serialize(entryWithInitiator);
        var deserialized = JsonSerializer.Deserialize<HarEntry>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Initiator.Should().NotBeNull();
        deserialized.Initiator!.Type.Should().Be("script");
        deserialized.Initiator.Url.Should().Be("https://example.com/app.js");
        deserialized.Initiator.LineNumber.Should().Be(42);
    }

    [Fact]
    public void HarInitiator_Parser_SerializesWithoutUrlAndLineNumber()
    {
        // Arrange — parser initiator has no url/lineNumber
        var initiator = new HarInitiator { Type = "parser" };

        // Act
        var json = JsonSerializer.Serialize(initiator);

        // Assert
        json.Should().Contain("\"type\":\"parser\"");
        json.Should().NotContain("url", "url is null and should be omitted");
        json.Should().NotContain("lineNumber", "lineNumber is null and should be omitted");
    }

    // _securityDetails extension field tests (HAR-07)

    [Fact]
    public void HarEntry_SecurityDetails_Serializes()
    {
        // Arrange
        var entry = CreateBaseEntry();
        var entryWithSecDetails = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = entry.Timings,
            SecurityDetails = new HarSecurityDetails
            {
                Protocol = "TLS 1.3",
                Cipher = "AES_256_GCM",
                SubjectName = "example.com",
                Issuer = "Let's Encrypt",
                ValidFrom = 1700000000L,
                ValidTo = 1730000000L
            }
        };

        // Act
        var json = JsonSerializer.Serialize(entryWithSecDetails);

        // Assert
        json.Should().Contain("\"_securityDetails\"");
        json.Should().Contain("\"protocol\":\"TLS 1.3\"");
        json.Should().Contain("\"cipher\":\"AES_256_GCM\"");
        json.Should().Contain("\"subjectName\":\"example.com\"");
        json.Should().Contain("\"issuer\":\"Let\\u0027s Encrypt\"");
        json.Should().Contain("\"validFrom\":1700000000");
        json.Should().Contain("\"validTo\":1730000000");
    }

    [Fact]
    public void HarEntry_SecurityDetails_Null_Omitted()
    {
        // Arrange — SecurityDetails defaults to null
        var entry = CreateBaseEntry();

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().NotContain("_securityDetails", "null SecurityDetails should be omitted via WhenWritingNull");
    }

    [Fact]
    public void HarEntry_SecurityDetails_RoundTrip()
    {
        // Arrange
        var entry = CreateBaseEntry();
        var entryWithSecDetails = new HarEntry
        {
            StartedDateTime = entry.StartedDateTime,
            Time = entry.Time,
            Request = entry.Request,
            Response = entry.Response,
            Cache = entry.Cache,
            Timings = entry.Timings,
            SecurityDetails = new HarSecurityDetails
            {
                Protocol = "TLS 1.3",
                Cipher = "AES_256_GCM",
                SubjectName = "example.com",
                Issuer = "DigiCert",
                ValidFrom = 1700000000L,
                ValidTo = 1730000000L
            }
        };

        // Act
        var json = JsonSerializer.Serialize(entryWithSecDetails);
        var deserialized = JsonSerializer.Deserialize<HarEntry>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.SecurityDetails.Should().NotBeNull();
        deserialized.SecurityDetails!.Protocol.Should().Be("TLS 1.3");
        deserialized.SecurityDetails.Cipher.Should().Be("AES_256_GCM");
        deserialized.SecurityDetails.SubjectName.Should().Be("example.com");
        deserialized.SecurityDetails.Issuer.Should().Be("DigiCert");
        deserialized.SecurityDetails.ValidFrom.Should().Be(1700000000L);
        deserialized.SecurityDetails.ValidTo.Should().Be(1730000000L);
    }

    [Fact]
    public void HarEntry_Cache_BeforeRequest_Serializes()
    {
        // Arrange — entry with cache hit sentinel values
        var entry = new HarEntry
        {
            StartedDateTime = DateTimeOffset.UtcNow,
            Time = 0,
            Request = CreateBaseEntry().Request,
            Response = CreateBaseEntry().Response,
            Cache = new HarCache
            {
                BeforeRequest = new HarCacheEntry
                {
                    LastAccess = DateTimeOffset.MinValue,
                    ETag = "",
                    HitCount = 0
                }
            },
            Timings = new HarTimings { Send = 0, Wait = 0, Receive = 0 }
        };

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().Contain("\"cache\"");
        json.Should().Contain("\"beforeRequest\"");
    }

    [Fact]
    public void HarEntry_Cache_Empty_BeforeRequest_Null()
    {
        // Arrange — normal 200 response: cache object present but beforeRequest is null
        var entry = CreateBaseEntry();
        // entry.Cache = new HarCache() (default from CreateBaseEntry) — beforeRequest is null

        // Act
        var json = JsonSerializer.Serialize(entry);

        // Assert
        json.Should().Contain("\"cache\"");
        // beforeRequest null means it's either absent or explicitly null — both are valid
        // The key point is that cache is present and no beforeRequest content
        json.Should().NotContain("\"hitCount\"", "no cache hit means no HarCacheEntry fields");
    }
}
