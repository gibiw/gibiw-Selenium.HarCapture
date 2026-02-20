using FluentAssertions;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ResponseBodyCaptureTests : IntegrationTestBase
{
    public ResponseBodyCaptureTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void CdpCapture_WhenCompatible_CapturesXhrResponseBody()
    {
        if (!IsCdpCompatible())
        {
            // Response body capture requires CDP with compatible Chrome version
            return;
        }

        // Arrange
        var options = new CaptureOptions()
            .WithCaptureTypes(CaptureType.AllText);
        using var capture = StartCapture(options);

        // Act
        NavigateTo("/with-fetch");
        WaitForNetworkIdle(1000);
        var har = capture.Stop();

        // Assert
        var apiEntry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/data"))
            .Which;
        apiEntry.Response.Content.Text.Should().Contain("hello");
    }

    [Fact]
    public void CdpCapture_WhenCompatible_MaxResponseBodySizeTruncatesBody()
    {
        if (!IsCdpCompatible())
        {
            // MaxResponseBodySize requires CDP with compatible Chrome version
            return;
        }

        // Arrange
        var options = new CaptureOptions()
            .WithCaptureTypes(CaptureType.AllText)
            .WithMaxResponseBodySize(100);
        using var capture = StartCapture(options);

        // Act
        NavigateTo("/api/large");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        var entry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/large"))
            .Which;
        if (entry.Response.Content.Text is not null)
        {
            entry.Response.Content.Text.Length.Should().BeLessThanOrEqualTo(200);
        }
    }
}
