# Roadmap: Selenium.HarCapture

## Overview

Selenium.HarCapture delivers proxy-free HTTP traffic capture for Selenium WebDriver sessions, using Chrome DevTools Protocol as the primary mechanism and Selenium INetwork API as cross-browser fallback. The journey moves from foundation (HAR 1.2 data model and JSON serialization) through core capture capabilities (CDP event handling, request/response correlation, filtering) to polished public API with thread-safe operation and one-liner extension methods. Each phase delivers a testable, verifiable capability that builds toward the core value: capturing complete HTTP traffic with a single line of code.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: HAR Foundation** - HAR 1.2 model classes and JSON serialization/deserialization
- [ ] **Phase 2: Capture Infrastructure** - Request/response correlation, capture lifecycle, filtering and configuration
- [ ] **Phase 3: CDP Strategy** - Chrome DevTools Protocol network capture with detailed timings and response bodies
- [ ] **Phase 4: Fallback Strategy** - Selenium INetwork API cross-browser capture with automatic detection
- [ ] **Phase 5: Public API** - HarCapture facade, WebDriver extensions, thread safety, and one-liner usage

## Phase Details

### Phase 1: HAR Foundation
**Goal**: HAR 1.2 compliant model and JSON serialization provide the data foundation for all capture strategies
**Depends on**: Nothing (first phase)
**Requirements**: MOD-01, MOD-02, MOD-03, SER-01, SER-02, SER-03, SER-04, SER-05
**Success Criteria** (what must be TRUE):
  1. Library provides 17 HAR 1.2 model classes (Log, Entry, Request, Response, Timings, Header, Cookie, etc.) with correct property names and types
  2. User can serialize Har object to JSON string (indented or compact) and deserialize back without data loss
  3. User can save Har to file asynchronously and load it back with correct ISO 8601 timestamp formatting
  4. All optional HAR fields serialize with JsonIgnore(WhenWritingNull) to produce clean, spec-compliant JSON
  5. Model classes are sealed, immutable where appropriate, and optimized for serialization performance
**Plans**: 2 plans

Plans:
- [ ] 01-01-PLAN.md — Solution setup and 18 HAR 1.2 model classes (sealed POCOs with System.Text.Json attributes)
- [ ] 01-02-PLAN.md — DateTimeOffsetConverter, HarSerializer, and comprehensive test suite

### Phase 2: Capture Infrastructure
**Goal**: Core capture primitives enable request/response correlation, lifecycle control, and filtering configuration
**Depends on**: Phase 1
**Requirements**: CAP-01, CAP-02, CAP-03, CAP-04, CAP-05, CAP-06
**Success Criteria** (what must be TRUE):
  1. User can start and stop network capture on a WebDriver session with explicit lifecycle methods
  2. User can configure CaptureType flags to control what is captured (headers, cookies, content, timings) and set maximum response body size
  3. User can set URL include/exclude patterns to filter captured requests by regex or glob patterns
  4. User can create multi-page captures with NewPage(pageRef, pageTitle) that organize entries into HAR pages
  5. User can call GetHar() to retrieve a deep-cloned snapshot while capture continues without blocking or race conditions
  6. ICaptureStrategy interface and RequestResponseCorrelator use ConcurrentDictionary with Lazy pattern for thread-safe correlation
**Plans**: 2 plans

Plans:
- [ ] 02-01-PLAN.md — Configuration system (CaptureType, CaptureOptions), strategy interface (INetworkCaptureStrategy), and internal utilities (RequestResponseCorrelator, UrlPatternMatcher)
- [ ] 02-02-PLAN.md — HarCaptureSession orchestrator and comprehensive unit tests for all Phase 2 components

### Phase 3: CDP Strategy
**Goal**: Chrome DevTools Protocol captures complete network traffic with detailed timings and response bodies on Chromium browsers
**Depends on**: Phase 2
**Requirements**: CDP-01, CDP-02, CDP-03, CDP-04
**Success Criteria** (what must be TRUE):
  1. Library captures all network traffic via CDP Network domain events (requestWillBeSent, responseReceived, dataReceived, loadingFinished/Failed) on Chromium browsers
  2. CDP capture includes detailed timing data (DNS, connect, SSL, send, wait, receive) correctly mapped from CDP ResourceTiming to HAR timings format
  3. CDP capture retrieves response bodies via Network.getResponseBody immediately after responseReceived to avoid timing race conditions
  4. CDP strategy handles event ordering races (requestWillBeSentExtraInfo vs requestWillBeSent), HTTP redirects (reusing requestId), and 304/204 status codes without duplicate entries or missing data
  5. CDP event subscriptions properly clean up via IDisposable (Network.disable, unsubscribe all handlers) to prevent memory leaks
**Plans**: TBD

Plans:
- [ ] 03-01: TBD
- [ ] 03-02: TBD

### Phase 4: Fallback Strategy
**Goal**: Selenium INetwork API provides cross-browser capture fallback when CDP is unavailable
**Depends on**: Phase 3
**Requirements**: FBK-01, FBK-02, FBK-03
**Success Criteria** (what must be TRUE):
  1. Library captures network traffic via Selenium INetwork API (NetworkRequestSent, NetworkResponseReceived) when CDP is unavailable
  2. Library automatically detects whether to use CDP or INetwork based on driver capabilities (HasDevTools, browser type) at Start() time
  3. Library falls back to INetwork at runtime if CDP session creation fails (exception handling with logging) and continues capture without user intervention
  4. INetwork strategy correctly handles reduced data availability (no detailed timings, limited response body access) and produces valid HAR entries
**Plans**: TBD

Plans:
- [ ] 04-01: TBD
- [ ] 04-02: TBD

### Phase 5: Public API
**Goal**: HarCapture class and WebDriver extensions provide clean, thread-safe API for one-liner usage
**Depends on**: Phase 4
**Requirements**: API-01, API-02, API-03, API-04, THR-01, THR-02, THR-03
**Success Criteria** (what must be TRUE):
  1. HarCapture class provides sync and async Start/Stop methods that manage strategy lifecycle, implement IDisposable, and expose IsCapturing and ActiveStrategyName for diagnostics
  2. WebDriver extension methods (StartHarCapture, CaptureHarAsync) enable one-liner capture usage with fluent CaptureOptions configuration
  3. CaptureOptions class provides fluent API for CaptureType flags, URL patterns, body size limits, and multi-page configuration
  4. HarCapture and all strategies are thread-safe for concurrent GetHar() calls and mutation operations using ConcurrentDictionary and deep-clone snapshots via JSON round-trip
  5. All async methods use ConfigureAwait(false) to prevent deadlocks when consumers call synchronously (.Result or .Wait())
**Plans**: TBD

Plans:
- [ ] 05-01: TBD
- [ ] 05-02: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. HAR Foundation | 2/2 | Complete | 2026-02-19 |
| 2. Capture Infrastructure | 0/2 | Planned | - |
| 3. CDP Strategy | 0/2 | Not started | - |
| 4. Fallback Strategy | 0/2 | Not started | - |
| 5. Public API | 0/2 | Not started | - |
