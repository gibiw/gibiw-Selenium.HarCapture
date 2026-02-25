using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FluentAssertions;
using OpenQA.Selenium;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Strategies;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Strategies;

/// <summary>
/// Unit tests for SeleniumNetworkCaptureStrategy.
/// Note: Integration tests with real INetwork sessions will be added in a future phase.
/// These tests focus on input validation, property values, and basic behavior.
/// </summary>
public class SeleniumNetworkCaptureStrategyTests
{
    [Fact]
    public void Constructor_NullDriver_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new SeleniumNetworkCaptureStrategy(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("driver");
    }

    [Fact]
    public void StrategyName_ReturnsINetwork()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new SeleniumNetworkCaptureStrategy(driver);

        // Act
        var name = strategy.StrategyName;

        // Assert
        name.Should().Be("INetwork");
    }

    [Fact]
    public void SupportsDetailedTimings_ReturnsFalse()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new SeleniumNetworkCaptureStrategy(driver);

        // Act
        var supports = strategy.SupportsDetailedTimings;

        // Assert
        supports.Should().BeFalse();
    }

    [Fact]
    public void SupportsResponseBody_ReturnsTrue()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new SeleniumNetworkCaptureStrategy(driver);

        // Act
        var supports = strategy.SupportsResponseBody;

        // Assert
        supports.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new SeleniumNetworkCaptureStrategy(driver);

        // Act & Assert
        Func<Task> act = async () => await strategy.StartAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Dispose_WhenNotStarted_DoesNotThrow()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new SeleniumNetworkCaptureStrategy(driver);

        // Act & Assert
        Action act = () => strategy.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new SeleniumNetworkCaptureStrategy(driver);

        // Act & Assert
        strategy.Dispose();
        Action act = () => strategy.Dispose();

        act.Should().NotThrow("Dispose should be idempotent");
    }

    [Theory]
    [InlineData("application/json; charset=utf-8", "application/json")]
    [InlineData("application/json", "application/json")]
    [InlineData("text/plain", "text/plain")]
    [InlineData("multipart/form-data; boundary=----WebKitFormBoundary7MA4YWxkTrZu0gW", "multipart/form-data")]
    [InlineData("application/x-www-form-urlencoded", "application/x-www-form-urlencoded")]
    public void ExtractRequestMimeType_WithContentTypeHeader_ReturnsMediaType(string contentTypeValue, string expectedMimeType)
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "Content-Type", contentTypeValue }
        };

        // Act
        var result = SeleniumNetworkCaptureStrategy.ExtractMimeType(headers);

        // Assert
        result.Should().Be(expectedMimeType);
    }

    [Fact]
    public void ExtractRequestMimeType_WithoutContentTypeHeader_ReturnsDefault()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "Accept", "application/json" },
            { "User-Agent", "TestAgent" }
        };

        // Act
        var result = SeleniumNetworkCaptureStrategy.ExtractMimeType(headers);

        // Assert
        result.Should().Be("application/octet-stream");
    }

    [Fact]
    public void ExtractRequestMimeType_WithNullHeaders_ReturnsDefault()
    {
        // Act
        var result = SeleniumNetworkCaptureStrategy.ExtractMimeType(null);

        // Assert
        result.Should().Be("application/octet-stream");
    }

    [Fact]
    public void ExtractRequestMimeType_WithEmptyHeaders_ReturnsDefault()
    {
        // Arrange
        var headers = new Dictionary<string, string>();

        // Act
        var result = SeleniumNetworkCaptureStrategy.ExtractMimeType(headers);

        // Assert
        result.Should().Be("application/octet-stream");
    }

    [Fact]
    public void ExtractRequestMimeType_CaseInsensitive_ReturnsMediaType()
    {
        // Arrange
        var headers = new Dictionary<string, string>
        {
            { "content-type", "application/xml" }
        };

        // Act
        var result = SeleniumNetworkCaptureStrategy.ExtractMimeType(headers);

        // Assert
        result.Should().Be("application/xml");
    }

    /// <summary>
    /// Minimal stub driver that does NOT implement IDevTools.
    /// Used to test validation logic and basic properties.
    /// </summary>
    private class NonDevToolsDriver : IWebDriver
    {
        public string Url
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public string Title => throw new NotImplementedException();

        public string PageSource => throw new NotImplementedException();

        public string CurrentWindowHandle => throw new NotImplementedException();

        public ReadOnlyCollection<string> WindowHandles => throw new NotImplementedException();

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Quit()
        {
            throw new NotImplementedException();
        }

        public IOptions Manage()
        {
            throw new NotImplementedException();
        }

        public INavigation Navigate()
        {
            throw new NotImplementedException();
        }

        public ITargetLocator SwitchTo()
        {
            throw new NotImplementedException();
        }

        public IWebElement FindElement(By by)
        {
            throw new NotImplementedException();
        }

        public ReadOnlyCollection<IWebElement> FindElements(By by)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            // No-op
        }
    }
}
