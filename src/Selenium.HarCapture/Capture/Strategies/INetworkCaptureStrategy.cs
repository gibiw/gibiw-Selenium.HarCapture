using System;
using System.Threading;
using System.Threading.Tasks;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Strategies;

/// <summary>
/// Internal interface for network capture strategy implementations (CDP, INetwork, etc.).
/// Each strategy handles browser-specific network event subscription and correlation.
/// </summary>
internal interface INetworkCaptureStrategy : IDisposable
{
    /// <summary>
    /// Gets a human-readable name for this strategy (e.g., "CDP", "INetwork").
    /// Used for diagnostics and logging.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Gets whether this strategy supports detailed HAR timing breakdown
    /// (blocked, dns, connect, send, wait, receive, ssl).
    /// </summary>
    /// <remarks>
    /// CDP supports detailed timings. INetwork does not (only provides total time).
    /// </remarks>
    bool SupportsDetailedTimings { get; }

    /// <summary>
    /// Gets whether this strategy can capture response body content.
    /// </summary>
    /// <remarks>
    /// CDP can capture response bodies via Network.getResponseBody.
    /// INetwork does not provide access to response bodies.
    /// </remarks>
    bool SupportsResponseBody { get; }

    /// <summary>
    /// Gets the monotonic timestamp (in milliseconds) of the last DOMContentLoaded event, or null if not fired.
    /// Relative to the first request timestamp for the current page.
    /// </summary>
    double? LastDomContentLoadedTimestamp { get; }

    /// <summary>
    /// Gets the monotonic timestamp (in milliseconds) of the last Load event, or null if not fired.
    /// Relative to the first request timestamp for the current page.
    /// </summary>
    double? LastLoadTimestamp { get; }

    /// <summary>
    /// Event fired when a fully-correlated HTTP request/response pair is ready.
    /// The first parameter is the complete HAR entry, the second is the internal request ID.
    /// </summary>
    /// <remarks>
    /// Strategies handle internal correlation of request/response events.
    /// The orchestrator subscribes to this event to collect completed entries.
    /// </remarks>
    event Action<HarEntry, string>? EntryCompleted;

    /// <summary>
    /// Starts network capture with the specified options.
    /// Must be called before any network traffic will be captured.
    /// </summary>
    /// <param name="options">Configuration options controlling capture behavior, URL filtering, and size limits.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when capture is successfully started.</returns>
    Task StartAsync(CaptureOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops network capture and flushes any pending entries.
    /// After calling this, no further network events will be captured.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when capture is successfully stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}
