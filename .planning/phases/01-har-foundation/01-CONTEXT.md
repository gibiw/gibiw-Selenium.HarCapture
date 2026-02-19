# Phase 1: HAR Foundation - Context

**Gathered:** 2026-02-19
**Status:** Ready for planning

<domain>
## Phase Boundary

17 HAR 1.2 compliant model classes + JSON serialization/deserialization + file save/load. This is the data foundation for all capture strategies. No capture logic, no WebDriver interaction — pure data model and serialization.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
User deferred all implementation decisions to Claude. Full flexibility on:

- **Model design**: Class mutability, constructors vs init properties, default values, validation
- **Spec compliance**: HAR 1.2 adherence level, comment fields, custom/extension fields handling
- **Serialization**: JSON formatting, unknown field handling, naming conventions, converter design
- **Project setup**: Solution structure, test framework choice, namespace organization, folder layout

Guidelines from architecture plan (locked decisions from PROJECT.md):
- Sealed model classes (decided in project initialization)
- System.Text.Json for serialization (decided in project initialization)
- netstandard2.0 target (decided in project initialization)
- JsonPropertyName attributes on all fields
- JsonIgnore(WhenWritingNull) for optional fields
- ISO 8601 DateTimeOffset via custom converter

</decisions>

<specifics>
## Specific Ideas

Architecture plan provides detailed model specification:
- 17 classes mapping to HAR 1.2 spec objects (Har, HarLog, HarCreator, HarPage, HarPageTimings, HarEntry, HarRequest, HarResponse, HarContent, HarCookie, HarHeader, HarQueryString, HarPostData, HarParam, HarCache, HarCacheEntry, HarTimings)
- HarSerializer static class with Serialize/Deserialize/SaveAsync/LoadAsync
- DateTimeOffsetConverter for ISO 8601 compliance

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-har-foundation*
*Context gathered: 2026-02-19*
