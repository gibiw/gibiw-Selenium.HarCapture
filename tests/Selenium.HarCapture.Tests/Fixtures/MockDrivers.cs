using System;
using System.Collections.ObjectModel;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;

namespace Selenium.HarCapture.Tests.Fixtures;

/// <summary>
/// Minimal stub driver that does NOT implement IDevTools.
/// Used to test that unsupported browsers throw clear exceptions.
/// </summary>
internal class NonDevToolsDriver : IWebDriver
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
internal class FakeDevToolsDriver : IWebDriver, IDevTools
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
