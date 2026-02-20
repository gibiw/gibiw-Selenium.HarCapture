using System;
using System.Collections.ObjectModel;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Strategies;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Strategies;

/// <summary>
/// Unit tests for StrategyFactory.
/// Tests strategy selection logic based on driver capabilities and configuration options.
/// </summary>
public class StrategyFactoryTests
{
    [Fact]
    public void Create_NullDriver_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new CaptureOptions();

        // Act & Assert
        Action act = () => StrategyFactory.Create(null!, options);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("driver");
    }

    [Fact]
    public void Create_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var driver = new NonDevToolsDriver();

        // Act & Assert
        Action act = () => StrategyFactory.Create(driver, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Create_ForceSeleniumNetworkApi_ReturnsSeleniumNetworkStrategy()
    {
        // Arrange
        var driver = new FakeDevToolsDriver(); // Use FakeDevToolsDriver to simulate real scenario
        var options = new CaptureOptions { ForceSeleniumNetworkApi = true };

        // Act
        var result = StrategyFactory.Create(driver, options);

        // Assert
        result.Should().BeOfType<SeleniumNetworkCaptureStrategy>();
        result.StrategyName.Should().Be("INetwork");
    }

    [Fact]
    public void Create_NonDevToolsDriver_ThrowsInvalidOperationException()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var options = new CaptureOptions(); // ForceSeleniumNetworkApi = false (default)

        // Act & Assert
        Action act = () => StrategyFactory.Create(driver, options);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IDevTools*");
    }

    [Fact]
    public void Create_DevToolsDriverWithCdpFailure_FallsBackToINetwork()
    {
        // Arrange
        var driver = new FakeDevToolsDriver(); // GetDevToolsSession throws WebDriverException
        var options = new CaptureOptions(); // ForceSeleniumNetworkApi = false (default)

        // Act
        var result = StrategyFactory.Create(driver, options);

        // Assert
        result.Should().BeOfType<SeleniumNetworkCaptureStrategy>()
            .Which.StrategyName.Should().Be("INetwork");
    }

    /// <summary>
    /// Minimal stub driver that does NOT implement IDevTools.
    /// Used to test that unsupported browsers throw clear exceptions.
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

    /// <summary>
    /// Fake driver that implements IDevTools but GetDevToolsSession always throws.
    /// Used to test runtime CDP failure fallback logic.
    /// </summary>
    private class FakeDevToolsDriver : IWebDriver, IDevTools
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

        public bool HasActiveDevToolsSession => false;

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

        public DevToolsSession GetDevToolsSession()
        {
            throw new WebDriverException("CDP not available");
        }

        public DevToolsSession GetDevToolsSession(int devToolsProtocolVersion)
        {
            throw new WebDriverException("CDP not available");
        }

        public DevToolsSession GetDevToolsSession(DevToolsOptions options)
        {
            throw new WebDriverException("CDP not available");
        }

        public void CloseDevToolsSession()
        {
            // No-op
        }
    }
}
