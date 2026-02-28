using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OpenQA.Selenium;
using Selenium.HarCapture;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Strategies;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Tests.Fixtures;
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
        mockStrategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/api/test"), "req1");

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

    [Fact]
    public async Task StopAndSaveAsync_Parameterless_WithCompression_DoesNotThrow()
    {
        // Arrange — reproduces FileNotFoundException when compression deletes original .har
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har");
        var gzFile = tempFile + ".gz";
        try
        {
            var options = new CaptureOptions()
                .WithOutputFile(tempFile)
                .WithCompression();
            var mockStrategy = new MockCaptureStrategy();
            var session = new HarCaptureSession(mockStrategy, options);
            var capture = new HarCapture(session);
            await capture.StartAsync("page1", "Test Page");

            mockStrategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/api/test"), "req1");

            // Act — this used to throw FileNotFoundException because
            // StopAsync compresses .har → .har.gz and deletes the original,
            // but StopAndSaveAsync tried to read FileInfo on the deleted .har path
            Func<Task> act = async () => await capture.StopAndSaveAsync();

            // Assert
            await act.Should().NotThrowAsync();
            File.Exists(gzFile).Should().BeTrue("compressed file should exist");
            File.Exists(tempFile).Should().BeFalse("original should be deleted after compression");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(gzFile)) File.Delete(gzFile);
        }
    }

    [Fact]
    public async Task StopAndSaveAsync_Parameterless_WithoutCompression_ReportsCorrectPath()
    {
        // Arrange — verify FinalOutputFilePath falls back to OutputFilePath when compression is not enabled
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har");
        try
        {
            var options = new CaptureOptions().WithOutputFile(tempFile);
            var mockStrategy = new MockCaptureStrategy();
            var session = new HarCaptureSession(mockStrategy, options);
            var capture = new HarCapture(session);
            await capture.StartAsync("page1", "Test Page");
            mockStrategy.SimulateEntry(HarEntryFactory.CreateTestEntry("https://example.com/api/test"), "req1");

            // Act
            Func<Task> act = async () => await capture.StopAndSaveAsync();

            // Assert
            await act.Should().NotThrowAsync();
            File.Exists(tempFile).Should().BeTrue("uncompressed file should exist when compression disabled");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task StopAsync_WithCancellationToken_DelegatesToSession()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        await capture.StartAsync();

        // Act
        var har = await capture.StopAsync(CancellationToken.None);

        // Assert
        har.Should().NotBeNull();
        har.Log.Should().NotBeNull();
        har.Log.Version.Should().Be("1.2");
    }

    [Fact]
    public async Task StartAsync_WithCancellationToken_DelegatesToSession()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);

        // Act
        await capture.StartAsync(null, null, CancellationToken.None);

        // Assert
        capture.IsCapturing.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WithCancelledToken_ThrowsOperationCancelled()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);
        await capture.StartAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await capture.StopAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void FinalOutputFilePath_ReturnsSessionValue()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".har");
        try
        {
            var options = new CaptureOptions().WithOutputFile(tempFile);
            var mockStrategy = new MockCaptureStrategy();
            var session = new HarCaptureSession(mockStrategy, options);
            var capture = new HarCapture(session);

            // Act
            var finalPath = capture.FinalOutputFilePath;

            // Assert
            finalPath.Should().Be(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void FinalOutputFilePath_WhenDisposed_ReturnsNull()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);

        // Act
        capture.Dispose();
        var finalPath = capture.FinalOutputFilePath;

        // Assert
        finalPath.Should().BeNull("session is null after disposal");
    }

    [Fact]
    public void Dispose_DoesNotDeadlock()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);

        // Act - call Dispose synchronously (should complete within timeout)
        var task = Task.Run(() => capture.Dispose());
        var completed = task.Wait(TimeSpan.FromSeconds(10));

        // Assert
        completed.Should().BeTrue("Dispose should complete within 10 seconds without deadlock");
    }

    [Fact]
    public async Task DisposeAsync_ThenDispose_IsIdempotent()
    {
        // Arrange
        var mockStrategy = new MockCaptureStrategy();
        var session = new HarCaptureSession(mockStrategy);
        var capture = new HarCapture(session);

        // Act - call DisposeAsync then Dispose
        await capture.DisposeAsync();
        Action act = () => capture.Dispose();

        // Assert - no exception thrown
        act.Should().NotThrow();
    }

}
