# Performance Tuning Guide

This guide explains which `CaptureOptions` settings affect throughput and memory, with concrete
recommendations for parallel and long-running capture scenarios.

## Introduction

HAR capture adds approximately 30% wall-time overhead when running 10 parallel Chrome instances.
The primary cause is CDP `Network.getResponseBody` calls: every response body retrieved requires
a round-trip on the Chrome DevTools Protocol WebSocket. Those round-trips compete with navigation
commands issued by your test code.

This guide shows how to reduce that overhead to an acceptable level for your test suite.

---

## Options-Impact Table

| Option | Default | Performance Impact | Recommendation |
|--------|---------|-------------------|----------------|
| `ResponseBodyScope` | `All` | Every response body triggers a CDP WebSocket round-trip | Use `PagesAndApi` for API test suites; `None` for headers-only capture |
| `MaxResponseBodySize` | 0 (unlimited) | Large bodies increase memory and serialization time | Set to `1_048_576` (1 MB) when responses include large downloads |
| `WithOutputFile()` / streaming | off | In-memory capture accumulates all entries in RAM | Use for sessions with >100 entries or with large bodies |
| `WithCompression()` | off | Reduces disk I/O, adds CPU at finalization | Only use with `WithOutputFile()`; skip for short captures |
| `MaxWebSocketFramesPerConnection` | 0 (unlimited) | Long-lived WebSocket connections accumulate frames in memory | Set to 100–1000 for long-running connections |
| `MaxOutputFileSize` | 0 (unlimited) | Prevents disk exhaustion for runaway captures | Use as a safety valve in production-like environments |
| `WithLogFile()` | off | Minimal cost (file I/O per event) | Enable only for diagnostics; disable in production |

---

## Detailed Sections

### ResponseBodyScope

`ResponseBodyScope` controls which responses trigger a `Network.getResponseBody` CDP command.
Each command is a synchronous round-trip on the CDP WebSocket. Under parallel load, these round-trips
compete with browser navigation, causing `WebDriverWait.Until()` timeouts.

**Values:**

| Value | Retrieved MIME types |
|-------|---------------------|
| `All` (default) | Every response — images, fonts, scripts, JSON, HTML |
| `PagesAndApi` | `text/html`, `application/json`, `application/xml`, `text/xml`, `multipart/form-data`, `application/x-www-form-urlencoded` |
| `TextContent` | `text/*` (prefix), `application/json`, `application/xml`, `application/javascript`, `application/x-javascript` |
| `None` | No bodies — only headers and timings captured |

**Code example:**

```csharp
// Recommended for most API test suites: HTML + JSON/XML only
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.PagesAndApi);

using var capture = new HarCapture(driver, options);
await capture.StartAsync("page1", "Home");
driver.Navigate().GoToUrl("https://example.com");
var har = await capture.StopAsync();
```

```csharp
// Headers and timings only — lowest possible overhead
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.None);
```

**When to use `PagesAndApi`:** Your test suite navigates HTML pages and calls JSON APIs, but does not
need image or font bodies in the HAR.

**When to use `None`:** You only need request/response timing data or header inspection.

**Expected impact:** Switching from `All` to `PagesAndApi` on a typical web application reduces
CDP WebSocket contention by approximately 60% (images, fonts, and scripts no longer trigger body
retrieval). In parallel capture scenarios with 10 Chrome instances, this eliminates the page
navigation competition that causes `WebDriverWait.Until()` timeouts.

**Adding extra MIME types:** If you use `PagesAndApi` but also need SVG bodies, add them without
switching to `TextContent`:

```csharp
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.PagesAndApi)
    .WithResponseBodyMimeFilter("image/svg+xml");
```

---

### MaxResponseBodySize

Caps the number of bytes stored per response body. Bodies larger than the limit are truncated.

```csharp
var options = new CaptureOptions()
    .WithMaxResponseBodySize(1_048_576); // 1 MB limit
```

**When to use:** Your application serves large file downloads, binary assets, or paginated data dumps
that would otherwise inflate the in-memory HAR or the streaming output file.

**When not to use:** Do not set `MaxResponseBodySize` together with `ResponseBodyScope.None` —
the validator will reject this combination at `StartAsync()` because no bodies are retrieved
when scope is `None`.

**Expected impact:** Prevents unbounded memory growth during long sessions with large payload APIs.

---

### WithOutputFile() — Streaming Mode

By default, HAR capture keeps every entry in memory until `StopAsync()` returns the final `Har`
object. For sessions with many entries or large bodies, this causes memory growth proportional
to total payload size.

Streaming mode writes each entry to a file as it arrives using a channel-based producer-consumer
pipeline. Memory usage is O(1) per entry rather than O(N total).

```csharp
var options = new CaptureOptions()
    .WithOutputFile("/tmp/session.har")
    .WithResponseBodyScope(ResponseBodyScope.PagesAndApi)
    .WithMaxResponseBodySize(1_048_576);

using var capture = new HarCapture(driver, options);
await capture.StartAsync();
driver.Navigate().GoToUrl("https://example.com");

// Use parameterless StopAndSaveAsync() in streaming mode
await capture.StopAndSaveAsync();

// Actual written path (includes .gz suffix if compression is enabled)
Console.WriteLine(capture.FinalOutputFilePath);
```

**When to use:**
- Sessions expected to produce more than 100 HAR entries
- Large response bodies that would exceed available RAM
- Long-running capture sessions (load tests, smoke test suites)

**When not to use:**
- Short unit tests that assert on response bodies immediately — in-memory `StopAsync()` returning
  a `Har` object is simpler
- `ForceSeleniumNetwork()` mode — streaming requires CDP body retrieval which INetwork does not provide

**In-memory comparison:**

