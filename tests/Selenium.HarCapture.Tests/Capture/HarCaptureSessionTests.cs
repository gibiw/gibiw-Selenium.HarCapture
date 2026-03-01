using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Strategies;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Serialization;
using Selenium.HarCapture.Tests.Fixtures;
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
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/page1"), "req1");
        var har1 = session.GetHar();

        // Act - create new page and simulate entry
        session.NewPage("p2", "About");
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/page2"), "req2");
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
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/test"), "req1");

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
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/api/test"), "req1");
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
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/logo.png"), "req1");
        var har1 = session.GetHar();

        // Act - simulate entry with .html URL (should be captured)
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/page.html"), "req2");
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
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/test"), "req1");

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

    [Fact]
    public async Task StartAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        // Act
        await session.StartAsync(null, null, CancellationToken.None);

        // Assert
        session.IsCapturing.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        await session.StartAsync();

        // Act
        var har = await session.StopAsync(CancellationToken.None);

        // Assert
        har.Should().NotBeNull();
        har.Log.Should().NotBeNull();
        session.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WithCancelledToken_ThrowsOperationCancelled()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        await session.StartAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await session.StopAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StopAsync_WithPageTimings_WritesOnContentLoadAndOnLoad()
    {
        // Arrange
        var strategy = new MockCaptureStrategy
        {
            LastDomContentLoadedTimestamp = 1234.5,
            LastLoadTimestamp = 2345.6
        };
        var session = new HarCaptureSession(strategy);
        await session.StartAsync("page_1", "Test Page");

        // Act
        var har = await session.StopAsync();

        // Assert
        har.Log.Pages.Should().NotBeNull();
        har.Log.Pages.Should().HaveCount(1);
        har.Log.Pages![0].PageTimings.OnContentLoad.Should().Be(1234.5);
        har.Log.Pages[0].PageTimings.OnLoad.Should().Be(2345.6);
    }

    [Fact]
    public async Task StopAsync_WithNullTimings_LeavesPageTimingsUnchanged()
    {
        // Arrange — MockCaptureStrategy has null timings by default (INetwork simulation)
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        await session.StartAsync("page_1", "Test Page");

        // Act
        var har = await session.StopAsync();

        // Assert — no exception, and timings remain null
        har.Log.Pages.Should().NotBeNull();
        har.Log.Pages.Should().HaveCount(1);
        har.Log.Pages![0].PageTimings.OnContentLoad.Should().BeNull();
        har.Log.Pages[0].PageTimings.OnLoad.Should().BeNull();
    }

    [Fact]
    public async Task StopAsync_WithNoPages_DoesNotCrash()
    {
        // Arrange — start without providing a page ref so HAR has no pages
        var strategy = new MockCaptureStrategy
        {
            LastDomContentLoadedTimestamp = 1000.0,
            LastLoadTimestamp = 2000.0
        };
        var session = new HarCaptureSession(strategy);
        await session.StartAsync(); // no page ref

        // Act — must not throw even though there are no pages
        var har = await session.StopAsync();

        // Assert
        (har.Log.Pages == null || har.Log.Pages.Count == 0).Should().BeTrue("no pages should exist");
    }

    [Fact]
    public async Task StopAsync_WithPartialTimings_WritesOnlyAvailableValues()
    {
        // Arrange — only DOMContentLoaded is set, Load is null
        var strategy = new MockCaptureStrategy
        {
            LastDomContentLoadedTimestamp = 500.0,
            LastLoadTimestamp = null
        };
        var session = new HarCaptureSession(strategy);
        await session.StartAsync("page_1", "Test Page");

        // Act
        var har = await session.StopAsync();

        // Assert
        har.Log.Pages.Should().NotBeNull();
        har.Log.Pages.Should().HaveCount(1);
        har.Log.Pages![0].PageTimings.OnContentLoad.Should().Be(500.0);
        har.Log.Pages[0].PageTimings.OnLoad.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Pause / Resume / IsPaused / EntryWritten tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Pause_WhenCapturing_DropsNewEntries()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/before"), "req1");

        // Act
        session.Pause();
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/during-pause"), "req2");
        var har = session.GetHar();

        // Assert
        har.Log.Entries.Should().HaveCount(1, "entry during pause should be dropped");
        har.Log.Entries[0].Request.Url.Should().Be("https://example.com/before");
    }

    [Fact]
    public void Resume_AfterPause_AcceptsNewEntries()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        // Act
        session.Pause();
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/dropped"), "req1");
        session.Resume();
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/after-resume"), "req2");
        var har = session.GetHar();

        // Assert
        har.Log.Entries.Should().HaveCount(1, "entry after resume should be accepted");
        har.Log.Entries[0].Request.Url.Should().Be("https://example.com/after-resume");
    }

    [Fact]
    public void Pause_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        // Act & Assert
        Action act = () =>
        {
            session.Pause();
            session.Pause();
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void Resume_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();
        session.Pause();

        // Act & Assert
        Action act = () =>
        {
            session.Resume();
            session.Resume();
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void Resume_WhenNotPaused_DoesNotThrow()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        // Act & Assert — Resume without prior Pause
        Action act = () => session.Resume();
        act.Should().NotThrow();
    }

    [Fact]
    public void IsPaused_ReflectsState()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        // Assert initial state
        session.IsPaused.Should().BeFalse("capture starts unpaused");

        // Act & Assert after Pause
        session.Pause();
        session.IsPaused.Should().BeTrue("IsPaused should be true after Pause()");

        // Act & Assert after Resume
        session.Resume();
        session.IsPaused.Should().BeFalse("IsPaused should be false after Resume()");
    }

    [Fact]
    public void EntryWritten_FiresAfterWrite()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start("page1", "Home");

        HarCaptureProgress? receivedProgress = null;
        session.EntryWritten += (_, p) => receivedProgress = p;

        // Act
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/api"), "req1");

        // Assert
        receivedProgress.Should().NotBeNull("EntryWritten should fire after entry write");
        receivedProgress!.EntryCount.Should().Be(1);
        receivedProgress.EntryUrl.Should().Be("https://example.com/api");
        receivedProgress.CurrentPageRef.Should().Be("page1");
    }

    [Fact]
    public void EntryWritten_NotFired_WhenPaused()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        int fireCount = 0;
        session.EntryWritten += (_, _) => fireCount++;

        // Act
        session.Pause();
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/dropped"), "req1");

        // Assert
        fireCount.Should().Be(0, "EntryWritten must not fire when paused");
    }

    [Fact]
    public void EntryWritten_CountIncrementsAcrossEntries()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        var progressList = new System.Collections.Generic.List<HarCaptureProgress>();
        session.EntryWritten += (_, p) => progressList.Add(p);

        // Act
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/1"), "req1");
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/2"), "req2");
        strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/3"), "req3");

        // Assert
        progressList.Should().HaveCount(3);
        progressList[0].EntryCount.Should().Be(1);
        progressList[1].EntryCount.Should().Be(2);
        progressList[2].EntryCount.Should().Be(3);
    }

    [Fact]
    public void EntryWritten_FiredOutsideLock_NoDeadlock()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);
        session.Start();

        bool deadlockDetected = false;

        // If EntryWritten fires inside the lock, calling GetHar() from the handler
        // would deadlock (GetHar also takes the same lock).
        session.EntryWritten += (_, _) =>
        {
            // This call will deadlock if the event is fired inside the lock
            var completed = Task.Run(() => session.GetHar()).Wait(TimeSpan.FromSeconds(5));
            if (!completed)
                deadlockDetected = true;
        };

        // Act — simulate entry, event fires, handler calls GetHar()
        var task = Task.Run(() =>
            strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/deadlock-test"), "req1"));
        var finished = task.Wait(TimeSpan.FromSeconds(10));

        // Assert
        finished.Should().BeTrue("SimulateEntry should complete without deadlock");
        deadlockDetected.Should().BeFalse("GetHar() in handler should not deadlock");
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

    // ========== CustomMetadata and MaxOutputFileSize Tests (Phase 20-02) ==========

    [Fact]
    public void HarCaptureSession_CustomMetadata_Null_NoCustomField()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var options = new CaptureOptions(); // no CustomMetadata set
        var session = new HarCaptureSession(strategy, options);

        // Act
        session.Start();
        var har = session.GetHar();

        // Assert
        har.Log.Custom.Should().BeNull("no CustomMetadata was configured");
    }

    [Fact]
    public void HarCaptureSession_CustomMetadata_PopulatesHarLog()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var options = new CaptureOptions()
            .WithCustomMetadata("env", "prod")
            .WithCustomMetadata("txId", "abc-123");
        var session = new HarCaptureSession(strategy, options);

        // Act
        session.Start();
        var har = session.GetHar();

        // Assert
        har.Log.Custom.Should().NotBeNull();
        har.Log.Custom!.Should().ContainKey("env");
        har.Log.Custom!.Should().ContainKey("txId");
        // After JSON round-trip (deep clone), values come back as JsonElement — compare as string
        har.Log.Custom["env"].ToString().Should().Contain("prod");
        har.Log.Custom["txId"].ToString().Should().Contain("abc-123");
    }

    [Fact]
    public async Task HarCaptureSession_StopAsync_LogsTruncation_AndReturnsCleanly()
    {
        // Arrange
        var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"truncation_test_{Guid.NewGuid():N}.har");
        try
        {
            var strategy = new MockCaptureStrategy();
            // Use very small max size to force truncation quickly
            var options = new CaptureOptions()
                .WithOutputFile(tempFile)
                .WithMaxOutputFileSize(500);
            var session = new HarCaptureSession(strategy, options);

            // Act
            await session.StartAsync();

            // Write enough entries to exceed the file size limit
            for (int i = 0; i < 30; i++)
            {
                strategy.SimulateEntry(HarEntryFactory.CreateTestEntry($"https://example.com/entry/{i}"), $"req{i}");
            }

            // StopAsync must NOT throw even when truncated
            Har har = null!;
            var act = async () => { har = await session.StopAsync(); };
            await act.Should().NotThrowAsync("StopAsync must return cleanly even when truncated");

            // File must still be valid JSON
            var fileContent = System.IO.File.ReadAllText(tempFile);
            var loadedHar = HarSerializer.Deserialize(fileContent);
            loadedHar.Log.Should().NotBeNull();
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task HarCaptureSession_StreamingMode_Custom_InFooter()
    {
        // Arrange
        var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"custom_footer_test_{Guid.NewGuid():N}.har");
        try
        {
            var strategy = new MockCaptureStrategy();
            var options = new CaptureOptions()
                .WithOutputFile(tempFile)
                .WithCustomMetadata("env", "test-env")
                .WithCustomMetadata("buildId", "42");
            var session = new HarCaptureSession(strategy, options);

            // Act
            await session.StartAsync();
            strategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/api"), "req1");
            await session.StopAsync();

            // Assert: _custom must appear in the streamed output file
            var fileContent = System.IO.File.ReadAllText(tempFile);
            fileContent.Should().Contain("_custom");
            fileContent.Should().Contain("env");
            fileContent.Should().Contain("test-env");
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
                System.IO.File.Delete(tempFile);
        }
    }

    // ========== Body Size Preservation Tests (Phase 20-03) ==========

    [Fact]
    public void HarCaptureSession_OnEntryCompleted_PreservesBodySizes()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        session.Start("page1", "Test Page");

        // Create an entry with body size extension fields
        var entryWithSizes = new HarEntry
        {
            StartedDateTime = DateTimeOffset.UtcNow,
            Time = 100,
            Request = new HarRequest
            {
                Method = "POST",
                Url = "https://example.com/api",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                QueryString = new List<HarQueryString>(),
                HeadersSize = -1,
                BodySize = 100
            },
            Response = new HarResponse
            {
                Status = 200,
                StatusText = "OK",
                HttpVersion = "HTTP/1.1",
                Cookies = new List<HarCookie>(),
                Headers = new List<HarHeader>(),
                Content = new HarContent { Size = 200, MimeType = "application/json" },
                RedirectURL = "",
                HeadersSize = -1,
                BodySize = 200
            },
            Cache = new HarCache(),
            Timings = new HarTimings { Send = 1, Wait = 50, Receive = 49 },
            RequestBodySize = 100,
            ResponseBodySize = 200
        };

        // Act — simulate the entry arriving (triggers OnEntryCompleted, which will copy PageRef and must preserve body sizes)
        strategy.SimulateEntry(entryWithSizes, "req1");

        var har = session.Stop();

        // Assert — body sizes survive the PageRef copy in OnEntryCompleted
        var capturedEntry = har.Log.Entries!.Should().ContainSingle().Subject;
        capturedEntry.RequestBodySize.Should().Be(100, "RequestBodySize must survive OnEntryCompleted PageRef copy");
        capturedEntry.ResponseBodySize.Should().Be(200, "ResponseBodySize must survive OnEntryCompleted PageRef copy");
        capturedEntry.PageRef.Should().Be("page1", "PageRef must be set by OnEntryCompleted");
    }

    [Fact]
    public void HarCaptureSession_OnEntryCompleted_ZeroBodySizes_OmittedFromJson()
    {
        // Arrange
        var strategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(strategy);

        session.Start();

        // Entry with no body sizes (GET request)
        var entry = HarEntryFactory.CreateTestEntry();  // BodySize = -1 (not 0)
        strategy.SimulateEntry(entry, "req1");

        var har = session.Stop();

        // Act — serialize the HAR
        var json = Selenium.HarCapture.Serialization.HarSerializer.Serialize(har);

        // Assert — zero body sizes should not appear in JSON
        json.Should().NotContain("_requestBodySize", "zero RequestBodySize should be omitted via WhenWritingDefault");
        json.Should().NotContain("_responseBodySize", "zero ResponseBodySize should be omitted via WhenWritingDefault");
    }
}
