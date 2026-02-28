using System;
using System.Threading;
using System.Threading.Tasks;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Strategies;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Tests.Fixtures;

/// <summary>
/// Mock capture strategy for unit testing.
/// Provides a SimulateEntry method to inject entries from tests.
/// </summary>
internal sealed class MockCaptureStrategy : INetworkCaptureStrategy
{
    public string StrategyName => "Mock";
    public bool SupportsDetailedTimings => true;
    public bool SupportsResponseBody => true;
    public double? LastDomContentLoadedTimestamp { get; set; }
    public double? LastLoadTimestamp { get; set; }
    public event Action<HarEntry, string>? EntryCompleted;

    public Task StartAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// Test helper to simulate an entry arriving.
    /// </summary>
    public void SimulateEntry(HarEntry entry, string requestId)
    {
        EntryCompleted?.Invoke(entry, requestId);
    }
}
