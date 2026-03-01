# Troubleshooting Guide

This guide covers the most common failure modes when using `Selenium.HarCapture`. Each failure
mode uses a consistent structure: **Symptom**, **Root Cause**, **Diagnostic Steps**, **Fix**.

---

## 1. WebDriverWait Timeouts Under Load

**Symptom:**

```
WebDriverException: Timed out waiting for page to load.
```

or

```
WebDriverException: timeout waiting for element
```

These exceptions appear when running multiple Chrome instances in parallel, even though the
same test passes when run in isolation.

**Root Cause:**

CDP `Network.getResponseBody` commands compete on the same WebSocket connection as Selenium's
own navigation and element-finding commands. When HAR capture retrieves response bodies from
multiple parallel Chrome instances simultaneously, each body retrieval occupies the WebSocket
briefly, delaying Selenium commands. Under 10 parallel instances with `ResponseBodyScope.All`,
the cumulative delay can exceed `WebDriverWait`'s timeout threshold.

**Diagnostic Steps:**

1. Enable diagnostic logging to observe body retrieval timing:

   ```csharp
   var options = new CaptureOptions()
       .WithLogFile("/tmp/har-capture.log");
   ```

   Inspect the log for entries showing body retrieval timing relative to navigation commands.

2. Switch from `WebDriverWait.Until()` to `Thread.Sleep` + implicit wait temporarily. If the
   timeouts disappear, the root cause is CDP WebSocket contention, not a genuine page load issue.

3. Confirm the active strategy is CDP by checking `capture.ActiveStrategyName` equals `"CDP"`.
   If it returns `"INetwork"`, WebSocket contention is not the cause — see issue 3 instead.

**Fix:**

- Reduce CDP body retrieval volume:

  ```csharp
  // Retrieve only HTML and API bodies — eliminates image/font/script retrieval
  var options = new CaptureOptions()
      .WithResponseBodyScope(ResponseBodyScope.PagesAndApi);
  ```

  Or skip body retrieval entirely to capture only headers and timings:

  ```csharp
  var options = new CaptureOptions()
      .WithResponseBodyScope(ResponseBodyScope.None);
  ```

- Replace `WebDriverWait.Until()` with implicit waits under parallel load:

  ```csharp
  driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
  driver.Navigate().GoToUrl("https://example.com");
  Thread.Sleep(500); // brief settle before asserting
  ```

- Do **not** add a `SemaphoreSlim` to throttle body retrievals. Throttling creates a convoy
  effect that serializes parallel work, increasing capture time by 2x or more.

---

## 2. Memory Growth with Large Responses

**Symptom:**

Process memory grows steadily during a capture session and does not release between test runs.
Memory usage correlates with the number of responses captured or the size of individual response
bodies (e.g., file downloads, large JSON payloads).

**Root Cause:**

By default, HAR capture stores all response bodies in memory using `ResponseBodyScope.All` with
no size limit (`MaxResponseBodySize = 0`, unlimited). A test suite that navigates many pages or
captures large API responses accumulates all body content until `StopAsync()` is called. For
long sessions or large payloads, this can exhaust available RAM.

**Diagnostic Steps:**

1. Check current `ResponseBodyScope` — if it is `All`, every response body is being retrieved
   and held in memory.

2. Check `MaxResponseBodySize` — if it is 0 (unlimited), large payloads are stored in full.

3. Check whether streaming mode is configured — if `OutputFilePath` is not set, all entries
   are held in memory until stop.

4. Count entries between stops. If your session produces >100 entries or includes large
   downloads (>1 MB each), in-memory mode will cause proportional memory growth.

**Fix:**

- Set a response body size cap:

  ```csharp
  var options = new CaptureOptions()
      .WithMaxResponseBodySize(1_048_576); // 1 MB per body
  ```

- Switch to streaming mode to avoid holding all entries in RAM:

  ```csharp
  var options = new CaptureOptions()
      .WithOutputFile("/tmp/session.har")
      .WithMaxResponseBodySize(1_048_576);

  using var capture = new HarCapture(driver, options);
  await capture.StartAsync();
  driver.Navigate().GoToUrl("https://example.com");
  await capture.StopAndSaveAsync(); // parameterless — streaming mode
  ```

- Reduce body scope to avoid retrieving binary assets:

  ```csharp
  var options = new CaptureOptions()
      .WithResponseBodyScope(ResponseBodyScope.PagesAndApi);
  ```

- For very long sessions, add a file size safety valve:

  ```csharp
  var options = new CaptureOptions()
      .WithOutputFile("/tmp/session.har")
      .WithMaxOutputFileSize(104_857_600); // stop streaming after 100 MB
  ```

---

## 3. Empty HAR Entries (No Request/Response Data)

**Symptom:**

