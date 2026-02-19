---
phase: 02-capture-infrastructure
plan: 02
subsystem: capture-orchestrator-and-comprehensive-tests
tags: [orchestrator, lifecycle-management, multi-page-capture, deep-clone, unit-tests, mock-strategy]
dependency_graph:
  requires:
    - CaptureType flags enum
    - CaptureOptions configuration class
    - INetworkCaptureStrategy interface
    - RequestResponseCorrelator for correlation
    - UrlPatternMatcher for URL filtering
    - HarSerializer for deep cloning
    - HAR model classes (Har, HarLog, HarEntry, HarPage, etc.)
  provides:
    - HarCaptureSession public orchestrator with Start/Stop/NewPage/GetHar
    - Comprehensive unit tests for all Phase 2 components
    - MockCaptureStrategy for testing
  affects:
    - Phase 03 (CDP strategy will use HarCaptureSession for integration tests)
    - Phase 04 (INetwork strategy will use HarCaptureSession for integration tests)
    - Phase 05 (Public API will build on HarCaptureSession)
tech_stack:
  added:
    - MockCaptureStrategy test helper pattern
  patterns:
    - Event subscription/unsubscription for EntryCompleted
    - JSON round-trip deep cloning via HarSerializer
    - Lock-based thread safety for Har rebuilding
    - Object initializer syntax for sealed classes with init properties
    - InternalsVisibleTo for test access to internal types
key_files:
  created:
    - src/Selenium.HarCapture/Capture/HarCaptureSession.cs
    - tests/Selenium.HarCapture.Tests/Capture/CaptureTypeTests.cs
    - tests/Selenium.HarCapture.Tests/Capture/CaptureOptionsTests.cs
    - tests/Selenium.HarCapture.Tests/Capture/Internal/UrlPatternMatcherTests.cs
    - tests/Selenium.HarCapture.Tests/Capture/Internal/RequestResponseCorrelatorTests.cs
    - tests/Selenium.HarCapture.Tests/Capture/HarCaptureSessionTests.cs
  modified:
    - src/Selenium.HarCapture/Selenium.HarCapture.csproj (added InternalsVisibleTo)
decisions:
  - decision: "Use object initializer syntax for sealed classes instead of 'with' expressions"
    rationale: "HAR models are sealed classes with init properties, NOT records. The 'with' expression does not work. Always create new instances using object initializer syntax."
    impact: "All Har/HarLog rebuilding uses explicit object initializers, copying all properties"
  - decision: "Deep clone GetHar() snapshots via JSON round-trip"
    rationale: "Simplest way to ensure complete independence of clones from live capture state. Avoids manual recursive cloning code."
    impact: "GetHar() has serialization overhead but guarantees isolation. Acceptable for snapshot use case."
  - decision: "Use lock-based thread safety for Har rebuilding"
    rationale: "Har models are immutable, so rebuilding requires creating new instances. Lock ensures atomicity of read+rebuild operations."
    impact: "Simple, correct thread safety. No lock contention expected (GetHar is snapshot operation, not hot path)."
metrics:
  duration_minutes: 5
  tasks_completed: 2
  files_created: 6
  files_modified: 1
  lines_added: 1235
  commits: 2
  tests_added: 39
  total_tests: 65
  completed_date: 2026-02-19
---

# Phase 02 Plan 02: HarCaptureSession Orchestrator and Comprehensive Tests Summary

**One-liner:** Created HarCaptureSession public orchestrator with Start/Stop/NewPage/GetHar lifecycle, deep-clone snapshots via JSON round-trip, URL filtering, and 39 comprehensive unit tests covering all Phase 2 infrastructure (total 65 tests pass).

## Overview

Implemented the HarCaptureSession public orchestrator class that users will interact with to manage HAR capture sessions. The orchestrator provides explicit lifecycle control (Start/Stop), multi-page organization (NewPage), and snapshot retrieval (GetHar with deep cloning). Added comprehensive unit tests for all Phase 2 components: CaptureType flags enum, CaptureOptions defaults, UrlPatternMatcher glob filtering, RequestResponseCorrelator thread-safe correlation, and HarCaptureSession lifecycle. All 65 tests pass (26 existing + 39 new).

## Tasks Completed

### Task 1: HarCaptureSession orchestrator class
**Status:** ✅ Complete
**Commit:** ba8277a
**Duration:** ~2 minutes

