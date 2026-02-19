using FluentAssertions;
using Selenium.HarCapture.Capture;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture;

public sealed class CaptureOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.CaptureTypes.Should().Be(CaptureType.AllText);
        options.CreatorName.Should().Be("Selenium.HarCapture");
        options.ForceSeleniumNetworkApi.Should().BeFalse();
        options.MaxResponseBodySize.Should().Be(0);
        options.UrlIncludePatterns.Should().BeNull();
        options.UrlExcludePatterns.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var options = new CaptureOptions();
        var includePatterns = new[] { "https://api.example.com/**" };
        var excludePatterns = new[] { "**/*.png" };

        // Act
        options.CaptureTypes = CaptureType.All;
        options.CreatorName = "CustomCreator";
        options.ForceSeleniumNetworkApi = true;
        options.MaxResponseBodySize = 1024000;
        options.UrlIncludePatterns = includePatterns;
        options.UrlExcludePatterns = excludePatterns;

        // Assert
        options.CaptureTypes.Should().Be(CaptureType.All);
        options.CreatorName.Should().Be("CustomCreator");
        options.ForceSeleniumNetworkApi.Should().BeTrue();
        options.MaxResponseBodySize.Should().Be(1024000);
        options.UrlIncludePatterns.Should().BeSameAs(includePatterns);
        options.UrlExcludePatterns.Should().BeSameAs(excludePatterns);
    }
}
