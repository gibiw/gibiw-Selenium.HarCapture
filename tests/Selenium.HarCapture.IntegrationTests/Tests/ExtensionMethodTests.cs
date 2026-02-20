using FluentAssertions;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Extensions;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ExtensionMethodTests : IntegrationTestBase
{
    public ExtensionMethodTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void StartHarCapture_ReturnsActiveCapture()
    {
        // Act
        using var capture = Driver.StartHarCapture(NetworkOptions());

        // Assert
        capture.IsCapturing.Should().BeTrue();
        capture.ActiveStrategyName.Should().Be("INetwork");
    }

    [Fact]
    public void StartHarCapture_WithFluentConfig_AppliesCreatorName()
    {
        // Act
        using var capture = Driver.StartHarCapture(o =>
        {
            o.ForceSeleniumNetwork();
            o.WithCreatorName("MyTool");
        });
        NavigateTo("/api/data");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        har.Log.Creator.Name.Should().Be("MyTool");
    }

    [Fact]
    public async Task CaptureHarAsync_OneLiner_ReturnsHarWithEntries()
    {
        // Act
        var har = await Driver.CaptureHarAsync(async () =>
        {
            NavigateTo("/api/data");
            await Task.Delay(500);
        }, NetworkOptions());

        // Assert
        har.Log.Entries.Should().NotBeEmpty();
        har.Log.Entries.Should().Contain(e => e.Request.Url.Contains("/api/data"));
    }

    [Fact]
    public void CaptureHar_OneLiner_ReturnsHarWithEntries()
    {
        // Act
        var har = Driver.CaptureHar(() =>
        {
            NavigateTo("/api/data");
            WaitForNetworkIdle();
        }, NetworkOptions());

        // Assert
        har.Log.Entries.Should().NotBeEmpty();
        har.Log.Entries.Should().Contain(e => e.Request.Url.Contains("/api/data"));
    }
}
