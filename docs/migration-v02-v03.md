# Migrating from v0.2.x to v0.3.x

v0.3.x is a significant feature release. The public API surface is largely backward compatible — existing code will compile — but `StartAsync()` now validates `CaptureOptions` at startup time and will throw `ArgumentException` for conflicting or invalid option combinations that were previously silently ignored. A handful of behavioral changes also affect the shape of captured HAR output. Review the sections below to identify anything that affects your setup.

---

## Breaking Changes

### 1. CaptureOptions Validated at `StartAsync()` — Conflicting Options Throw

v0.2.x accepted any combination of `CaptureOptions` without complaint. Conflicting or invalid combinations were silently ignored, which made bugs hard to diagnose.

v0.3.x introduces `CaptureOptionsValidator`, which runs at `StartAsync()` time and throws a single `ArgumentException` listing **all** violations at once.

**v0.2.x — no error thrown, behavior silently undefined:**

```csharp
// WithCompression() + ForceSeleniumNetwork() — INetwork has no response bodies, so
// compression did nothing. No error was raised.
var options = new CaptureOptions()
    .WithCompression()
    .ForceSeleniumNetwork();

await using var capture = new HarCapture(driver, options);
await capture.StartAsync(); // silently accepted
```

**v0.3.x — ArgumentException thrown at StartAsync():**

```csharp
var options = new CaptureOptions()
    .WithCompression()
    .ForceSeleniumNetwork();

await using var capture = new HarCapture(driver, options);
await capture.StartAsync();
// throws ArgumentException:
// "CaptureOptions validation failed with 1 error(s):
//  EnableCompression and ForceSeleniumNetworkApi cannot both be true — ..."
```

**Why changed:** Silent failures were hard to debug. All violations are surfaced at startup so you can fix them all at once.

**All validated conflict rules and field constraints:**

| Rule | Error |
|---|---|
| `WithCompression()` + `ForceSeleniumNetwork()` | Compression requires body retrieval; INetwork does not retrieve bodies |
| `ResponseBodyScope.None` + `MaxResponseBodySize > 0` | No bodies are retrieved so the size limit has no effect |
| `MaxResponseBodySize < 0` | Must be >= 0 (use 0 for unlimited) |
| `MaxWebSocketFramesPerConnection < 0` | Must be >= 0 (use 0 for unlimited) |
| `MaxOutputFileSize < 0` | Must be >= 0 (use 0 for unlimited) |
| `MaxOutputFileSize > 0` without `OutputFilePath` | Requires streaming mode (WithOutputFile) |
| `CreatorName = ""` (empty string) | Must be non-empty or null (null uses default) |
| `UrlIncludePatterns[i] = null/empty` | Each pattern must be non-empty |
| `UrlExcludePatterns[i] = null/empty` | Each pattern must be non-empty |

**Migration:** Wrap `StartAsync()` in a try-catch if your options are built dynamically from user input. For static options, fix any flagged conflicts at compile time.

```csharp
try
{
    await capture.StartAsync();
}
catch (ArgumentException ex)
{
    // ex.Message lists all violations — fix and restart
    Console.Error.WriteLine(ex.Message);
    throw;
}
```

---

### 2. `StartAsync()` and `StopAsync()` Accept `CancellationToken`

The signatures of `StartAsync()` and `StopAsync()` now include an optional `CancellationToken` parameter. All existing call sites without a token continue to compile and behave identically.

**v0.2.x:**

```csharp
public Task StartAsync(string? initialPageRef = null, string? initialPageTitle = null)
public Task<Har> StopAsync()
public Task<Har> StopAndSaveAsync(string filePath, bool writeIndented = true)
public Task StopAndSaveAsync()  // parameterless streaming overload
public Task<Har> CaptureHarAsync(Func<Task> action, CaptureOptions? options = null)
```

**v0.3.x:**