Created HarCaptureSession.cs as the main public class users interact with:

**Class structure:**
- `public sealed class HarCaptureSession : IDisposable`
- Two constructors: public parameterless (for future factory use) and internal with INetworkCaptureStrategy (for testing via InternalsVisibleTo)
- Private fields: `_options`, `_urlMatcher`, `_lock`, `_strategy`, `_har`, `_currentPageRef`, `_isCapturing`, `_disposed`
- Public properties: `IsCapturing` (bool), `ActiveStrategyName` (string?)

**Public methods:**
- `StartAsync(string? initialPageRef, string? initialPageTitle)` / `Start(...)`: Initialize HAR, subscribe to strategy EntryCompleted event, start strategy, set capturing flag
- `StopAsync()` / `Stop()`: Stop strategy, unsubscribe from event, clear capturing flag, return final Har (no clone needed)
- `NewPage(string pageRef, string pageTitle)`: Create new HarPage, rebuild Har with new page added, update `_currentPageRef`
- `GetHar()`: Deep clone current Har via JSON round-trip (serialize + deserialize) for isolation
- `Dispose()`: Unsubscribe event, dispose strategy, set disposed flag

**Private methods:**
- `InitializeHar(...)`: Create initial Har with version "1.2", creator metadata from options, optional initial page
- `OnEntryCompleted(HarEntry entry, string requestId)`: Filter by URL pattern, add entry to Har (with PageRef if current page set), rebuild Har

**Key patterns:**
- Har models are sealed classes with `init` properties — cannot use `with` expressions. All rebuilding uses explicit object initializer syntax copying all properties.
- Thread-safe using `lock (_lock)` for all Har read/write operations
- Deep cloning via `HarSerializer.Serialize` + `HarSerializer.Deserialize` ensures GetHar() returns independent snapshot

**InternalsVisibleTo:** Added to csproj via `<InternalsVisibleTo Include="Selenium.HarCapture.Tests" />` to allow tests to access internal types (INetworkCaptureStrategy, RequestResponseCorrelator, UrlPatternMatcher) and internal constructor.

**Verification:** Build succeeded with 0 errors, 2 warnings (NU1603 DotNet.Glob version resolution, acceptable).

### Task 2: Comprehensive unit tests for all Phase 2 components
**Status:** ✅ Complete
**Commit:** e77ff69
**Duration:** ~3 minutes

Created 5 test files with 39 new tests:

**CaptureTypeTests.cs (6 tests):**
- `None_HasValue_Zero`: Verifies None = 0
- `IndividualFlags_ArePowersOfTwo`: Verifies all 10 individual flags are distinct powers of 2
- `HasFlag_Works_ForCombinations`: Verifies AllText includes correct flags and excludes binary content
- `HeadersAndCookies_IncludesCorrectFlags`: Verifies convenience combination correctness
- `All_IncludesEverything`: Verifies All includes every individual flag
- `BitwiseOr_CombinesFlags`: Verifies bitwise OR combines flags correctly

**CaptureOptionsTests.cs (2 tests):**
- `Defaults_AreCorrect`: Verifies CaptureTypes=AllText, CreatorName="Selenium.HarCapture", ForceSeleniumNetworkApi=false, MaxResponseBodySize=0, patterns=null
- `Properties_CanBeSet`: Verifies all properties are mutable and can be set

**UrlPatternMatcherTests.cs (8 tests):**
- `NoPatterns_CapturesAll`: No patterns specified captures everything
- `IncludePattern_MatchesUrl`: Include pattern matches URL returns true
- `IncludePattern_RejectsNonMatchingUrl`: Include pattern does not match returns false
- `ExcludePattern_RejectsMatchingUrl`: Exclude pattern matches returns false
- `ExcludePattern_AllowsNonMatchingUrl`: Exclude pattern does not match returns true
- `ExcludeTakesPrecedence_OverInclude`: Exclude wins over include
- `MultipleIncludePatterns_AnyMatch`: Any include pattern matching returns true
- `CaptureAll_CapturesEverything`: Static CaptureAll instance captures all URLs

