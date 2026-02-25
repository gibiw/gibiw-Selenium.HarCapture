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
- **Streaming capture to file** with O(1) memory — always-valid HAR, crash-safe
- **File-based diagnostic logging** via `WithLogFile()`
- **Fluent configuration API**
- **One-liner capture** via extension methods
- **Serialization** to/from JSON files
- **Dual disposal** pattern (IDisposable + IAsyncDisposable)

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
    .WithCreatorName("MyTestSuite")
    .WithOutputFile("capture.har")             // streaming mode (O(1) memory)
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
| `HeadersAndCookies` | All headers + cookies |
| `AllText` | Headers, cookies, text content, timings **(default)** |
| `All` | Everything including binary content |

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

#### Capture Strategy

By default, the library uses CDP if available and falls back to INetwork. To force INetwork:

```csharp
var options = new CaptureOptions().ForceSeleniumNetwork();
```

| Feature | CDP | INetwork |
|---|---|---|
| Detailed timings (dns, connect, ssl, send, wait, receive) | Yes | No (basic send/wait/receive only) |
| Response body capture | Yes | Yes |
| Cross-browser support | Chrome only | All browsers |
| Requires specific Chrome version match | Yes | No |

### Streaming Capture to File

For large captures or memory-constrained environments, use streaming mode. Entries are written directly to the file as they arrive — O(1) memory, and the file is always a valid HAR (crash-safe).

```csharp
var options = new CaptureOptions()
    .WithOutputFile(@"C:\Logs\capture.har")    // enables streaming mode
    .WithLogFile(@"C:\Logs\capture.log")        // optional diagnostics
    .WithMaxResponseBodySize(5_000_000);

using var capture = new HarCapture(driver, options);
capture.Start("page1", "Home");

driver.Navigate().GoToUrl("https://example.com");
// entries streamed to file immediately, file is always valid HAR

capture.NewPage("page2", "Dashboard");
driver.Navigate().GoToUrl("https://example.com/dashboard");

capture.StopAndSave(); // completes file, O(1) memory
```

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

// Save compact JSON
await HarSerializer.SaveAsync(har, "output.har", writeIndented: false);

// Load from file
var loaded = await HarSerializer.LoadAsync("output.har");

// Serialize to string
string json = HarSerializer.Serialize(har);

// Deserialize from string
var har = HarSerializer.Deserialize(json);
```

The output conforms to HAR 1.2 specification and can be imported into:
- **Chrome DevTools**: Network tab > Import HAR file
- **Firefox DevTools**: Network tab > Import HAR
- **HAR Viewer**: [http://www.softwareishard.com/har/viewer/](http://www.softwareishard.com/har/viewer/)

## Disposal

`HarCapture` implements both `IDisposable` and `IAsyncDisposable`:

```csharp
// Sync disposal
using var capture = new HarCapture(driver);

// Async disposal
await using var capture = new HarCapture(driver);
```

- Disposing while capturing automatically stops capture
- Double disposal is safe (no-throw)
- Accessing methods after disposal throws `ObjectDisposedException`

## HAR Model

The library provides a complete HAR 1.2 object model:

| Class | Description |
|---|---|
| `Har` | Root object containing `Log` |
| `HarLog` | Version, creator, browser, pages, entries |
| `HarEntry` | Single request/response pair with timings |
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
| `HarCache` | Cache usage info |
| `HarCacheEntry` | Detailed cache entry (expires, lastAccess, eTag, hitCount) |

## Project Structure

```
Selenium.HarCapture/
├── src/
│   └── Selenium.HarCapture/              # Main library (netstandard2.0)
│       ├── HarCapture.cs                 # Public facade
│       ├── Capture/
│       │   ├── CaptureOptions.cs         # Configuration
│       │   ├── CaptureType.cs            # Flags enum
│       │   ├── HarCaptureSession.cs      # Session orchestrator
│       │   ├── Internal/
│       │   │   ├── HarStreamWriter.cs    # Incremental HAR file writer (seek-back)
│       │   │   ├── FileLogger.cs         # Diagnostic file logging
│       │   │   ├── UrlPatternMatcher.cs  # URL glob filtering
│       │   │   ├── RequestResponseCorrelator.cs
│       │   │   ├── CdpTimingMapper.cs
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
    ├── Selenium.HarCapture.Tests/             # Unit tests (151 tests)
    └── Selenium.HarCapture.IntegrationTests/  # Integration tests (25 tests)
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

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes.

## License

See [LICENSE](LICENSE) for details.
