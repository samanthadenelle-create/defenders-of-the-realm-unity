<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 121 — Web Metrics & Analytics Dashboard

> (Renumbered from WO-106 → WO-109 to avoid collision with UI's WO-106 in-game XP HUD. Distinct features: this is the **web/backend** dashboard; WO-106 is the **in-game** XP display.)

**Status:** READY TO SPEC-REVIEW
**Date:** 2026-05-29
**Target repo:** `defenders-of-the-realm` (Vercel `/api` + Neon Postgres) — **NOT the Unity repo**
**Owner of build:** backend dev (Kayden). Unity-side event instrumentation = CLI.
**Priority:** Medium — owner is a metric junkie; this is the *aggregate/historical* counterpart to the in-game F1 dev panel.

---

## Reconciliation (read first — most of this already exists)

> Per the project's recurring "reconcile, don't duplicate" rule — **do NOT build a new app.** The pieces already in place:

| Piece | State | Location |
|---|---|---|
| Client emits telemetry | **BUILT** — `EventTracker.Track(name, payload)` POSTs to the backend (`UnityWebRequest`, `EventTracker.cs:290`) | Unity `Assets/_Modules/Core/Analytics/EventTracker.cs` |
| Ingest endpoint | **DRAFTED** — `POST /api/metrics { identity, eventName, payload, clientTs }` → appends one row | `docs/draft-backend-endpoints/metrics.ts` (copy to backend `api/metrics.ts`) |
| Storage | Neon Postgres `metrics` table (`_db` `insertMetricEvent` / `ensureSchema`) | backend `api/_db.ts` |
| Admin web console | **SPECCED** — password-protected `/admin`, tabs: Live Config · Events/Sales · Players · Ops · Audit Log | `docs/admin-console-spec.md` |

**This WO = add a 6th tab — "Metrics" — to the admin console** that reads the `metrics` table and charts it. It depends on the admin console (`admin-console-spec.md`) shipping first (shares its auth + layout).

---

## Scope

### A. Backend (Vercel `/api`, Kayden)
1. **Deploy ingest:** copy `docs/draft-backend-endpoints/metrics.ts` → `api/metrics.ts`; confirm `metrics` table schema (`ensureSchema`). Until this is live, events are POSTed into the void.
2. **New read endpoint `api/admin/metrics.js`** (auth-gated like the other `api/admin/*`):
   - `GET /api/admin/metrics?view=<name>&from=<ts>&to=<ts>` → runs a parameterized SQL aggregate against the `metrics` table, returns JSON for charting.
   - Views (each one SQL query): `dau` (daily active by `identity`), `retention` (D1/D7), `session_length`, `wave_progress` (max wave reached, clear rate per wave), `economy` (crystal/coin/glimmer sources vs sinks from economy events), `purchase_funnel` (`bundle_viewed` → `purchase_completed` by SKU), `errors` (client error events).
   - Read-only; no PII beyond the existing `identity` string.

### B. Admin console UI (`/admin`, new "Metrics" tab)
- Date-range picker (default last 7d) + refresh.
- Cards: DAU, new players, D1/D7 retention, avg session, total revenue (devnet-labeled).
- Charts (a light lib — Recharts/Chart.js): DAU over time, wave-progress histogram, economy source/sink bars, purchase funnel.
- A raw "event explorer" table (filter by `eventName`, paginated) for ad-hoc digging.

### C. Unity-side instrumentation gap (CLI — small)
The dashboard is only as good as the events emitted. Today `EventTracker` fires e.g. `bundle_viewed`, `purchase_completed`. To populate the views above, add `EventTracker.Track(...)` calls for:
- `session_start` / `session_end` (+ duration) — session metrics.
- `wave_started` / `wave_cleared` / `run_ended` (wave reached) — progression + clear rates.
- `economy_grant` / `economy_spend` (resource, amount, reason) — economy source/sink. (Hook the existing `GameStateService.ResourcesChanged` / `EconomyService`.)
- `level_up` (hero level) — progression.
- This is the only part that touches Unity → routed to CLI as a follow-up, gated behind the new build/compile checklist.

---

## Why a separate web view (not just the F1 dev panel)
The in-game `DevPanelController` reads **one live session** in real time (debugging *this* run). The web dashboard reads **all players, historically** (LiveOps decisions: retention, economy balance, funnel). Different jobs; both wanted.

## Out of scope
- No new DB beyond the `metrics` table + admin auth (reuse).
- No real-money revenue (payments are devnet-stub; label revenue as devnet).
- No realtime streaming — periodic GET refresh is fine.

## Routing
- **Admin console + this Metrics tab** → backend repo `defenders-of-the-realm` (Kayden).
- **Unity event instrumentation (C)** → CLI, as a small follow-up WO with the brace/compile gate.

🤖 Spec drafted by the build-connected CLI for owner routing.