**RequestResponseCorrelatorTests.cs (6 tests):**
- `OnRequestSent_ThenResponseReceived_ReturnsEntry`: Normal correlation flow produces HarEntry
- `OnResponseReceived_WithoutRequest_ReturnsNull`: Response without request returns null
- `OnRequestSent_DoesNotReturnEntry`: OnRequestSent is void, just tracks pending
- `PendingCount_TracksUnmatchedRequests`: PendingCount accurate before/after correlation
- `Clear_RemovesAllPending`: Clear() removes all pending entries
- `ConcurrentAccess_DoesNotThrow`: Parallel.For 100 iterations produces all entries without exceptions

**HarCaptureSessionTests.cs (17 tests):**
- `Start_InitializesHar_WithVersion12`: Har.Log.Version is "1.2"
- `Start_InitializesHar_WithCreatorName`: Har.Log.Creator.Name from options
- `Start_WithInitialPage_CreatesPage`: Initial page added to Har.Log.Pages
- `Start_WithoutInitialPage_HasEmptyPages`: No initial page means Pages is null
- `Start_WhenAlreadyStarted_ThrowsInvalidOperation`: Double start throws
- `Stop_ReturnsHar`: Stop returns non-null Har
- `Stop_WhenNotStarted_ThrowsInvalidOperation`: Stop without start throws
- `NewPage_AddsPageToHar`: NewPage adds page to Pages list
- `NewPage_WhenNotCapturing_ThrowsInvalidOperation`: NewPage before start throws
- `NewPage_SetsCurrentPageRef_OnNewEntries`: Entries get correct PageRef after NewPage
- `GetHar_ReturnsDeepClone`: Two GetHar() calls return different instances, modifying one does not affect the other
- `GetHar_WhenNotCapturing_ThrowsInvalidOperation`: GetHar before start throws
- `EntryCompleted_AddsEntryToHar`: Simulated entry appears in Har
- `EntryCompleted_FiltersExcludedUrls`: UrlExcludePatterns filter out matching URLs
- `Dispose_CleansUpStrategy`: Dispose prevents further operation, throws ObjectDisposedException
- `Dispose_WhenCalledTwice_DoesNotThrow`: Dispose idempotent
- `ActiveStrategyName_ReturnsStrategyName`: Property returns strategy name

**Test helpers:**
- `MockCaptureStrategy`: Internal test implementation of INetworkCaptureStrategy with `SimulateEntry(HarEntry, string)` method to trigger EntryCompleted event
- `CreateTestRequest(string url)`: Helper to create minimal valid HarRequest
- `CreateTestResponse()`: Helper to create minimal valid HarResponse
- `CreateTestEntry(string url)`: Helper to create complete valid HarEntry

**Verification:** All 65 tests pass (26 existing + 39 new). Build succeeded with 1 code warning (CS0414 unused field `_started` in MockCaptureStrategy, acceptable for test code).

## Deviations from Plan

No deviations. Plan executed exactly as written.

## Verification Results

### Build Status
✅ `dotnet build` - Succeeded with 2 package warnings (NU1603 DotNet.Glob version resolution) and 1 code warning (CS0414 unused field in test mock)

### Test Status
✅ `dotnet test` - 65/65 tests passed, 0 failures
- 6 CaptureType tests
- 2 CaptureOptions tests
- 8 UrlPatternMatcher tests
- 6 RequestResponseCorrelator tests
- 17 HarCaptureSession tests
- 26 existing tests (models, serialization)

### Code Quality
- 0 errors
- 3 warnings (2 package version resolution, 1 unused field in test mock — all acceptable)
- All public types have XML documentation
- HarCaptureSession is public sealed with IDisposable
- Thread-safe using lock for Har rebuilding
- Deep clone via JSON round-trip ensures GetHar() isolation

## Architecture Notes

### HarCaptureSession Lifecycle

**Start:**
1. Validate state (not disposed, not already capturing, strategy configured)
2. Subscribe to `_strategy.EntryCompleted` event
3. Initialize HAR with version 1.2, creator metadata, optional initial page
4. Call `await _strategy.StartAsync(_options)`
5. Set `_isCapturing = true`

**EntryCompleted (event handler):**
1. Filter by URL pattern (`_urlMatcher.ShouldCapture`)
2. Lock `_lock`
3. Create new entry with PageRef if current page set
4. Rebuild Har with new entry added (object initializer copying all properties)

