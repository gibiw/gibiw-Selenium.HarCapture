using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Extensions;
using Selenium.HarCapture.Tests.Fixtures;
using Xunit;

namespace Selenium.HarCapture.Tests.Extensions;

public sealed class WebDriverExtensionsTests
{
    [Fact]
    public void StartHarCapture_WithNullDriver_ThrowsArgumentNullException()
    {
        // Arrange
        IWebDriver driver = null!;

        // Act
        Action act = () => driver.StartHarCapture();

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("driver");
    }

    [Fact]
    public void StartHarCapture_WithNonDevToolsDriver_ThrowsInvalidOperationException()
    {
        // Arrange
        var driver = new NonDevToolsDriver();

        // Act
        Action act = () => driver.StartHarCapture();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IDevTools*");
    }

    [Fact]
    public void StartHarCapture_ConfigureOverload_AppliesConfiguration()
    {
        // Arrange - just test that the configuration callback is invoked
        var driver = new FakeDevToolsDriver();
        var configureWasCalled = false;
        CaptureOptions? capturedOptions = null;

        // Act - this will fail on Start() because of the fake driver,
        // but we can verify configure was called by catching the exception
        Action act = () => driver.StartHarCapture(options =>
        {
            configureWasCalled = true;
            capturedOptions = options;
            options.ForceSeleniumNetworkApi = true;
            options.CreatorName = "TestCreator";
            options.WithMaxResponseBodySize(500);
        });

        // Assert - configure was called and options were set
        act.Should().Throw<NotImplementedException>(); // Start fails on mock
        configureWasCalled.Should().BeTrue();
        capturedOptions.Should().NotBeNull();
        capturedOptions!.ForceSeleniumNetworkApi.Should().BeTrue();
        capturedOptions.CreatorName.Should().Be("TestCreator");
        capturedOptions.MaxResponseBodySize.Should().Be(500);
    }

    [Fact]
    public void StartHarCapture_ConfigureOverload_WithNullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var driver = new FakeDevToolsDriver();

        // Act
        Action act = () => driver.StartHarCapture((Action<CaptureOptions>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }

    [Fact]
    public void StartHarCapture_ConfigureOverload_ChainsFluentMethods()
    {
        // Arrange
        var driver = new FakeDevToolsDriver();
        CaptureOptions? capturedOptions = null;

        // Act - test that fluent API compiles and chains within extension method context
        Action act = () => driver.StartHarCapture(o =>
        {
            capturedOptions = o;
            o.WithCaptureTypes(CaptureType.All)
             .WithMaxResponseBodySize(500)
             .WithCreatorName("FluentTest");
        });

        // Assert - configure was executed and fluent chain worked
        act.Should().Throw<NotImplementedException>(); // Start fails on mock
        capturedOptions.Should().NotBeNull();
        capturedOptions!.CaptureTypes.Should().Be(CaptureType.All);
        capturedOptions.MaxResponseBodySize.Should().Be(500);
        capturedOptions.CreatorName.Should().Be("FluentTest");
    }

    [Fact]
    public void CaptureHar_WithNonDevToolsDriver_ThrowsInvalidOperationException()
    {
        // Arrange
        var driver = new NonDevToolsDriver();

        // Act
        Action act = () => driver.CaptureHar(() => { /* no-op */ });

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IDevTools*");
    }

    [Fact]
    public void CaptureHar_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var driver = new FakeDevToolsDriver();

        // Act
        Action act = () => driver.CaptureHar((Action)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("action");
    }

    [Fact]
    public async Task CaptureHarAsync_WithNonDevToolsDriver_ThrowsInvalidOperationException()
    {
        // Arrange
        var driver = new NonDevToolsDriver();

        // Act
        Func<Task> act = async () => await driver.CaptureHarAsync(async () => await Task.CompletedTask);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IDevTools*");
    }

    [Fact]
    public async Task CaptureHarAsync_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var driver = new FakeDevToolsDriver();

        // Act
        Func<Task> act = async () => await driver.CaptureHarAsync((Func<Task>)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .Where(e => e.ParamName == "action");
    }

}
