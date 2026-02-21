# Selenium.HarCapture

[![CI](https://github.com/gibiw/gibiw-Selenium.HarCapture/actions/workflows/ci.yml/badge.svg)](https://github.com/gibiw/gibiw-Selenium.HarCapture/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Selenium.HarCapture.svg)](https://www.nuget.org/packages/Selenium.HarCapture/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Selenium.HarCapture.svg)](https://www.nuget.org/packages/Selenium.HarCapture/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.0-purple.svg)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)

A .NET library for capturing HTTP Archive (HAR 1.2) files from Selenium WebDriver sessions. Supports Chrome DevTools Protocol (CDP) for detailed timings and response bodies, with automatic fallback to Selenium's INetwork API for cross-browser compatibility.

## Features

- **HAR 1.2 compliant** output, openable in Chrome/Firefox DevTools
- **Two capture strategies**: CDP (detailed timings, response bodies) and INetwork (cross-browser)
- **Automatic strategy selection** with manual override
- **URL filtering** via glob patterns (include/exclude)
- **Multi-page capture** support
- **Response body size limiting** to control memory usage
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
using Selenium.HarCapture.Serialization;

// Create a WebDriver instance
var driver = new ChromeDriver();

// Start capturing
using var capture = new HarCapture(driver);
capture.Start();

// Navigate and interact
driver.Navigate().GoToUrl("https://example.com");

// Stop and get HAR
var har = capture.Stop();

// Save to file (HAR 1.2 JSON, openable in browser DevTools)
await HarSerializer.SaveAsync(har, "traffic.har");
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

Glob patterns powered by [DotNet.Glob](https://github.com/dazinator/DotNet.Glob):

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
| Detailed timings (dns, connect, ssl, send, wait, receive) | Yes | No |
| Response body capture | Yes | No |
| Cross-browser support | Chrome only | All browsers |
| Requires specific Chrome version match | Yes | No |

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
| `HarCreator` | Creator tool name and version |
| `HarCookie` | Cookie details (name, value, path, domain, expires, httpOnly, secure) |
| `HarHeader` | Header name-value pair |
| `HarQueryString` | Query parameter name-value pair |
| `HarPostData` | POST data (MIME type, text, params) |
| `HarCache` | Cache usage info |

## Project Structure

```
Selenium.HarCapture/
├── src/
│   └── Selenium.HarCapture/          # Main library (netstandard2.0)
│       ├── HarCapture.cs             # Public facade
│       ├── Capture/
│       │   ├── CaptureOptions.cs     # Configuration
│       │   ├── CaptureType.cs        # Flags enum
│       │   ├── HarCaptureSession.cs  # Session orchestrator
│       │   └── Strategies/
│       │       ├── ICaptureStrategy.cs
│       │       ├── CdpNetworkCaptureStrategy.cs
│       │       └── SeleniumNetworkCaptureStrategy.cs
│       ├── Extensions/
│       │   └── WebDriverExtensions.cs # One-liner methods
│       ├── Models/                    # HAR 1.2 data model
│       └── Serialization/
│           └── HarSerializer.cs       # JSON serialization
└── tests/
    ├── Selenium.HarCapture.Tests/             # Unit tests (126 tests)
    └── Selenium.HarCapture.IntegrationTests/  # Integration tests (18 tests)
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
| `BasicCaptureTests` | 3 | URL capture, status codes, CDP timings |
| `ResponseBodyCaptureTests` | 2 | Response body capture, max body size (CDP only) |
| `UrlFilteringTests` | 2 | Include/exclude URL glob patterns |
| `MultiPageCaptureTests` | 1 | Multi-page capture with correct PageRef |
| `ExtensionMethodTests` | 4 | StartHarCapture, fluent config, CaptureHarAsync, CaptureHar |
| `INetworkFallbackTests` | 1 | ForceSeleniumNetwork with separate Chrome instance |
| `SerializationRoundtripTests` | 2 | Save/Load roundtrip, HAR 1.2 structure validation |
| `DisposeCleanupTests` | 3 | Dispose stops capture, ObjectDisposedException, double dispose |

#### Chrome Version Compatibility

The CDP strategy requires Chrome version matching the Selenium.WebDriver DevTools version. With Selenium 4.40.0, this is Chrome 142-144. Tests that require CDP automatically detect the installed Chrome version and skip gracefully if incompatible.

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

## License

See [LICENSE](LICENSE) for details.
