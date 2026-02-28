# Changelog

All notable changes to this project will be documented in this file.

## [0.3.0] - 2026-02-28

### Added

- **Sensitive data redaction** — `WithSensitiveHeaders(params string[])`, `WithSensitiveCookies(params string[])`, and `WithSensitiveQueryParams(params string[])` fluent methods on `CaptureOptions`. Matched values are replaced with `[REDACTED]` at capture time — original data is never stored. Headers and cookies use case-insensitive exact matching; query parameters support glob wildcards (`*`, `?`).
- `SensitiveDataRedactor` — internal redactor integrated into both CDP and INetwork capture strategies.
- **CancellationToken support** — all async methods (`StartAsync`, `StopAsync`, `StopAndSaveAsync`, `CaptureHarAsync`, `SaveAsync`, `LoadAsync`) accept an optional `CancellationToken` with default parameter.
- **CDP Page domain events** — `Page.domContentEventFired` and `Page.loadEventFired` events are now captured for page timing data.
- **ResponseBodyScope in INetwork strategy** — `ResponseBodyScope` filtering now applies to the Selenium INetwork capture strategy in addition to CDP.
- **SourceLink and symbol packages** — `.snupkg` symbol packages with embedded source for debugging.
- `HttpParsingHelper` — shared internal utility for HTTP header and cookie parsing, extracted from duplicated code.
- MIT LICENSE file.
- 44 new unit tests (265 → 309) covering redaction, CancellationToken overloads, LRU cache, stopping flag, and compression path.
- 11 new integration tests (25 → 36): `RedactionTests` (4), `ResponseBodyScopeTests` (4), `CancellationTokenTests` (3).

### Fixed

- **Race condition on stop** — added volatile `_stopping` flag to guard all CDP event handlers, preventing `NullReferenceException` during `capture.Stop()` under heavy parallel load.
- **Channel completion mode** — changed body retrieval channel from `TryComplete` to `Wait` mode with backpressure diagnostics, fixing dropped body retrieval requests.
- **LRU cache for response bodies** — replaced unbounded `ConcurrentDictionary` with bounded LRU cache to prevent memory growth during long captures.
- **Compression path** — fixed `FileNotFoundException` when using `StopAndSave` with `WithCompression()` enabled.

### Changed

- CancellationToken parameters use default values instead of separate overloads, reducing API surface.
- Package version bumped to 0.3.0 with SourceLink configuration.

## [0.2.0] - 2026-02-27

### Added

- **Response body scope filtering** — new `ResponseBodyScope` enum (`All`, `PagesAndApi`, `TextContent`, `None`) controls which response bodies are retrieved via CDP `Network.getResponseBody`. Skipping bodies for CSS, JS, images, and fonts (typically 90% of requests) reduces CDP WebSocket contention and speeds up navigation.
- `CaptureOptions.WithResponseBodyScope(ResponseBodyScope scope)` fluent method.
- `CaptureOptions.WithResponseBodyMimeFilter(params string[] mimeTypes)` fluent method — additive extra MIME types on top of any scope preset.
- `MimeTypeMatcher` — internal MIME-based filter with case-insensitive matching, charset parameter stripping, and `text/*` prefix matching for `TextContent` scope.
- 14 unit tests for `MimeTypeMatcher` (all scopes, composability, charset handling, case insensitivity, null safety).
- 4 unit tests for `CaptureOptions` new properties and fluent API.
- **WebSocket capture support** — captures WebSocket frames (sent/received) via CDP events and stores them in the Chrome DevTools `_webSocketMessages` HAR extension field for compatibility with Chrome HAR viewers.
- `HarWebSocketMessage` model with `Type` ("send"/"receive"), `Time` (epoch seconds), `Opcode` (1=text, 2=binary), and `Data` properties.
- `HarEntry.WebSocketMessages` property (omitted from JSON when null).
- `CaptureType.WebSocket` flag (`1 << 10`, included in `All`, excluded from `AllText`).
- `CaptureOptions.WithWebSocketCapture()` fluent method.
- `WebSocketFrameAccumulator` — thread-safe deferred-entry accumulator that holds WS connections until close/stop, then emits complete entries with all frames attached.
- CDP WebSocket event handling in both `RawCdpNetworkAdapter` and `ReflectiveCdpNetworkAdapter` (graceful fallback if events missing on older CDP versions).
- 8 unit tests for `WebSocketFrameAccumulator` (including concurrency and time conversion).
- 3 serialization tests for `_webSocketMessages` (null omission, round-trip, Chrome format compatibility).
- Updated `CaptureTypeTests` and `CaptureOptionsTests` for WebSocket flag.
- **Gzip compression for HAR files** — `HarSerializer.SaveAsync()`/`Save()` and `LoadAsync()`/`Load()` auto-detect `.gz` extension and transparently compress/decompress via `GZipStream`.
- **Gzip compression in streaming mode** — `CaptureOptions.WithCompression()` enables post-finalization gzip compression; after the stream writer completes, the HAR file is compressed to `.gz` and the original is deleted.
- `CaptureOptions.EnableCompression` property and `WithCompression()` fluent method.
- **Async channel-based streaming writer** — `HarStreamWriter` replaced lock-based synchronous writes with `Channel<WriteOperation>` producer-consumer pattern for non-blocking `WriteEntry`/`AddPage` calls and background drain.
- `IAsyncDisposable` support across the full disposal chain: `HarCapture` → `HarCaptureSession` → `HarStreamWriter`. Using `await using` ensures complete channel drain before resource cleanup.
- **Automatic directory creation** — `HarSerializer.SaveAsync()` and `HarSerializer.Save()` now automatically create intermediate directories if they don't exist, preventing `DirectoryNotFoundException`.
- **Browser auto-detection** — `HarBrowser.Name` and `HarBrowser.Version` are automatically populated from WebDriver capabilities (`IHasCapabilities`) when available.
- `BrowserCapabilityExtractor` — internal helper that extracts and normalizes browser name/version from W3C standard capability keys.
- Browser name normalization for standard browsers (chrome → Chrome, MicrosoftEdge → Microsoft Edge, firefox → Firefox, etc.).
- `CaptureOptions.WithBrowser(name, version)` fluent method for manual browser override (takes precedence over auto-detection).
- **Request MimeType extraction** — `HarPostData.MimeType` is now automatically populated from the `Content-Type` request header in the INetwork (Selenium) capture strategy.
- Conflict guard for `WithOutputFile()` + `StopAndSaveAsync(path)` — throws `InvalidOperationException` when attempting to pass a path to `StopAndSaveAsync` in streaming mode.

