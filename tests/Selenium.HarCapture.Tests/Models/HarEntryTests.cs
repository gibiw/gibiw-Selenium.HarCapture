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
}
