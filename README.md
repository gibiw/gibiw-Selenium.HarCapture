# Selenium.HarCapture

[![CI](https://github.com/gibiw/gibiw-Selenium.HarCapture/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/gibiw/gibiw-Selenium.HarCapture/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Selenium.HarCapture.svg)](https://www.nuget.org/packages/Selenium.HarCapture/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Selenium.HarCapture.svg)](https://www.nuget.org/packages/Selenium.HarCapture/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.0-purple.svg)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)

A .NET library for capturing HTTP Archive (HAR 1.2) files from Selenium WebDriver sessions. Supports Chrome DevTools Protocol (CDP) for detailed timings and response bodies, with automatic fallback to Selenium's INetwork API for cross-browser compatibility.

## Features

- **HAR 1.2 compliant** output, openable in Chrome/Firefox DevTools
- **Two capture strategies**: CDP (detailed timings, response bodies) and INetwork (cross-browser)
- **Automatic CDP version discovery** via assembly scanning — zero code changes when Selenium updates
- **Automatic strategy selection** with manual override
- **URL filtering** via glob patterns (include/exclude)
- **Multi-page capture** support
- **Response body size limiting** to control memory usage
- **Streaming capture to file** with O(1) memory — async channel-based writer, always-valid HAR, crash-safe
- **WebSocket capture** — captures WS frames via CDP `_webSocketMessages` extension (Chrome DevTools HAR compatible)
- **Sensitive data redaction** — mask headers, cookies, query parameters, response/request bodies, and WebSocket payloads at capture time with wildcard and regex support
- **Built-in PII patterns** — `HarPiiPatterns` ships regex for credit cards, emails, SSNs, phone numbers, and IP addresses
- **Redaction audit trail** — aggregate redaction counts logged at session stop (body, WebSocket, skipped)
- **Response body scope filtering** — skip expensive CDP `getResponseBody` calls for unwanted MIME types (CSS, JS, images, fonts) to reduce WebSocket contention and speed up navigation
- **Bounded body retrieval concurrency** — channel-based worker pool (3 workers) replaces unbounded `Task.Run` for predictable CDP load
- **Gzip compression** — automatic `.gz` detection in `HarSerializer`, and `WithCompression()` for streaming mode
- **File-based diagnostic logging** via `WithLogFile()`
- **Browser auto-detection** from WebDriver capabilities with manual override
- **Fluent configuration API**
- **One-liner capture** via extension methods
- **Serialization** to/from JSON and `.gz` files with auto directory creation
- **Pause/Resume capture** — skip sections (e.g., auth flows) without stopping the session
- **Output file size limit** — `WithMaxOutputFileSize()` aborts streaming gracefully when file exceeds limit
- **Progress events** — `EntryWritten` event delivers entry count, page ref, and URL as entries are captured
- **Custom metadata** — embed key-value data in HAR via `WithCustomMetadata()` (`_custom` extension field)
- **Request/response size metrics** — `_requestBodySize` and `_responseBodySize` extension fields with on-wire byte counts
- **HAR extensions** — `_initiator` (request origin), `_securityDetails` (TLS certificate), cache hit/miss status from CDP
- **WebSocket frame cap** — `WithMaxWebSocketFramesPerConnection()` limits stored frames per connection (oldest-first eviction)
- **CaptureOptions validation** — conflicting or invalid options detected at `StartAsync()` with actionable error messages
- **HAR Validation API** — `HarValidator.Validate(har)` checks HAR 1.2 structural correctness before saving
- **Dual disposal** pattern (IDisposable + IAsyncDisposable) with full async channel drain

## Installation

```bash
dotnet add package Selenium.HarCapture
```

**Requirements:**
- .NET Standard 2.0+
- Selenium.WebDriver 4.40.0+
- Chrome/Chromium browser (for CDP strategy)

## Quick Start

### Basic Capture

```csharp
using Selenium.HarCapture;
using Selenium.HarCapture.Capture;

var driver = new ChromeDriver();

// Start capturing — entries stream to file immediately, O(1) memory
var options = new CaptureOptions().WithOutputFile("traffic.har");
using var capture = new HarCapture(driver, options);
capture.Start();

// Navigate and interact
driver.Navigate().GoToUrl("https://example.com");

// Stop and save (file is already written, just finalize)
capture.StopAndSave();
```

### One-Liner Capture (Extension Methods)

```csharp
using Selenium.HarCapture.Extensions;

// Async one-liner
var har = await driver.CaptureHarAsync(async () =>
{
    driver.Navigate().GoToUrl("https://example.com");
    await Task.Delay(1000); // wait for async requests
});

// Sync one-liner
var har = driver.CaptureHar(() =>
{
    driver.Navigate().GoToUrl("https://example.com");
    Thread.Sleep(1000);
});
```

### Start and Control Capture

```csharp
using Selenium.HarCapture.Extensions;

// Start capture via extension method
using var capture = driver.StartHarCapture();

driver.Navigate().GoToUrl("https://example.com");

// Check state
Console.WriteLine(capture.IsCapturing);       // True
Console.WriteLine(capture.ActiveStrategyName); // "CDP" or "INetwork"

// Get snapshot while capture continues
var snapshot = capture.GetHar();

// Stop and get final HAR
var har = capture.Stop();
```

## Configuration

### CaptureOptions

```csharp
using Selenium.HarCapture.Capture;

var options = new CaptureOptions();
```

All options support a fluent API:

```csharp
var options = new CaptureOptions()
    .WithCaptureTypes(CaptureType.All)
    .WithMaxResponseBodySize(1_000_000)       // 1 MB limit
    .WithUrlIncludePatterns("**/api/**")       // only API calls
    .WithUrlExcludePatterns("**/*.png", "**/*.css") // skip static assets
    .WithResponseBodyScope(ResponseBodyScope.PagesAndApi) // only retrieve HTML/JSON/XML bodies
    .WithResponseBodyMimeFilter("image/png")  // additionally retrieve PNG bodies
    .WithCreatorName("MyTestSuite")
    .WithBrowser("Chrome", "131.0.6778.86")   // manual browser override (auto-detected by default)
    .WithOutputFile("capture.har")             // streaming mode (O(1) memory)
    .WithMaxOutputFileSize(50_000_000)         // abort streaming at 50 MB
    .WithSensitiveHeaders("Authorization")     // redact header values with [REDACTED]
    .WithSensitiveCookies("session_id")        // redact cookie values
    .WithSensitiveQueryParams("api_key")       // redact query params (supports wildcards)
    .WithSensitiveBodyPatterns(HarPiiPatterns.CreditCard) // redact PII in bodies
    .WithWebSocketCapture()                    // capture WebSocket frames (CDP only)
    .WithMaxWebSocketFramesPerConnection(1000) // cap frames per WS connection
    .WithCustomMetadata("env", "staging")      // embed metadata in HAR
    .WithCompression()                         // gzip compress on finalization (.har → .har.gz)
    .WithLogFile("capture.log")                // diagnostic logging
    .ForceSeleniumNetwork();                   // force INetwork API
```

#### CaptureType (flags enum)

| Value | Description |
|---|---|
| `None` | No data captured |
| `RequestHeaders` | HTTP request headers |
| `RequestCookies` | HTTP request cookies |
| `RequestContent` | Request body (text) |
| `RequestBinaryContent` | Request body (binary) |
| `ResponseHeaders` | HTTP response headers |
| `ResponseCookies` | HTTP response cookies |
| `ResponseContent` | Response body (text) |
| `ResponseBinaryContent` | Response body (binary) |
| `Timings` | Detailed timing breakdown (send/wait/receive/dns/connect/ssl) |
| `ConnectionInfo` | Server IP address, connection ID |
| `WebSocket` | WebSocket frames (CDP only, opt-in) |
| `HeadersAndCookies` | All headers + cookies |
| `AllText` | Headers, cookies, text content, timings **(default)** |
| `All` | Everything including binary content and WebSocket |

#### URL Filtering

Glob patterns for URL filtering:

```csharp
// Only capture API requests
var options = new CaptureOptions()
    .WithUrlIncludePatterns("**/api/**");

// Capture everything except static assets
var options = new CaptureOptions()
    .WithUrlExcludePatterns("**/*.png", "**/*.jpg", "**/*.css", "**/*.js");
```

Exclude patterns take precedence over include patterns.

#### Response Body Size Limit

```csharp
// Limit response bodies to 500 KB
var options = new CaptureOptions()
    .WithMaxResponseBodySize(512_000);
```

#### Response Body Scope

By default, the library retrieves response bodies for all requests. This means every CSS, JS, image, and font triggers a CDP `Network.getResponseBody` call, competing with navigation traffic over the WebSocket connection.

Use `ResponseBodyScope` to skip body retrieval for resource types you don't need:

```csharp
// Only retrieve bodies for HTML pages and API responses (JSON, XML)
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.PagesAndApi);

// Retrieve all text content (text/*, application/json, application/xml, application/javascript)
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.TextContent);

// Skip all body retrieval — headers and timings only
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.None);
```

| Scope | MIME types retrieved |
|---|---|
| `All` | Everything **(default)** |
| `PagesAndApi` | `text/html`, `application/json`, `application/xml`, `text/xml`, `multipart/form-data`, `application/x-www-form-urlencoded` |
| `TextContent` | `text/*` (prefix match), `application/json`, `application/xml`, `application/javascript`, `application/x-javascript` |
| `None` | Nothing (headers and timings only) |

Extra MIME types can be added on top of any scope:

```csharp
// PagesAndApi + additionally capture PNG and SVG images
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.PagesAndApi)
    .WithResponseBodyMimeFilter("image/png", "image/svg+xml");

// None + only retrieve specific types
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.None)
    .WithResponseBodyMimeFilter("application/json");
```

#### Sensitive Data Redaction

Redact sensitive values from captured HAR data at capture time — the original values are never stored:

```csharp
var options = new CaptureOptions()
    .WithSensitiveHeaders("Authorization", "X-API-Key")
    .WithSensitiveCookies("session_id", "auth_token")
    .WithSensitiveQueryParams("api_key", "token_*")
    .WithSensitiveBodyPatterns(
        HarPiiPatterns.CreditCard,
        HarPiiPatterns.Email,
        @"secret[_-]?key\s*[:=]\s*\S+"   // custom regex
    );
```

Matched values are replaced with `[REDACTED]` in headers, cookies, query string parameters, request/response bodies, and WebSocket payloads.

- **Headers and cookies**: case-insensitive exact name matching
- **Query parameters**: support glob wildcards (`*` matches any characters, `?` matches a single character) — e.g. `token_*` matches `token_access`, `token_refresh`, etc.
- **Body patterns**: regex with 100ms timeout per match; bodies over 512 KB are skipped (ReDoS protection)
- **Built-in PII patterns** (`HarPiiPatterns`): `CreditCard`, `Email`, `Ssn`, `Phone`, `IpAddress`
- **Audit trail**: aggregate redaction counts (body, WebSocket, skipped) logged at session stop via `WithLogFile()`

#### Capture Strategy

By default, the library uses CDP if available and falls back to INetwork. To force INetwork:

```csharp
var options = new CaptureOptions().ForceSeleniumNetwork();
```

| Feature | CDP | INetwork |
|---|---|---|
| Detailed timings (dns, connect, ssl, send, wait, receive) | Yes | No (basic send/wait/receive only) |
| Response body capture | Yes | Yes |
| WebSocket frame capture | Yes | No |
| Cross-browser support | Chrome only | All browsers |
| Requires specific Chrome version match | Yes | No |

### Browser Auto-Detection

The library automatically detects the browser name and version from WebDriver capabilities — no configuration needed:

```csharp
var driver = new ChromeDriver();
using var capture = new HarCapture(driver);
capture.Start();

// HAR output will include: "browser": { "name": "Chrome", "version": "131.0.6778.86" }
```

Browser names are normalized for consistency (e.g., `chrome` → `Chrome`, `MicrosoftEdge` → `Microsoft Edge`).

To override auto-detected values:

```csharp
var options = new CaptureOptions()
    .WithBrowser("CustomBrowser", "1.0");

using var capture = new HarCapture(driver, options);
```

### Streaming Capture to File

For large captures or memory-constrained environments, use streaming mode. Entries are written via an async channel-based producer-consumer to the file as they arrive — O(1) memory, and the file is always a valid HAR (crash-safe).

```csharp
var options = new CaptureOptions()
    .WithOutputFile(@"C:\Logs\capture.har")    // enables streaming mode
    .WithCompression()                          // optional: gzip on finalization → .har.gz
    .WithMaxOutputFileSize(50_000_000)          // optional: abort at 50 MB
    .WithLogFile(@"C:\Logs\capture.log")        // optional diagnostics
    .WithMaxResponseBodySize(5_000_000);

await using var capture = new HarCapture(driver, options);
capture.Start("page1", "Home");

driver.Navigate().GoToUrl("https://example.com");
// entries streamed to file immediately, file is always valid HAR

capture.NewPage("page2", "Dashboard");
driver.Navigate().GoToUrl("https://example.com/dashboard");

capture.StopAndSave(); // completes file, O(1) memory
// with WithCompression(): capture.har is compressed to capture.har.gz
```

**Note**: Both streaming mode (`WithOutputFile`) and the serialization methods (`HarSerializer.SaveAsync`/`Save`) automatically create any intermediate directories in the file path if they don't exist.

| | In-memory (default) | Streaming (`WithOutputFile`) |
|---|---|---|
| Memory | O(N) entries in RAM | O(1), each entry serialized directly to file |
| Stop | `Stop()` or `StopAndSave(path)` | `StopAndSave()` (parameterless) |
| Crash safety | Data lost | File valid after each entry |
| `GetHar()` | Full snapshot | Metadata only (pages, creator) |

### Multi-Page Capture

```csharp
using var capture = new HarCapture(driver);
capture.Start();

// Page 1
capture.NewPage("page1", "Home Page");
driver.Navigate().GoToUrl("https://example.com");

// Page 2
capture.NewPage("page2", "Dashboard");
driver.Navigate().GoToUrl("https://example.com/dashboard");

var har = capture.Stop();

// har.Log.Pages contains 2 pages
// Each entry's PageRef links to the correct page
```

### Fluent Configuration via Extension Methods

```csharp
using var capture = driver.StartHarCapture(options =>
{
    options.ForceSeleniumNetwork();
    options.WithCreatorName("MyTool");
    options.WithCaptureTypes(CaptureType.All);
});
```

## Serialization

### Save and Load HAR Files

```csharp
using Selenium.HarCapture.Serialization;

// Save to file (indented JSON by default)
await HarSerializer.SaveAsync(har, "output.har");

// Save to nested directories (auto-created if they don't exist)
await HarSerializer.SaveAsync(har, @"C:\Logs\2024\January\capture.har");

// Save compact JSON
await HarSerializer.SaveAsync(har, "output.har", writeIndented: false);

// Save as gzip — auto-detected by .gz extension
await HarSerializer.SaveAsync(har, "output.har.gz");

// Load from file (gzip auto-detected by .gz extension)
var loaded = await HarSerializer.LoadAsync("output.har.gz");

// Serialize to string
string json = HarSerializer.Serialize(har);

// Deserialize from string
var har = HarSerializer.Deserialize(json);
```

**Note**: `SaveAsync()`/`Save()` automatically create intermediate directories and auto-detect `.gz` extension for transparent gzip compression/decompression.

The output conforms to HAR 1.2 specification and can be imported into:
- **Chrome DevTools**: Network tab > Import HAR file
- **Firefox DevTools**: Network tab > Import HAR
- **HAR Viewer**: [http://www.softwareishard.com/har/viewer/](http://www.softwareishard.com/har/viewer/)

### Pause/Resume Capture

Skip sections of traffic (e.g., auth flows) without stopping the session:

```csharp
using var capture = new HarCapture(driver);
capture.Start();

driver.Navigate().GoToUrl("https://example.com/login");
capture.Pause();   // entries during auth are dropped, not queued

driver.FindElement(By.Id("username")).SendKeys("user");
driver.FindElement(By.Id("password")).SendKeys("pass");
driver.FindElement(By.Id("login")).Click();

capture.Resume();  // capture resumes normally
driver.Navigate().GoToUrl("https://example.com/dashboard");

var har = capture.Stop(); // contains only pre-login and post-login traffic
```

Both `Pause()` and `Resume()` are idempotent — calling either multiple times is safe.

### Progress Events

Monitor capture progress in real time:

```csharp
using var capture = new HarCapture(driver);
capture.EntryWritten += (sender, progress) =>
{
    Console.WriteLine($"[{progress.EntryCount}] {progress.EntryUrl} (page: {progress.CurrentPageRef})");
};

capture.Start();
driver.Navigate().GoToUrl("https://example.com");
```

### Custom Metadata

Embed arbitrary key-value metadata in the HAR output under the `_custom` extension field:

```csharp
var options = new CaptureOptions()
    .WithCustomMetadata("environment", "staging")
    .WithCustomMetadata("transaction_id", "txn-12345")
    .WithCustomMetadata("test_name", "checkout_flow");

using var capture = new HarCapture(driver, options);
```

### HAR Validation

Validate HAR objects for structural correctness before saving:

```csharp
using Selenium.HarCapture.Serialization;

var result = HarValidator.Validate(har);

if (!result.IsValid)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"{error.Severity}: {error.Field} — {error.Message}");
}

// Validation modes: Strict, Standard (default), Lenient
var strictResult = HarValidator.Validate(har, HarValidationMode.Strict);
```

### HAR Extension Fields (CDP)

When using the CDP strategy, entries include additional extension fields:

| Field | Type | Description |
|---|---|---|
| `_initiator` | `HarInitiator` | Request origin — type, script URL, line number |
| `_securityDetails` | `HarSecurityDetails` | TLS certificate — protocol, cipher, subject, issuer, validity |
| `_requestBodySize` | `long` | On-wire request body bytes |
| `_responseBodySize` | `long` | On-wire response body bytes (compressed) |
| `_webSocketMessages` | `HarWebSocketMessage[]` | WebSocket frames (Chrome DevTools format) |

Cache hit/miss information is populated in `HarCache.BeforeRequest` for 304 or cache-served responses.

## Disposal

`HarCapture` implements both `IDisposable` and `IAsyncDisposable`:

```csharp
// Sync disposal
using var capture = new HarCapture(driver);

// Async disposal
await using var capture = new HarCapture(driver);
```

- `await using` ensures full async channel drain in streaming mode (`HarCapture` → `HarCaptureSession` → `HarStreamWriter`)
- Disposing while capturing automatically stops capture
- Double disposal is safe (no-throw)
- Accessing methods after disposal throws `ObjectDisposedException`

## CancellationToken Support

All async methods accept an optional `CancellationToken` for cooperative cancellation:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await capture.StartAsync(cancellationToken: cts.Token);
var har = await capture.StopAsync(cts.Token);
await capture.StopAndSaveAsync(cts.Token);

var har = await driver.CaptureHarAsync(async () =>
{
    driver.Navigate().GoToUrl("https://example.com");
}, cancellationToken: cts.Token);

await HarSerializer.SaveAsync(har, "output.har", cancellationToken: cts.Token);
var loaded = await HarSerializer.LoadAsync("output.har", cts.Token);
```

## HAR Model

The library provides a complete HAR 1.2 object model:

| Class | Description |
|---|---|
| `Har` | Root object containing `Log` |
| `HarLog` | Version, creator, browser, pages, entries |
| `HarEntry` | Single request/response pair with timings (+ optional `_webSocketMessages`) |
| `HarWebSocketMessage` | WebSocket frame (type, time, opcode, data) |
| `HarRequest` | Method, URL, HTTP version, headers, cookies, query string, body |
| `HarResponse` | Status, headers, cookies, content, redirect URL |
| `HarContent` | Response body (size, MIME type, text, encoding) |
| `HarTimings` | Timing breakdown (blocked, dns, connect, send, wait, receive, ssl) |
| `HarPage` | Page metadata (id, title, started time, page timings) |
| `HarPageTimings` | Page timing info (onContentLoad, onLoad) |
| `HarCreator` | Creator tool name and version |
| `HarBrowser` | Browser name and version |
| `HarCookie` | Cookie details (name, value, path, domain, expires, httpOnly, secure) |
| `HarHeader` | Header name-value pair |
| `HarQueryString` | Query parameter name-value pair |
| `HarPostData` | POST data (MIME type, text, params) |
| `HarParam` | Posted data parameter (name, value, fileName, contentType) |
| `HarCache` | Cache usage info (beforeRequest, afterRequest) |
| `HarCacheEntry` | Detailed cache entry (expires, lastAccess, eTag, hitCount) |
| `HarInitiator` | Request initiator (type, script URL, line number) — `_initiator` extension |
| `HarSecurityDetails` | TLS certificate (protocol, cipher, subject, issuer, validity) — `_securityDetails` extension |
| `HarCaptureProgress` | Progress event data (entry count, page ref, URL) |
| `HarValidationResult` | Validation findings (errors, warnings, `IsValid`) |
| `HarValidationError` | Single validation finding (field path, message, severity) |

## Project Structure

```
Selenium.HarCapture/
├── src/
│   └── Selenium.HarCapture/              # Main library (netstandard2.0)
│       ├── HarCapture.cs                 # Public facade
│       ├── Capture/
│       │   ├── CaptureOptions.cs         # Configuration
│       │   ├── CaptureType.cs            # Flags enum
│       │   ├── ResponseBodyScope.cs      # Body retrieval scope enum
│       │   ├── HarCaptureSession.cs      # Session orchestrator
│       │   ├── Internal/
│       │   │   ├── HarStreamWriter.cs    # Async channel-based incremental HAR writer
│       │   │   ├── BrowserCapabilityExtractor.cs # Browser auto-detection
│       │   │   ├── FileLogger.cs         # Diagnostic file logging
│       │   │   ├── MimeTypeMatcher.cs    # MIME-based body retrieval filter
│       │   │   ├── UrlPatternMatcher.cs  # URL glob filtering
│       │   │   ├── WebSocketFrameAccumulator.cs # WebSocket frame accumulator
│       │   │   ├── RequestResponseCorrelator.cs
│       │   │   ├── CdpTimingMapper.cs
│       │   │   ├── HttpParsingHelper.cs  # Shared HTTP header/cookie parsing
│       │   │   ├── SensitiveDataRedactor.cs # Header/cookie/query param redaction
│       │   │   └── Cdp/                  # CDP reflection adapters
│       │   └── Strategies/
│       │       ├── INetworkCaptureStrategy.cs
│       │       ├── StrategyFactory.cs
│       │       ├── CdpNetworkCaptureStrategy.cs
│       │       └── SeleniumNetworkCaptureStrategy.cs
│       ├── Extensions/
│       │   └── WebDriverExtensions.cs    # One-liner methods
│       ├── Models/                        # HAR 1.2 data model
│       └── Serialization/
│           └── HarSerializer.cs           # JSON serialization
└── tests/
    ├── Selenium.HarCapture.Tests/             # Unit tests (460 tests)
    ├── Selenium.HarCapture.IntegrationTests/  # Integration tests (38 tests)
    └── Selenium.HarCapture.StressTests/       # Stress tests (>500 request capture, dispose mid-capture)
```

## Running Tests

### Prerequisites

- .NET 10.0 SDK
- Google Chrome browser installed (Selenium Manager downloads chromedriver automatically)

### Unit Tests

```bash
dotnet test tests/Selenium.HarCapture.Tests
```

Unit tests use mocked dependencies and do not require a browser.

### Integration Tests

```bash
dotnet test tests/Selenium.HarCapture.IntegrationTests
```

Integration tests launch a real Chrome browser (headless) and a local ASP.NET Core test server. They verify end-to-end HAR capture with real HTTP traffic.

#### What Integration Tests Cover

| Test Class | Tests | Description |
|---|---|---|
| `BasicCaptureTests` | 2 | URL capture, status codes |
| `CdpCaptureTests` | 5 | CDP entries, serialization, headers, subresources, consistency |
| `INetworkCaptureTests` | 5 | INetwork entries, serialization, response body, large responses, sync save |
| `UrlFilteringTests` | 2 | Include/exclude URL glob patterns |
| `MultiPageCaptureTests` | 1 | Multi-page capture with correct PageRef |
| `ExtensionMethodTests` | 4 | StartHarCapture, fluent config, CaptureHarAsync, CaptureHar |
| `INetworkFallbackTests` | 1 | ForceSeleniumNetwork with separate Chrome instance |
| `SerializationRoundtripTests` | 2 | Save/Load roundtrip, HAR 1.2 structure validation |
| `DisposeCleanupTests` | 3 | Dispose stops capture, ObjectDisposedException, double dispose |
| `RedactionTests` | 4 | Header, cookie, and query parameter redaction |
| `ResponseBodyScopeTests` | 4 | Body scope filtering by MIME type |
| `CancellationTokenTests` | 3 | CancellationToken propagation in async methods |

#### Chrome Version Compatibility

The CDP strategy requires Chrome version matching the Selenium.WebDriver DevTools version. The library automatically discovers all available CDP versions at runtime via assembly scanning — no code changes needed when Selenium adds or drops CDP versions. Tests that require CDP automatically detect the installed Chrome version and skip gracefully if incompatible.

The INetwork strategy works with any Chrome version.

#### Running by Category

```bash
# Integration tests only
dotnet test --filter "Category=Integration"

# All tests (unit + integration)
dotnet test
```

#### Integration Test Infrastructure

- **TestWebServer**: ASP.NET Core minimal API on `127.0.0.1:0` (dynamic port). Provides endpoints: `/`, `/api/data`, `/api/large`, `/api/cookies`, `/with-fetch`, `/page2`, `/redirect`, `/api/slow`
- **IntegrationTestBase**: Creates a fresh Chrome headless instance per test class. Provides `NavigateTo()`, `WaitForNetworkIdle()`, `StartCapture()`, `IsCdpCompatible()` helpers
- **IntegrationTestCollection**: Shares `TestWebServer` across test classes via xUnit `ICollectionFixture`

## Documentation

- [Performance Tuning Guide](docs/performance-tuning.md) — optimize capture speed, memory usage, and CDP WebSocket contention
- [Troubleshooting Guide](docs/troubleshooting.md) — diagnose and fix common issues (timeouts, memory growth, empty entries)
- [Migration Guide: v0.2.x to v0.3.x](docs/migration-v02-v03.md) — breaking changes, behavioral changes, and new features

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes.

## License

See [LICENSE](LICENSE) for details.
