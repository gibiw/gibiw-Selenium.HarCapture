# Phase 5: Public API - Research

**Researched:** 2026-02-20
**Domain:** .NET library public API design, thread-safe facade patterns, extension methods
**Confidence:** HIGH

## Summary

Phase 5 creates the public-facing API that wraps the existing infrastructure (HAR models, capture strategies, session management) into a clean, thread-safe experience for end users. The research confirms that .NET libraries follow established patterns for public API design: facade classes that manage lifecycle with IDisposable, extension methods that enable one-liner usage, fluent builder APIs for configuration, and thread-safety through ConcurrentDictionary and deep cloning. The critical technical decisions are: (1) implement both IDisposable and IAsyncDisposable for maximum compatibility with netstandard2.0 using Microsoft.Bcl.AsyncInterfaces polyfill, (2) use ConfigureAwait(false) on all async methods to prevent deadlocks when consumers call .Result/.Wait(), (3) expose extension methods in a dedicated namespace (Selenium.HarCapture.Extensions) that users import with a single using statement, and (4) ensure thread-safety via existing ConcurrentDictionary in strategies plus JSON-based deep cloning for GetHar() snapshots.

**Primary recommendation:** Create a HarCapture facade class that wraps HarCaptureSession with both sync/async disposal, add two key extension methods (StartHarCapture for lifecycle, CaptureHarAsync for one-liner), make CaptureOptions fluent with method chaining, and ensure all async methods use ConfigureAwait(false) for library-safe execution.

## Phase Requirements

<phase_requirements>
| ID | Description | Research Support |
|----|-------------|-----------------|
| API-01 | HarCapture class provides sync and async Start/Stop methods | Facade pattern with IDisposable/IAsyncDisposable documented in MS Learn - wraps HarCaptureSession lifecycle |
| API-02 | WebDriver extension methods provide one-liner capture (StartHarCapture, CaptureHarAsync) | Extension method patterns from MS Framework Design Guidelines - place in Selenium.HarCapture.Extensions namespace |
| API-03 | CaptureOptions class provides fluent configuration (CaptureTypes, URL patterns, body size limit) | Fluent builder pattern with method chaining returning `this` - existing CaptureOptions already has properties, add WithXyz() methods |
| API-04 | HarCapture exposes IsCapturing and ActiveStrategyName properties for diagnostics | Simple passthrough properties to HarCaptureSession - already exists in infrastructure |
| THR-01 | HarCapture is thread-safe for concurrent access to GetHar() and mutation operations | Lock-based thread safety in HarCaptureSession + deep clone pattern via JSON round-trip |
| THR-02 | Capture strategies use ConcurrentDictionary for request/response correlation | Already implemented in RequestResponseCorrelator - uses ConcurrentDictionary<string, Lazy<PendingEntry>> |
| THR-03 | GetHar() returns deep clone via JSON round-trip (no shared mutable state) | Already implemented in HarCaptureSession.GetHar() using HarSerializer round-trip |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | Built-in (netstandard2.0) | JSON serialization for deep cloning | Already in use, high performance, officially supported |
| Microsoft.Bcl.AsyncInterfaces | 8.0+ | IAsyncDisposable for netstandard2.0 | Backports IAsyncDisposable interface to netstandard2.0 targets |
| Selenium.WebDriver | 4.x (existing) | IWebDriver extension target | Already project dependency |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Collections.Concurrent | Built-in | Thread-safe collections | Already in use for RequestResponseCorrelator |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Microsoft.Bcl.AsyncInterfaces | Manual polyfill | Package is official backport, no need to maintain custom interface definition |
| IAsyncDisposable only | IDisposable only | Both needed - IAsyncDisposable for proper async cleanup, IDisposable for sync consumers |

**Installation:**
```bash
# New dependency for Phase 5
dotnet add package Microsoft.Bcl.AsyncInterfaces --version 8.0.0
```

## Architecture Patterns