```csharp
public Task StartAsync(string? initialPageRef = null, string? initialPageTitle = null, CancellationToken cancellationToken = default)
public Task<Har> StopAsync(CancellationToken cancellationToken = default)
public Task<Har> StopAndSaveAsync(string filePath, bool writeIndented = true, CancellationToken cancellationToken = default)
public Task StopAndSaveAsync(CancellationToken cancellationToken = default)
public Task<Har> CaptureHarAsync(Func<Task> action, CaptureOptions? options = null, CancellationToken cancellationToken = default)
```

All parameters are optional with a `default` value, so **existing call sites compile without changes**. This is a source-compatible extension.

---

## Behavioral Changes

### 1. Pause/Resume Uses "Drop Semantics"

`Pause()` and `Resume()` are new in v0.3.x. However, the drop behavior during pause is worth understanding to avoid data loss surprises.

When `Pause()` is called, entries that are **in-flight** at the CDP layer (request sent, response not yet received) continue to complete — but they are **dropped** at `OnEntryCompleted`, not stored. There is no buffer. Entries do not queue up and replay when `Resume()` is called.

```csharp
capture.Pause();
// Any responses that arrive now are dropped, not queued
driver.Navigate().GoToUrl("https://example.com/page-while-paused");
// entries for this navigation are dropped

capture.Resume();
// Only entries for requests made after Resume() are recorded
driver.Navigate().GoToUrl("https://example.com/page-after-resume");
// these entries ARE recorded
```

**Impact:** If you use `Pause()` to suppress a section of traffic, expect that some overlap entries may be silently dropped even if they were initiated before `Pause()` was called.

---

### 2. `EntryWritten` Event Raised Outside Lock

The new `EntryWritten` event (`HarCaptureProgress`) is raised **outside** the internal capture lock. This was intentional to prevent deadlocks when handlers call `GetHar()` (which acquires the same lock).

**Impact:** If your `EntryWritten` handler performs state mutation that needs to be thread-safe, synchronize it independently. Do not assume the event fires under any lock.

---

### 3. `MaxOutputFileSize` Truncation Check Runs After Footer Write

When `MaxOutputFileSize` is configured and the file size limit is exceeded, the streaming writer:

1. Writes the entry that pushed the file past the limit (the entry is **fully written**)
2. Writes the HAR footer (`]}}`), keeping the file valid JSON
3. Sets `IsTruncated = true` internally and drops all subsequent entries

This means the output file is always valid JSON at truncation time. However, the final file will be **slightly larger than `MaxOutputFileSize`** because the last entry and footer are written after the limit is crossed.

**v0.2.x behavior:** `MaxOutputFileSize` did not exist; no truncation occurred.

**v0.3.x behavior:** File is truncated cleanly — always valid JSON — but may slightly exceed the configured byte limit.

---

### 4. New Extension Fields in HAR Entries (CDP Strategy)

CDP entries now include additional extension fields (underscore-prefixed per HAR spec convention). Strict downstream parsers that reject unknown fields will break.

| Field | Location | Description |
|---|---|---|
| `_requestBodySize` | `HarEntry` | Encoded request body size in bytes (-1 if unavailable) |
| `_responseBodySize` | `HarEntry` | Encoded response body size in bytes (-1 if unavailable) |
| `_initiator` | `HarEntry` | CDP initiator info (type, URL, line number for script-initiated requests) |
| `_securityDetails` | `HarEntry` | TLS details for HTTPS entries (protocol, cipher, issuer, subject) |

These fields follow the HAR extension field convention (underscore prefix = non-standard, tool-specific) and are accepted without error by `HarValidator` in Standard and Lenient modes.

**Migration:** If you parse HAR output with a strict schema validator that rejects unknown fields, either configure it to allow extension fields or switch to Standard/Lenient validation via `HarValidator.Validate(har, HarValidationMode.Standard)`.

---

### 5. Cache `beforeRequest` Populated for 304 and Cache-Hit Responses

In v0.2.x, `entry.Cache.BeforeRequest` was null for all responses.

