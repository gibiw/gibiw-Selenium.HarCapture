using FluentAssertions;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Stress")]
public sealed class StressTests : IntegrationTestBase
{
    public StressTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void Capture_Over500Requests_AllCaptured_Network()
    {
        // Arrange — INetwork strategy (always available, no CDP version dependency)
        using var capture = StartCapture(NetworkOptions());

        // Act — /with-fetch triggers 2+ events per navigation (document + JS fetch to /api/data)
        // 250 iterations * 2+ events = 500+ entries
        for (int i = 0; i < 250; i++)
        {
            NavigateTo("/with-fetch");
            WaitForNetworkIdle(100);
        }

        var har = capture.Stop();

        // Assert
        har.Log.Entries.Count.Should().BeGreaterThan(500,
            "each /with-fetch navigation produces at least 2 network events, so 250 iterations should yield 500+ entries");
    }

    [Fact]
    public void Capture_Over500Requests_AllCaptured_Cdp()
    {
        // Skip if browser version not CDP-compatible
        if (!IsCdpCompatible())
        {
            return;
        }

        // Arrange — CDP strategy
        using var capture = StartCapture(CdpOptions());

        // Act — /with-fetch triggers 2+ events per navigation (document + JS fetch to /api/data)
        // 250 iterations * 2+ events = 500+ entries
        for (int i = 0; i < 250; i++)
        {
            NavigateTo("/with-fetch");
            WaitForNetworkIdle(100);
        }

        var har = capture.Stop();

        // Assert
        har.Log.Entries.Count.Should().BeGreaterThan(500,
            "each /with-fetch navigation produces at least 2 network events, so 250 iterations should yield 500+ entries");
    }
}
