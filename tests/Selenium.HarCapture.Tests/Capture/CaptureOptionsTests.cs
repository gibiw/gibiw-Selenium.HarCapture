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
        options.ResponseBodyScope.Should().Be(ResponseBodyScope.All);
        options.ResponseBodyMimeFilter.Should().BeNull();
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

    [Fact]
    public void ResponseBodyScope_DefaultIsAll()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.ResponseBodyScope.Should().Be(ResponseBodyScope.All);
    }

    [Fact]
    public void FluentApi_WithResponseBodyScope_SetsValueAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithResponseBodyScope(ResponseBodyScope.PagesAndApi);

        // Assert
        options.ResponseBodyScope.Should().Be(ResponseBodyScope.PagesAndApi);
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void FluentApi_WithResponseBodyMimeFilter_SetsFilterAndReturnsThis()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithResponseBodyMimeFilter("image/png", "image/svg+xml");

        // Assert
        options.ResponseBodyMimeFilter.Should().Equal("image/png", "image/svg+xml");
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void FluentApi_ScopeAndFilter_Composable()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithResponseBodyScope(ResponseBodyScope.PagesAndApi)
            .WithResponseBodyMimeFilter("image/png", "image/svg+xml");

        // Assert
        options.ResponseBodyScope.Should().Be(ResponseBodyScope.PagesAndApi);
        options.ResponseBodyMimeFilter.Should().Equal("image/png", "image/svg+xml");
    }

    // ── MaxOutputFileSize ──────────────────────────────────────────────────────

    [Fact]
    public void CaptureOptions_WithMaxOutputFileSize_SetsProperty()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithMaxOutputFileSize(1024);

        // Assert
        options.MaxOutputFileSize.Should().Be(1024);
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void MaxOutputFileSize_DefaultIsZero()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.MaxOutputFileSize.Should().Be(0);
    }

    // ── CustomMetadata ─────────────────────────────────────────────────────────

    [Fact]
    public void CaptureOptions_WithCustomMetadata_AddsToDict()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        var result = options.WithCustomMetadata("env", "prod");

        // Assert
        options.CustomMetadata.Should().NotBeNull();
        options.CustomMetadata.Should().ContainKey("env");
        options.CustomMetadata!["env"].Should().Be("prod");
        result.Should().BeSameAs(options);
    }

    [Fact]
    public void CaptureOptions_WithCustomMetadata_Chainable()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act
        options
            .WithCustomMetadata("env", "staging")
            .WithCustomMetadata("txId", "abc-123")
            .WithCustomMetadata("retries", 3);

        // Assert
        options.CustomMetadata.Should().HaveCount(3);
        options.CustomMetadata!["env"].Should().Be("staging");
        options.CustomMetadata["txId"].Should().Be("abc-123");
        options.CustomMetadata["retries"].Should().Be(3);
    }

    [Fact]
    public void CustomMetadata_DefaultIsNull()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.CustomMetadata.Should().BeNull();
    }
}
