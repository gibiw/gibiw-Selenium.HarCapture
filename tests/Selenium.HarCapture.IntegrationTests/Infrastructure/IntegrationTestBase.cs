using OpenQA.Selenium.Chrome;
using Selenium.HarCapture.Capture;

namespace Selenium.HarCapture.IntegrationTests.Infrastructure;

[Trait("Category", "Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly TestWebServer _server;

    protected ChromeDriver Driver { get; private set; } = null!;
    protected string ServerUrl => _server.BaseUrl;

    protected IntegrationTestBase(TestWebServer serverFixture)
    {
        _server = serverFixture;
    }

    public Task InitializeAsync()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--disable-extensions");

        Driver = new ChromeDriver(options);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Driver?.Quit();
        Driver?.Dispose();
        return Task.CompletedTask;
    }

    protected void WaitForNetworkIdle(int delayMs = 500)
    {
        Thread.Sleep(delayMs);
    }

    protected void NavigateTo(string path)
    {
        Driver.Navigate().GoToUrl($"{ServerUrl}{path}");
    }

    protected HarCapture StartCapture(CaptureOptions? options = null)
    {
        var capture = new HarCapture(Driver, options);
        capture.Start();
        return capture;
    }

    protected static CaptureOptions NetworkOptions()
    {
        return new CaptureOptions().ForceSeleniumNetwork();
    }

    protected bool IsCdpCompatible()
    {
        try
        {
            var capabilities = Driver.Capabilities;
            var version = capabilities.GetCapability("browserVersion")?.ToString();
            if (version is null) return false;

            var majorStr = version.Split('.')[0];
            if (int.TryParse(majorStr, out var major))
            {
                // Selenium 4.40.0 ships with DevTools V142-V144
                return major >= 142 && major <= 144;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