In v0.3.x, cache-served responses (HTTP 304 or CDP-detected cache hit) populate `entry.Cache.BeforeRequest` with sentinel values:

```json
{
  "lastAccess": "0001-01-01T00:00:00Z",
  "eTag": "",
  "hitCount": 0
}
```

These sentinel values indicate a cache hit without real cache metadata (CDP does not expose full cache metadata). The `lastAccess` sentinel is `DateTimeOffset.MinValue` serialized as ISO 8601.

**Impact:** Code that checks `entry.Cache.BeforeRequest != null` to detect cache hits will now match v0.3.x output. Code that treats `null` as "not cached" will see a behavior change for 304 responses.

---

## New Features

These additions require no migration — they are opt-in. No existing code is affected.

### ResponseBodyScope Filtering

Limit which response bodies trigger expensive `Network.getResponseBody` CDP calls. Default is `ResponseBodyScope.All` (backward compatible — all bodies retrieved).

```csharp
// Only retrieve bodies for HTML pages and API responses (JSON, XML)
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.PagesAndApi);

// Skip all body retrieval — headers and timings only (lowest CDP overhead)
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.None);

// Add extra MIME types on top of a scope
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.PagesAndApi)
    .WithResponseBodyMimeFilter("image/png", "image/svg+xml");
```

See the [Performance Tuning Guide](performance-tuning.md) for expected impact.

---

### Body Regex Redaction (`WithSensitiveBodyPatterns`) + `HarPiiPatterns` Built-ins

Redact sensitive values from request/response bodies using regex patterns.

```csharp
using Selenium.HarCapture;

var options = new CaptureOptions()
    .WithSensitiveBodyPatterns(
        HarPiiPatterns.Email,       // \b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b
        HarPiiPatterns.CreditCard,  // Visa/MC/Amex/Discover card numbers
        HarPiiPatterns.Ssn,         // NNN-NN-NNNN format
        HarPiiPatterns.Phone,       // US phone numbers
        HarPiiPatterns.IpAddress    // IPv4 dotted-decimal
    );
```

Each pattern runs with a 100ms timeout and a 512 KB body-size gate for ReDoS protection. Bodies exceeding 512 KB are skipped. Base64-encoded bodies are not redacted.

---

### WebSocket Frame Cap (`MaxWebSocketFramesPerConnection`)

Cap the number of stored WebSocket frames per connection to prevent unbounded memory growth from long-lived connections.

```csharp
var options = new CaptureOptions()
    .WithMaxWebSocketFramesPerConnection(500); // keep newest 500 frames per connection

// 0 = unlimited (default, backward compatible)
var options = new CaptureOptions()
    .WithMaxWebSocketFramesPerConnection(0);
```

When the limit is reached, the oldest frames are evicted (oldest-first). Negative values are rejected at `StartAsync()`.

---

### Pause/Resume

Temporarily suppress entry recording without stopping capture.

```csharp
using var capture = driver.StartHarCapture();
driver.Navigate().GoToUrl("https://example.com/login"); // recorded

capture.Pause();
// Fill in login credentials — no entries recorded
driver.FindElement(By.Id("password")).SendKeys("secret");
driver.FindElement(By.Id("submit")).Click();
capture.Resume();

driver.Navigate().GoToUrl("https://example.com/dashboard"); // recorded
var har = capture.Stop();
```

See "Drop Semantics" under Behavioral Changes for important notes on in-flight entries.

---

### Progress Events (`capture.EntryWritten`)

Subscribe to receive a notification after each HAR entry is written.

```csharp
using Selenium.HarCapture.Models;

capture.EntryWritten += (sender, progress) =>
{
    Console.WriteLine($"Entry #{progress.TotalEntries}: {progress.LastEntry.Request.Url}");
};
```

`HarCaptureProgress` provides `TotalEntries` (count so far) and `LastEntry` (the entry just written). The event is raised outside the internal capture lock — see Behavioral Changes section.

---

### Custom Metadata (`WithCustomMetadata`)

Embed arbitrary key-value metadata in the HAR file under the `_custom` extension key.