### Changed

- **Bounded body retrieval concurrency** — replaced unbounded `ConcurrentBag<Task>` with a `Channel<BodyRetrievalRequest>` and 3 worker tasks in `CdpNetworkCaptureStrategy`. This provides predictable CDP load instead of flooding the WebSocket with concurrent `getResponseBody` calls.
- `ShouldRetrieveResponseBody` now checks MIME type in addition to status code and capture type flags.
- Removed unused `_responseBodies` field from `CdpNetworkCaptureStrategy`.
- `HarStreamWriter` internals rewritten from lock-based to channel-based async producer-consumer (`System.Threading.Channels` 8.0.0 dependency added).
- `HarCaptureSession` and `HarCapture` now implement `IAsyncDisposable` with synchronous `Dispose` preserved as fallback.

## [0.1.3] - 2026-02-25

### Added

- **Streaming capture to file** via `CaptureOptions.WithOutputFile(path)` — entries are written incrementally to the HAR file using seek-back technique. The file is always valid JSON after each entry, providing crash safety and O(1) memory usage.
- `HarStreamWriter` — internal incremental HAR writer with seek-back footer rewrite, thread-safe via locking.
- Parameterless `StopAndSave()` / `StopAndSaveAsync()` on `HarCapture` for streaming mode.
- `CaptureOptions.WithOutputFile(string path)` fluent method and `OutputFilePath` property.
- 12 new unit tests for `HarStreamWriter` (empty HAR, single/multi entry, always-valid without Complete, pages, browser/comment, concurrent write with 8 threads, dispose safety, footer truncation).

### Changed

- `HarSerializer.CreateOptions()` visibility changed from `private` to `internal` for reuse by `HarStreamWriter`.
- `HarCaptureSession` now forks between in-memory and streaming mode in `OnEntryCompleted`, `NewPage`, `GetHar`, `StopAsync`, and `Dispose`.

## [0.1.2] - 2026-02-24

### Changed

- Replaced three hardcoded CDP adapters (V142/V143/V144) with a single reflection-based `ReflectiveCdpNetworkAdapter` that auto-discovers available CDP versions at runtime via assembly scanning.
- `CdpAdapterFactory` now dynamically finds all `V{N}.DevToolsSessionDomains` types and tries them from newest to oldest — no code changes needed when Selenium adds or drops CDP versions.

### Removed

- `CdpNetworkAdapterV142`, `CdpNetworkAdapterV143`, `CdpNetworkAdapterV144` — replaced by `ReflectiveCdpNetworkAdapter`.

## [0.1.1] - 2026-02-24

### Added

- Multi-CDP-version support (V142–V144) via adapter pattern with automatic fallback from newest to oldest.
- Response body capture in INetwork strategy.
- Basic wait timing in INetwork strategy for more accurate `send`/`wait`/`receive` breakdown.
- Status badges to README (CI, NuGet, license).

### Fixed

- CI badge now shows main branch status only.

## [0.1.0] - 2026-02-21

### Added

- HAR 1.2 compliant data model (18 classes).
- JSON serialization/deserialization via `HarSerializer`.
- `CaptureOptions` with fluent API for configuring capture behavior.
- `CaptureType` flags enum for fine-grained control over captured data.
- URL filtering via glob patterns (include/exclude).
- `CdpNetworkCaptureStrategy` — CDP-based capture with detailed timings, response bodies, and request bodies.
- `SeleniumNetworkCaptureStrategy` — INetwork API fallback for cross-browser compatibility.
- `StrategyFactory` with automatic strategy selection and runtime fallback.
- `HarCaptureSession` orchestrator with Start/Stop/NewPage/GetHar lifecycle.
- `HarCapture` public facade with dual disposal pattern (IDisposable + IAsyncDisposable).
- `WebDriverExtensions` — one-liner capture via `CaptureHar`, `CaptureHarAsync`, `StartHarCapture`.
- Multi-page capture support with correct PageRef linking.
- Response body size limiting to control memory usage.
- 126 unit tests covering all components.
- 18 integration tests with real Chrome browser and local test server.
- GitHub Actions CI pipeline.