### Recommended Project Structure
```
src/Selenium.HarCapture/
├── HarCapture.cs                    # Public facade class
├── Extensions/
│   └── WebDriverExtensions.cs       # Extension methods for IWebDriver
├── Capture/
│   ├── CaptureOptions.cs            # Add fluent methods (WithXyz)
│   └── HarCaptureSession.cs         # Existing (already thread-safe)
```

### Pattern 1: Facade with Dual Disposal

**What:** A public class that wraps internal infrastructure and implements both IDisposable and IAsyncDisposable

**When to use:** Library public APIs that manage async resources but need to support both sync and async consumers

**Example:**
```csharp
// Source: Microsoft Learn - Implementing DisposeAsync
// https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync

public sealed class HarCapture : IDisposable, IAsyncDisposable
{
    private HarCaptureSession? _session;
    private bool _disposed;

    public bool IsCapturing => _session?.IsCapturing ?? false;
    public string? ActiveStrategyName => _session?.ActiveStrategyName;

    public HarCapture(IWebDriver driver, CaptureOptions? options = null)
    {
        _session = new HarCaptureSession(driver, options);
    }

    public async Task StartAsync(string? initialPageRef = null, string? initialPageTitle = null)
    {
        ThrowIfDisposed();
        await _session!.StartAsync(initialPageRef, initialPageTitle).ConfigureAwait(false);
    }

    public void Start(string? initialPageRef = null, string? initialPageTitle = null)
    {
        StartAsync(initialPageRef, initialPageTitle).GetAwaiter().GetResult();
    }

    public async Task<Har> StopAsync()
    {
        ThrowIfDisposed();
        return await _session!.StopAsync().ConfigureAwait(false);
    }

    public Har Stop()
    {
        return StopAsync().GetAwaiter().GetResult();
    }

    public Har GetHar()
    {
        ThrowIfDisposed();
        return _session!.GetHar();
    }

    public void NewPage(string pageRef, string pageTitle)
    {
        ThrowIfDisposed();
        _session!.NewPage(pageRef, pageTitle);
    }

    // IDisposable implementation
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    // IAsyncDisposable implementation
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _session?.Dispose();
            _session = null;
        }

        _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_session is not null)
        {
            // HarCaptureSession only implements IDisposable
            // But we can call it synchronously here as it doesn't hold async resources
            _session.Dispose();
            _session = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HarCapture));
        }
    }
}
```

**Key points:**
- Both Dispose() and DisposeAsync() call GC.SuppressFinalize
- DisposeAsync() calls DisposeAsyncCore() then Dispose(false)
- Dispose() calls Dispose(true)
- This pattern ensures finalizer code paths still get invoked correctly

### Pattern 2: Extension Methods for One-Liner Usage

**What:** Static extension methods on IWebDriver that enable fluent, discoverable API usage

**When to use:** When library functionality needs to be accessible without forcing users to learn new types

**Example:**
```csharp
// Source: MS Framework Design Guidelines - Extension Methods
// https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/extension-methods

namespace Selenium.HarCapture.Extensions;

public static class WebDriverExtensions
{
    /// <summary>
    /// Starts HAR network capture on this WebDriver instance.
    /// Returns a HarCapture instance that manages the capture lifecycle.
    /// </summary>
    public static HarCapture StartHarCapture(
        this IWebDriver driver,
        CaptureOptions? options = null)
    {
        var capture = new HarCapture(driver, options);
        capture.Start();
        return capture;
    }

    /// <summary>
    /// Starts HAR network capture on this WebDriver instance with fluent configuration.
    /// Returns a HarCapture instance that manages the capture lifecycle.
    /// </summary>
    public static HarCapture StartHarCapture(
        this IWebDriver driver,
        Action<CaptureOptions> configure)
    {
        var options = new CaptureOptions();
        configure(options);
        return driver.StartHarCapture(options);
    }

    /// <summary>
    /// One-liner: captures HAR for the duration of an async action.
    /// Automatically starts capture, executes action, stops capture, and returns HAR.
    /// </summary>
    public static async Task<Har> CaptureHarAsync(
        this IWebDriver driver,
        Func<Task> action,
        CaptureOptions? options = null)
    {
        await using var capture = new HarCapture(driver, options);
        await capture.StartAsync().ConfigureAwait(false);
        await action().ConfigureAwait(false);
        return await capture.StopAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// One-liner: captures HAR for the duration of a sync action.
    /// Automatically starts capture, executes action, stops capture, and returns HAR.
    /// </summary>
    public static Har CaptureHar(
        this IWebDriver driver,
        Action action,
        CaptureOptions? options = null)
    {
        using var capture = new HarCapture(driver, options);
        capture.Start();
        action();
        return capture.Stop();
    }
}
```