The saved HAR file contains entries, but they have empty or missing fields:
- No response body
- Empty headers arrays
- Missing timing data (`time: 0`, `receive: -1`)
- No `pageref` association

**Root Cause:**

Several distinct causes can produce empty entries:

1. **CDP events arrive out of order or are missed** — rapid navigation triggers `Network.requestWillBeSent`
   and `Network.responseReceived` events in quick succession; if the page navigates again before
   body retrieval completes, the CDP context is torn down and the body request returns nothing.

2. **`ForceSeleniumNetworkApi` is active** — Selenium's `INetwork` API provides significantly less
   data than CDP. Timings, response bodies, and initiator information are not available through
   INetwork.

3. **URL filters are excluding entries** — `UrlIncludePatterns` or `UrlExcludePatterns` may be
   configured to match fewer URLs than expected.

**Diagnostic Steps:**

1. Enable diagnostic logging and inspect for warning or error lines:

   ```csharp
   var options = new CaptureOptions()
       .WithLogFile("/tmp/har-capture.log");
   ```

   Look for lines containing `body retrieval failed`, `responseBody error`, or `strategy: INetwork`.

2. Check the active strategy:

   ```csharp
   Console.WriteLine(capture.ActiveStrategyName); // "CDP" or "INetwork"
   ```

   If it shows `"INetwork"`, the driver did not support CDP session creation. Only Chrome and
   Chromium-based browsers support CDP strategy.

3. Verify URL filter patterns are not over-broad:

   ```csharp
   // Check if an exclude pattern is accidentally matching more than intended
   var options = new CaptureOptions()
       .WithUrlExcludePatterns("**/*.png", "**/*.jpg"); // fine — but double-check your patterns
   ```

4. Add a short delay between rapid navigations to allow CDP events to drain:

   ```csharp
   driver.Navigate().GoToUrl("https://example.com/page1");
   Thread.Sleep(200); // give CDP time to dispatch pending events
   driver.Navigate().GoToUrl("https://example.com/page2");
   ```

**Fix:**

- Ensure you are using a Chrome or Chromium-based browser with CDP support. Firefox and Safari
  fall back to INetwork automatically.

- Remove `ForceSeleniumNetwork()` if it was set, unless cross-browser compatibility is required:

  ```csharp
  // Remove this if CDP data is needed
  // options.ForceSeleniumNetwork();
  ```

- If navigations are rapid, add brief delays or use `NewPage()` to track page boundaries:

  ```csharp
  capture.NewPage("page2", "Second Page");
  driver.Navigate().GoToUrl("https://example.com/page2");
  ```

---

## 4. Redaction Not Applying

**Symptom:**

Sensitive values (email addresses, credit card numbers, tokens) are visible in the saved HAR
file even after configuring `WithSensitiveBodyPatterns()`.

**Root Cause:**

Three distinct causes can prevent redaction:

1. **Pattern does not match** — the provided regex pattern does not match the actual body content
   (wrong syntax, unanchored pattern, case mismatch).

2. **Body exceeds 512 KB size gate** — for ReDoS safety, bodies larger than 512 KB are skipped
   by the redaction engine entirely. No patterns are applied to large bodies.

3. **Body is binary or base64-encoded** — binary and base64 responses are not redacted. Only
   text-content bodies are processed.

**Diagnostic Steps:**

1. Verify the regex pattern compiles and matches your sample data. Test with a known value:

   ```csharp
   var pattern = HarPiiPatterns.Email; // use a built-in to rule out regex issues
   var match = Regex.IsMatch("user@example.com", pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
   Console.WriteLine(match); // should be True
   ```

2. Check body sizes in the captured HAR. If a body exceeds 512 KB, it bypasses redaction:

   ```csharp
   foreach (var entry in har.Log.Entries ?? [])
   {
       var size = entry.Response?.Content?.Size ?? 0;
       if (size > 512 * 1024)
           Console.WriteLine($"Body too large for redaction: {entry.Request?.Url} ({size} bytes)");
   }
   ```

3. Enable diagnostic logging — the redaction engine logs when bodies are skipped due to size
   or encoding:

   ```csharp
   var options = new CaptureOptions()
       .WithLogFile("/tmp/har-capture.log")
       .WithSensitiveBodyPatterns(HarPiiPatterns.Email);
   ```

**Fix:**

- Use the pre-built `HarPiiPatterns` constants, which are verified to be linear-time and
  correctly anchored:

  ```csharp
  var options = new CaptureOptions()
      .WithSensitiveBodyPatterns(
          HarPiiPatterns.Email,
          HarPiiPatterns.CreditCard,
          HarPiiPatterns.Ssn,
          HarPiiPatterns.Phone,
          HarPiiPatterns.IpAddress);
  ```

