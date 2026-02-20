using FluentAssertions;
using OpenQA.Selenium.Chrome;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Trait("Category", "Integration")]
public sealed class INetworkFallbackTests : IAsyncLifetime
{
    private ChromeDriver _driver = null!;
    private TestWebServer _server = null!;

    public async Task InitializeAsync()
    {
        _server = new TestWebServer();
        await _server.InitializeAsync();

        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--disable-extensions");

        _driver = new ChromeDriver(options);
    }

    public async Task DisposeAsync()
    {
        _driver?.Quit();
        _driver?.Dispose();
        await _server.DisposeAsync();
    }

    [Fact]
    public void ForceSeleniumNetwork_CapturesEntries()
    {
        // Arrange
        var options = new CaptureOptions().ForceSeleniumNetwork();
        using var capture = new HarCapture(_driver, options);
        capture.Start();

        // Act
        _driver.Navigate().GoToUrl($"{_server.BaseUrl}/api/data");
        Thread.Sleep(1000);
        var har = capture.Stop();

        // Assert
        capture.ActiveStrategyName.Should().Be("INetwork");
        har.Log.Entries.Should().NotBeEmpty();
    }
}
