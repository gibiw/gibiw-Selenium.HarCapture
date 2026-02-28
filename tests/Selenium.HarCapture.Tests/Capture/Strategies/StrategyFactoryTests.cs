using System;
using System.Collections.ObjectModel;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Strategies;
using Selenium.HarCapture.Tests.Fixtures;
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

}
