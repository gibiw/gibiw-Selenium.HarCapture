# Requirements: Selenium.HarCapture

**Defined:** 2026-02-19
**Core Value:** Developers can capture complete HTTP traffic from Selenium browser sessions into standard HAR format with a single line of code — no external proxies, no complex setup.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### HAR Model

- [x] **MOD-01**: Library provides HAR 1.2 compliant model classes (17 classes covering full spec)
- [x] **MOD-02**: All model classes use System.Text.Json attributes for correct serialization
- [x] **MOD-03**: Model supports nullable optional fields with JsonIgnore(WhenWritingNull)

### Serialization

- [x] **SER-01**: User can serialize Har object to JSON string (indented or compact)
- [x] **SER-02**: User can deserialize JSON string back to Har object
- [x] **SER-03**: User can save Har to file asynchronously
- [x] **SER-04**: User can load Har from file asynchronously
- [x] **SER-05**: DateTimeOffset fields serialize to ISO 8601 format per HAR spec

### Capture Control

- [x] **CAP-01**: User can start and stop network capture on a WebDriver session
- [x] **CAP-02**: User can configure CaptureType flags to control what is captured (headers, cookies, content, timings)
- [x] **CAP-03**: User can set URL include/exclude patterns to filter captured requests
- [x] **CAP-04**: User can set maximum response body size to limit memory usage
- [x] **CAP-05**: User can create multi-page captures with NewPage(pageRef, pageTitle)
- [x] **CAP-06**: User can get HAR snapshot (deep clone) while capture continues via GetHar()

### CDP Capture

- [x] **CDP-01**: Library captures network traffic via Chrome DevTools Protocol on Chromium browsers
- [x] **CDP-02**: CDP capture includes detailed timing data (DNS, connect, SSL, send, wait, receive)
- [x] **CDP-03**: CDP capture retrieves response bodies via Network.getResponseBody
- [x] **CDP-04**: CDP ResourceTiming maps correctly to HAR timings format

### Fallback Capture

- [x] **FBK-01**: Library captures network traffic via Selenium INetwork API when CDP is unavailable
- [ ] **FBK-02**: Library automatically detects whether to use CDP or INetwork based on driver capabilities
- [ ] **FBK-03**: Library falls back to INetwork at runtime if CDP session creation fails

### Public API

- [ ] **API-01**: HarCapture class provides sync and async Start/Stop methods
- [ ] **API-02**: WebDriver extension methods provide one-liner capture (StartHarCapture, CaptureHarAsync)
- [ ] **API-03**: CaptureOptions class provides fluent configuration (CaptureTypes, URL patterns, body size limit)
- [ ] **API-04**: HarCapture exposes IsCapturing and ActiveStrategyName properties for diagnostics

### Thread Safety

- [ ] **THR-01**: HarCapture is thread-safe for concurrent access to GetHar() and mutation operations
- [ ] **THR-02**: Capture strategies use ConcurrentDictionary for request/response correlation
- [ ] **THR-03**: GetHar() returns deep clone via JSON round-trip (no shared mutable state)

## v2 Requirements

### Advanced Features

- **ADV-01**: Sensitive data sanitization (auto-remove auth headers, cookies, tokens)
- **ADV-02**: WebSocket message capture
- **ADV-03**: Incremental/streaming HAR export for memory efficiency
- **ADV-04**: HAR schema validation
- **ADV-05**: Binary content encoding (base64 for images/PDFs)
- **ADV-06**: Compression detection (gzip/brotli decode, transfer vs content size)

## Out of Scope

| Feature | Reason |
|---------|--------|
| HAR visualization/analysis UI | Excellent tools exist (Chrome DevTools, HAR Viewer, DebugBear) |
| Proxy-based capture (BrowserMob) | Goal is proxy-free native approach |
| Request modification/interception | Separate concern, not HAR capture |
| HAR import/replay | Different problem domain (mocking/stubbing) |
| Bandwidth throttling | Wrong layer — use CDP Network.emulateNetworkConditions directly |
| Multiple output formats | HAR is the standard — users can convert if needed |
| Cross-browser CDP polyfill | Maintenance nightmare — INetwork fallback covers cross-browser |
| Mobile app support | Web-first library |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| MOD-01 | Phase 1 | Complete |
| MOD-02 | Phase 1 | Complete |
| MOD-03 | Phase 1 | Complete |
| SER-01 | Phase 1 | Complete |
| SER-02 | Phase 1 | Complete |
| SER-03 | Phase 1 | Complete |
| SER-04 | Phase 1 | Complete |
| SER-05 | Phase 1 | Complete |
| CAP-01 | Phase 2 | Complete |
| CAP-02 | Phase 2 | Complete |
| CAP-03 | Phase 2 | Complete |
| CAP-04 | Phase 2 | Complete |
| CAP-05 | Phase 2 | Complete |
| CAP-06 | Phase 2 | Complete |
| CDP-01 | Phase 3 | Complete |
| CDP-02 | Phase 3 | Complete |
| CDP-03 | Phase 3 | Complete |
| CDP-04 | Phase 3 | Complete |
| FBK-01 | Phase 4 | Complete |
| FBK-02 | Phase 4 | Pending |
| FBK-03 | Phase 4 | Pending |
| API-01 | Phase 5 | Pending |
| API-02 | Phase 5 | Pending |
| API-03 | Phase 5 | Pending |
| API-04 | Phase 5 | Pending |
| THR-01 | Phase 5 | Pending |
| THR-02 | Phase 5 | Pending |
| THR-03 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 28 total
- Mapped to phases: 28
- Unmapped: 0 ✓

---
*Requirements defined: 2026-02-19*
*Last updated: 2026-02-19 after roadmap creation*
