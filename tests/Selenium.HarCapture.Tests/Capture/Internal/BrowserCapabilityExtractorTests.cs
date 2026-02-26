using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using OpenQA.Selenium;
using Selenium.HarCapture.Capture.Internal;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Internal;

public sealed class BrowserCapabilityExtractorTests
{
    [Fact]
    public void Extract_WithValidCapabilities_ReturnsBrowserNameAndVersion()
    {
        // Arrange
        var capabilities = new MockCapabilities();
        capabilities.Set("browserName", "chrome");
        capabilities.Set("browserVersion", "142.0.6261.94");
        var driver = new MockCapabilitiesDriver { Capabilities = capabilities };

        // Act
        var (name, version) = BrowserCapabilityExtractor.Extract(driver);

        // Assert
        name.Should().Be("Chrome");
        version.Should().Be("142.0.6261.94");
    }

    [Fact]
    public void Extract_WithNonCapabilitiesDriver_ReturnsNulls()
    {
        // Arrange
        var driver = new NonCapabilitiesDriver();

        // Act
        var (name, version) = BrowserCapabilityExtractor.Extract(driver);

        // Assert
        name.Should().BeNull();
        version.Should().BeNull();
    }

    [Fact]
    public void Extract_WithNullBrowserName_ReturnsNulls()
    {
        // Arrange
        var capabilities = new MockCapabilities();
        capabilities.Set("browserName", null);
        capabilities.Set("browserVersion", "142.0");
        var driver = new MockCapabilitiesDriver { Capabilities = capabilities };

        // Act
        var (name, version) = BrowserCapabilityExtractor.Extract(driver);

        // Assert
        name.Should().BeNull();
        version.Should().BeNull();
    }

    [Fact]
    public void Extract_WithEmptyBrowserName_ReturnsNulls()
    {
        // Arrange
        var capabilities = new MockCapabilities();
        capabilities.Set("browserName", "");
        capabilities.Set("browserVersion", "142.0");
        var driver = new MockCapabilitiesDriver { Capabilities = capabilities };

        // Act
        var (name, version) = BrowserCapabilityExtractor.Extract(driver);

        // Assert
        name.Should().BeNull();
        version.Should().BeNull();
    }

    [Fact]
    public void Extract_WithMissingBrowserVersion_ReturnsNameOnly()
    {
        // Arrange
        var capabilities = new MockCapabilities();
        capabilities.Set("browserName", "firefox");
        var driver = new MockCapabilitiesDriver { Capabilities = capabilities };

        // Act
        var (name, version) = BrowserCapabilityExtractor.Extract(driver);

        // Assert
        name.Should().Be("Firefox");
        version.Should().BeNull();
    }

    [Fact]
    public void Extract_WithEmptyBrowserVersion_ReturnsNameWithNullVersion()
    {
        // Arrange
        var capabilities = new MockCapabilities();
        capabilities.Set("browserName", "chrome");
        capabilities.Set("browserVersion", "");
        var driver = new MockCapabilitiesDriver { Capabilities = capabilities };

        // Act
        var (name, version) = BrowserCapabilityExtractor.Extract(driver);

        // Assert
        name.Should().Be("Chrome");
        version.Should().BeNull();
    }

    [Theory]
    [InlineData("chrome", "Chrome")]
    [InlineData("firefox", "Firefox")]
    [InlineData("safari", "Safari")]
    [InlineData("MicrosoftEdge", "Microsoft Edge")]
    [InlineData("msedge", "Microsoft Edge")]
    [InlineData("internet explorer", "Internet Explorer")]
    [InlineData("opera", "Opera")]
    [InlineData("customBrowser", "CustomBrowser")]
    [InlineData("CHROME", "Chrome")]
    public void NormalizeBrowserName_NormalizesCorrectly(string input, string expected)
    {
        // Act
        var result = BrowserCapabilityExtractor.NormalizeBrowserName(input);

        // Assert
        result.Should().Be(expected);
    }

    private class MockCapabilitiesDriver : IWebDriver, IHasCapabilities
    {
        public ICapabilities Capabilities { get; set; } = null!;

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

    private class MockCapabilities : ICapabilities
    {
        private readonly Dictionary<string, object?> _caps = new();

        public void Set(string key, object? value) => _caps[key] = value;

        public object this[string capabilityName] => GetCapability(capabilityName)!;

        public object? GetCapability(string capabilityName)
        {
            return _caps.TryGetValue(capabilityName, out var value) ? value : null;
        }

        public bool HasCapability(string capabilityName)
        {
            return _caps.ContainsKey(capabilityName);
        }

        public IDictionary<string, object> ToDictionary()
        {
            throw new NotImplementedException();
        }
    }

    private class NonCapabilitiesDriver : IWebDriver
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
