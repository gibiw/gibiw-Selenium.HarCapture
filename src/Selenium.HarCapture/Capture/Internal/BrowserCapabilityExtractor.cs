using OpenQA.Selenium;

namespace Selenium.HarCapture.Capture.Internal;

internal static class BrowserCapabilityExtractor
{
    internal static (string? Name, string? Version) Extract(IWebDriver driver)
    {
        if (driver is not IHasCapabilities capDriver)
            return (null, null);

        var capabilities = capDriver.Capabilities;
        var rawName = capabilities.GetCapability("browserName")?.ToString();
        var rawVersion = capabilities.GetCapability("browserVersion")?.ToString();

        if (string.IsNullOrEmpty(rawName))
            return (null, null);

        var name = NormalizeBrowserName(rawName!);
        var version = string.IsNullOrEmpty(rawVersion) ? null : rawVersion;

        return (name, version);
    }

    internal static string NormalizeBrowserName(string rawName)
    {
        return rawName.ToLowerInvariant() switch
        {
            "chrome" => "Chrome",
            "firefox" => "Firefox",
            "safari" => "Safari",
            "microsoftedge" => "Microsoft Edge",
            "msedge" => "Microsoft Edge",
            "internet explorer" => "Internet Explorer",
            "opera" => "Opera",
            _ => char.ToUpperInvariant(rawName[0]) + rawName.Substring(1)
        };
    }
}
