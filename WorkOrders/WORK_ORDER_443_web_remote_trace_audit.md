**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 443 — Web remote trace/audit: FlowTrace → DB sink (WebGL), 7-day TTL

**Status: DESIGN SPEC.** Owner (2026-06-17): "for the web version we need some audit in place — we'll
see real issues and need to activate web tracing, a debug method for web that writes to a log in DB.
Lifespan 7 days." Lane 7 (Persistence/Backend) + Core/Diagnostics. Forward-looking — for the WebGL grant
demo where you can't reproduce a real player's issue locally.

## Goal
A toggleable remote-logging sink so that, when **web tracing is activated**, the WebGL build streams its
diagnostic logs (`FlowTrace` Step/Warn/Fail + Unity errors/exceptions) to the backend, which writes them
to the DB. Rows **auto-expire after 7 days**. Off by default (don't spam the DB); activatable per
session/player when chasing a real issue.

## Why it fits cleanly
`FlowTrace` (Core/Diagnostics) already tags every diagnostic line `[Flow:<system>]` and routes through
Debug.Log/Warn/Error; `BreakCaptureHarness` already captures locally to break-log.jsonl. This WO just adds
a SECOND sink — a remote one for the web build — reusing the existing backend call pattern
(`GameStateService` UnityWebRequest + auth headers, the Neon DB; security model per WO-429).

## Client (this repo)
1. **`WebTrace` sink** (`Assets/_Modules/Core/Diagnostics/WebTrace.cs`): subscribes to
   `Application.logMessageReceived` (catches Log/Warning/Error/Exception incl. the stack) AND/OR taps
   `FlowTrace` directly. Buffers entries in a bounded ring (cap N, drop-oldest) and FLUSHES in batches
   (every ~5s or N entries) via a single `UnityWebRequest` POST — WebGL-safe, non-blocking, never throws
   into gameplay (wrap in `Guard.Try`). Each entry: { sessionId, utcMs, kind (log/warn/error/exception/flow),
   tag, message, stack?, buildId, scene }. Include a short anonymous `sessionId` (no PII).
2. **Activation — 3 tiers (owner 2026-06-17):**
   - `FeatureFlags.WebTrace` (PlayerPrefs `ff.webtrace`, default OFF) — local/dev.
   - WebGL URL `?trace=1` — per-session ad-hoc (support gives a player a link).
   - **ACCOUNT-LEVEL flag (the robust one)** — a server-set boolean on the player's account/record
     (e.g. `GameState.Account.TraceEnabled` synced from the backend on load). When the backend flags an
     account for tracing, the client reads it on login and activates WebTrace for THAT player across
     sessions — so support can target a specific real player who's hitting an issue, without a link or a
     rebuild. The account flag is authoritative; any of the three turns it on. Add the field to the
     account/save sync (additive; backend sets it, client reads it).
   Only when active does `WebTrace` POST.
3. **Gate to WebGL** — `#if UNITY_WEBGL && !UNITY_EDITOR` (or a platform check) so it's a no-op on
   standalone/editor (which already have break-log.jsonl). Batch + cap so a log storm can't flood.

## Backend (React/Vercel repo — confirm/needed)
- A `POST /api/trace` endpoint that validates the auth header (same as save/load), accepts the batch, and
  inserts rows into a Neon `web_traces` table: `(id, session_id, utc, kind, tag, message, stack, build_id,
  scene, created_at default now())`.
- **7-day TTL:** either a scheduled cleanup (`DELETE FROM web_traces WHERE created_at < now() - interval
  '7 days'`, a daily Vercel cron) OR a Postgres `created_at` index + the cron. (Neon has no native TTL —
  use the cron.)
- A read path (admin-only) to query recent traces by session/build for triage.

## Security (non-negotiable — per WO-429)
- The Neon connection string lives ONLY in the backend env (Vercel), NEVER in the client (it would ship in
  the WebGL build = extractable). Client → HTTPS → backend → Neon.
- No PII in traces (anonymous session id only). Rate-limit / cap the endpoint so a hostile client can't
  flood the DB.

## Acceptance
- [ ] Compile gate green (client). WebGL build only — no-op elsewhere.
- [ ] With `ff.webtrace=1` (or `?trace=1`), a WebGL session's FlowTrace + errors land in `web_traces`.
- [ ] OFF by default — no traffic when not activated.
- [ ] Rows older than 7 days are purged (cron verified).
- [ ] No connection string in the client build; endpoint auth + rate-limit in place.
- [ ] WebTrace never throws into gameplay (Guard.Try); batched, bounded, non-blocking.

## What NOT to touch
- Don't change `FlowTrace`/`BreakCaptureHarness` local behavior — ADD the remote sink alongside.
- Don't ship the connstring client-side. Reconcile onto `GameStateService`'s backend call pattern.

*Cross-ref:* `FlowTrace.cs`/`Guard.cs`/`BreakCaptureHarness.cs` (§12), `GameStateService.cs` (backend
call + auth-header pattern), WO-429 (Neon + security model), `FeatureFlags.cs`.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