**NewPage:**
1. Validate state (not disposed, capturing)
2. Lock `_lock`
3. Create new HarPage
4. Rebuild Har with new page added (object initializer copying all properties)
5. Update `_currentPageRef`

**GetHar:**
1. Validate state (not disposed, capturing)
2. Lock `_lock`
3. Serialize Har to JSON string (compact, no indentation)
4. Deserialize JSON back to new Har instance
5. Return clone

**Stop:**
1. Validate state (not disposed, capturing)
2. Call `await _strategy.StopAsync()`
3. Set `_isCapturing = false`
4. Unsubscribe from `_strategy.EntryCompleted`
5. Return `_har` (no clone needed, capture stopped)

**Dispose:**
1. If already disposed, return
2. If strategy not null: unsubscribe EntryCompleted, dispose strategy, set null
3. Set `_disposed = true`

### HAR Model Immutability Pattern

HAR models are sealed classes with `init` properties (NOT records). The `with` expression does NOT work. To modify Har/HarLog, we must:

1. Extract current values from existing instance
2. Create new lists/collections with modifications
3. Use object initializer syntax to create new instance, copying ALL properties

Example (adding entry):
```csharp
var entries = new List<HarEntry>(_har.Log.Entries ?? Array.Empty<HarEntry>()) { newEntry };
_har = new Har
{
    Log = new HarLog
    {
        Version = _har.Log.Version,
        Creator = _har.Log.Creator,
        Browser = _har.Log.Browser,
        Pages = _har.Log.Pages,
        Entries = entries,  // New list with entry added
        Comment = _har.Log.Comment
    }
};
```

### Deep Clone Strategy

GetHar() uses JSON round-trip for deep cloning:

**Pros:**
- Simple, no manual recursive cloning code
- Guaranteed complete independence (no shared references)
- Leverages existing HarSerializer

**Cons:**
- Serialization overhead (serialize + deserialize)
- Not hot path (GetHar is snapshot operation, not called frequently)

**Alternative considered:** Manual recursive cloning. Rejected because:
- Complex to implement for 18 HAR model classes
- Error-prone (easy to miss a nested collection)
- JSON approach is simpler and correct by construction

### Thread Safety

HarCaptureSession uses simple lock-based thread safety:

**Lock scope:** `lock (_lock)` for:
- InitializeHar (Start)
- OnEntryCompleted (event handler from strategy)
- NewPage
- GetHar

**Rationale:**
- Har models are immutable, so rebuilding requires atomic read+write
- Lock ensures no race conditions during rebuild
- No contention expected (GetHar is infrequent snapshot operation)

**Not locked:** Start, Stop, Dispose (use boolean flags for state checks)

### URL Pattern Filtering

UrlPatternMatcher filters entries before adding to Har:

**Flow:**
1. Strategy produces HarEntry
2. Fires EntryCompleted event
3. HarCaptureSession.OnEntryCompleted receives entry
4. Calls `_urlMatcher.ShouldCapture(entry.Request.Url)`
5. If false, returns early (entry discarded)
6. If true, adds entry to Har

**Semantics:**
- Exclude patterns evaluated first (highest priority)
- Include patterns evaluated second (only if not excluded)
- No patterns specified = capture all (default permissive)

## Dependencies

### Upstream (Required)
- Phase 01: HAR model classes (Har, HarLog, HarEntry, HarPage, HarRequest, HarResponse, HarTimings, HarCache, HarCreator, HarContent, HarCookie, HarHeader, HarQueryString, HarPageTimings)
- Phase 02 Plan 01: CaptureType, CaptureOptions, INetworkCaptureStrategy, RequestResponseCorrelator, UrlPatternMatcher
- HarSerializer for deep cloning

### Downstream (Enables)
- Phase 03: CDP capture strategy implementation (will use HarCaptureSession for integration tests)
- Phase 04: INetwork capture strategy implementation (will use HarCaptureSession for integration tests)
- Phase 05: Public API with WebDriver extension methods (will build on HarCaptureSession)

### Test-Only
- MockCaptureStrategy: Test helper implementing INetworkCaptureStrategy for unit testing HarCaptureSession without real WebDriver

## Next Steps

1. **Immediate (Phase 03):** Implement CDP capture strategy using INetworkCaptureStrategy interface. Strategy will subscribe to CDP Network domain events (requestWillBeSent, responseReceived, loadingFinished) and use RequestResponseCorrelator to produce HarEntry instances.

