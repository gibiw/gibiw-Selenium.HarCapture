using FluentAssertions;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class MultiPageCaptureTests : IntegrationTestBase
{
    public MultiPageCaptureTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void NewPage_TwoPages_EntriesHaveCorrectPageRef()
    {
        // Arrange
        using var capture = StartCapture(NetworkOptions());
        capture.NewPage("page1", "Home Page");

        // Act — page 1
        NavigateTo("/");
        WaitForNetworkIdle();

        // Act — page 2
        capture.NewPage("page2", "Second Page");
        NavigateTo("/page2");
        WaitForNetworkIdle();

        var har = capture.Stop();

        // Assert
        har.Log.Pages.Should().HaveCount(2);
        har.Log.Pages.Should().Contain(p => p.Id == "page1");
        har.Log.Pages.Should().Contain(p => p.Id == "page2");

        har.Log.Entries.Should().Contain(e => e.PageRef == "page1");
        har.Log.Entries.Should().Contain(e => e.PageRef == "page2");
    }
}