- For large bodies that exceed the 512 KB gate, cap the body size so the gate does not trigger:

  ```csharp
  var options = new CaptureOptions()
      .WithMaxResponseBodySize(524_288) // 512 KB — matches the redaction gate exactly
      .WithSensitiveBodyPatterns(HarPiiPatterns.Email);
  ```

- For custom patterns, test them against your actual response content before use. Avoid nested
  quantifiers to prevent catastrophic backtracking.

---

## 5. CaptureOptions Validation Errors at StartAsync()

**Symptom:**

```
System.ArgumentException: CaptureOptions validation failed with N error(s):
  EnableCompression and ForceSeleniumNetworkApi cannot both be true — ...
  ResponseBodyScope.None combined with MaxResponseBodySize = 1048576 is contradictory — ...
```

An `ArgumentException` is thrown from `StartAsync()` listing one or more validation violations.

**Root Cause:**

`CaptureOptions` are validated at `StartAsync()` time, not at the property setter level. Fluent
chaining does not throw — only `StartAsync()` does. Conflicting options include:

- `WithCompression()` combined with `ForceSeleniumNetwork()` — INetwork does not retrieve bodies,
  so there is nothing to compress.
- `ResponseBodyScope.None` combined with `WithMaxResponseBodySize(n > 0)` — no bodies are
  retrieved, so the size limit has no effect.
- `WithMaxOutputFileSize(n > 0)` without `WithOutputFile()` — file size limits only apply in
  streaming mode.
- `WithCreatorName("")` (empty string, not null) — empty names are rejected; set to null to use
  the default.
- Negative values for `MaxResponseBodySize`, `MaxWebSocketFramesPerConnection`, or
  `MaxOutputFileSize`.

**Diagnostic Steps:**

Read the exception message — it lists every violation at once so you can fix them all in one
pass. The format is:

```
CaptureOptions validation failed with 2 error(s):
  <violation 1>
  <violation 2>
```

**Fix:**

Remove the conflicting option combination. Examples:

```csharp
// BEFORE (throws): compression requires a file output
var options = new CaptureOptions()
    .ForceSeleniumNetwork()
    .WithCompression(); // invalid — INetwork + compression conflict

// AFTER: remove compression when using INetwork
var options = new CaptureOptions()
    .ForceSeleniumNetwork();
```

```csharp
// BEFORE (throws): None scope + size limit is contradictory
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.None)
    .WithMaxResponseBodySize(1_048_576); // invalid — no bodies retrieved

// AFTER: choose one or the other
var options = new CaptureOptions()
    .WithResponseBodyScope(ResponseBodyScope.None); // headers only
```

Validation runs only at `StartAsync()`. You can configure options incrementally across
multiple statements without triggering errors — the check is deferred until capture begins.

---

## 6. NullReferenceException in capture.StopAsync()

**Symptom:**

```
System.NullReferenceException: Object reference not set to an instance of an object.
```

This exception is thrown from `StopAsync()` or during disposal. It occurs intermittently
(approximately 10% of the time) under heavy parallel load with 10 or more Chrome instances.

**Root Cause:**

A race condition in CDP event processing during shutdown. When `StopAsync()` signals the CDP
strategy to stop while body retrieval tasks are still in-flight, a small window exists where
an event handler receives a response after the internal session state has been torn down.

**Diagnostic Steps:**

1. Confirm the pattern: check whether the exception correlates with the number of parallel
   Chrome instances. If it only occurs with 5+ instances and not with 1–2, it is likely this
   race condition.

2. Wrap `StopAsync()` in a try-catch to confirm the exception type and stack trace:

   ```csharp
   try
   {
       var har = await capture.StopAsync();
   }
   catch (NullReferenceException ex)
   {
       Console.WriteLine($"StopAsync NRE (race condition): {ex.StackTrace}");
   }
   ```

**Fix:**

Use the `using` disposal pattern — `DisposeAsync()` handles shutdown errors gracefully:

```csharp
await using var capture = new HarCapture(driver, options);
await capture.StartAsync();
driver.Navigate().GoToUrl("https://example.com");
// IAsyncDisposable.DisposeAsync() handles shutdown; NRE is swallowed
```

Or apply a defensive catch around `StopAsync()` when in-memory HAR is needed:

```csharp
Har? har = null;
try
{
    har = await capture.StopAsync();
}
catch (NullReferenceException)
{
    // Race condition under heavy parallel load — HAR data may be partial
    har = capture.GetHar(); // snapshot what was captured before stop
}
```

Note: This issue occurs most frequently with `ResponseBodyScope.All` under high parallelism.
Switching to `ResponseBodyScope.PagesAndApi` or `ResponseBodyScope.None` reduces in-flight
body retrieval tasks at shutdown time and therefore reduces the occurrence rate.
