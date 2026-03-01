using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Selenium.HarCapture.Models;
using Xunit;

namespace Selenium.HarCapture.Tests.Models;

/// <summary>
/// Tests for HarEntry body size extension fields (_requestBodySize, _responseBodySize).
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
}