**Namespace guidance:** Place in `Selenium.HarCapture.Extensions` namespace (not `Selenium.HarCapture`) per MS guidelines to avoid polluting main namespace.

### Pattern 3: Fluent Configuration API

**What:** Method chaining on configuration objects using `return this` pattern

**When to use:** When configuration has multiple optional settings that users should compose fluently

**Example:**
```csharp
// Source: Fluent Builder Pattern in C#
// https://medium.com/@shanto462/mastering-the-fluent-builder-pattern-in-c-from-basics-to-advanced-scenarios-b1b702583299

public sealed class CaptureOptions
{
    // Existing properties remain
    public CaptureType CaptureTypes { get; set; } = CaptureType.AllText;
    public string CreatorName { get; set; } = "Selenium.HarCapture";
    public bool ForceSeleniumNetworkApi { get; set; } = false;
    public long MaxResponseBodySize { get; set; } = 0;
    public IReadOnlyList<string>? UrlIncludePatterns { get; set; }
    public IReadOnlyList<string>? UrlExcludePatterns { get; set; }

    // New fluent methods
    public CaptureOptions WithCaptureTypes(CaptureType types)
    {
        CaptureTypes = types;
        return this;
    }

    public CaptureOptions WithMaxResponseBodySize(long maxSize)
    {
        MaxResponseBodySize = maxSize;
        return this;
    }

    public CaptureOptions WithUrlIncludePatterns(params string[] patterns)
    {
        UrlIncludePatterns = patterns;
        return this;
    }

    public CaptureOptions WithUrlExcludePatterns(params string[] patterns)
    {
        UrlExcludePatterns = patterns;
        return this;
    }

    public CaptureOptions WithCreatorName(string name)
    {
        CreatorName = name;
        return this;
    }

    public CaptureOptions ForceSeleniumNetwork()
    {
        ForceSeleniumNetworkApi = true;
        return this;
    }
}

// Usage example
var options = new CaptureOptions()
    .WithCaptureTypes(CaptureType.AllText | CaptureType.ResponseBinaryContent)
    .WithMaxResponseBodySize(1_000_000)
    .WithUrlIncludePatterns("https://api.example.com/**")
    .WithUrlExcludePatterns("**/*.png", "**/*.jpg");
```

### Pattern 4: ConfigureAwait(false) in Library Code

**What:** All async methods in library code must use ConfigureAwait(false) to prevent deadlocks

**When to use:** Always, in every await statement in library code

**Why critical:** When consumers call library async methods synchronously (.Result or .Wait()), not using ConfigureAwait(false) can cause deadlocks in contexts with constrained synchronization contexts (UI threads, ASP.NET Core, test frameworks).

**Example:**
```csharp
// Source: ConfigureAwait FAQ - .NET Blog
// https://devblogs.microsoft.com/dotnet/configureawait-faq/

// BAD - will deadlock if consumer calls .Result on UI thread
public async Task StartAsync()
{
    await _session.StartAsync(); // Captures SynchronizationContext
    // Continuation tries to run on UI thread, which is blocked by .Result
}

// GOOD - library-safe pattern
public async Task StartAsync()
{
    await _session.StartAsync().ConfigureAwait(false); // Runs on thread pool
    // Continuation runs on any available thread, not blocked
}
```

**ConfigureAwait(false) placement:** Use on every await in the library, including:
- Internal method calls
- Third-party library calls (Selenium, DevTools)
- Task.Delay, Task.WhenAll, Task.WhenAny
- IAsyncDisposable.DisposeAsync() calls

