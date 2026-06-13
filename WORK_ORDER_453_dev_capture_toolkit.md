# WORK ORDER 453 — Dev Capture Toolkit (DevCaptureService spine + detector probes + processor)

**Status: READY TO IMPLEMENT** · Lane: QA/Tooling · P1 · Owner directive 2026-06-13 (Grok-seeded, CLI-synthesized)
**Companion:** `DEBUGGER_TOOLKIT_DESIGN.md` (architect — the 5 detector probes + `DebugProbe` base).
This WO is the **capture spine + processor + best-practices** that those probes feed into.

## Principle (owner): "tools HP depends on daily"
One-tap/hotkey access, maximize actionable data, **zero impact on shipped player builds**, works in Editor
AND device builds, and **integrates with the existing FlowTrace / Guard / BreakCaptureHarness / AutoPilot /
SessionRegression** — do NOT rebuild what we have; enrich + unify it.

## What already exists (do NOT greenfield — verified from code)
- **F8 capture** → `BreakCaptureHarness` (screenshot + `break-log.jsonl` + Player.log). → ENRICH, don't replace.
- **Flow buffer** → `FlowTrace` (Step/Warn/Fail/Throttle/Once/Measure, per-category mute).
- **UI click-intercept dump** → `DebuggingController` (just built: F9 overlay, `Capture()`, `FindFrame`) + `PointerInterceptDiagnostic`.
- **Bot action traces + tickets** → `AutoPilotDriver` / `AutoPilotTickets`. **Data round-trip** → `SessionRegression` / `DataRegression`.
- **Seam proximity** → `SeamTrace` in `SceneTransitionTrigger` (+ nearest-in-range-wins).
- **NavMesh verify (editor, scene-hardcoded)** → `CastleGateNavVerify` (generalize, don't restart).

## A. THE SPINE — `DevCaptureService` (DeNelle.Core.Diagnostics, singleton)
Every capture (F8, F9/DBG, F10 perf, AutoPilot ticket, SessionRegression failure) routes through ONE service that
emits a self-contained report:
- **Output:** `BugReports/BugReport_YYYYMMDD_HHMMSS.zip` (PC: project/persistent path; device: `Application.persistentDataPath`).
  Each zip = structured **`report.json`** + human-readable **`report.txt`** sidecar + **screenshot.png**.
- **Contents:** high-res screenshot (`ScreenCapture`), last **120s FlowTrace ring buffer**, full **state dump**
  (resources, mine/harvest nodes, pets, inventory, wave phase, hero pos + equipped gear, active UI panels list,
  Solana/wallet status), **last 30 actions timeline**, and any **detector flags** raised this session (magenta,
  dup-panel, seam-overlap — from §B probes).
- **Tags (every report):** build hash, **git commit**, scene name, **seed** (bot runs), device/screen specs, timestamp.
- **Gating:** wrap the whole service in `#if DEVELOPMENT_BUILD || UNITY_EDITOR` so it is **stripped from store/release
  builds** (caveat: our current Windows *playtest* builds are dev-class — F8 works — so tools stay live there; only the
  final store build path strips them). **Auto-delete** reports older than **14 days** on boot.
- **Hooks:** F8 → full capture (extend BreakCaptureHarness to call this). F9 → DebuggingController overlay (built).
  F10 → perf snapshot (§B6). `DevCaptureService.Capture(label)` → callable from any seam (e.g. Yarn-exit
  `CompanionDialoguePresenter.OnDialogueCompleteAsync` — "grab everything + log the next action").

## B. DETECTORS (each = a `DebugProbe`; flag-gated `Enabled=false`; feeds the spine AND a bot assert)
Per the owner: **the bots must be able to target these too** — so EACH probe exposes a headless
`Assert()` (pass/fail + findings) that `AutoPilotDriver` / `SessionRegression` call (ties to WO-452). Same check,
two consumers: on-screen/capture for manual triage, structured result for the fleet.
1. **MagentaMaterialProbe** — real-time + on-capture log scan for missing-shader/magenta/null-sprite signatures;
   flags the offending assets into the report. (Headless-safe — Unity logs these in `-nographics`.) [WO-452 §A]
2. **PanelHealthProbe** — overlapping UIDocuments / `panel=<null>` / stuck CanvasGroups / raycast blockers (the
   "buttons dead after Yarn" class). [architect doc]
3. **SeamReachabilityProbe** — generalize `CastleGateNavVerify` to any scene: per-seam `closestEver` vs radius +
   warn on multi-seam overlap. [architect doc]
4. **SubscriptionProbe** (DeNelle.Village — asmdef boundary is mandatory) — assert key service events have
   subscribers (e.g. `EconomyService.OnChanged subscribers>0`). This is the missing *detector* for the bug
   `HeartHudBridgeBootstrap` just patched blind.
5. **TripoAssetProbe** / **HeroPresenceProbe** — Phong/mis-rotation; hero-destroyed-by-single-load.

## C. SUPPORTING CAPTURES
- **Action Recorder** — background ring buffer of last 60–90s of key actions (move targets, taps, dialogues opened,
  builds placed) → readable timeline in every report. (Manual-play complement to the bot's action trace.)
- **Perf snapshot (F10 / in F8)** — FPS history, memory, draw calls, top profiler samples; flag < 30 FPS / GC spikes.
- **BugReportProcessor** (Editor window) — scans `BugReports/`, groups similar reports, suggests root causes from the
  flags, emits a daily summary markdown. AutoPilot tickets + SessionRegression failures use the SAME report format so
  the processor ingests all three.
- **Dev menu panel** listing every hotkey + toggle (verbose-FlowTrace toggle, each probe's Enabled flag).

## Build order (max triage ROI first)
1. **`DebugProbe` base + `DevCaptureService` spine** (built together) — the foundation everything plugs into.
2. **MagentaMaterialProbe** — directly serves the visual bugs we're still chasing (the wight class).
3. **PanelHealthProbe + SeamReachabilityProbe** — the two multi-hour-RCA classes this session.
4. **SubscriptionProbe**, Action Recorder, Perf snapshot.
5. **BugReportProcessor** + dev-menu panel + AutoPilot/SessionRegression report-format unification.

## Acceptance
- [ ] F8 produces a tagged `BugReport_*.zip` (json + txt + png) with state + 120s flow + last-30 actions.
- [ ] A magenta asset in a scene raises a flag that lands in the next capture AND a headless `Assert()` failure.
- [ ] Each probe is `Enabled=false` by default, toggleable, and `#if DEVELOPMENT_BUILD || UNITY_EDITOR`-gated.
- [ ] An AutoPilot run emits the same report format; the processor groups it with manual F8 reports.
- [ ] Nothing in this WO compiles into a store/release build (verify the release build path strips it).

## Do NOT
- Rebuild BreakCaptureHarness / FlowTrace / DebuggingController / PointerInterceptDiagnostic — extend them.
- Put a Village-service probe in DeNelle.HUD (asmdef boundary — that gap caused the economy bug).
- Ship any of it to players (the `#if` guard is mandatory).
