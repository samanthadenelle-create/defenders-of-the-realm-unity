**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 445 — Remote (server-controlled) feature flags — kill-switch without a build

**Status: DESIGN SPEC.** Owner (2026-06-17): "enough safety flags where you can remotely turn off a
feature by flag… it might persist an hour… **but it will not wait for the next build.**" Lane 7
(Persistence/Backend) + Core. The kill-switch half of zero-downtime ops (sibling to WO-443 WebTrace).

## The hard requirement (the whole point)
The flag value is read at **RUNTIME from the server** by the **already-shipped build**. Flipping a flag
server-side changes the behavior of the **live build a player/reviewer is running right now** —
**NO rebuild, NO re-host, NO store review.** A build-time/baked flag is useless for incident response
(changing it = a new build); this is the opposite of that. **It must not wait for the next build.**

## Behavior
- On load (and on a refresh cadence), the client **fetches the authoritative flag set from the backend**
  and caches it. A flip propagates to all live clients within the refresh window (owner: "might persist
  an hour" — the realistic TTL/poll cadence; an hour to contain a bad feature with zero deploy beats a
  multi-hour hotfix).
- **Server is authoritative**; the local PlayerPrefs/URL overrides stay for dev, but the remote value
  wins for shipped builds (with a clear precedence rule + a "stale cache" fallback if the fetch fails →
  use the last-known-good, never hard-fail).
- **Two tiers** (same plumbing as WO-443's account flag):
  - **Global** — kill/enable a feature for EVERYONE (incident response: a broken `Raid`/`Arena`/panel off).
  - **Account / cohort / %** — per-player, per-cohort, or % rollout (canary, A/B, "enable trace for THIS user").

## Why it's mostly assembly
`FeatureFlags` already centralizes every gate and already encodes the law **"a reachable feature must
WORK or be HIDDEN"** — so the off-state is already a clean, designed state for every flag. This WO just
changes where the value COMES FROM (server, runtime-fetched) — the gates, the call sites, and the
"hidden is clean" guarantee are already in place.

## Client (this repo)
1. **`RemoteFlags` fetch + cache** (`Core`): on startup, GET the flag set from the backend (mirror
   `GameStateService` UnityWebRequest + auth headers; reuse WO-443's backend pattern). Cache to disk
   (last-known-good). Refresh on a cadence (~1h) and/or on resume. WebGL-safe; never blocks gameplay
   (Guard.Try); on failure → keep the cached set (degrade, don't disable everything).
2. **`FeatureFlags.Get` precedence** — resolve order: explicit local dev override (PlayerPrefs/URL, dev
   only) → **remote value (authoritative for shipped builds)** → baked default. So a server flip wins on
   a live build, while a dev can still force a value locally. Keep `Get` O(1) (read from the cached set).
3. **Cohort resolution** — the client sends its anonymous session/account id (WO-443) so the backend can
   return the player's effective flag set (global ∩ cohort/%). The client just consumes the resolved set.

## Backend (React/Vercel repo — confirm/needed)
- `GET /api/flags?session=…&build=…` → returns the effective flag set for that client (global overrides +
  cohort/% applied), with cache headers (~1h) so a CDN can serve it cheaply. An admin path to flip a
  global flag or set a cohort/% rollout. Persist in Neon (a `flags` + `flag_overrides`/`cohort` table).
- Cache/CDN TTL = the propagation window. Security per WO-429 (no secrets client-side; auth + rate-limit).

## Acceptance
- [ ] Flipping a GLOBAL flag on the server turns the feature off in an ALREADY-RUNNING/shipped build
      within the refresh window — **no rebuild, verified.**
- [ ] Remote value is authoritative for shipped builds; dev override still works locally; precedence clear.
- [ ] Fetch failure → last-known-good cache (no hard-fail, no all-off).
- [ ] Cohort/% rollout works (a flag ON for one cohort, OFF for another).
- [ ] WORK-or-HIDDEN preserved: the off-state of every remote-flagged feature is clean (no broken-visible).
- [ ] No secrets client-side; endpoint auth + rate-limit; client never blocks on the fetch.

## What NOT to touch
- Don't change the flag GATES/call sites or the WORK-or-HIDDEN law — only the value SOURCE. Reconcile onto
  `FeatureFlags` + `GameStateService` + WO-443's backend; additive. §0: client edits on Windows path.

*Cross-ref:* `FeatureFlags.cs` (the demo law + gates), WO-443 (WebTrace, same backend/account tiers),
WO-429 (Neon + security), `ARCHITECTURE_PRINCIPLES.md §0` (HP B2B zero-downtime ops). The incident triangle:
**see it (WebTrace) → kill it (this WO) → fix it calmly** — guards keep the off-state clean throughout.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