2. **Phase 04:** Implement INetwork fallback strategy for cross-browser support when CDP is unavailable.

3. **Phase 05:** Create public API facade with WebDriver extension methods for one-liner capture usage.

4. **Future enhancement:** Consider async/await pattern for GetHar() if JSON round-trip overhead becomes performance concern (unlikely, snapshot operation is not hot path).

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|---------|------------|
| JSON round-trip performance in GetHar() | Low | Low | GetHar is snapshot operation, not hot path. If needed, implement manual recursive clone later |
| Lock contention on `_lock` | Very Low | Low | GetHar is infrequent. OnEntryCompleted rebuild is fast (just list append + object creation) |
| HAR model property missed during rebuild | Low | High | Comprehensive tests verify all properties preserved. Pattern is consistent across all rebuild sites |
| Strategy EntryCompleted fires after Dispose | Low | Medium | Dispose unsubscribes event handler before disposing strategy. Check `_disposed` in handler if needed |

## Lessons Learned

1. **Sealed classes with init properties require object initializers:** HAR models are NOT records. Cannot use `with` expressions. Always create new instances with explicit object initializers.

2. **JSON round-trip is simplest deep clone:** For complex object graphs (18 model classes with nested collections), JSON serialization+deserialization is simpler and safer than manual recursive cloning.

3. **InternalsVisibleTo enables testability:** Making INetworkCaptureStrategy internal (not public) keeps API surface small while allowing comprehensive unit testing via InternalsVisibleTo.

4. **Lock-based thread safety is sufficient:** No need for ConcurrentDictionary or other advanced patterns when rebuilding immutable objects. Simple lock ensures correctness.

5. **Test helpers reduce boilerplate:** MockCaptureStrategy and CreateTest* helpers make tests readable and maintainable. 17 tests written with minimal code duplication.

## Self-Check: PASSED

**Created files exist:**
✅ src/Selenium.HarCapture/Capture/HarCaptureSession.cs
✅ tests/Selenium.HarCapture.Tests/Capture/CaptureTypeTests.cs
✅ tests/Selenium.HarCapture.Tests/Capture/CaptureOptionsTests.cs
✅ tests/Selenium.HarCapture.Tests/Capture/Internal/UrlPatternMatcherTests.cs
✅ tests/Selenium.HarCapture.Tests/Capture/Internal/RequestResponseCorrelatorTests.cs
✅ tests/Selenium.HarCapture.Tests/Capture/HarCaptureSessionTests.cs

**Modified files exist:**
✅ src/Selenium.HarCapture/Selenium.HarCapture.csproj

**Commits exist:**
✅ ba8277a: feat(02-02): add HarCaptureSession orchestrator with Start/Stop/NewPage/GetHar lifecycle
✅ e77ff69: test(02-02): add comprehensive unit tests for Phase 2 components

**Verification commands:**
```bash
# Check files
[ -f "src/Selenium.HarCapture/Capture/HarCaptureSession.cs" ] && echo "✅ HarCaptureSession.cs"
[ -f "tests/Selenium.HarCapture.Tests/Capture/CaptureTypeTests.cs" ] && echo "✅ CaptureTypeTests.cs"
[ -f "tests/Selenium.HarCapture.Tests/Capture/CaptureOptionsTests.cs" ] && echo "✅ CaptureOptionsTests.cs"
[ -f "tests/Selenium.HarCapture.Tests/Capture/Internal/UrlPatternMatcherTests.cs" ] && echo "✅ UrlPatternMatcherTests.cs"
[ -f "tests/Selenium.HarCapture.Tests/Capture/Internal/RequestResponseCorrelatorTests.cs" ] && echo "✅ RequestResponseCorrelatorTests.cs"
[ -f "tests/Selenium.HarCapture.Tests/Capture/HarCaptureSessionTests.cs" ] && echo "✅ HarCaptureSessionTests.cs"
[ -f "src/Selenium.HarCapture/Selenium.HarCapture.csproj" ] && echo "✅ Selenium.HarCapture.csproj"

# Check commits
git log --oneline --all | grep -q "ba8277a" && echo "✅ Commit ba8277a"
git log --oneline --all | grep -q "e77ff69" && echo "✅ Commit e77ff69"
```

All checks passed. Plan execution complete.
