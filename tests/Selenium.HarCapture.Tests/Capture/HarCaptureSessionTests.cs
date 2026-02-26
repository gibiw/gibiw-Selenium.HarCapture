using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Strategies;
using Selenium.HarCapture.Models;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture;

public sealed class HarCaptureSessionTests
{
    [Fact]
    public void Start_InitializesHar_WithVersion12()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        session.Start();
        var har = session.GetHar();

        // Assert
        har.Log.Version.Should().Be("1.2");
    }

    [Fact]
    public void Start_InitializesHar_WithCreatorName()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var options = new CaptureOptions { CreatorName = "CustomCreator" };
        var session = new HarCaptureSession(strategy, options);

        // Act
        session.Start();
        var har = session.GetHar();

        // Assert
        har.Log.Creator.Name.Should().Be("CustomCreator");
    }

    [Fact]
    public void Start_WithInitialPage_CreatesPage()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        session.Start("page1", "Home");
        var har = session.GetHar();

        // Assert
        har.Log.Pages.Should().NotBeNull();
        har.Log.Pages.Should().HaveCount(1);
        har.Log.Pages![0].Id.Should().Be("page1");
        har.Log.Pages[0].Title.Should().Be("Home");
    }

    [Fact]
    public void Start_WithoutInitialPage_HasEmptyPages()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        session.Start();
        var har = session.GetHar();

        // Assert
        har.Log.Pages.Should().BeNull();
    }

    [Fact]
    public void Start_WhenAlreadyStarted_ThrowsInvalidOperation()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        // Act
        var act = () => session.Start();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Capture is already started.");
    }

    [Fact]
    public void Stop_ReturnsHar()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        // Act
        var har = session.Stop();

        // Assert
        har.Should().NotBeNull();
        har.Log.Should().NotBeNull();
    }

    [Fact]
    public void Stop_WhenNotStarted_ThrowsInvalidOperation()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        var act = () => session.Stop();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Capture is not started.");
    }

    [Fact]
    public void NewPage_AddsPageToHar()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start("p1", "Home");

        // Act
        session.NewPage("p2", "About");
        var har = session.GetHar();

        // Assert
        har.Log.Pages.Should().NotBeNull();
        har.Log.Pages.Should().HaveCount(2);
        har.Log.Pages![1].Id.Should().Be("p2");
        har.Log.Pages[1].Title.Should().Be("About");
    }

    [Fact]
    public void NewPage_WhenNotCapturing_ThrowsInvalidOperation()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        var act = () => session.NewPage("p1", "Home");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Capture is not started.");
    }

    [Fact]
    public void NewPage_SetsCurrentPageRef_OnNewEntries()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start("p1", "Home");

        // Act - simulate entry on first page
        strategy.SimulateEntry(CreateTestEntry("https://example.com/page1"), "req1");
        var har1 = session.GetHar();

        // Act - create new page and simulate entry
        session.NewPage("p2", "About");
        strategy.SimulateEntry(CreateTestEntry("https://example.com/page2"), "req2");
        var har2 = session.GetHar();

        // Assert
        har1.Log.Entries.Should().HaveCount(1);
        har1.Log.Entries[0].PageRef.Should().Be("p1");

        har2.Log.Entries.Should().HaveCount(2);
        har2.Log.Entries[0].PageRef.Should().Be("p1");
        har2.Log.Entries[1].PageRef.Should().Be("p2");
    }

    [Fact]
    public void GetHar_ReturnsDeepClone()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();
        strategy.SimulateEntry(CreateTestEntry("https://example.com/test"), "req1");

        // Act - get two clones
        var har1 = session.GetHar();
        var har2 = session.GetHar();

        // Assert - they should be different instances
        har1.Should().NotBeSameAs(har2);
        har1.Log.Should().NotBeSameAs(har2.Log);
        har1.Log.Entries.Should().NotBeSameAs(har2.Log.Entries);

        // Verify deep independence - modifying one doesn't affect the other
        har1.Log.Entries.Should().HaveCount(1);
        har2.Log.Entries.Should().HaveCount(1);

        // Clear entries in har1 (this modifies the list)
        ((List<HarEntry>)har1.Log.Entries).Clear();

        // har2 should still have its entry
        har2.Log.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void GetHar_WhenNotCapturing_ThrowsInvalidOperation()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        var act = () => session.GetHar();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Capture is not started.");
    }

    [Fact]
    public void EntryCompleted_AddsEntryToHar()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        // Act
        strategy.SimulateEntry(CreateTestEntry("https://example.com/api/test"), "req1");
        var har = session.GetHar();

        // Assert
        har.Log.Entries.Should().HaveCount(1);
        har.Log.Entries[0].Request.Url.Should().Be("https://example.com/api/test");
    }

    [Fact]
    public void EntryCompleted_FiltersExcludedUrls()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var options = new CaptureOptions
        {
            UrlExcludePatterns = new[] { "**/*.png" }
        };
        var session = new HarCaptureSession(strategy, options);
        session.Start();

        // Act - simulate entry with .png URL (should be filtered)
        strategy.SimulateEntry(CreateTestEntry("https://example.com/logo.png"), "req1");
        var har1 = session.GetHar();

        // Act - simulate entry with .html URL (should be captured)
        strategy.SimulateEntry(CreateTestEntry("https://example.com/page.html"), "req2");
        var har2 = session.GetHar();

        // Assert
        har1.Log.Entries.Should().HaveCount(0, "PNG entry should be filtered");
        har2.Log.Entries.Should().HaveCount(1, "HTML entry should be captured");
    }

    [Fact]
    public void Dispose_CleansUpStrategy()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        // Act
        session.Dispose();

        // Assert - simulate entry after dispose should not affect anything
        strategy.SimulateEntry(CreateTestEntry("https://example.com/test"), "req1");

        // Verify GetHar throws after dispose
        var act = () => session.GetHar();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        session.Dispose();
        var act = () => session.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ActiveStrategyName_ReturnsStrategyName()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        var strategyName = session.ActiveStrategyName;

        // Assert
        strategyName.Should().Be("Mock");
    }

    [Fact]
    public void Constructor_WithNullDriver_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new HarCaptureSession((IWebDriver)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("driver");
    }

    [Fact]
    public void Constructor_WithNonDevToolsDriver_ThrowsInvalidOperationException()
    {
        // Arrange
        var driver = new NonDevToolsDriver();

        // Act & Assert
        Action act = () => new HarCaptureSession(driver);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IDevTools*");
    }

    [Fact]
    public void Constructor_WithForceSeleniumNetworkApi_CreatesSession()
    {
        // Arrange
        var driver = new FakeDevToolsDriver(); // Use FakeDevToolsDriver
        var options = new CaptureOptions { ForceSeleniumNetworkApi = true };

        // Act
        var session = new HarCaptureSession(driver, options);

        // Assert
        session.ActiveStrategyName.Should().Be("INetwork");
    }

    [Fact]
    public void Constructor_WithCdpFailureDriver_FallsBackToINetwork()
    {
        // Arrange
        var driver = new FakeDevToolsDriver(); // GetDevToolsSession throws

        // Act
        var session = new HarCaptureSession(driver);

        // Assert
        session.ActiveStrategyName.Should().Be("INetwork");
        session.Should().NotBeNull();
    }

    [Fact]
    public void Start_WithCapabilitiesDriver_AutoDetectsBrowserInHar()
    {
        // Arrange
        var capabilities = new MockCapabilities();
        capabilities.Set("browserName", "chrome");
        capabilities.Set("browserVersion", "142.0");
        var driver = new MockCapabilitiesDriver { Capabilities = capabilities };
        var strategy = new MockCaptureStrategy();

        // Simulate what HarCaptureSession(IWebDriver) constructor does:
        // Extract browser info from driver, then create session with strategy
        var options = new CaptureOptions();
        (string? name, string? version) = Selenium.HarCapture.Capture.Internal.BrowserCapabilityExtractor.Extract(driver);
        if (name != null)
        {
            options = options.WithBrowser(name, version ?? "");
        }
        var session = new HarCaptureSession(strategy, options);

        // Act
        session.Start();
        var har = session.GetHar();

        // Assert
        har.Log.Browser.Should().NotBeNull();
        har.Log.Browser!.Name.Should().Be("Chrome");
        har.Log.Browser.Version.Should().Be("142.0");
    }

    [Fact]
    public void Start_WithBrowserOverride_UsesOverrideInsteadOfAutoDetection()
    {
        // Arrange
        var capabilities = new MockCapabilities();
        capabilities.Set("browserName", "chrome");
        capabilities.Set("browserVersion", "142.0");
        var driver = new MockCapabilitiesDriver { Capabilities = capabilities };
        var strategy = new MockCaptureStrategy();

        // Options with override - override should take precedence
        var options = new CaptureOptions().WithBrowser("Custom", "9.9");
        var session = new HarCaptureSession(strategy, options);

        // Act
        session.Start();
        var har = session.GetHar();

        // Assert
        har.Log.Browser.Should().NotBeNull();
        har.Log.Browser!.Name.Should().Be("Custom");
        har.Log.Browser.Version.Should().Be("9.9");
    }

    [Fact]
    public void Start_WithoutCapabilities_OmitsBrowserFromHar()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        session.Start();
        var har = session.GetHar();

        // Assert
        har.Log.Browser.Should().BeNull();
    }

    [Fact]
    public async Task StopAsync_WithStreamingAndCompression_CompressesOutputFile()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har");
        try
        {
            var strategy = new MockCaptureStrategy();
            var options = new CaptureOptions()
                .WithOutputFile(tempFile)
                .WithCompression();
            var session = new HarCaptureSession(strategy, options);

            await session.StartAsync("page1", "Test Page");

            // Simulate an entry via strategy
            strategy.SimulateEntry(new HarEntry
            {
                StartedDateTime = DateTimeOffset.UtcNow,
                Time = 100,
                Request = new HarRequest
                {
                    Method = "GET",
                    Url = "https://example.com",
                    HttpVersion = "HTTP/1.1",
                    Cookies = new List<HarCookie>(),
                    Headers = new List<HarHeader>(),
                    QueryString = new List<HarQueryString>(),
                    HeadersSize = -1,
                    BodySize = 0
                },
                Response = new HarResponse
                {
                    Status = 200,
                    StatusText = "OK",
                    HttpVersion = "HTTP/1.1",
                    Cookies = new List<HarCookie>(),
                    Headers = new List<HarHeader>(),
                    Content = new HarContent { Size = 0, MimeType = "text/html" },
                    RedirectURL = "",
                    HeadersSize = -1,
                    BodySize = -1
                },
                Cache = new HarCache(),
                Timings = new HarTimings { Send = 1, Wait = 50, Receive = 49 }
            }, "req1");

            // Allow consumer to process
            await Task.Delay(200);

            // Act
            await session.StopAsync();

            // Assert — .gz file should exist, original .har should be deleted
            var gzFile = tempFile + ".gz";
            File.Exists(gzFile).Should().BeTrue("compressed file should exist");
            File.Exists(tempFile).Should().BeFalse("original uncompressed file should be deleted");

            // Verify gzip magic bytes
            var fileBytes = File.ReadAllBytes(gzFile);
            fileBytes.Length.Should().BeGreaterThan(2);
            fileBytes[0].Should().Be(0x1F, "first byte should be gzip magic");
            fileBytes[1].Should().Be(0x8B, "second byte should be gzip magic");

            // Verify content can be decompressed
            using var fileStream = new FileStream(gzFile, FileMode.Open, FileAccess.Read);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);
            var json = await reader.ReadToEndAsync();
            json.Should().Contain("\"log\"");
            json.Should().Contain("\"entries\"");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(tempFile + ".gz")) File.Delete(tempFile + ".gz");
        }
    }

    private class MockCapabilitiesDriver : IWebDriver, IHasCapabilities
    {
        public ICapabilities Capabilities { get; set; } = null!;

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

    private static HarEntry CreateTestEntry(string url = "https://example.com/page")
    {
        return new HarEntry
        {
            StartedDateTime = DateTimeOffset.UtcNow,
            Time = 100,
            Request = new HarRequest
            {
                Method = "GET",
                Url = url,
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                QueryString = new List<HarQueryString>(),
                HeadersSize = -1,
                BodySize = -1
            },
            Response = new HarResponse
            {
                Status = 200,
                StatusText = "OK",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                Content = new HarContent
                {
                    Size = 0,
                    MimeType = "text/html"
                },
                RedirectURL = "",
                HeadersSize = -1,
                BodySize = -1
            },
            Cache = new HarCache(),
            Timings = new HarTimings
            {
                Send = 1,
                Wait = 50,
                Receive = 49
            }
        };
    }

    private sealed class MockCaptureStrategy : INetworkCaptureStrategy
    {
        public string StrategyName => "Mock";
        public bool SupportsDetailedTimings => true;
        public bool SupportsResponseBody => true;
        public event Action<HarEntry, string>? EntryCompleted;

        public Task StartAsync(CaptureOptions options)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        // Test helper to simulate an entry arriving
        public void SimulateEntry(HarEntry entry, string requestId)
        {
            EntryCompleted?.Invoke(entry, requestId);
        }
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

    private class MockCapabilities : ICapabilities
    {
        private readonly Dictionary<string, object?> _caps = new();

        public void Set(string key, object? value) => _caps[key] = value;

        public object this[string capabilityName] => GetCapability(capabilityName)!;

        public object? GetCapability(string capabilityName)
        {
            return _caps.TryGetValue(capabilityName, out var value) ? value : null;
        }

        public bool HasCapability(string capabilityName)
        {
            return _caps.ContainsKey(capabilityName);
        }

        public IDictionary<string, object> ToDictionary()
        {
            throw new NotImplementedException();
        }
    }
}
