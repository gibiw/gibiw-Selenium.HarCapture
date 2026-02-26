using FluentAssertions;
using Selenium.HarCapture.Capture;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture;

public sealed class CaptureTypeTests
{
    [Fact]
    public void None_HasValue_Zero()
    {
        // Arrange & Act
        var noneValue = (int)CaptureType.None;

        // Assert
        noneValue.Should().Be(0);
    }

    [Fact]
    public void WebSocket_HasValue_1024()
    {
        // Arrange & Act
        var value = (int)CaptureType.WebSocket;

        // Assert
        value.Should().Be(1024);
    }

    [Fact]
    public void IndividualFlags_ArePowersOfTwo()
    {
        // Arrange
        var individualFlags = new[]
        {
            CaptureType.RequestHeaders,      // 1
            CaptureType.RequestCookies,      // 2
            CaptureType.RequestContent,      // 4
            CaptureType.RequestBinaryContent,// 8
            CaptureType.ResponseHeaders,     // 16
            CaptureType.ResponseCookies,     // 32
            CaptureType.ResponseContent,     // 64
            CaptureType.ResponseBinaryContent,// 128
            CaptureType.Timings,             // 256
            CaptureType.ConnectionInfo,      // 512
            CaptureType.WebSocket            // 1024
        };

        // Act & Assert
        foreach (var flag in individualFlags)
        {
            var value = (int)flag;
            // Check that it's a power of 2 (only one bit set)
            (value > 0 && (value & (value - 1)) == 0).Should().BeTrue(
                $"{flag} should be a power of 2, but was {value}");
        }

        // Verify all values are distinct
        var values = individualFlags.Select(f => (int)f).ToList();
        values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void HasFlag_Works_ForCombinations()
    {
        // Arrange
        var allText = CaptureType.AllText;

        // Act & Assert
        allText.HasFlag(CaptureType.RequestHeaders).Should().BeTrue();
        allText.HasFlag(CaptureType.RequestCookies).Should().BeTrue();
        allText.HasFlag(CaptureType.ResponseHeaders).Should().BeTrue();
        allText.HasFlag(CaptureType.ResponseCookies).Should().BeTrue();
        allText.HasFlag(CaptureType.RequestContent).Should().BeTrue();
        allText.HasFlag(CaptureType.ResponseContent).Should().BeTrue();
        allText.HasFlag(CaptureType.Timings).Should().BeTrue();

        // AllText excludes binary content and WebSocket
        allText.HasFlag(CaptureType.RequestBinaryContent).Should().BeFalse();
        allText.HasFlag(CaptureType.ResponseBinaryContent).Should().BeFalse();
        allText.HasFlag(CaptureType.WebSocket).Should().BeFalse();
    }

    [Fact]
    public void HeadersAndCookies_IncludesCorrectFlags()
    {
        // Arrange
        var headersAndCookies = CaptureType.HeadersAndCookies;

        // Act & Assert - should include
        headersAndCookies.HasFlag(CaptureType.RequestHeaders).Should().BeTrue();
        headersAndCookies.HasFlag(CaptureType.RequestCookies).Should().BeTrue();
        headersAndCookies.HasFlag(CaptureType.ResponseHeaders).Should().BeTrue();
        headersAndCookies.HasFlag(CaptureType.ResponseCookies).Should().BeTrue();

        // Should NOT include
        headersAndCookies.HasFlag(CaptureType.RequestContent).Should().BeFalse();
        headersAndCookies.HasFlag(CaptureType.ResponseContent).Should().BeFalse();
        headersAndCookies.HasFlag(CaptureType.Timings).Should().BeFalse();
        headersAndCookies.HasFlag(CaptureType.RequestBinaryContent).Should().BeFalse();
        headersAndCookies.HasFlag(CaptureType.ResponseBinaryContent).Should().BeFalse();
        headersAndCookies.HasFlag(CaptureType.ConnectionInfo).Should().BeFalse();
    }

    [Fact]
    public void All_IncludesEverything()
    {
        // Arrange
        var all = CaptureType.All;

        // Act & Assert - should include every individual flag
        all.HasFlag(CaptureType.RequestHeaders).Should().BeTrue();
        all.HasFlag(CaptureType.RequestCookies).Should().BeTrue();
        all.HasFlag(CaptureType.RequestContent).Should().BeTrue();
        all.HasFlag(CaptureType.RequestBinaryContent).Should().BeTrue();
        all.HasFlag(CaptureType.ResponseHeaders).Should().BeTrue();
        all.HasFlag(CaptureType.ResponseCookies).Should().BeTrue();
        all.HasFlag(CaptureType.ResponseContent).Should().BeTrue();
        all.HasFlag(CaptureType.ResponseBinaryContent).Should().BeTrue();
        all.HasFlag(CaptureType.Timings).Should().BeTrue();
        all.HasFlag(CaptureType.ConnectionInfo).Should().BeTrue();
        all.HasFlag(CaptureType.WebSocket).Should().BeTrue();
    }

    [Fact]
    public void BitwiseOr_CombinesFlags()
    {
        // Arrange & Act
        var combined = CaptureType.RequestHeaders | CaptureType.Timings;

        // Assert
        combined.HasFlag(CaptureType.RequestHeaders).Should().BeTrue();
        combined.HasFlag(CaptureType.Timings).Should().BeTrue();
        combined.HasFlag(CaptureType.ResponseHeaders).Should().BeFalse();
    }
}
