using FluentAssertions;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class UrlFilteringTests : IntegrationTestBase
{
    public UrlFilteringTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void UrlIncludePattern_OnlyCapturesMatchingUrls()
    {
        // Arrange
        var options = NetworkOptions()
            .WithUrlIncludePatterns("**/api/**");
        using var capture = StartCapture(options);

        // Act
        NavigateTo("/with-fetch");
        WaitForNetworkIdle(1000);
        var har = capture.Stop();

        // Assert
        har.Log.Entries.Should().NotBeEmpty();
        har.Log.Entries.Should().OnlyContain(e => e.Request.Url.Contains("/api/"));
    }

    [Fact]
    public void UrlExcludePattern_FiltersOutMatchingUrls()
    {
        // Arrange
        var options = NetworkOptions()
            .WithUrlExcludePatterns("**/api/**");
        using var capture = StartCapture(options);

        // Act
        NavigateTo("/with-fetch");
        WaitForNetworkIdle(1000);
        var har = capture.Stop();

        // Assert
        har.Log.Entries.Should().NotContain(e => e.Request.Url.Contains("/api/"));
    }
}
