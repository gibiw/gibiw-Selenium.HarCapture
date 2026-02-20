using System.Text.Json;
using FluentAssertions;
using Selenium.HarCapture.IntegrationTests.Infrastructure;
using Selenium.HarCapture.Serialization;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class SerializationRoundtripTests : IntegrationTestBase
{
    public SerializationRoundtripTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_ProducesEquivalentHar()
    {
        // Arrange — capture real traffic via INetwork
        using var capture = StartCapture(NetworkOptions());
        NavigateTo("/api/data");
        WaitForNetworkIdle();
        var originalHar = capture.Stop();

        originalHar.Log.Entries.Should().NotBeEmpty("captured traffic is needed for roundtrip test");

        var tempFile = Path.Combine(Path.GetTempPath(), $"har_test_{Guid.NewGuid()}.har");

        try
        {
            // Act
            await HarSerializer.SaveAsync(originalHar, tempFile);
            var loadedHar = await HarSerializer.LoadAsync(tempFile);

            // Assert
            loadedHar.Should().BeEquivalentTo(originalHar);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_ProducesValidHar12Json_OpenableByBrowser()
    {
        // Arrange — capture real traffic
        using var capture = StartCapture(NetworkOptions());
        NavigateTo("/api/data");
        WaitForNetworkIdle();
        var har = capture.Stop();

        har.Log.Entries.Should().NotBeEmpty("captured traffic is needed for HAR 1.2 validation");

        var tempFile = Path.Combine(Path.GetTempPath(), $"har12_test_{Guid.NewGuid()}.har");

        try
        {
            // Act — save to file and read raw JSON
            await HarSerializer.SaveAsync(har, tempFile);
            var json = await File.ReadAllTextAsync(tempFile);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert — HAR 1.2 required top-level structure
            root.TryGetProperty("log", out var log).Should().BeTrue("HAR root must contain 'log'");

            // log.version
            log.TryGetProperty("version", out var version).Should().BeTrue("log must contain 'version'");
            version.GetString().Should().Be("1.2");

            // log.creator with name and version
            log.TryGetProperty("creator", out var creator).Should().BeTrue("log must contain 'creator'");
            creator.TryGetProperty("name", out var creatorName).Should().BeTrue("creator must contain 'name'");
            creatorName.GetString().Should().NotBeNullOrEmpty();
            creator.TryGetProperty("version", out var creatorVersion).Should().BeTrue("creator must contain 'version'");
            creatorVersion.GetString().Should().NotBeNullOrEmpty();

            // log.entries (required, non-empty array)
            log.TryGetProperty("entries", out var entries).Should().BeTrue("log must contain 'entries'");
            entries.ValueKind.Should().Be(JsonValueKind.Array);
            entries.GetArrayLength().Should().BeGreaterThan(0);

            // Validate each entry has required HAR 1.2 fields
            foreach (var entry in entries.EnumerateArray())
            {
                entry.TryGetProperty("startedDateTime", out _).Should().BeTrue("entry must contain 'startedDateTime'");
                entry.TryGetProperty("time", out _).Should().BeTrue("entry must contain 'time'");

                // request
                entry.TryGetProperty("request", out var request).Should().BeTrue("entry must contain 'request'");
                request.TryGetProperty("method", out _).Should().BeTrue("request must contain 'method'");
                request.TryGetProperty("url", out var url).Should().BeTrue("request must contain 'url'");
                url.GetString().Should().StartWith("http", "URL must be absolute");
                request.TryGetProperty("httpVersion", out _).Should().BeTrue("request must contain 'httpVersion'");
                request.TryGetProperty("cookies", out _).Should().BeTrue("request must contain 'cookies'");
                request.TryGetProperty("headers", out _).Should().BeTrue("request must contain 'headers'");
                request.TryGetProperty("queryString", out _).Should().BeTrue("request must contain 'queryString'");
                request.TryGetProperty("headersSize", out _).Should().BeTrue("request must contain 'headersSize'");
                request.TryGetProperty("bodySize", out _).Should().BeTrue("request must contain 'bodySize'");

                // response
                entry.TryGetProperty("response", out var response).Should().BeTrue("entry must contain 'response'");
                response.TryGetProperty("status", out _).Should().BeTrue("response must contain 'status'");
                response.TryGetProperty("statusText", out _).Should().BeTrue("response must contain 'statusText'");
                response.TryGetProperty("httpVersion", out _).Should().BeTrue("response must contain 'httpVersion'");
                response.TryGetProperty("cookies", out _).Should().BeTrue("response must contain 'cookies'");
                response.TryGetProperty("headers", out _).Should().BeTrue("response must contain 'headers'");
                response.TryGetProperty("content", out var content).Should().BeTrue("response must contain 'content'");
                content.TryGetProperty("size", out _).Should().BeTrue("content must contain 'size'");
                content.TryGetProperty("mimeType", out _).Should().BeTrue("content must contain 'mimeType'");
                response.TryGetProperty("redirectURL", out _).Should().BeTrue("response must contain 'redirectURL'");
                response.TryGetProperty("headersSize", out _).Should().BeTrue("response must contain 'headersSize'");
                response.TryGetProperty("bodySize", out _).Should().BeTrue("response must contain 'bodySize'");

                // cache
                entry.TryGetProperty("cache", out _).Should().BeTrue("entry must contain 'cache'");

                // timings with required send/wait/receive
                entry.TryGetProperty("timings", out var timings).Should().BeTrue("entry must contain 'timings'");
                timings.TryGetProperty("send", out _).Should().BeTrue("timings must contain 'send'");
                timings.TryGetProperty("wait", out _).Should().BeTrue("timings must contain 'wait'");
                timings.TryGetProperty("receive", out _).Should().BeTrue("timings must contain 'receive'");
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
