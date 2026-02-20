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
}
