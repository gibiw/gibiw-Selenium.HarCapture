using System;
using OpenQA.Selenium.DevTools;

namespace Selenium.HarCapture.Capture.Internal.Cdp;

/// <summary>
/// Creates the appropriate CDP Network adapter based on the browser's DevTools protocol version.
/// Tries newest version first (V144), then falls back to V143, then V142.
/// </summary>
internal static class CdpAdapterFactory
{
    /// <summary>
    /// Creates an <see cref="ICdpNetworkAdapter"/> for the given DevTools session.
    /// Auto-detects the matching CDP version by trying V144 → V143 → V142.
    /// </summary>
    /// <param name="session">An active DevTools session.</param>
    /// <returns>A version-specific adapter that implements <see cref="ICdpNetworkAdapter"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when none of the supported CDP versions (V142-V144) match the browser.
    /// </exception>
    internal static ICdpNetworkAdapter Create(DevToolsSession session)
    {
        // Try newest first: V144 → V143 → V142
        try
        {
            return new CdpNetworkAdapterV144(session);
        }
        catch (InvalidOperationException)
        {
            // V144 not supported, try next
        }

        try
        {
            return new CdpNetworkAdapterV143(session);
        }
        catch (InvalidOperationException)
        {
            // V143 not supported, try next
        }

        try
        {
            return new CdpNetworkAdapterV142(session);
        }
        catch (InvalidOperationException)
        {
            // V142 not supported either
        }

        throw new InvalidOperationException(
            "None of the supported CDP versions (V142, V143, V144) match the browser's DevTools protocol. " +
            "Ensure your Chrome/Edge version is compatible with Selenium 4.40.0 (Chrome 142-144). " +
            "Use CaptureOptions.ForceSeleniumNetworkApi = true as a workaround.");
    }
}
