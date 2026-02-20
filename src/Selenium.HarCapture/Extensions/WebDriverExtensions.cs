using System;
using System.Threading.Tasks;
using OpenQA.Selenium;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Extensions;

/// <summary>
/// Extension methods for <see cref="IWebDriver"/> that enable one-liner HAR capture usage.
/// </summary>
public static class WebDriverExtensions
{
    /// <summary>
    /// Starts HAR network capture on this WebDriver instance. Returns a HarCapture that manages the capture lifecycle. Dispose the returned HarCapture to stop capture and clean up resources.
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture network traffic from.</param>
    /// <param name="options">Configuration options for capture behavior. If null, default options are used.</param>
    /// <returns>A started HarCapture instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the driver does not support network capture.</exception>
    public static HarCapture StartHarCapture(this IWebDriver driver, CaptureOptions? options = null)
    {
        var capture = new HarCapture(driver, options);
        capture.Start();
        return capture;
    }

    /// <summary>
    /// Starts HAR capture with fluent configuration. Example: driver.StartHarCapture(o => o.WithCaptureTypes(CaptureType.All).WithMaxResponseBodySize(1_000_000))
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture network traffic from.</param>
    /// <param name="configure">Action to configure capture options using fluent API.</param>
    /// <returns>A started HarCapture instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when driver or configure is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the driver does not support network capture.</exception>
    public static HarCapture StartHarCapture(this IWebDriver driver, Action<CaptureOptions> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new CaptureOptions();
        configure(options);
        return driver.StartHarCapture(options);
    }

    /// <summary>
    /// One-liner: captures HAR for the duration of an async action. Automatically starts capture, executes action, stops capture, and returns HAR.
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture network traffic from.</param>
    /// <param name="action">The async action to execute while capturing network traffic.</param>
    /// <param name="options">Configuration options for capture behavior. If null, default options are used.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the captured HAR object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when driver or action is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the driver does not support network capture.</exception>
    public static async Task<Har> CaptureHarAsync(this IWebDriver driver, Func<Task> action, CaptureOptions? options = null)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        await using var capture = new HarCapture(driver, options);
        await capture.StartAsync().ConfigureAwait(false);
        await action().ConfigureAwait(false);
        return await capture.StopAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// One-liner: captures HAR for the duration of a sync action. Automatically starts capture, executes action, stops capture, and returns HAR.
    /// </summary>
    /// <param name="driver">The WebDriver instance to capture network traffic from.</param>
    /// <param name="action">The action to execute while capturing network traffic.</param>
    /// <param name="options">Configuration options for capture behavior. If null, default options are used.</param>
    /// <returns>The captured HAR object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when driver or action is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the driver does not support network capture.</exception>
    public static Har CaptureHar(this IWebDriver driver, Action action, CaptureOptions? options = null)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        using var capture = new HarCapture(driver, options);
        capture.Start();
        action();
        return capture.Stop();
    }
}
