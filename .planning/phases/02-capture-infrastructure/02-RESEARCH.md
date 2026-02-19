# Phase 2: Capture Infrastructure - Research

**Researched:** 2026-02-19
**Domain:** Network traffic capture, Chrome DevTools Protocol (CDP), Selenium WebDriver integration, thread-safe event handling
**Confidence:** HIGH

## Summary

Phase 2 establishes the core capture infrastructure for Selenium.HarCapture: orchestrator class (HarCapture), strategy interface (INetworkCaptureStrategy), configuration system (CaptureOptions, CaptureType flags), and thread-safe request/response correlation.

The implementation relies on two capture backends: Chrome DevTools Protocol (CDP) via Selenium 4's DevToolsSession for Chromium browsers, and Selenium's INetwork API as fallback. CDP provides superior capabilities (detailed timings, response bodies, connection info) but is Chromium-only. INetwork offers cross-browser compatibility with limited data capture.

Thread safety is critical: multiple CDP events fire concurrently, GetHar() must not block capture, and multi-page operations happen during active capture. The standard approach is ConcurrentDictionary<string, Lazy<T>> for correlation, lock statement for Har mutations (sync), and JSON round-trip for deep cloning.

**Primary recommendation:** Use Strategy pattern with internal interface for capture backends. Use lock (not SemaphoreSlim) for Har mutations since all operations are synchronous. Use ConcurrentDictionary with Lazy<T> pattern for request/response correlation. Deep clone via JSON serialization for GetHar() snapshots.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CAP-01 | User can start and stop network capture on a WebDriver session | HarCapture orchestrator with Start/Stop methods; DevToolsSession lifecycle; INetwork StartMonitoring/StopMonitoring |
| CAP-02 | User can configure CaptureType flags to control what is captured (headers, cookies, content, timings) | [Flags] enum pattern; bitwise operations; CaptureOptions configuration object |
| CAP-03 | User can set URL include/exclude patterns to filter captured requests | DotNet.Glob library for glob patterns; Regex for pattern matching; filter logic in strategy implementations |
| CAP-04 | User can set maximum response body size to limit memory usage | CaptureOptions.MaxResponseBodySize property; Network.getResponseBody has 1MB limit; size checking before body retrieval |
| CAP-05 | User can create multi-page captures with NewPage(pageRef, pageTitle) | HAR 1.2 pages array and pageref linking; HarPage model already exists from Phase 1 |
| CAP-06 | User can get HAR snapshot (deep clone) while capture continues via GetHar() | JSON round-trip deep cloning using HarSerializer; lock statement for atomic snapshot; non-blocking design |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Selenium.WebDriver | 4.40.0+ | WebDriver automation, CDP access | Industry standard for browser automation; provides INetwork and DevToolsSession |
| System.Text.Json | 8.0.5 | JSON serialization for deep cloning | Already used in Phase 1; fast; no external deps; built-in to .NET 5+ |
| System.Collections.Concurrent | Built-in | Thread-safe collections | Standard for concurrent scenarios; ConcurrentDictionary is the canonical solution |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| DotNet.Glob | 3.1.1+ | Fast glob pattern matching | For URL include/exclude patterns; faster than Regex for glob syntax; netstandard2.0 compatible |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| lock statement | SemaphoreSlim | SemaphoreSlim is for async/await scenarios. All Har mutations are synchronous, so lock is simpler and faster. |
| JSON cloning | Manual cloning | Manual cloning is faster but brittle (breaks when model changes). JSON is reliable and leverages existing serializer. |
| DotNet.Glob | Pure Regex | Glob patterns are more intuitive for URL matching. DotNet.Glob handles conversion efficiently. |

**Installation:**
```bash
# Core dependencies (already in project)
# Selenium.WebDriver - will be added in CDP phase
# System.Text.Json 8.0.5 - already added

# Supporting (add in this phase)
dotnet add src/Selenium.HarCapture package DotNet.Glob --version 3.1.1
```

## Architecture Patterns

