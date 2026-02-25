using System;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using Selenium.HarCapture.Capture.Internal;
using Selenium.HarCapture.Capture.Internal.Cdp;

namespace Selenium.HarCapture.Capture.Strategies;

/// <summary>
/// Factory for creating network capture strategies based on driver capabilities.
/// Automatically detects CDP support and falls back to INetwork when needed.
/// </summary>
internal static class StrategyFactory
{
    /// <summary>
    /// Creates the most appropriate network capture strategy for the given driver.
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture traffic from.</param>
    /// <param name="options">Configuration options controlling capture behavior.</param>
    /// <returns>A configured INetworkCaptureStrategy instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when driver or options is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the driver does not support network capture (no IDevTools).</exception>
    /// <remarks>
    /// Selection logic:
    /// 1. If ForceSeleniumNetworkApi is true, always use INetwork strategy
    /// 2. If driver implements IDevTools, attempt CDP strategy with runtime fallback to INetwork
    /// 3. If driver does not implement IDevTools, throw InvalidOperationException
    /// </remarks>
    internal static INetworkCaptureStrategy Create(IWebDriver driver, CaptureOptions options, FileLogger? logger = null)
    {
        if (driver == null)
        {
            throw new ArgumentNullException(nameof(driver));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // User forced INetwork strategy
        if (options.ForceSeleniumNetworkApi)
        {
            logger?.Log("HarCapture", "ForceSeleniumNetworkApi=true, using INetwork strategy");
            return new SeleniumNetworkCaptureStrategy(driver, logger);
        }

        // Capability detection - try CDP if driver supports IDevTools
        if (driver is IDevTools devToolsDriver)
        {
            try
            {
                logger?.Log("HarCapture", "IDevTools detected, attempting CDP strategy");
                // Validate CDP session and version-specific domains (test then discard)
                var session = devToolsDriver.GetDevToolsSession();
                try
                {
                    var adapter = CdpAdapterFactory.Create(session);
                    adapter.Dispose();
                }
                finally
                {
                    session.Dispose();
                }
                return new CdpNetworkCaptureStrategy(driver, logger);
            }
            catch (Exception ex)
            {
                logger?.Log("HarCapture", $"CDP session creation failed: {ex.Message}, falling back to INetwork strategy");
                return new SeleniumNetworkCaptureStrategy(driver, logger);
            }
        }

        // No IDevTools support - neither CDP nor INetwork will work
        // (INetwork is currently CDP-backed and requires IDevTools)
        throw new InvalidOperationException(
            "Network capture requires a browser that supports DevTools (Chrome, Edge). " +
            "The provided driver does not implement IDevTools. " +
            "Firefox and Safari support is planned for a future version via WebDriver BiDi.");
    }
}
