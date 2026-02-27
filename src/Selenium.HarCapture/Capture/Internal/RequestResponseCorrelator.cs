using System;
using System.Collections.Concurrent;
using System.Threading;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Internal utility for thread-safe correlation of HTTP requests with their responses.
/// Uses lazy initialization to handle race conditions where response arrives before request completes.
/// </summary>
internal sealed class RequestResponseCorrelator
{
    private readonly ConcurrentDictionary<string, Lazy<PendingEntry>> _pending = new ConcurrentDictionary<string, Lazy<PendingEntry>>();

    /// <summary>
    /// Gets the count of pending entries (requests awaiting responses).
    /// </summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Records a sent HTTP request.
    /// </summary>
    /// <param name="requestId">Unique identifier for this request.</param>
    /// <param name="request">The HAR request data.</param>
    /// <param name="startedDateTime">The timestamp when the request was initiated.</param>
    public void OnRequestSent(string requestId, HarRequest request, DateTimeOffset startedDateTime)
    {
        var lazy = _pending.GetOrAdd(requestId, id => new Lazy<PendingEntry>(() => new PendingEntry(id), LazyThreadSafetyMode.ExecutionAndPublication));
        var entry = lazy.Value;
        entry.Request = request;
        entry.StartedDateTime = startedDateTime;
    }

    /// <summary>
    /// Records a received HTTP response and attempts to correlate it with the matching request.
    /// </summary>
    /// <param name="requestId">Unique identifier for the request this response belongs to.</param>
    /// <param name="response">The HAR response data.</param>
    /// <param name="timings">Optional detailed timing breakdown (null if not available).</param>
    /// <param name="totalTime">Total elapsed time for the request in milliseconds.</param>
    /// <param name="resourceType">CDP resource type (e.g. "document", "script", "stylesheet").</param>
    /// <returns>
    /// A complete <see cref="HarEntry"/> if the matching request was found; otherwise null.
    /// </returns>
    public HarEntry? OnResponseReceived(string requestId, HarResponse response, HarTimings? timings, double totalTime, string? resourceType = null)
    {
        if (!_pending.TryRemove(requestId, out var lazy))
        {
            // Response received without matching request (rare edge case)
            return null;
        }

        var entry = lazy.Value;
        entry.Response = response;
        entry.Timings = timings;
        entry.Time = totalTime;
        entry.ResourceType = resourceType;

        return entry.ToHarEntry();
    }

    /// <summary>
    /// Clears all pending entries. Call this when stopping capture to release memory.
    /// </summary>
    public void Clear()
    {
        _pending.Clear();
    }

    /// <summary>
    /// Represents a pending HTTP request/response pair that is being correlated.
    /// </summary>
    private sealed class PendingEntry
    {
        public string RequestId { get; }
        public HarRequest? Request { get; set; }
        public HarResponse? Response { get; set; }
        public HarTimings? Timings { get; set; }
        public DateTimeOffset StartedDateTime { get; set; }
        public double Time { get; set; }
        public string? ResourceType { get; set; }

        public PendingEntry(string requestId)
        {
            RequestId = requestId;
        }

        /// <summary>
        /// Converts this pending entry into a complete HAR entry.
        /// </summary>
        /// <returns>A fully populated <see cref="HarEntry"/>.</returns>
        public HarEntry ToHarEntry()
        {
            return new HarEntry
            {
                StartedDateTime = StartedDateTime,
                Time = Time,
                Request = Request!,
                Response = Response!,
                Cache = new HarCache(),
                Timings = Timings ?? new HarTimings
                {
                    Send = 0,
                    Wait = 0,
                    Receive = 0
                },
                ResourceType = ResourceType
            };
        }
    }
}