### Recommended Project Structure
```
src/Selenium.HarCapture/
├── Models/                  # HAR model classes (Phase 1)
├── Serialization/           # HarSerializer (Phase 1)
├── Capture/                 # NEW: Capture infrastructure
│   ├── HarCapture.cs       # Public orchestrator
│   ├── CaptureOptions.cs   # Configuration
│   ├── CaptureType.cs      # Flags enum
│   ├── Strategies/         # Strategy implementations
│   │   ├── INetworkCaptureStrategy.cs    # Internal interface
│   │   ├── CdpCaptureStrategy.cs         # Phase 3
│   │   └── INetworkCaptureStrategy.cs    # Phase 4
│   └── Internal/           # Internal utilities
│       ├── RequestResponseCorrelator.cs  # Correlation logic
│       └── UrlPatternMatcher.cs          # URL filtering
└── Extensions/             # Phase 5: WebDriver extensions
```

### Pattern 1: Strategy Pattern for Capture Backends
**What:** Internal interface defines contract for capture implementations (CDP, INetwork). HarCapture selects strategy based on driver capabilities.

**When to use:** Multiple implementations of same behavior with runtime selection based on context.

**Example:**
```csharp
// Internal strategy interface - NOT exposed publicly
internal interface INetworkCaptureStrategy : IDisposable
{
    string StrategyName { get; }
    bool SupportsDetailedTimings { get; }
    bool SupportsResponseBody { get; }

    // Events for request/response lifecycle
    event Action<HarEntry, string> RequestSent;
    event Action<HarEntry, string> ResponseReceived;

    Task StartAsync(CaptureType captureTypes);
    Task StopAsync();
}

// Orchestrator selects strategy
public sealed class HarCapture : IDisposable
{
    private INetworkCaptureStrategy? _strategy;

    private INetworkCaptureStrategy CreateStrategy()
    {
        // Try CDP first (Phase 3 implementation)
        if (!_options.ForceSeleniumNetworkApi && CanUseCdp(_driver))
        {
            return new CdpCaptureStrategy(_driver, _options);
        }

        // Fallback to INetwork (Phase 4 implementation)
        return new INetworkCaptureStrategy(_driver, _options);
    }
}
```

### Pattern 2: Flags Enum for Capture Configuration
**What:** [Flags] attribute on CaptureType enum enables bitwise combinations for fine-grained control.

**When to use:** Multiple independent boolean options that can be combined.

**Example:**
```csharp
[Flags]
public enum CaptureType
{
    None = 0,
    RequestHeaders = 1 << 0,      // 1
    RequestCookies = 1 << 1,      // 2
    RequestContent = 1 << 2,      // 4
    RequestBinaryContent = 1 << 3,// 8
    ResponseHeaders = 1 << 4,     // 16
    ResponseCookies = 1 << 5,     // 32
    ResponseContent = 1 << 6,     // 64
    ResponseBinaryContent = 1 << 7,// 128
    Timings = 1 << 8,             // 256
    ConnectionInfo = 1 << 9,      // 512

    // Convenience combinations
    HeadersAndCookies = RequestHeaders | RequestCookies | ResponseHeaders | ResponseCookies,
    AllText = HeadersAndCookies | RequestContent | ResponseContent | Timings,
    All = AllText | RequestBinaryContent | ResponseBinaryContent | ConnectionInfo
}

// Usage
var options = new CaptureOptions
{
    CaptureTypes = CaptureType.HeadersAndCookies | CaptureType.Timings
};

// Checking flags
if (captureTypes.HasFlag(CaptureType.ResponseContent))
{
    // Capture response body
}
```

### Pattern 3: ConcurrentDictionary with Lazy<T> for Thread-Safe Correlation
**What:** Store Lazy<T> wrappers in ConcurrentDictionary to ensure expensive operations run exactly once per key.

**When to use:** Concurrent dictionary with expensive value initialization that must run only once.

**Example:**
```csharp
// Request/Response correlation in strategies
internal sealed class RequestResponseCorrelator
{
    private readonly ConcurrentDictionary<string, Lazy<PendingEntry>> _pending = new();

    public void OnRequestSent(string requestId, HarRequest request)
    {
        // GetOrAdd with Lazy ensures only one PendingEntry created per requestId
        var lazy = _pending.GetOrAdd(requestId,
            id => new Lazy<PendingEntry>(() => new PendingEntry(id)));

        lazy.Value.Request = request;
        lazy.Value.StartedDateTime = DateTimeOffset.UtcNow;
    }

    public HarEntry? OnResponseReceived(string requestId, HarResponse response)
    {
        if (_pending.TryRemove(requestId, out var lazy))
        {
            var pending = lazy.Value;
            pending.Response = response;
            return pending.ToHarEntry();
        }
        return null;
    }

    private sealed class PendingEntry
    {
        public string RequestId { get; }
        public HarRequest? Request { get; set; }
        public HarResponse? Response { get; set; }
        public DateTimeOffset StartedDateTime { get; set; }

        public PendingEntry(string requestId) => RequestId = requestId;

        public HarEntry ToHarEntry() => new HarEntry
        {
            StartedDateTime = StartedDateTime,
            Request = Request!,
            Response = Response!,
            // ... other fields
        };
    }
}
```