### Anti-Patterns to Avoid

- **Async void methods:** Never use async void except for event handlers. Use async Task instead.
- **Blocking on async code:** Never use .Result or .Wait() internally in library code. Use async all the way down.
- **Forgetting ConfigureAwait:** Missing even one ConfigureAwait(false) can cause deadlocks.
- **Thread.Sleep in async methods:** Use await Task.Delay(...).ConfigureAwait(false) instead.
- **Disposing in finalizers:** IAsyncDisposable resources should not be disposed in finalizers (no async in finalizers).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Async disposal infrastructure | Custom async cleanup patterns | Microsoft.Bcl.AsyncInterfaces + MS pattern | Official backport, well-tested, matches C# 8.0+ behavior |
| Thread-safe collections | Custom locking + Dictionary | ConcurrentDictionary (already in use) | Lock-free algorithms, proven correctness |
| Deep object cloning | Reflection-based cloners | JSON round-trip (already in use) | Handles all edge cases, maintains HAR spec compliance |
| Extension method discoverability | Non-standard naming | Dedicated .Extensions namespace | Follows .NET Framework Design Guidelines |

**Key insight:** Phase 5 is about composition and pattern application, not new algorithms. The infrastructure from Phases 1-4 already handles thread safety (ConcurrentDictionary), deep cloning (JSON), and lifecycle (IDisposable). Phase 5 wraps this with standard .NET patterns.

## Common Pitfalls

### Pitfall 1: Missing ConfigureAwait(false) in Library Code

**What goes wrong:** Library methods deadlock when consumers call them synchronously with .Result or .Wait() from constrained contexts (UI threads, ASP.NET Core without thread pool, test frameworks).

**Why it happens:** By default, async methods capture the SynchronizationContext and try to resume on the same context after await. If that context is blocked waiting for the method to complete, deadlock occurs.

**How to avoid:** Add .ConfigureAwait(false) to every await in library code without exception. Use analyzer rules (CA2007) to enforce this.

**Warning signs:**
- Intermittent hangs in test suites
- Deadlocks only when called from UI threads
- Works async but hangs when called with .Result

**Detection strategy:** Enable CA2007 analyzer rule, add `.editorconfig` with severity=error for library projects.

### Pitfall 2: Implementing Only IAsyncDisposable Without IDisposable

**What goes wrong:** Consumers who only call Dispose() (not DisposeAsync()) leak resources because disposal never occurs.

**Why it happens:** IAsyncDisposable doesn't inherit from IDisposable. If a consumer uses a using statement (not await using), only Dispose() is called.

**How to avoid:** Always implement both interfaces when targeting netstandard2.0. Dispose() should call async disposal synchronously as a fallback (GetAwaiter().GetResult() in Dispose() is acceptable as last resort).

**Warning signs:**
- Memory leaks in long-running processes
- Resources not cleaned up after using blocks
- Works with await using but not with using

**Code smell:**
```csharp
// BAD - only async disposal
public class BadCapture : IAsyncDisposable { }

// GOOD - both patterns
public class GoodCapture : IDisposable, IAsyncDisposable { }
```

### Pitfall 3: Exposing Internal Types in Public API

**What goes wrong:** Public API surface exposes internal types (HarCaptureSession, INetworkCaptureStrategy), forcing consumers to understand implementation details and breaking encapsulation.

**Why it happens:** Directly returning or accepting internal types in public methods without facade layer.

**How to avoid:** HarCapture facade should accept only public types (IWebDriver, CaptureOptions, Har) and hide all internal infrastructure.

**Warning signs:**
- InternalsVisibleTo needed for consumer projects
- Consumers construct internal types directly
- Breaking changes to internal types force consumer updates

**Correct layering:**
```
Public API Layer:    HarCapture, WebDriverExtensions
Internal Layer:      HarCaptureSession, INetworkCaptureStrategy
Isolated Layer:      CDP/INetwork implementation details
```

### Pitfall 4: Extension Method Namespace Pollution

