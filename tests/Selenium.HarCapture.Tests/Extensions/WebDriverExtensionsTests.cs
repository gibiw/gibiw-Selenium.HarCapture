using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Extensions;
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