```csharp
// In-memory (default): all entries held in RAM
var har = await capture.StopAsync();
await HarSerializer.SaveAsync(har, "/tmp/session.har");
```

---

### WithCompression()

Compresses the output file to gzip format at finalization. The `.gz` suffix is appended to the
configured `OutputFilePath` if not already present.

```csharp
var options = new CaptureOptions()
    .WithOutputFile("/tmp/session.har")
    .WithCompression(); // output will be /tmp/session.har.gz
```

**When to use:** Disk I/O is a bottleneck (e.g., CI environments with slow disk or large captures
exceeding 100 MB).

**When not to use:**
- Without `WithOutputFile()` — compression is only supported in streaming mode.
  Using `WithCompression()` alone without `WithOutputFile()` will raise an `ArgumentException` from
  `ForceSeleniumNetwork()` conflict validation if `ForceSeleniumNetworkApi` is also set, or simply
  have no effect.
- When downstream tooling needs to read the HAR immediately without decompression.

**Expected impact:** Compression ratio typically 5:1 to 10:1 for HAR files (JSON is highly
compressible). Adds CPU time at finalization, not during capture.

---

### MaxWebSocketFramesPerConnection

Caps the number of frames stored per WebSocket connection. When the limit is reached, the oldest
frames are evicted (oldest-first).

```csharp
var options = new CaptureOptions()
    .WithWebSocketCapture()
    .WithMaxWebSocketFramesPerConnection(500); // keep last 500 frames per connection
```

**When to use:** Your application uses long-lived WebSocket connections (chat, live dashboards,
financial feeds) that generate hundreds or thousands of frames per session.

**When not to use:** Short-lived WebSocket connections with predictable frame counts. Setting a
cap on those adds unnecessary complexity.

**Default:** 0 (unlimited — all frames retained). Negative values are rejected at `StartAsync()`.

**Expected impact:** Prevents unbounded memory growth for long-lived WebSocket sessions without
disabling WebSocket capture entirely.

---

### MaxOutputFileSize

Aborts streaming output when the file exceeds the specified byte limit. After the limit is
exceeded, subsequent entries are silently dropped. The file remains valid JSON with a complete
footer.

```csharp
var options = new CaptureOptions()
    .WithOutputFile("/tmp/session.har")
    .WithMaxOutputFileSize(104_857_600); // abort after 100 MB
```

**When to use:** Production-like environments where a runaway capture could fill the disk.

**When not to use:** Test suites where incomplete captures would invalidate assertions.

**Requires:** `WithOutputFile()` must be set. Using `WithMaxOutputFileSize` without
`OutputFilePath` throws at `StartAsync()`.

---

### WithLogFile() — Diagnostic Logging

Writes timestamped diagnostic messages to a file for post-capture analysis.

```csharp
var options = new CaptureOptions()
    .WithLogFile("/tmp/capture.log");
```

**Performance cost:** Minimal — one file write per CDP event. Not recommended for sustained
high-frequency capture in performance-sensitive scenarios.

**Primary use:** Diagnosing body retrieval timing and identifying which entries are slow.
See the [Troubleshooting Guide](troubleshooting.md) for details.

---

## Parallel Capture Tips

Running multiple Chrome instances simultaneously amplifies CDP WebSocket contention:

### Why parallel capture is slower

CDP body retrieval from page N competes with navigation commands on page N+1. Each
`Network.getResponseBody` call occupies the WebSocket briefly. With 10 parallel instances
and `ResponseBodyScope.All`, body retrieval tasks from all pages compete simultaneously.
The measured overhead is approximately 30% wall-time for 10 instances on a typical test suite.

### Do NOT use SemaphoreSlim throttling

A common instinct is to add a `SemaphoreSlim` to limit concurrent body retrievals:

```csharp
// DO NOT DO THIS
await _semaphore.WaitAsync();
try { await GetResponseBodyAsync(...); }
finally { _semaphore.Release(); }
```

This creates a **convoy effect**: all instances wait on the semaphore, turning parallel work
into serialized work. Measured result: `SemaphoreSlim(3)` increases capture time from 3.4s
to 8.9s — a 2.6x regression.

### Recommended approach for parallel captures

1. **Reduce body scope:** Use `ResponseBodyScope.PagesAndApi` or `ResponseBodyScope.None`
   to minimize the number of CDP body retrieval calls.

2. **Avoid `WebDriverWait.Until()` under heavy parallel load:** CDP round-trips can delay
   Selenium's own commands enough to trigger wait timeouts. Use implicit waits with
   `driver.Manage().Timeouts().ImplicitWait` combined with `Thread.Sleep` as a fallback.

3. **Use streaming mode for long sessions:** Streaming's O(1) memory footprint prevents GC
   pressure from competing with CDP processing under load.

```csharp
// Recommended pattern for parallel Chrome instances
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.PagesAndApi)
    .WithOutputFile($"/tmp/session-{instanceId}.har");

using var capture = new HarCapture(driver, options);
await capture.StartAsync();

driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
driver.Navigate().GoToUrl("https://example.com");
Thread.Sleep(500); // brief settle; avoid WebDriverWait.Until() under parallel load

await capture.StopAndSaveAsync();
```

---

## Quick Reference

**If you only read one thing:**

- **Reduce CDP contention** — Set `ResponseBodyScope.PagesAndApi`. This is the single highest-impact
  change for parallel and high-frequency test suites. Reduces CDP WebSocket round-trips by ~60%
  on typical web apps.

- **Prevent memory growth** — Use `WithOutputFile()` for sessions with >100 entries, and
  `WithMaxResponseBodySize(1_048_576)` when responses include large payloads.

- **Diagnose before optimizing** — Enable `WithLogFile()` to measure body retrieval timing before
  applying other tuning. You may discover that a specific endpoint, not overall scope, is the
  bottleneck.