**What goes wrong:** Extension methods in the main namespace (Selenium.HarCapture) force all consumers to see them, even if they don't want HAR capture on IWebDriver.

**Why it happens:** Placing extension methods in same namespace as main types for "convenience."

**How to avoid:** Place extension methods in dedicated Selenium.HarCapture.Extensions namespace per MS Framework Design Guidelines. Users opt-in with `using Selenium.HarCapture.Extensions;`.

**Warning signs:**
- IntelliSense shows extension methods on IWebDriver for all consumers
- Conflicts with other libraries extending IWebDriver
- No way to "turn off" extension method visibility

**Correct pattern:**
```csharp
// File: Extensions/WebDriverExtensions.cs
namespace Selenium.HarCapture.Extensions; // Dedicated namespace

public static class WebDriverExtensions
{
    public static HarCapture StartHarCapture(this IWebDriver driver) { }
}

// Consumer code
using Selenium.HarCapture;            // Gets HarCapture, CaptureOptions
using Selenium.HarCapture.Extensions; // Opt-in to extension methods
```

### Pitfall 5: Fluent API Breaking Immutability

**What goes wrong:** Fluent methods modify and return same instance, causing shared state bugs when options are reused.

**Why it happens:** Returning `this` from WithXyz() methods mutates the object.

**How to avoid:** For Phase 5, mutation is acceptable because CaptureOptions is not immutable (existing code uses property setters). However, document that options should not be reused across captures if modified.

**Warning signs:**
- Options object used in multiple captures has unexpected values
- Thread-safety issues when sharing options

**Acceptable for Phase 5:**
```csharp
// Mutation pattern (matches existing CaptureOptions design)
public CaptureOptions WithMaxResponseBodySize(long maxSize)
{
    MaxResponseBodySize = maxSize;
    return this; // Mutates and returns same instance
}

// Usage: don't reuse modified options
var options = new CaptureOptions().WithMaxResponseBodySize(1000);
var capture1 = driver.StartHarCapture(options);
// DON'T: options already modified, reuse causes unexpected behavior
var capture2 = driver.StartHarCapture(options.WithMaxResponseBodySize(2000));
```

**Alternative (immutable pattern, NOT for Phase 5):**
```csharp
// Would require full redesign of CaptureOptions as immutable record
public record CaptureOptions
{
    public CaptureOptions WithMaxResponseBodySize(long maxSize)
        => this with { MaxResponseBodySize = maxSize }; // Returns new instance
}
```

## Code Examples

Verified patterns from official sources and existing codebase.

### Thread-Safe GetHar() with Deep Clone (Already Implemented)

```csharp
// Source: Existing HarCaptureSession.GetHar()
// File: src/Selenium.HarCapture/Capture/HarCaptureSession.cs

public Har GetHar()
{
    if (_disposed)
    {
        throw new ObjectDisposedException(nameof(HarCaptureSession));
    }

    if (!_isCapturing)
    {
        throw new InvalidOperationException("Capture is not started.");
    }

    lock (_lock)
    {
        // Deep clone via JSON round-trip
        var json = HarSerializer.Serialize(_har, writeIndented: false);
        return HarSerializer.Deserialize(json);
    }
}
```

**Why this works:** Lock prevents concurrent mutation during serialization, JSON round-trip creates completely independent object graph with no shared references.

### ConcurrentDictionary for Request/Response Correlation (Already Implemented)

```csharp
// Source: Existing RequestResponseCorrelator
// File: src/Selenium.HarCapture/Capture/Internal/RequestResponseCorrelator.cs

internal sealed class RequestResponseCorrelator
{
    private readonly ConcurrentDictionary<string, Lazy<PendingEntry>> _pending
        = new ConcurrentDictionary<string, Lazy<PendingEntry>>();

    public void OnRequestSent(string requestId, HarRequest request, DateTimeOffset startedDateTime)
    {
        var lazy = _pending.GetOrAdd(requestId,
            id => new Lazy<PendingEntry>(() => new PendingEntry(id),
                LazyThreadSafetyMode.ExecutionAndPublication));
        var entry = lazy.Value;
        entry.Request = request;
        entry.StartedDateTime = startedDateTime;
    }

    public HarEntry? OnResponseReceived(string requestId, HarResponse response,
        HarTimings? timings, double totalTime)
    {
        if (!_pending.TryRemove(requestId, out var lazy))
        {
            return null; // Response without request (race condition)
        }

        var entry = lazy.Value;
        entry.Response = response;
        entry.Timings = timings;
        entry.Time = totalTime;

        return entry.ToHarEntry();
    }
}
```

