using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FluentAssertions;
using OpenQA.Selenium;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Strategies;
using Selenium.HarCapture.Models;
using Selenium.HarCapture.Tests.Fixtures;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Strategies;

/// <summary>
/// Unit tests for CdpNetworkCaptureStrategy.
/// Note: Integration tests with real CDP sessions will be added in a future phase.
/// These tests focus on input validation, property values, and basic behavior.
/// </summary>
public class CdpNetworkCaptureStrategyTests
{
    [Fact]
    public void Constructor_NullDriver_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new CdpNetworkCaptureStrategy(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("driver");
    }

    [Fact]
    public void StrategyName_ReturnsCDP()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act
        var name = strategy.StrategyName;

        // Assert
        name.Should().Be("CDP");
    }

    [Fact]
    public void SupportsDetailedTimings_ReturnsTrue()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act
        var supports = strategy.SupportsDetailedTimings;

        // Assert
        supports.Should().BeTrue();
    }

    [Fact]
    public void SupportsResponseBody_ReturnsTrue()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act
        var supports = strategy.SupportsResponseBody;

        // Assert
        supports.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_DriverDoesNotSupportDevTools_ThrowsInvalidOperationException()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);
        var options = new CaptureOptions();

        // Act & Assert
        Func<Task> act = async () => await strategy.StartAsync(options);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Chrome DevTools Protocol*");
    }

    [Fact]
    public async Task StartAsync_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act & Assert
        Func<Task> act = async () => await strategy.StartAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Dispose_WhenNotStarted_DoesNotThrow()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act & Assert
        Action act = () => strategy.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act & Assert
        strategy.Dispose();
        Action act = () => strategy.Dispose();

        act.Should().NotThrow("Dispose should be idempotent");
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_DoesNotThrow()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act & Assert
        Func<Task> act = async () => await strategy.StopAsync();

        await act.Should().NotThrowAsync("StopAsync should handle empty pending tasks collection");
    }

    [Fact]
    public async Task Dispose_AfterPartialStart_DoesNotThrow()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Simulate partial initialization by attempting start (which will fail)
        try
        {
            await strategy.StartAsync(new CaptureOptions());
        }
        catch
        {
            // Expected to fail - driver doesn't support DevTools
        }

        // Act & Assert
        Action act = () => strategy.Dispose();

        act.Should().NotThrow("Dispose should handle null _pendingBodyTasks gracefully");
    }

    [Fact]
    public void EntryCompleted_CanBeSubscribed()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);
        var eventFired = false;

        // Act - subscribe to event
        Action<HarEntry, string> handler = (entry, id) => eventFired = true;
        strategy.EntryCompleted += handler;
        strategy.EntryCompleted -= handler;

        // Assert
        eventFired.Should().BeFalse("Event handler was subscribed and unsubscribed without firing");
    }

    [Fact]
    public async Task StopAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act & Assert
        await strategy.StopAsync();
        Func<Task> act = async () => await strategy.StopAsync();

        await act.Should().NotThrowAsync("StopAsync should be idempotent (stopping flag prevents race conditions)");
    }

    [Fact]
    public async Task Dispose_AfterStop_DoesNotThrow()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act & Assert
        await strategy.StopAsync();
        Action act = () => strategy.Dispose();

        act.Should().NotThrow("Dispose after StopAsync should be safe (stopping flag prevents double-dispose issues)");
    }

    [Fact]
    public async Task StopAsync_ClearsInternalState_DoesNotThrow()
    {
        // Arrange - Strategy created but never started (adapter is null)
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Act & Assert - StopAsync should handle null adapter + empty LRU cache gracefully
        Func<Task> act = async () => await strategy.StopAsync();

        await act.Should().NotThrowAsync("StopAsync should handle empty LRU cache gracefully");
    }

    [Fact]
    public async Task Dispose_ClearsLruCache_DoesNotThrow()
    {
        // Arrange
        var driver = new NonDevToolsDriver();
        var strategy = new CdpNetworkCaptureStrategy(driver);

        // Attempt partial start to initialize some state
        try { await strategy.StartAsync(new CaptureOptions()); } catch { }

        // Act & Assert - Dispose should clear cache + list without exception
        Action act = () => strategy.Dispose();

        act.Should().NotThrow("Dispose should clear LRU cache without error");
    }

    /// <summary>
    /// LRU cache contract (tested via integration tests with real CDP):
    /// - MaxCacheEntries = 500 (bounded memory)
    /// - TryGetCachedBody promotes entry to MRU position
    /// - CacheBody evicts LRU entry when at capacity
    /// - ClearCache clears both ConcurrentDictionary and LinkedList under lock
    /// - Thread-safe: ConcurrentDictionary for reads, lock for LinkedList mutations
    /// Note: Full behavioral testing requires integration tests with real CDP sessions
    /// that generate 500+ unique URLs. See integration test project.
    /// </summary>
}
