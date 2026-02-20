using FluentAssertions;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class DisposeCleanupTests : IntegrationTestBase
{
    public DisposeCleanupTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void Dispose_WhileCapturing_StopsCapture()
    {
        // Arrange
        var capture = StartCapture(NetworkOptions());
        capture.IsCapturing.Should().BeTrue();

        // Act
        capture.Dispose();

        // Assert
        capture.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public void Dispose_AfterDispose_ThrowsObjectDisposedOnAccess()
    {
        // Arrange
        var capture = StartCapture(NetworkOptions());
        capture.Dispose();

        // Act & Assert
        var act = () => capture.Stop();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DoubleDispose_DoesNotThrow()
    {
        // Arrange
        var capture = StartCapture(NetworkOptions());

        // Act & Assert
        var act = () =>
        {
            capture.Dispose();
            capture.Dispose();
        };
        act.Should().NotThrow();
    }
}
