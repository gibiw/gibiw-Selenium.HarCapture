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
        options.EnableCompression.Should().BeFalse();
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

    [Fact]
    public void FluentApi_WithCaptureTypes_SetsValueAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithCaptureTypes(CaptureType.All);

        // Assert
        options.CaptureTypes.Should().Be(CaptureType.All);
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void FluentApi_WithMaxResponseBodySize_SetsValueAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithMaxResponseBodySize(1024000);

        // Assert
        options.MaxResponseBodySize.Should().Be(1024000);
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void FluentApi_WithUrlIncludePatterns_SetsValueAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithUrlIncludePatterns("*.com/**", "*.org/**");

        // Assert
        options.UrlIncludePatterns.Should().Equal("*.com/**", "*.org/**");
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void FluentApi_WithUrlExcludePatterns_SetsValueAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithUrlExcludePatterns("**/*.png", "**/*.jpg");

        // Assert
        options.UrlExcludePatterns.Should().Equal("**/*.png", "**/*.jpg");
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void FluentApi_WithCreatorName_SetsValueAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithCreatorName("CustomCreator");

        // Assert
        options.CreatorName.Should().Be("CustomCreator");
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void FluentApi_ForceSeleniumNetwork_SetsValueAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.ForceSeleniumNetwork();

        // Assert
        options.ForceSeleniumNetworkApi.Should().BeTrue();
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void FluentApi_MethodChaining_ConfiguresMultipleOptions()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options
            .WithCaptureTypes(CaptureType.All)
            .WithMaxResponseBodySize(2048000)
            .WithCreatorName("ChainedCreator")
            .WithUrlIncludePatterns("https://api.example.com/**")
            .ForceSeleniumNetwork();

        // Assert
        options.CaptureTypes.Should().Be(CaptureType.All);
        options.MaxResponseBodySize.Should().Be(2048000);
        options.CreatorName.Should().Be("ChainedCreator");
        options.UrlIncludePatterns.Should().Equal("https://api.example.com/**");
        options.ForceSeleniumNetworkApi.Should().BeTrue();
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void BrowserOverride_DefaultIsNull()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.BrowserName.Should().BeNull();
        options.BrowserVersion.Should().BeNull();
    }

    [Fact]
    public void FluentApi_WithBrowser_SetsOverrideAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithBrowser("MyBrowser", "1.0");

        // Assert
        options.BrowserName.Should().Be("MyBrowser");
        options.BrowserVersion.Should().Be("1.0");
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void Compression_DefaultIsFalse()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.EnableCompression.Should().BeFalse();
    }

    [Fact]
    public void FluentApi_WithCompression_SetsValueAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithCompression();

        // Assert
        options.EnableCompression.Should().BeTrue();
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void FluentApi_WithWebSocketCapture_SetsFlag()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithWebSocketCapture();

        // Assert
        options.CaptureTypes.HasFlag(CaptureType.WebSocket).Should().BeTrue();
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void WithWebSocketCapture_PreservesExistingFlags()
    {
        // Arrange
        var options = new CaptureOptions()
            .WithCaptureTypes(CaptureType.AllText);

        // Act
        options.WithWebSocketCapture();

        // Assert
        options.CaptureTypes.HasFlag(CaptureType.AllText).Should().BeTrue();
        options.CaptureTypes.HasFlag(CaptureType.WebSocket).Should().BeTrue();
    }
}
