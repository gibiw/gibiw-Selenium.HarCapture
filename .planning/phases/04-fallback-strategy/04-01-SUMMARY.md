---
phase: 04-fallback-strategy
plan: 01
subsystem: network-capture
tags: [selenium, inetwork, event-driven, fallback, strategy-pattern]

# Dependency graph
requires:
  - phase: 03-cdp-strategy
    provides: INetworkCaptureStrategy interface and CdpNetworkCaptureStrategy implementation
  - phase: 02-capture-orchestration
    provides: RequestResponseCorrelator and UrlPatternMatcher utilities
provides:
  - SeleniumNetworkCaptureStrategy using INetwork event-driven API
  - Alternative capture backend using simpler event-based API vs direct CDP commands
  - Foundation for future BiDi migration when Selenium implements it
affects: [05-integration, strategy-selection]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - INetwork event subscription pattern (NetworkRequestSent, NetworkResponseReceived)
    - IReadOnlyDictionary header handling for INetwork API
    - Event-driven capture strategy with simplified API surface

key-files:
  created:
    - src/Selenium.HarCapture/Capture/Strategies/SeleniumNetworkCaptureStrategy.cs
    - tests/Selenium.HarCapture.Tests/Capture/Strategies/SeleniumNetworkCaptureStrategyTests.cs
  modified: []

key-decisions:
  - "Use IReadOnlyDictionary<string, string> for INetwork headers (API contract difference from CDP)"
  - "Cast ResponseStatusCode from long to int for HarResponse.Status"
  - "Return simple HarTimings with all zeros since INetwork lacks detailed timing data"
  - "Set SupportsDetailedTimings=false and SupportsResponseBody=false to document INetwork limitations"

patterns-established:
  - "Event-driven network capture: subscribe to events BEFORE calling StartMonitoring()"
  - "INetwork strategy reuses existing RequestResponseCorrelator and UrlPatternMatcher (no duplication)"
  - "Parse MIME type from Content-Type header for INetwork responses"

requirements-completed: [FBK-01]

# Metrics
duration: 3min
completed: 2026-02-20
---

# Phase 04 Plan 01: SeleniumNetworkCaptureStrategy Summary

**Event-driven network capture using Selenium INetwork API with NetworkRequestSent/NetworkResponseReceived events, reusing existing correlation and filtering utilities**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-20T08:07:58Z
- **Completed:** 2026-02-20T08:11:28Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Implemented SeleniumNetworkCaptureStrategy using INetwork event-driven API
- Reused RequestResponseCorrelator and UrlPatternMatcher from existing infrastructure
- Added 7 unit tests validating constructor, properties, and disposal behavior
- All 88 tests pass (81 existing + 7 new) with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement SeleniumNetworkCaptureStrategy** - `9e708e6` (feat)
2. **Task 2: Add unit tests for SeleniumNetworkCaptureStrategy** - `75cdae2` (test)

## Files Created/Modified
- `src/Selenium.HarCapture/Capture/Strategies/SeleniumNetworkCaptureStrategy.cs` - INetwork-based capture strategy using event subscription pattern
- `tests/Selenium.HarCapture.Tests/Capture/Strategies/SeleniumNetworkCaptureStrategyTests.cs` - Unit tests for validation, properties, and disposal

## Decisions Made
- Used `IReadOnlyDictionary<string, string>` for INetwork headers instead of `Dictionary<string, string>` (API contract from Selenium)
- Cast `e.ResponseStatusCode` from `long` to `int` for HarResponse.Status field compatibility
- Return simple HarTimings with Send=0, Wait=0, Receive=0 since INetwork lacks ResourceTiming data
- Set SupportsDetailedTimings=false and SupportsResponseBody=false to clearly document INetwork limitations

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed type mismatch for INetwork headers and status code**
- **Found during:** Task 1 (SeleniumNetworkCaptureStrategy implementation)
- **Issue:** Initial implementation used `Dictionary<string, string>` but INetwork API uses `IReadOnlyDictionary<string, string>`; ResponseStatusCode is `long` but HarResponse.Status expects `int`
- **Fix:** Changed ParseSetCookieHeaders parameter to `IReadOnlyDictionary<string, string>?`; added cast `(int)e.ResponseStatusCode` for Status field
- **Files modified:** src/Selenium.HarCapture/Capture/Strategies/SeleniumNetworkCaptureStrategy.cs
- **Verification:** Build succeeds with zero errors, all nullable warnings match existing CDP strategy pattern
- **Committed in:** 9e708e6 (Task 1 commit)

**2. [Rule 1 - Bug] Fixed nullable reference warnings for event args properties**
- **Found during:** Task 1 (Build verification)
- **Issue:** e.RequestUrl, e.RequestId could be null causing CS8604 warnings
- **Fix:** Added null-coalescing operators (`?? ""`) for RequestUrl, RequestId parameters
- **Files modified:** src/Selenium.HarCapture/Capture/Strategies/SeleniumNetworkCaptureStrategy.cs
- **Verification:** Build succeeds, warnings reduced to match existing codebase pattern
- **Committed in:** 9e708e6 (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (2 type/null bugs)
**Impact on plan:** Both auto-fixes were necessary for compilation correctness. Type signatures discovered from actual INetwork API contract, not plan assumptions. No scope creep.

## Issues Encountered
None - plan executed smoothly with only type signature corrections during implementation.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- SeleniumNetworkCaptureStrategy ready for integration
- Next: Implement StrategyFactory for automatic CDP vs INetwork selection based on driver capabilities
- Next: Add runtime fallback logic in HarCaptureSession to handle CDP initialization failures

## Self-Check

**Status: PASSED**

Verified claims:
- ✅ FOUND: SeleniumNetworkCaptureStrategy.cs
- ✅ FOUND: SeleniumNetworkCaptureStrategyTests.cs
- ✅ FOUND: commit 9e708e6 (Task 1)
- ✅ FOUND: commit 75cdae2 (Task 2)

All files created and commits exist as documented.

---
*Phase: 04-fallback-strategy*
*Completed: 2026-02-20*