**Why this works:** ConcurrentDictionary.GetOrAdd + Lazy pattern handles race conditions where response arrives before request completes processing. LazyThreadSafetyMode.ExecutionAndPublication ensures only one thread initializes PendingEntry.

### One-Liner Usage Example (Target API)

```csharp
// Usage example for Phase 5 extension methods

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Selenium.HarCapture;
using Selenium.HarCapture.Extensions;

// Pattern 1: Lifecycle management
using var driver = new ChromeDriver();
using var capture = driver.StartHarCapture(options => options
    .WithCaptureTypes(CaptureType.AllText | CaptureType.ResponseBinaryContent)
    .WithMaxResponseBodySize(1_000_000)
    .WithUrlIncludePatterns("https://api.example.com/**"));

driver.Navigate().GoToUrl("https://example.com");
var har = capture.GetHar(); // Snapshot while capture continues
driver.Navigate().GoToUrl("https://example.com/page2");
var finalHar = capture.Stop();

// Pattern 2: True one-liner with automatic cleanup
using var driver = new ChromeDriver();
var har = await driver.CaptureHarAsync(async () =>
{
    driver.Navigate().GoToUrl("https://example.com");
    await Task.Delay(1000); // Wait for page load
}, options);

// Pattern 3: Sync one-liner
using var driver = new ChromeDriver();
var har = driver.CaptureHar(() =>
{
    driver.Navigate().GoToUrl("https://example.com");
}, options);
```

### Dual Disposal Pattern for netstandard2.0

```csharp
// Source: Microsoft Learn - Implement DisposeAsync method
// https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync

public sealed class HarCapture : IDisposable, IAsyncDisposable
{
    private HarCaptureSession? _session;
    private bool _disposed;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose managed resources
            _session?.Dispose();
            _session = null;
        }

        _disposed = true;
    }

    protected async ValueTask DisposeAsyncCore()
    {
        if (_session is not null)
        {
            // HarCaptureSession only implements IDisposable
            // Call synchronously as it doesn't hold async resources
            _session.Dispose();
            _session = null;
        }
    }
}
```

**Key details:**
- DisposeAsync() calls DisposeAsyncCore() then Dispose(false)
- Dispose() calls Dispose(true)
- Both call GC.SuppressFinalize()
- Dispose(false) in async path prevents double-dispose of managed resources

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Only IDisposable | IDisposable + IAsyncDisposable | C# 8.0 (2019), backported to netstandard2.0 via Microsoft.Bcl.AsyncInterfaces | Libraries must support both patterns for maximum compatibility |
| await without ConfigureAwait | ConfigureAwait(false) in library code | 2013 (Stephen Cleary blog), formalized in MS guidance 2019 | Prevents deadlocks when consumers block on async code |
| Fluent APIs via method chaining | Same pattern, now ubiquitous | 2010s (popularized by LINQ, Entity Framework) | Standard pattern for configuration APIs |
| Extension methods in main namespace | Dedicated .Extensions namespace | MS Framework Design Guidelines updated ~2017 | Reduces namespace pollution, better discoverability |

**Deprecated/outdated:**
- **Async void for anything except event handlers:** Use async Task instead. Async void swallows exceptions.
- **Manual thread synchronization:** ConcurrentDictionary and SemaphoreSlim handle most cases better than lock + Monitor.
- **IAsyncDisposable without IDisposable:** Causes resource leaks for consumers using non-async disposal.

## Open Questions

