using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Selenium.HarCapture.Capture.Internal;
using Selenium.HarCapture.Models;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Internal;

public sealed class RequestResponseCorrelatorTests
{
    [Fact]
    public void OnRequestSent_ThenResponseReceived_ReturnsEntry()
    {
        // Arrange
        var correlator = new RequestResponseCorrelator();
        var request = CreateTestRequest("https://example.com/api/users");
        var response = CreateTestResponse();
        var timings = new HarTimings { Send = 1, Wait = 50, Receive = 49 };
        var startTime = DateTimeOffset.UtcNow;

        // Act
        correlator.OnRequestSent("req1", request, startTime);
        var entry = correlator.OnResponseReceived("req1", response, timings, 100);

        // Assert
        entry.Should().NotBeNull();
        entry!.Request.Should().BeSameAs(request);
        entry.Response.Should().BeSameAs(response);
        entry.Timings.Should().BeSameAs(timings);
        entry.StartedDateTime.Should().Be(startTime);
        entry.Time.Should().Be(100);
    }

    [Fact]
    public void OnResponseReceived_WithoutRequest_ReturnsNull()
    {
        // Arrange
        var correlator = new RequestResponseCorrelator();
        var response = CreateTestResponse();

        // Act
        var entry = correlator.OnResponseReceived("req99", response, null, 100);

        // Assert
        entry.Should().BeNull();
    }

    [Fact]
    public void OnRequestSent_DoesNotReturnEntry()
    {
        // Arrange
        var correlator = new RequestResponseCorrelator();
        var request = CreateTestRequest();

        // Act
        correlator.OnRequestSent("req1", request, DateTimeOffset.UtcNow);

        // Assert - just verify no exception is thrown
        correlator.PendingCount.Should().Be(1);
    }

    [Fact]
    public void PendingCount_TracksUnmatchedRequests()
    {
        // Arrange
        var correlator = new RequestResponseCorrelator();

        // Act - send 3 requests
        correlator.OnRequestSent("req1", CreateTestRequest(), DateTimeOffset.UtcNow);
        correlator.OnRequestSent("req2", CreateTestRequest(), DateTimeOffset.UtcNow);
        correlator.OnRequestSent("req3", CreateTestRequest(), DateTimeOffset.UtcNow);

        // Assert
        correlator.PendingCount.Should().Be(3);

        // Act - receive 1 response
        correlator.OnResponseReceived("req1", CreateTestResponse(), null, 100);

        // Assert
        correlator.PendingCount.Should().Be(2);
    }

    [Fact]
    public void Clear_RemovesAllPending()
    {
        // Arrange
        var correlator = new RequestResponseCorrelator();
        correlator.OnRequestSent("req1", CreateTestRequest(), DateTimeOffset.UtcNow);
        correlator.OnRequestSent("req2", CreateTestRequest(), DateTimeOffset.UtcNow);
        correlator.OnRequestSent("req3", CreateTestRequest(), DateTimeOffset.UtcNow);

        // Act
        correlator.Clear();

        // Assert
        correlator.PendingCount.Should().Be(0);
    }

    [Fact]
    public void ConcurrentAccess_DoesNotThrow()
    {
        // Arrange
        var correlator = new RequestResponseCorrelator();
        var entries = new List<HarEntry>();
        var lockObject = new object();
        var exception = (Exception?)null;

        // Act - concurrent requests and responses
        try
        {
            Parallel.For(0, 100, i =>
            {
                var requestId = $"req{i}";

                // Send request
                correlator.OnRequestSent(requestId, CreateTestRequest($"https://example.com/api/{i}"), DateTimeOffset.UtcNow);

                // Small interleaving opportunity
                Thread.Sleep(0);

                // Receive response
                var entry = correlator.OnResponseReceived(requestId, CreateTestResponse(), null, 100);

                if (entry != null)
                {
                    lock (lockObject)
                    {
                        entries.Add(entry);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().BeNull("no exceptions should be thrown during concurrent access");
        entries.Should().HaveCount(100, "all requests should produce entries");
        correlator.PendingCount.Should().Be(0, "all requests should be matched");
    }

    private static HarRequest CreateTestRequest(string url = "https://example.com")
    {
        return new HarRequest
        {
            Method = "GET",
            Url = url,
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            QueryString = new List<HarQueryString>(),
            HeadersSize = -1,
            BodySize = -1
        };
    }

    private static HarResponse CreateTestResponse()
    {
        return new HarResponse
        {
            Status = 200,
            StatusText = "OK",
            HttpVersion = "HTTP/1.1",
            Cookies = new List<HarCookie>(),
            Headers = new List<HarHeader>(),
            Content = new HarContent
            {
                Size = 0,
                MimeType = "text/html"
            },
            RedirectURL = "",
            HeadersSize = -1,
            BodySize = -1
        };
    }
}
