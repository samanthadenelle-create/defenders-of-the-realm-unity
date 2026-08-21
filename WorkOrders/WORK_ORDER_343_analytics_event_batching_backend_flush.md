<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-343 — Analytics: event batching + periodic backend flush

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).

**Depends on:** WO-121 (metrics dashboard exists), WO-80 (backend routing ready)

**Lane:** 10 (Build/Deploy/Performance)

---

## Summary

Analytics sends one HTTP request per event (kills performance). This WO **batches events in memory** and flushes them to the backend in a single POST every N seconds (default 30s or on scene unload).

Reduces HTTP overhead, improves frame stability.

---

## Files to edit

- `Assets/_Modules/Core/Analytics/AnalyticsService.cs`
  - Add `List<AnalyticsEvent> eventQueue`
  - Method `LogEvent(string eventType, params)` → adds to queue (non-blocking)
  - Coroutine `FlushEvents()` → POST `/api/analytics/batch` with queue every 30s
  - On scene unload: force flush (send any pending events)
  - Cache HTTP client to reuse connections
- `Assets/_Modules/Core/Analytics/AnalyticsEvent.cs` (struct)
  - Fields: `timestamp`, `userId`, `eventType`, `data` (Dict<string, object>)
  - Serializable for JSON

---

## Acceptance criteria

- [ ] Events queue in memory without blocking main thread
- [ ] Flush endpoint accepts batch array of events
- [ ] Flushes every 30s ± 5s (timer-based)
- [ ] On scene unload: FlushEvents() called before scene load completes
- [ ] If POST fails: events stay in queue (retry on next flush)
- [ ] Brace balance check passes
- [ ] No blocking network calls on main thread

---

## What NOT to do

- Do NOT send analytics for test/editor builds
- Do NOT batch debug logs (only game events)
- Do NOT implement the backend POST endpoint (that's a separate Vercel work order)

---

## Notes

Test with a lot of concurrent events (e.g., 100 enemies spawning = 100 "enemy_spawn" events). Queue should handle it without lag.

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `EventTracker.cs:6-63` — batching/retry/circuit-breaker shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