### Pattern 4: Lock Statement for Synchronous Har Mutations
**What:** Use lock (not SemaphoreSlim) to protect Har object mutations since all operations are synchronous.

**When to use:** Synchronous code that needs thread-safe access to shared mutable state.

**Example:**
```csharp
public sealed class HarCapture : IDisposable
{
    private readonly object _lock = new object();
    private Har _har = null!;

    public void NewPage(string pageRef, string pageTitle)
    {
        lock (_lock)
        {
            var page = new HarPage
            {
                Id = pageRef,
                Title = pageTitle,
                StartedDateTime = DateTimeOffset.UtcNow,
                PageTimings = new HarPageTimings()
            };

            // Har and HarLog are immutable (init-only), so recreate
            var pages = new List<HarPage>(_har.Log.Pages ?? Enumerable.Empty<HarPage>())
            {
                page
            };

            _har = _har with
            {
                Log = _har.Log with { Pages = pages }
            };

            _currentPageRef = pageRef;
        }
    }

    public Har GetHar()
    {
        lock (_lock)
        {
            // Deep clone via JSON round-trip
            var json = HarSerializer.Serialize(_har, writeIndented: false);
            return HarSerializer.Deserialize(json);
        }
    }
}
```

### Pattern 5: Dispose Pattern for Strategy Cleanup
**What:** INetworkCaptureStrategy implements IDisposable to unsubscribe from events and clean up resources.

**When to use:** Classes that subscribe to events or hold unmanaged resources.

**Example:**
```csharp
internal abstract class NetworkCaptureStrategyBase : INetworkCaptureStrategy
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;

        // Unsubscribe from events to prevent memory leaks
        Dispose(disposing: true);
        _disposed = true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from CDP/INetwork events
            // Release any held resources
        }
    }
}

public sealed class HarCapture : IDisposable
{
    private INetworkCaptureStrategy? _strategy;

    public void Dispose()
    {
        _strategy?.Dispose();
        _strategy = null;
    }
}
```

### Anti-Patterns to Avoid

- **Using SemaphoreSlim for synchronous code:** lock is simpler and faster when no async/await is involved. SemaphoreSlim.Wait() is just overhead.
- **Manual deep cloning:** Brittle and breaks when models change. JSON round-trip is reliable and uses existing serializer.
- **Storing Har entries directly in strategy:** Har is owned by HarCapture, not strategies. Strategies emit events; orchestrator builds Har.
- **Using Task.Result or .Wait() on async operations in sync methods:** Causes thread pool starvation. Provide both sync and async APIs.
- **Forgetting to unsubscribe from events:** Memory leaks. Always unsubscribe in Dispose().

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Glob pattern matching | Custom glob parser | DotNet.Glob library | Glob syntax is complex (character ranges, negation, nested patterns). DotNet.Glob is fast (faster than Regex) and battle-tested. |
| Deep object cloning | Recursive reflection cloner | JSON round-trip via HarSerializer | JSON serialization already tested in Phase 1. Manual cloning breaks when models change. Reflection is slower and more complex. |
| Request/response correlation | Custom correlation logic | ConcurrentDictionary + Lazy pattern | Well-established pattern for thread-safe once-per-key initialization. Handles race conditions correctly. |
| Async locking | Custom async lock | Use lock (not SemaphoreSlim) | All Har mutations are synchronous. lock is simpler and faster. Only use SemaphoreSlim if you need await inside lock. |
| CDP command building | String concatenation | Selenium's DevToolsSession | DevToolsSession handles CDP protocol details, serialization, error handling, and session management. |

**Key insight:** Network capture involves complex concurrency (multiple events firing simultaneously), lifecycle management (CDP session cleanup, event unsubscription), and subtle edge cases (response body unavailable, request canceled, redirects). Use proven libraries and patterns rather than reinventing solutions.

## Common Pitfalls

