# Changelog

All notable changes to this project will be documented in this file.

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