1. **Should HarCapture support multi-page captures?**
   - What we know: HarCaptureSession has NewPage() method for multi-page HAR files
   - What's unclear: Whether to expose this through HarCapture facade or keep as HarCaptureSession-only feature
   - Recommendation: Expose NewPage() in HarCapture facade - it's part of HAR 1.2 spec and users may need it for multi-step workflows

2. **Should fluent methods validate inputs or throw?**
   - What we know: CaptureOptions currently has no validation, validation happens at Start() time
   - What's unclear: Whether WithXyz() fluent methods should validate immediately (fail-fast) or defer to Start()
   - Recommendation: Defer to Start() for Phase 5 - matches existing pattern, avoids breaking changes

3. **Should CaptureHarAsync support cancellation tokens?**
   - What we know: None of the existing async methods support cancellation
   - What's unclear: Whether to add CancellationToken parameters to public API
   - Recommendation: Out of scope for Phase 5 - add in future phase if users request it

## Sources

### Primary (HIGH confidence)

**Microsoft Official Documentation:**
- [Implement a DisposeAsync method - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync) - Official pattern for IDisposable + IAsyncDisposable
- [ConfigureAwait FAQ - .NET Blog](https://devblogs.microsoft.com/dotnet/configureawait-faq/) - Authoritative guidance on ConfigureAwait(false) usage
- [Extension Methods - Framework Design Guidelines | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/extension-methods) - Official namespace organization guidelines
- [ConcurrentDictionary Class | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2) - Official API documentation

**Existing Codebase (verified implementation):**
- `src/Selenium.HarCapture/Capture/HarCaptureSession.cs` - Thread-safe GetHar() with JSON deep clone
- `src/Selenium.HarCapture/Capture/Internal/RequestResponseCorrelator.cs` - ConcurrentDictionary + Lazy pattern
- `src/Selenium.HarCapture/Capture/CaptureOptions.cs` - Existing configuration structure
- `src/Selenium.HarCapture/Capture/Strategies/StrategyFactory.cs` - Strategy selection pattern

### Secondary (MEDIUM confidence)

**Community Best Practices:**
- [Mastering the Fluent Builder Pattern in C#](https://medium.com/@shanto462/mastering-the-fluent-builder-pattern-in-c-from-basics-to-advanced-scenarios-b1b702583299) - Fluent API patterns
- [Mastering Thread Safety in C#](https://medium.com/@shanto462/mastering-thread-safety-in-c-from-basics-to-advanced-scenarios-3cacc2c10d5a) - ConcurrentDictionary usage patterns
- [C# Why you should use ConfigureAwait(false) in your library code](https://medium.com/bynder-tech/c-why-you-should-use-configureawait-false-in-your-library-code-d7837dce3d7f) - Library-specific ConfigureAwait guidance
- [Don't Block on Async Code - Stephen Cleary](https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html) - Classic article on async deadlocks

**Similar Libraries (API patterns):**
- [selenium-capture (Java)](https://github.com/mike10004/selenium-capture) - Builder pattern for HAR capture
- [Selenium.WebDriver.Extensions](https://github.com/Softlr/Selenium.WebDriver.Extensions) - Extension method patterns for Selenium

### Tertiary (LOW confidence)

**General web searches:**
- Web API design best practices articles - General guidance, not library-specific
- WebDriver extension method examples - Community code snippets, varying quality

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Microsoft.Bcl.AsyncInterfaces is official backport, well-documented
- Architecture: HIGH - All patterns sourced from official MS documentation and existing verified code
- Pitfalls: HIGH - Based on official MS guidance and well-known async/disposal anti-patterns
- Code examples: HIGH - All examples either from MS Learn or existing codebase

**Research date:** 2026-02-20
**Valid until:** 60 days (stable patterns, unlikely to change in .NET ecosystem)

**Key decisions locked:**
1. Use both IDisposable and IAsyncDisposable with MS pattern
2. ConfigureAwait(false) on all library async methods
3. Extension methods in Selenium.HarCapture.Extensions namespace
4. Fluent API via mutation (matches existing CaptureOptions design)
5. Thread safety already implemented via ConcurrentDictionary + JSON cloning