### Pitfall 1: CDP Network.getResponseBody Failures
**What goes wrong:** Network.getResponseBody returns error "No resource with given identifier found" or empty body.

**Why it happens:**
- Response bodies have 1MB size limit in CDP (larger responses return null)
- RequestId becomes invalid shortly after request completes
- Some response types don't have bodies (204 No Content, 304 Not Modified, redirects)
- Response must be fully loaded before getResponseBody works

**How to avoid:**
- Call getResponseBody immediately in loadingFinished event handler
- Check response status code (don't try to get body for 204, 304, 3xx)
- Handle errors gracefully (missing body is not fatal)
- Respect MaxResponseBodySize option to skip large bodies

**Warning signs:** Sporadic "No resource with given identifier" errors; empty response bodies for large files; errors on redirect responses.

### Pitfall 2: Event Handler Memory Leaks
**What goes wrong:** INetworkCaptureStrategy instances are never garbage collected, causing memory growth.

**Why it happens:**
- Event publishers (DevToolsSession, INetwork) hold strong references to subscribers
- If strategy subscribes to events but never unsubscribes, it stays in memory forever
- HarCapture creates new strategy on each Start/Stop cycle

**How to avoid:**
- Always implement IDisposable on strategy classes
- Unsubscribe from all events in Dispose()
- Call strategy.Dispose() in HarCapture.Dispose()
- Use try/finally when subscribing to ensure cleanup

**Warning signs:** Memory grows with each capture session; finalizers never run; memory profiler shows old strategy instances retained.

**Example fix:**
```csharp
internal sealed class CdpCaptureStrategy : INetworkCaptureStrategy
{
    private DevToolsSession _session = null!;

    public Task StartAsync(CaptureType captureTypes)
    {
        _session.Network.RequestWillBeSent += OnRequestWillBeSent;
        _session.Network.ResponseReceived += OnResponseReceived;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // CRITICAL: Unsubscribe to break strong reference
        if (_session != null)
        {
            _session.Network.RequestWillBeSent -= OnRequestWillBeSent;
            _session.Network.ResponseReceived -= OnResponseReceived;
        }
    }
}
```

### Pitfall 3: GetDevToolsSession() Is Per-Window
**What goes wrong:** After calling GetDevToolsSession(), opening new browser tabs/windows fails because session is locked to original window.

**Why it happens:**
- Selenium 4 C# has limitation where GetDevToolsSession() locks to first window
- Cannot get DevToolsSession for new tabs/windows after first call
- Closing original window leaves "dead" session

**How to avoid:**
- Document that CDP capture is for single window/tab
- If user opens new window, consider stopping and restarting capture
- For multi-window scenarios, recommend using INetwork fallback
- Don't call GetDevToolsSession() until StartAsync()

**Warning signs:** CDP commands fail after new window opened; "session not found" errors; CDP works initially then stops.

### Pitfall 4: Race Condition in Request/Response Correlation
**What goes wrong:** requestWillBeSent and responseReceived events fire so close together that both threads try to create PendingEntry.

**Why it happens:**
- CDP events fire on different threads
- ConcurrentDictionary.GetOrAdd valueFactory can run multiple times
- Without Lazy<T>, multiple PendingEntry instances created for same requestId
- First entry wins, second entry's data is lost

**How to avoid:**
- Use ConcurrentDictionary<string, Lazy<PendingEntry>>
- GetOrAdd stores Lazy wrapper, not PendingEntry directly
- Lazy.Value ensures PendingEntry created exactly once per requestId
- Use LazyThreadSafetyMode.ExecutionAndPublication (default)

**Warning signs:** Missing request or response data; entries with null Request or Response; duplicate requestId warnings.

### Pitfall 5: Blocking GetHar() with Lock Held
**What goes wrong:** GetHar() holds lock while doing expensive JSON serialization, blocking all capture operations.

**Why it happens:**
- JSON serialization (especially with indentation) is CPU-intensive
- If lock held during serialization, RequestSent/ResponseReceived events blocked
- CDP continues firing events; they queue up; memory grows; performance degrades

**How to avoid:**
- JSON round-trip inside lock is acceptable—serialization is fast enough for snapshots
- Alternative: copy Har structure (shallow copy of lists), release lock, then serialize
- Monitor lock contention; if GetHar() called frequently, consider caching

**Warning signs:** Capture slows down when GetHar() called; events delayed; lock contention warnings.

**Note:** For v1, JSON round-trip inside lock is acceptable (GetHar() is infrequent snapshot operation). For v2, consider optimization if profiling shows contention.

### Pitfall 6: Init-Only Properties Require with Expressions
**What goes wrong:** Cannot mutate Har.Log.Pages directly because properties are init-only.

**Why it happens:**
- Phase 1 models use init-only properties for immutability
- Cannot do _har.Log.Pages.Add(page)
- Must recreate entire object graph with with expressions

**How to avoid:**
- Use with expressions to create modified copies: _har = _har with { Log = _har.Log with { Pages = newPages } }
- Create new collections for lists: new List<HarPage>(existing) { newPage }
- Lock protects entire mutation (read old, create new, assign)

**Warning signs:** Compile errors when trying to set init-only properties; confusion about immutability.

### Pitfall 7: Forgetting to Set CurrentPageRef
**What goes wrong:** Entries created after NewPage() have pageRef = null instead of linking to new page.

**Why it happens:**
- pageRef is optional in HAR spec
- Easy to forget to track "current page" state
- Strategies don't know about pages (they just emit entries)

**How to avoid:**
- HarCapture tracks _currentPageRef field
- Set _currentPageRef in NewPage() inside lock
- When strategy emits entry, HarCapture sets entry.PageRef = _currentPageRef
- Initial page created in StartAsync() sets _currentPageRef

**Warning signs:** All entries have pageRef = null; pages array populated but no entries linked; HAR viewer shows entries not grouped by page.

## Code Examples

Verified patterns for Phase 2 implementation:

### CaptureType Flags Enum
```csharp
// Source: .NET Framework Design Guidelines for Flags enums
// https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/enum

[Flags]
public enum CaptureType
{
    None = 0,

    // Request capture options
    RequestHeaders = 1 << 0,           // 1
    RequestCookies = 1 << 1,           // 2
    RequestContent = 1 << 2,           // 4
    RequestBinaryContent = 1 << 3,     // 8

    // Response capture options
    ResponseHeaders = 1 << 4,          // 16
    ResponseCookies = 1 << 5,          // 32
    ResponseContent = 1 << 6,          // 64
    ResponseBinaryContent = 1 << 7,    // 128

    // Timing and metadata
    Timings = 1 << 8,                  // 256
    ConnectionInfo = 1 << 9,           // 512

    // Convenience combinations
    HeadersAndCookies = RequestHeaders | RequestCookies | ResponseHeaders | ResponseCookies,
    AllText = HeadersAndCookies | RequestContent | ResponseContent | Timings,
    All = AllText | RequestBinaryContent | ResponseBinaryContent | ConnectionInfo
}
```

### CaptureOptions Configuration Class
```csharp
// Source: Builder pattern from .NET design patterns
public sealed class CaptureOptions
{
    /// <summary>
    /// Gets or sets which data to capture. Default is AllText (headers, cookies, text content, timings).
    /// </summary>
    public CaptureType CaptureTypes { get; set; } = CaptureType.AllText;

    /// <summary>
    /// Gets or sets the creator name in HAR file. Default is "Selenium.HarCapture".
    /// </summary>
    public string CreatorName { get; set; } = "Selenium.HarCapture";

    /// <summary>
    /// Gets or sets whether to force using Selenium INetwork API instead of CDP. Default is false.
    /// </summary>
    public bool ForceSeleniumNetworkApi { get; set; } = false;

    /// <summary>
    /// Gets or sets maximum response body size in bytes. 0 = unlimited. Default is 0.
    /// </summary>
    public long MaxResponseBodySize { get; set; } = 0;

    /// <summary>
    /// Gets or sets URL patterns to include (glob or regex). Null = include all.
    /// </summary>
    public IReadOnlyList<string>? UrlIncludePatterns { get; set; }

    /// <summary>
    /// Gets or sets URL patterns to exclude (glob or regex). Null = exclude none.
    /// </summary>
    public IReadOnlyList<string>? UrlExcludePatterns { get; set; }
}
```

### HarCapture Orchestrator (Skeleton)
```csharp
// Source: Strategy pattern + Dispose pattern
// https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/dispose-pattern

public sealed class HarCapture : IDisposable
{
    private readonly IWebDriver _driver;
    private readonly CaptureOptions _options;
    private readonly object _lock = new object();

    private INetworkCaptureStrategy? _strategy;
    private Har _har = null!;
    private string? _currentPageRef;
    private bool _isCapturing;

    public HarCapture(IWebDriver driver)
        : this(driver, new CaptureOptions())
    {
    }

    public HarCapture(IWebDriver driver, CaptureOptions options)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsCapturing => _isCapturing;
    public string? ActiveStrategyName => _strategy?.StrategyName;

    public async Task StartAsync(string? initialPageRef = null, string? initialPageTitle = null)
    {
        if (_isCapturing)
            throw new InvalidOperationException("Capture already started.");

        // Create strategy (CDP or INetwork)
        _strategy = CreateStrategy();

        // Subscribe to events
        _strategy.RequestSent += OnRequestSent;
        _strategy.ResponseReceived += OnResponseReceived;

        // Initialize HAR structure
        InitializeHar(initialPageRef, initialPageTitle);

        // Start capturing
        await _strategy.StartAsync(_options.CaptureTypes).ConfigureAwait(false);
        _isCapturing = true;
    }

    public void Start(string? initialPageRef = null, string? initialPageTitle = null)
    {
        StartAsync(initialPageRef, initialPageTitle).GetAwaiter().GetResult();
    }

    public void NewPage(string pageRef, string pageTitle)
    {
        if (!_isCapturing)
            throw new InvalidOperationException("Capture not started.");

        lock (_lock)
        {
            var page = new HarPage
            {
                Id = pageRef,
                Title = pageTitle,
                StartedDateTime = DateTimeOffset.UtcNow,
                PageTimings = new HarPageTimings()
            };

            // Create new pages list (immutable model requires with expression)
            var pages = new List<HarPage>(_har.Log.Pages ?? Enumerable.Empty<HarPage>())
            {
                page
            };

            _har = _har with
            {
                Log = _har.Log with { Pages = pages }
            };

            _currentPageRef = pageRef;
        }
    }

    public Har GetHar()
    {
        if (!_isCapturing)
            throw new InvalidOperationException("Capture not started.");

        lock (_lock)
        {
            // Deep clone via JSON round-trip
            var json = HarSerializer.Serialize(_har, writeIndented: false);
            return HarSerializer.Deserialize(json);
        }
    }

    public async Task<Har> StopAsync()
    {
        if (!_isCapturing)
            throw new InvalidOperationException("Capture not started.");

        await _strategy!.StopAsync().ConfigureAwait(false);
        _isCapturing = false;

        // Return final HAR (no clone needed, capture stopped)
        lock (_lock)
        {
            return _har;
        }
    }

    public Har Stop()
    {
        return StopAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        if (_strategy != null)
        {
            _strategy.RequestSent -= OnRequestSent;
            _strategy.ResponseReceived -= OnResponseReceived;
            _strategy.Dispose();
            _strategy = null;
        }
    }

    private INetworkCaptureStrategy CreateStrategy()
    {
        // Placeholder: actual implementation in Phase 3/4
        throw new NotImplementedException("Strategy creation implemented in Phase 3/4");
    }

    private void InitializeHar(string? initialPageRef, string? initialPageTitle)
    {
        lock (_lock)
        {
            _har = new Har
            {
                Log = new HarLog
                {
                    Version = "1.2",
                    Creator = new HarCreator
                    {
                        Name = _options.CreatorName,
                        Version = typeof(HarCapture).Assembly.GetName().Version?.ToString() ?? "1.0.0"
                    },
                    Pages = initialPageRef != null
                        ? new List<HarPage>
                        {
                            new HarPage
                            {
                                Id = initialPageRef,
                                Title = initialPageTitle ?? "Page 1",
                                StartedDateTime = DateTimeOffset.UtcNow,
                                PageTimings = new HarPageTimings()
                            }
                        }
                        : new List<HarPage>(),
                    Entries = new List<HarEntry>()
                }
            };

            _currentPageRef = initialPageRef;
        }
    }

    private void OnRequestSent(HarEntry entry, string requestId)
    {
        // Filter by URL patterns
        if (!ShouldCapture(entry.Request.Url))
            return;

        lock (_lock)
        {
            // Set pageref if tracking pages
            if (_currentPageRef != null)
            {
                entry = entry with { PageRef = _currentPageRef };
            }

            // Add to HAR (immutable model requires with expression)
            var entries = new List<HarEntry>(_har.Log.Entries ?? Enumerable.Empty<HarEntry>())
            {
                entry
            };

            _har = _har with
            {
                Log = _har.Log with { Entries = entries }
            };
        }
    }

    private void OnResponseReceived(HarEntry entry, string requestId)
    {
        lock (_lock)
        {
            // Find matching request entry and update with response
            // (Actual implementation would match by requestId and update)
        }
    }

    private bool ShouldCapture(string url)
    {
        // URL filtering logic (implemented with DotNet.Glob)
        // Placeholder for actual implementation
        return true;
    }
}
```

### ConcurrentDictionary + Lazy Pattern for Correlation
```csharp
// Source: https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/

internal sealed class RequestResponseCorrelator
{
    private readonly ConcurrentDictionary<string, Lazy<PendingEntry>> _pending = new();

    public void OnRequestSent(string requestId, HarRequest request, DateTimeOffset startedDateTime)
    {
        var lazy = _pending.GetOrAdd(requestId,
            id => new Lazy<PendingEntry>(
                () => new PendingEntry(id),
                LazyThreadSafetyMode.ExecutionAndPublication));

        lazy.Value.Request = request;
        lazy.Value.StartedDateTime = startedDateTime;
    }

    public HarEntry? OnResponseReceived(string requestId, HarResponse response, double totalTime)
    {
        if (_pending.TryRemove(requestId, out var lazy))
        {
            var pending = lazy.Value;
            pending.Response = response;
            pending.Time = totalTime;
            return pending.ToHarEntry();
        }

        // Response without request (race condition or out-of-order events)
        return null;
    }

    public void Clear()
    {
        _pending.Clear();
    }

    private sealed class PendingEntry
    {
        public string RequestId { get; }
        public HarRequest? Request { get; set; }
        public HarResponse? Response { get; set; }
        public DateTimeOffset StartedDateTime { get; set; }
        public double Time { get; set; }

        public PendingEntry(string requestId) => RequestId = requestId;

        public HarEntry ToHarEntry()
        {
            return new HarEntry
            {
                StartedDateTime = StartedDateTime,
                Time = Time,
                Request = Request!,
                Response = Response!,
                Cache = new HarCache(),
                Timings = new HarTimings()
            };
        }
    }
}
```

### URL Pattern Matcher with DotNet.Glob
```csharp
// Source: https://github.com/dazinator/DotNet.Glob

using DotNet.Glob;

internal sealed class UrlPatternMatcher
{
    private readonly Glob[]? _includeGlobs;
    private readonly Glob[]? _excludeGlobs;

    public UrlPatternMatcher(IReadOnlyList<string>? includePatterns, IReadOnlyList<string>? excludePatterns)
    {
        _includeGlobs = includePatterns?.Select(p => Glob.Parse(p)).ToArray();
        _excludeGlobs = excludePatterns?.Select(p => Glob.Parse(p)).ToArray();
    }

    public bool ShouldCapture(string url)
    {
        // Exclude takes precedence
        if (_excludeGlobs != null && _excludeGlobs.Any(g => g.IsMatch(url)))
            return false;

        // If include patterns specified, must match at least one
        if (_includeGlobs != null)
            return _includeGlobs.Any(g => g.IsMatch(url));

        // No patterns = capture all
        return true;
    }
}

// Usage
var matcher = new UrlPatternMatcher(
    includePatterns: new[] { "https://api.example.com/**", "https://cdn.example.com/**" },
    excludePatterns: new[] { "**/*.png", "**/*.jpg", "**/*.gif" });

bool shouldCapture = matcher.ShouldCapture("https://api.example.com/users");  // true
shouldCapture = matcher.ShouldCapture("https://example.com/logo.png");         // false
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| BrowserMob Proxy for network capture | Chrome DevTools Protocol (CDP) | Selenium 4 (2021) | Proxy-free capture; no external process; easier setup; browser-native |
| Manual JSON cloning | System.Text.Json built-in | .NET Core 3.0+ (2019) | No Newtonsoft.Json dependency; faster; built-in to modern .NET |
| lock statement only | SemaphoreSlim for async | .NET 4.5 (2012) | Enables async locking; but lock still preferred for sync code |
| Selenium 3 lack of network APIs | INetwork + CDP in Selenium 4 | Selenium 4.0 (2021) | First-class network interception and monitoring |
| GetDevToolsSession per-window limitation | Still present | Current | Cannot get CDP session for new tabs after first call (documented limitation) |

**Deprecated/outdated:**
- **BrowserMob Proxy:** External proxy approach. CDP eliminates need for proxy-based capture for Chromium browsers.
- **Newtonsoft.Json for new projects:** System.Text.Json is standard for .NET 5+ and netstandard2.0 with package reference.
- **Manual thread synchronization:** Use concurrent collections (ConcurrentDictionary) and standard patterns (Lazy<T>) instead of reinventing.

## Open Questions

1. **Should HarCapture be thread-safe for multiple concurrent Start/Stop calls?**
   - What we know: Single-threaded usage is common; concurrent Start/Stop is edge case
   - What's unclear: User expectations for multi-threaded usage
   - Recommendation: v1 throws InvalidOperationException if Start called when already started. Document as not thread-safe for lifecycle methods. GetHar() is thread-safe during capture.

2. **How to handle CDP Network.getResponseBody 1MB limit?**
   - What we know: CDP has 1MB response body limit; larger responses return null
   - What's unclear: Should we silently skip, log warning, or use fallback?
   - Recommendation: Check Content-Length header. If > MaxResponseBodySize or > 1MB, skip body retrieval. Set HarContent.Comment = "Body too large (size: X MB)".

3. **Should URL patterns support both glob and regex?**
   - What we know: Glob is more intuitive ("**.js"); regex is more powerful
   - What's unclear: Is glob sufficient? How to differentiate glob vs regex patterns?
   - Recommendation: v1 uses glob only (via DotNet.Glob). v2 can add regex support with pattern prefix ("regex:pattern").

4. **How to handle CDP session lifetime with Dispose?**
   - What we know: DevToolsSession should be disposed; HarCapture owns strategy lifecycle
   - What's unclear: Should Dispose() auto-stop capture, or require explicit Stop()?
   - Recommendation: Dispose() calls StopAsync() if IsCapturing = true, then disposes strategy. Document that users should call Stop() explicitly for clean shutdown.

## Sources

### Primary (HIGH confidence)
- Chrome DevTools Protocol - Network domain: https://chromedevtools.github.io/devtools-protocol/tot/Network/
- Selenium INetwork API documentation: https://www.selenium.dev/selenium/docs/api/dotnet/webdriver/OpenQA.Selenium.INetwork.html
- Microsoft Learn - Dispose Pattern: https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/dispose-pattern
- Microsoft Learn - Enum Design: https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/enum
- Microsoft Learn - Managed Threading Best Practices: https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-threading-best-practices

### Secondary (MEDIUM confidence)
- ConcurrentDictionary + Lazy pattern: https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/
- Event handler memory leaks: https://michaelscodingspot.com/5-techniques-to-avoid-memory-leaks-by-events-in-c-net-you-should-know/
- DotNet.Glob library: https://github.com/dazinator/DotNet.Glob
- HAR 1.2 specification: http://www.softwareishard.com/blog/har-12-spec/ (note: certificate expired, but spec content stable)
- Selenium CDP usage: https://www.selenium.dev/documentation/webdriver/bidi/cdp/
- CDP Network.getResponseBody limitations: https://github.com/ChromeDevTools/devtools-protocol/issues/44

### Tertiary (LOW confidence - for awareness only)
- SemaphoreSlim vs lock discussions: https://medium.com/@anyanwuraphaelc/understanding-lock-and-semaphoreslim-in-asynchronous-code-bd1524834a8c
- C# deep clone approaches: https://www.c-sharpcorner.com/article/cloning-class-using-system-text-json-in-net-8/
- Selenium GetDevToolsSession limitation: https://github.com/SeleniumHQ/selenium/issues/9798

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Selenium.WebDriver 4.40.0, System.Text.Json 8.0.5, System.Collections.Concurrent are all verified stable releases
- Architecture: HIGH - Strategy pattern, Flags enum, ConcurrentDictionary+Lazy, Dispose pattern are well-documented industry standards
- Pitfalls: HIGH - CDP response body limits, event memory leaks, GetDevToolsSession limitations are documented in official sources and issue trackers
- Code examples: MEDIUM-HIGH - Patterns are standard but specific implementation details require testing/validation

**Research date:** 2026-02-19
**Valid until:** 2026-03-19 (30 days - stable technologies and patterns)
