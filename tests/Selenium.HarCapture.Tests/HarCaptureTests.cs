using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using OpenQA.Selenium;
using Selenium.HarCapture;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Strategies;
using Selenium.HarCapture.Models;
using Xunit;

namespace Selenium.HarCapture.Tests;

public sealed class HarCaptureTests
{
    [Fact]
    public void Constructor_WithNullDriver_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new HarCapture((IWebDriver)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("driver");
    }

    [Fact]
    public void Start_DelegatesToSession()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);

        // Act
        capture.Start();

        // Assert
        capture.IsCapturing.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_DelegatesToSession()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);

        // Act
        await capture.StartAsync();

        // Assert
        capture.IsCapturing.Should().BeTrue();
    }

    [Fact]
    public void Stop_ReturnsFinalHar()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        capture.Start();

        // Act
        var har = capture.Stop();

        // Assert
        har.Should().NotBeNull();
        har.Log.Should().NotBeNull();
        har.Log.Version.Should().Be("1.2");
    }

    [Fact]
    public async Task StopAsync_ReturnsFinalHar()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        await capture.StartAsync();

        // Act
        var har = await capture.StopAsync();

        // Assert
        har.Should().NotBeNull();
        har.Log.Should().NotBeNull();
        har.Log.Version.Should().Be("1.2");
    }

    [Fact]
    public void GetHar_ReturnsDeepClone()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        capture.Start();
        mockStrategy.SimulateEntry(CreateTestEntry("https://example.com/api/test"), "req1");

        // Act - get two snapshots
        var har1 = capture.GetHar();
        var har2 = capture.GetHar();

        // Assert - they should be different instances
        har1.Should().NotBeSameAs(har2);
        har1.Log.Should().NotBeSameAs(har2.Log);
        har1.Log.Entries.Should().NotBeSameAs(har2.Log.Entries);
    }

    [Fact]
    public void NewPage_DelegatesToSession()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        capture.Start("page1", "Home");

        // Act
        capture.NewPage("page2", "About");
        var har = capture.GetHar();

        // Assert
        har.Log.Pages.Should().NotBeNull();
        har.Log.Pages.Should().HaveCount(2);
        har.Log.Pages![0].Id.Should().Be("page1");
        har.Log.Pages[1].Id.Should().Be("page2");
    }

    [Fact]
    public void IsCapturing_ReflectsSessionState()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);

        // Act & Assert - before start
        capture.IsCapturing.Should().BeFalse();

        // Act - start
        capture.Start();
        capture.IsCapturing.Should().BeTrue();

        // Act - stop
        capture.Stop();
        capture.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public void ActiveStrategyName_ReturnsStrategyName()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);

        // Act
        var strategyName = capture.ActiveStrategyName;

        // Assert
        strategyName.Should().Be("Mock");
    }

    [Fact]
    public void Dispose_PreventsSubsequentCalls()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        capture.Start();

        // Act
        capture.Dispose();

        // Assert
        Action act = () => capture.GetHar();
        act.Should().Throw<ObjectDisposedException>()
            .WithMessage("*HarCapture*");
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);

        // Act
        capture.Dispose();
        Action act = () => capture.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_PreventsSubsequentCalls()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        await capture.StartAsync();

        // Act
        await capture.DisposeAsync();

        // Assert
        Action act = () => capture.GetHar();
        act.Should().Throw<ObjectDisposedException>()
            .WithMessage("*HarCapture*");
    }

    [Fact]
    public void ThrowIfDisposed_AllMethods()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        capture.Dispose();

        // Act & Assert - verify all methods throw ObjectDisposedException
        Action startAction = () => capture.Start();
        startAction.Should().Throw<ObjectDisposedException>()
            .WithMessage("*HarCapture*");

        Func<Task> startAsyncAction = async () => await capture.StartAsync();
        startAsyncAction.Should().ThrowAsync<ObjectDisposedException>()
            .WithMessage("*HarCapture*");

        Action stopAction = () => capture.Stop();
        stopAction.Should().Throw<ObjectDisposedException>()
            .WithMessage("*HarCapture*");

        Func<Task> stopAsyncAction = async () => await capture.StopAsync();
        stopAsyncAction.Should().ThrowAsync<ObjectDisposedException>()
            .WithMessage("*HarCapture*");

        Action getHarAction = () => capture.GetHar();
        getHarAction.Should().Throw<ObjectDisposedException>()
            .WithMessage("*HarCapture*");

        Action newPageAction = () => capture.NewPage("page", "Page");
        newPageAction.Should().Throw<ObjectDisposedException>()
            .WithMessage("*HarCapture*");
    }

    [Fact]
    public async Task StopAndSaveAsync_WithOutputFileConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var options = new CaptureOptions().WithOutputFile(tempFile);
            var mockStrategy = new MockCaptureStrategy();
            var session = new HarCaptureSession(mockStrategy, options);
            var capture = new HarCapture(session);
            await capture.StartAsync();

            var anotherTempFile = Path.GetTempFileName();
            try
            {
                // Act & Assert
                Func<Task> act = async () => await capture.StopAndSaveAsync(anotherTempFile);

                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("*WithOutputFile*StopAndSaveAsync*");
            }
            finally
            {
                if (File.Exists(anotherTempFile))
                    File.Delete(anotherTempFile);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void StopAndSave_WithOutputFileConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var options = new CaptureOptions().WithOutputFile(tempFile);
            var mockStrategy = new MockCaptureStrategy();
            var session = new HarCaptureSession(mockStrategy, options);
            var capture = new HarCapture(session);
            capture.Start();

            var anotherTempFile = Path.GetTempFileName();
            try
            {
                // Act & Assert
                Action act = () => capture.StopAndSave(anotherTempFile);

                act.Should().Throw<InvalidOperationException>()
                    .WithMessage("*WithOutputFile*StopAndSaveAsync*");
            }
            finally
            {
                if (File.Exists(anotherTempFile))
                    File.Delete(anotherTempFile);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task StopAndSaveAsync_WithoutOutputFile_WorksNormally()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        await capture.StartAsync();

        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var har = await capture.StopAndSaveAsync(tempFile);

            // Assert
            har.Should().NotBeNull();
            har.Log.Should().NotBeNull();
            File.Exists(tempFile).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task StopAndSaveAsync_WithOutputFileConfigured_ExceptionMessageExplainsConflict()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var options = new CaptureOptions().WithOutputFile(tempFile);
            var mockStrategy = new MockCaptureStrategy();
            var session = new HarCaptureSession(mockStrategy, options);
            var capture = new HarCapture(session);
            await capture.StartAsync();

            var anotherTempFile = Path.GetTempFileName();
            try
            {
                // Act & Assert
                Func<Task> act = async () => await capture.StopAndSaveAsync(anotherTempFile);

                var exception = await act.Should().ThrowAsync<InvalidOperationException>();
                exception.Which.Message.Should().Contain("WithOutputFile");
                exception.Which.Message.Should().Contain("StopAndSaveAsync");
                exception.Which.Message.Should().Contain(tempFile);
            }
            finally
            {
                if (File.Exists(anotherTempFile))
                    File.Delete(anotherTempFile);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
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
}