```csharp
var options = new CaptureOptions()
    .WithCustomMetadata("env", "staging")
    .WithCustomMetadata("buildId", "12345")
    .WithCustomMetadata("testSuite", "checkout-flow");
```

Values must be JSON-primitive-compatible (string, int, long, double, bool). Metadata appears in the HAR as `log._custom`.

---

### MaxOutputFileSize (`WithMaxOutputFileSize`)

Abort streaming capture when the output file exceeds a size limit. Safety valve for runaway long captures.

```csharp
var options = new CaptureOptions()
    .WithOutputFile("capture.har")
    .WithMaxOutputFileSize(100 * 1024 * 1024); // stop after 100 MB
```

Requires `WithOutputFile()` (streaming mode). See Behavioral Changes section for truncation semantics.

---

### HAR Validation API (`HarValidator.Validate`)

Validate a captured HAR object against the HAR 1.2 spec.

```csharp
using Selenium.HarCapture.Serialization;

var har = capture.Stop();
var result = HarValidator.Validate(har); // HarValidationMode.Standard (default)

if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.Error.WriteLine($"[{error.Severity}] {error.Path}: {error.Message}");
    }
}

// Strict mode — warns about absent optional fields
var strict = HarValidator.Validate(har, HarValidationMode.Strict);
```

`HarValidator` never throws for validation findings — it returns a `HarValidationResult`. Standard mode accepts `-1` sentinel values and absent `pages` arrays (both produced by this library). Strict mode warns on absent optional fields.

---

### CaptureOptions Validation at `StartAsync()`

See "Breaking Changes" section above. Validation is a first-class feature — not just a side effect.

---

## Quick Migration Checklist

Follow these steps when upgrading a project from v0.2.x to v0.3.x:

1. **Update NuGet package to v0.3.x:**

   ```bash
   dotnet add package Selenium.HarCapture --version 0.3.*
   ```

2. **Check for conflicting `CaptureOptions` combinations.** The following pairs now throw `ArgumentException` at `StartAsync()`:
   - `WithCompression()` + `ForceSeleniumNetwork()` — remove `WithCompression()` if using INetwork strategy
   - `ResponseBodyScope.None` + `MaxResponseBodySize > 0` — set `MaxResponseBodySize = 0` or change scope
   - `MaxOutputFileSize > 0` without `WithOutputFile(path)` — add `WithOutputFile()` or remove `MaxOutputFileSize`

3. **Wrap `StartAsync()` in try-catch if options are user-configurable:**

   ```csharp
   try
   {
       await capture.StartAsync();
   }
   catch (ArgumentException ex)
   {
       // ex.Message contains a list of all validation errors
       logger.LogError(ex.Message);
       throw;
   }
   ```

4. **Review new extension fields in HAR output if downstream parsers are strict:**
   The new `_requestBodySize`, `_responseBodySize`, `_initiator`, and `_securityDetails` fields follow the HAR underscore-extension convention but may cause schema validation failures in strict parsers. Configure your parser to allow unknown fields, or use `HarValidator.Validate()` which is pre-tuned to accept these fields.

5. **Consider adopting streaming mode for large captures:**
   For sessions with many entries or large response bodies, `WithOutputFile()` reduces memory usage to O(1) and provides crash-safe output:

   ```csharp
   var options = new CaptureOptions()
       .WithOutputFile("capture.har")
       .WithResponseBodyScope(ResponseBodyScope.PagesAndApi); // reduce CDP overhead

   await using var capture = new HarCapture(driver, options);
   await capture.StartAsync("page1", "Home");
   // navigate...
   await capture.StopAndSaveAsync(); // file already written, just finalize
   ```

6. **Check `entry.Cache.BeforeRequest` usage:**
   If your code checks `entry.Cache.BeforeRequest != null` as a cache hit signal, note that 304 responses now populate this field with sentinel values. This makes the check more accurate. No action needed unless you were specifically relying on `null` to mean "not a cache hit."
