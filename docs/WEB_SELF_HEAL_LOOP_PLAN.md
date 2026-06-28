# Web Self-Healing Loop — Implementation Plan (drafted 2026-06-27 overnight)

**Goal (owner vision):** after a web deploy, a cron job runs the autopilot bots in a real browser
against the deployed build, the bots' chaos run streams logs to the database, a timed cron pulls those
logs, triages/fixes, redeploys, and runs again — a closed self-healing loop.

**Why this is a PLAN, not a done thing tonight:** steps 3–4 are **outward-facing** (deploy to Vercel +
read/write the production Neon DB + arm a cron). Per the confirm-before-outward rule — and because the
itch→Vercel move itself is still the owner's pending call — these are staged to a **one-command morning
GO**, not executed autonomously at night. The buildable, reversible code pieces (steps 1–2) can be
authored + gated overnight on request; nothing here is deployed yet.

---

## Current state (verified by recon, 2026-06-27)
Good news — the loop is **~60% already built**:

| Piece | State | Path |
|---|---|---|
| Client log→HTTP sink (`WebTrace`) | **built**, batched ring-buffer, WebGL-gated — but **dormant** (`TraceEndpoint = ""`) | `Assets/_Modules/Core/Diagnostics/WebTrace.cs` |
| Analytics backend | **built + deployable** — Vercel serverless → Neon `analytics_events` | `api/events/track.js` |
| Event client | **built** | `Assets/_Modules/Core/Analytics/EventTracker.cs` |
| Bug-report endpoint | **built** | `api/bug-report.js` |
| DB schema | **built** | `api/schema.sql` (+ `api/DB_SETUP.md`, `api/DEPLOY.md`) |
| WebGL build pipeline | **proven** | `build-webgl.ps1` (`DeNelle.Editor.WebGLBuild.BuildWebGL`) |
| Vercel config | **present** (`deploymentEnabled:false`, outputs `Builds/WebGL`) | `vercel.json` |
| AutoPilot driver/installer | **built but editor/dev-only** (`#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR`); reads `--autopilot`/`AUTOPILOT` | `AutoPilotDriver.cs` / `AutoPilotInstaller.cs` |

**So the only gaps are:** (1) wire the dormant trace sink, (2) make AutoPilot launchable in a browser
build, (3) a browser driver, (4) the cron orchestration + triage glue.

---

## Step 1 — Wire telemetry (SMALL, client-side, reversible)
Two viable routes; **Route A preferred** (no new endpoint/table/deploy):

- **Route A — reuse `api/events/track`.** Point `WebTrace` at `/api/events/track` and shape each batched
  trace as a track event: `eventName="flow_trace"`, `properties = {tag, msg, scene, ts}` (JSON string),
  `playerId = boundWallet|"autopilot"`. Lands in the existing `analytics_events` table. **Change = a
  handful of lines in `WebTrace.cs`** (endpoint const + payload formatter) + flip `FeatureFlags.WebTrace`
  on for autopilot URLs (`?trace=1` already supported). Gate the .cs. No backend change.
- **Route B — dedicated `api/trace.js` + `web_traces` table.** Cleaner separation, but needs a new
  endpoint + a `schema.sql` migration + a deploy. Only worth it if trace volume should not pollute
  `analytics_events`. Defer unless the owner wants the split.

**Recommendation:** Route A first (zero backend risk); split to B later if volume warrants.

## Step 2 — Make AutoPilot runnable in a browser build (MEDIUM, client-side)
AutoPilot is compiled out of release WebGL. To run bots in a browser:
1. Build WebGL as a **development build** (so `DEVELOPMENT_BUILD` is defined and AutoPilot compiles in).
   Confirm/THEN add a dev flag to `build-webgl.ps1` (or a `BuildWebGLDev` method).
2. Add a **URL→autopilot bridge**: a tiny WebGL template JS hook that reads `?autopilot=1&seed=N` and
   calls `unityInstance.SendMessage("[AutoPilot]", "StartFromWeb", seedJson)`, plus a runtime entry on
   the AutoPilot installer that accepts that SendMessage (mirrors the `--autopilot` arg path).
3. Bots then drive the deployed page headlessly with the same oracle/chaos seeds as the desktop fleet.

Gate the .cs; this is all reversible client code behind the dev build + a URL param (inert in prod).

## Step 3 — Browser driver (MEDIUM, outward-facing read) — MORNING GO
- A small driver (Claude-in-Chrome tools are available, or Playwright/puppeteer in a Vercel/GitHub
  action) opens N tabs at `https://<deploy-url>/?autopilot=1&trace=1&seed=<i>`, lets each run a chaos
  pass for T minutes, then closes. Traces stream to the DB via Step 1.
- **First milestone before any cron:** run this against **localhost** (`vercel dev` or a local static
  serve of `Builds/WebGL`) to confirm the trace pipeline end-to-end with zero production exposure.

## Step 4 — Cron orchestration (outward-facing) — MORNING GO
1. **Deploy** the WebGL dev build to Vercel (manual `vercel --prod` or re-enable git deploy).
2. **Cron A (post-deploy bot run):** Vercel Cron / GitHub Action triggers the Step-3 driver on a schedule.
3. **Cron B (pull + triage):** a timed job queries `analytics_events WHERE event_name='flow_trace'
   AND client_ts > <last>` for `Fail/Warn` lines, ranks by repro count (mirror `AutoPilotTickets.Emit`),
   and writes tickets. CLI triages → fixes → re-gates → redeploys → loop.
4. **Cleanup:** a retention job prunes old trace rows so the table stays small.

---

## Risks / cost / guardrails
- **Outward exposure:** Steps 3–4 read/write the prod DB + deploy. Get owner GO; start on localhost.
- **DB volume:** traces are chatty — keep `FlowTrace.Enabled`/`WebTrace` gated to `?trace=1` (autopilot
  only), not every real player. Add retention (Step 4.4).
- **Dev build size/leak:** the autopilot dev build is for the loop, NOT the public player build. Don't
  ship `?autopilot` wired into the production URL.
- **Auth:** `api/events/track` is currently open (fire-and-forget). Fine for traces; if abused, gate by a
  shared header.

## Suggested execution order (morning)
1. Step 1 Route A (wire `WebTrace` → `/api/events/track`) + gate. *(safe, do first)*
2. Step 2 (dev WebGL build + URL→autopilot bridge) + gate.
3. Step 3 against **localhost** — confirm traces land in `analytics_events`.
4. Owner GO → deploy + Step 3 against the live URL.
5. Step 4 crons once the manual loop is proven.

> This doc is the #21/#23 deliverable: the loop is **planned, de-risked, and staged**. Nothing is
> deployed. Steps 1–2 are buildable overnight on request; 3–5 are the owner's GO.
