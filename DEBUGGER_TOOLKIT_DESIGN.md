# Debugger Toolkit Design — how recurring bug classes self-report in this project

**Original:** 2026-06-13 (ARCHITECT design doc, pre-implementation).
**Reconciled:** 2026-06-19 — rewritten to match the system that was actually built.
**Type:** doc-only (no `.cs` touched). **Branch:** feat/tower-core-loop.

> ## Reconciliation note (2026-06-19) — read this first
>
> The original version of this file was a **design written before the diagnostic
> system existed.** It proposed a `DebugProbe` MonoBehaviour base class with one
> per-bug-class on-screen debugger, each on its own **F9–F12 hotkey** that a human
> presses during a manual playtest (`PanelHealthProbe`, `SeamReachabilityProbe`,
> `TripoAssetProbe`, `SubscriptionProbe`, `HeroPresenceProbe`).
>
> **None of that `DebugProbe` / per-probe-hotkey machinery was ever built, and it
> will not be.** The real system went a different — and better — direction:
> failures **self-report** through TGVRU instrumentation + a single F8 flight
> recorder, and structural/UX defects are caught by **headless fleet oracles**
> (`AutoPilotProbes`) ranked by reproduction count. This document now describes
> that real system, and reframes the two genuinely-useful original ideas as
> clearly-labelled **PROPOSED** additions to the fleet.
>
> **This supersedes the old `DebugProbe` / F9–F12-hotkey design in full.** If you
> are an AI or a human reading this to generate code: there is no `DebugProbe`
> type, no `PanelHealthProbe`/`SeamReachabilityProbe`/etc., and no per-probe
> hotkey. Do not generate code against those names. The only diagnostic hotkey is
> **F8** (the break-capture flag). Detection lives in the fleet, not in keypresses.

---

## The two banned paradigms (binding owner canon)

Everything below follows from two hard rules. They are stated up front because the
original doc violated both, and any future "debugger" proposal must honour them.

1. **Authoring is NEVER inspector drag-drop field wiring.** Content (structures,
   vendors, bodies, panels) is built via **script injectors / DB recipes**, not by
   dragging references onto a MonoBehaviour in the inspector. This is exactly why a
   `EnemyStrongholdGenerator_NavReady` MonoBehaviour was **rejected** in favour of
   the recipe-driven `EnemyStrongholdBuilder`. A "debugger" that asks the owner to
   wire it up per scene is the wrong shape.

2. **Detection is NEVER a human pressing a hotkey during a manual playtest.**
   Failures must self-report two ways:
   - **TGVRU instrumentation** — `FlowTrace.Fail` at the failing step, caught by the
     always-on F8 `BreakCaptureHarness` into `break-log.jsonl`.
   - **Headless fleet oracles** — `AutoPilotProbes` ride a chaos autopilot run and
     `FlowTrace.Fail` on structural/UX defects; the fleet ranks each by how many
     distinct runs reproduced it.

   **The owner is NEVER the detector.** A debugger you have to remember to turn on
   and press a key to query has already lost — by the time the owner notices, the
   data is gone. The signal must be a logged line that the recorder already caught.

---

## Part 0 — what already exists (verified from code, do NOT duplicate)

The diagnostic substrate is real and shipped. All of it lives under
`Assets/_Modules/Core/Diagnostics/` (runtime) plus the DevTools fleet and the
editor emitter. Method signatures below are read from the source, not the headers.

### 0.1 `FlowTrace` — the trace API
`Assets/_Modules/Core/Diagnostics/FlowTrace.cs` (`DeNelle.Core.Diagnostics`, static).
Every line is prefixed `[Flow:<system>]` so logs are greppable per bounded context.
Routed through a swappable `ITraceSink` (default `UnityLogSink` = `Debug.Log/LogWarning/LogError`).

| Call | Signature | Level | Use |
|---|---|---|---|
| `Step` | `Step(string system, string message)` | `Debug.Log` | breadcrumb — a step you reached |
| `Warn` | `Warn(string system, string message)` | `Debug.LogWarning` | fallback / anomaly taken |
| `Fail` | `Fail(string system, string message)` | `Debug.LogError` | **hard failure → break-log + screenshot** |
| `Throttle` | `Throttle(string system, string key, float everySeconds, string message)` | Info | hot-path log, at most 1 per interval per key |
| `Once` | `Once(string system, string key, string message)` | Info | first hit only this session |
| `Measure` | `using var t = FlowTrace.Measure(system, what, warnAboveMs)` | Info/Warn | scoped stopwatch; Warn if over budget |
| `Enter` | `using var _ = FlowTrace.Enter(system, what)` | Info | nested enter/exit; indents by call depth |
| `Try` | `Try(system, what, Action)` / `Try<T>(…, Func<T>, fallback)` | Error on throw | run + roll the exception up |

Controls (all O(1), default = everything on, no shipped-behaviour change):
`FlowTrace.Enabled` (master switch), `FlowTrace.Only(params…)` (allow-list),
`FlowTrace.Mute(params…)` (deny-set), `FlowTrace.AllOn()`, `FlowTrace.ResetSession()`.
`FlowTrace.Configure(TraceConfig)` selects log-vs-weblog sink + filters from a
config/remote source, reversibly, with no redeploy.

### 0.2 `Guard` — the error factory (always-on safety net)
`Assets/_Modules/Core/Diagnostics/Guard.cs` (`DeNelle.Core.Diagnostics`, static).
- `Guard.Try(system, what, Action) -> bool ok`
- `Guard.Try<T>(system, what, Func<T>, fallback) -> T`
- `Guard.TryEach<T>(system, what, IEnumerable<T>, Action<T>) -> (int built, int failed)`

One bad object logs (error-level, same `[Flow:<system>]` tag → break-log) and is
**skipped**, never blanking a whole list/screen. `Guard.Report` logs **error-level
directly** (not via `FlowTrace.Fail`) so the safety net survives even if FlowTrace
is later compile-stripped. **A silent catch is forbidden.**

### 0.3 `BreakCaptureHarness` — the F8 flight recorder
`Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs` (`DeNelle.Core.Diagnostics`).
Auto-installs at startup (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`), no
scene setup. Captures, passively, every run:
- **Errors / exceptions / failed asserts** (via `Application.logMessageReceived`) —
  so every `FlowTrace.Fail` and every `Guard` report lands here. Deduped by
  `type | condition | first-stack-line` (WO-459) so distinct NRE sites don't collapse.
- **Possible softlocks** (no hero movement AND no progress event for 75s).
- **Scene transitions** (breadcrumb trail) and an owner-pressed **F8 bug flag**
  (the ONLY hotkey in the system: screenshot the clean frame → freeze → type one note).

Writes `<persistentDataPath>/break-log.jsonl` (one JSON record per line) + PNG
screenshots, next to `Player.log` on Standalone. Fleet runs namespace output per
`--run=<id>` so concurrent instances don't clobber one shared log. Disabled on WebGL.

### 0.4 `ScreenOpenWatchdog` — names every screen that pops
`Assets/_Modules/Core/Diagnostics/ScreenOpenWatchdog.cs` (`DeNelle.Core.Diagnostics`).
Subscribes to `PanelManager.OpenStateChanged` (the single modal arbiter) and emits a
`[Flow:ScreenOpen]` `Step` naming every panel that becomes active — so a stray open
shows up by name in the F8 capture. A panel that opens the frame after a non-pointer
keypress with no pointer is flagged as a possible stray-hotkey **Warn** (downgraded
from Fail 2026-06-19: legit dialogue-command-routed opens were false-flagging).

### 0.5 `WebTrace` + `WebTraceSink` — the remote (WebGL) path
`Assets/_Modules/Core/Diagnostics/WebTrace.cs` (WO-443) captures the whole Unity log
pump and batch-POSTs to a backend for the WebGL grant-demo target that can't be
reproduced locally — **dormant by default** (needs `FeatureFlags.WebTrace` ON *and* a
configured `TraceEndpoint`, which is empty until the backend lands).
`WebTraceSink` is the FlowTrace-routed "weblog" half, selected via `FlowTrace.Configure`.
Both are bounded, reentrancy-guarded, never-throw, and fall back to the Unity log so
nothing is silently lost. Off-WebGL they no-op (local capture already covers it).

### 0.6 `AutoPilotProbes` — the real "probes" (headless fleet oracles)
`Assets/_Modules/DevTools/AutoPilotProbes.cs` (`DeNelle.DevTools`,
`#if DEVELOPMENT_BUILD || UNITY_EDITOR`). **THIS is what "a probe" means in this
project** — not an on-screen on-demand debugger, but a passive assertion oracle that
rides alongside the `AutoPilotDriver`'s scripted phases and watches world state over
time. Spawned + `Arm()`-ed only by the driver (autopilot-only). Every violation is a
`FlowTrace.Fail` tagged `[Flow:AutoTest]` → break-log → ranked ticket. The probes
actually present:

1. **UNEXPECTED-CROSS** — a raid-destination scene (`Garrison*`/`Outpost*`/`Village2`/
   `Raid*`) loaded while the bot is in normal town traversal (not an intentional
   cross phase). Catches the `requireConfirm=false` proximity auto-teleport.
2. **COPLANAR-FLOOR** — two large opaque floor `MeshRenderer`s overlap in XZ with
   centre-Y within 0.1m → a z-fight cause (works headless, per-pair Once).
3. **WALL-CLIP** — `OverlapBox` at the hero capsule (~4/sec); if the hero is inside a
   non-trigger collider whose name/ancestor reads `Wall`/`Palisade`/`Fortif`/`Rampart`,
   Fail (walking inside wall geometry).
4. **DUAL-NAVMESH / STRANDED** — (a) >1 additively-loaded scene whose XZ footprints
   overlap while a baked NavMesh is present (two navmeshes over one region); plus
   (b) the hero's `NavMeshAgent` has made no path progress toward any goal for >20s
   (possible softlock / no path to exit).
5. **NAVMESH-LINK census + connectivity** — enumerates every `NavMeshLink`, validates
   both endpoints sample onto the baked navmesh (a dangling link bridges nothing), and
   Fails when two overlapping additive scenes carry navmesh but **no** `NavMeshLink`
   bridges them (the WO-453 castle↔OuterWorld warp-only-seam class).

### 0.7 `AutoPilotDriver` + the fleet + `AutoPilotTickets`
- `Assets/_Modules/DevTools/AutoPilotDriver.cs` — the chaos autopilot bot: a coroutine
  state machine that drives the game through its **real public seams** (walk to each
  gate, open every vendor/HUD panel, assert vendor stock contracts, assert a buy
  deducts exactly + grows inventory, assert equip changes the loadout, force a wave,
  exit the castle, walk to an OuterWorld outpost on foot). A seeded RNG shuffles work
  order per run so different seeds explore different paths (chaos, not one scripted
  path). It writes no break file of its own — the always-on `BreakCaptureHarness`
  records every `FlowTrace.Warn/Fail` it emits, plus a per-run `autopilot-summary.json`.
- `run-autopilot-fleet.ps1` — launches **N headless player `.exe` instances** in
  parallel (`-Count`, `-SeedStart`, `-TimeoutMin`), each with a distinct `--seed` and
  `--run=<i>`. A player build needs no Unity license, so dozens run concurrently.
  `-batchmode -nographics` = logic/flow/crash coverage (no UI-picking/visuals). Wipes
  stale run logs first so aggregation reflects only this fleet.
- `Assets/Editor/AutoPilot/AutoPilotTickets.cs` (`DeNelle.Editor`) — headless emitter:
  scans every run's `break-log.jsonl`, **dedupes by (kind + normalized message)**
  (strips volatile coords/timings/ids), classifies, and **ranks each ticket by how
  many distinct runs reproduced it**, into `Builds/autopilot-tickets.{md,json}` with a
  single `AUTOPILOT_TICKETS_OK: <n>` / `AUTOPILOT_TICKETS_FAIL` marker.

### 0.8 On-screen UI dumps that already exist (UI bug class — fully covered)
| Tool | File | Role |
|---|---|---|
| `DebuggingController` | `Assets/_Modules/HUD/DebuggingController.cs` | on-screen 🐞 button, capture-next-click, full uGUI + UITK dump (lists every UIDocument's `panelSettings`/live panel) |
| `PointerInterceptDiagnostic` (+Bootstrap) | `Assets/_Modules/HUD/PointerInterceptDiagnostic.cs` | auto-armed pointer-intercept dump while a dev/settings overlay is open |

These predate this doc and **fully cover** the UI click-intercept / `panel=<null>`
symptom dump. Reuse them; do not rebuild.

### 0.9 Editor-only NavMesh verifiers (the seam-oracle seed)
- `Assets/Editor/CastleGateNavVerify.cs` — editor-only, MainCastle_Hall-hardcoded:
  for each castle gate, `NavMesh.CalculatePath(spawn → gate)` and assert (a) a complete
  path AND (b) closest-reach ≤ proximity radius. Emits a `GATE_NAV_OK/FAIL` marker.
- `Assets/Editor/SpawnPathVerifier.cs` — editor-only enemy-spawn variant.

These are the **proven logic** the PROPOSED runtime seam-reachability oracle (Part 3)
generalizes — but as a **fleet oracle**, not an editor menu.

---

## Part 1 — the model that replaced per-bug on-screen debuggers

The original doc's premise — "one attachable debugger per recurring bug class, each
on its own hotkey" — was superseded by the **TGVRU mandate (WO-430, 2026-06-19)**:
instead of a separate observer you turn on after the fact, **every render/build/spawn
site instruments itself** so the failure is already a logged line. TGVRU = the five
properties every such site must have:

- **T — Trace:** `FlowTrace.Step` at entry + each branch + each fallback + the
  render/commit seam (no bare `Debug.Log` — it never reaches the break-log).
- **G — Guard:** `Guard.Try` / `Guard.TryEach` / real try-catch on the risky op; no
  silent catch.
- **V — Verify:** assert the **actual built/rendered result** (≥1 enabled
  `SkinnedMeshRenderer` with a `sharedMesh`; animator bound + not T-pose; rows > 0;
  agent `isOnNavMesh`). `FlowTrace.Fail` on wrong state.
- **R — Rollback:** restore a safe visible state on failure (base body / placeholder /
  empty-state). Never leave blank / T-pose / magenta.
- **U — Up:** the failure is a `FlowTrace.Fail` (error-level → break-log) so it
  self-reports — the owner is never the detector.

**Gold-standard reference:** `Assets/_Modules/Village/Hero/HeroArmorVisual.cs`
(`VerifyArmorRendersNow` + deferred `VerifyPoseThenMaybeRollback` + `RollbackArmor`).
Authoring method: `docs/INSTRUMENTATION_STANDARD.md`. Rule of record: `CLAUDE.md §12`.

So the "debugger per bug class" question becomes: *is this class already covered by
TGVRU + an existing `AutoPilotProbes` oracle, or does it need a NEW fleet oracle?*

---

## Part 2 — recurring failure modes, mapped to the real system

Ranked by frequency × hand-diagnosis cost. Each row says how it is caught **today** or
what (PROPOSED) fleet oracle would catch it.

| Rank | Bug class | How it's caught now | Gap → action |
|---|---|---|---|
| 1 | **UITK `panel=<null>` / borrowed-PanelSettings teardown** (Settings/dev-tools dead, click eaten) | `DebuggingController` dump lists each UIDocument's `panelSettings`/live panel; `ScreenOpenWatchdog` names opens; `PointerInterceptDiagnostic` for intercepts | Symptom dump exists. A *proactive* panel-health assertion is not built. Low priority; see Part 3 note. |
| 2 | **NavMesh seam reachability — agent stalls short of a transition trigger** | `AutoPilotProbes` NAVMESH-LINK (missing/dangling bridge) + STRANDED (no path progress) already Fail on the structural cause; `CastleGateNavVerify` (editor) checks the castle case | The *reachability of each `SceneTransitionTrigger` from the hero* is not yet a per-seam fleet assertion → **PROPOSED seam-reachability oracle** (Part 3.1). |
| 3 | **UI click-intercept (Canvas sortingOrder / UITK raycaster eats the click)** | `DebuggingController` + `PointerInterceptDiagnostic` | **Fully covered.** No new work. |
| 4 | **Tripo asset — raw Phong (magenta in URP) and/or mis-oriented body** | TGVRU choke point `TripoMaterialFixer.Run` (WO-430) is slated to Fail + post-rebuild magenta verify; `VisualFactory.Skin` render-verify covers spawn bodies | Rig/material-integrity sweep across spawned renderers is not a fleet oracle yet → **PROPOSED rig-integrity oracle** (Part 3.2) covers the missing-rig half; material half is lower-priority (Part 3.3). |
| 5 | **Service-subscription missing in scene (`EconomyService.OnChanged subscribers=0`)** | `HeartHudBridgeBootstrap` (the fix) logs the `subscribers=0` line; `AutoPilotDriver.AssertEconomyDeduct` exercises the economy end-to-end on every run | A generic "HUD present but 0 subscribers" assertion is not built. Lower priority (Part 3.3). |
| 6 | **Scene-teardown hero-destroy (Single load nuked the hero → black screen)** | `BreakCaptureHarness` softlock watchdog + `AutoPilotProbes` STRANDED catch the *symptom*; the bot's `ResolveHero` Fails if no hero | Covered well enough by existing capture; no dedicated oracle needed. |
| 7 | **Unity fake-null — `??`/`?.` on a `UnityEngine.Object`** | — | Not a runtime-observable state; an **authoring/lint** concern, not a debugger. `DataRegression` / a grep-lint in `Assets/Editor/Regression/` is the right arm (INSTRUMENTATION_STANDARD §4). |

**Headline:** classes 1, 3, 6 are covered by existing dumps + capture. Classes 2 and 4
have the *structural* cause covered by `AutoPilotProbes` but would benefit from two
**new fleet oracles** (below). Class 7 is a lint, not a debugger.

---

## Part 3 — PROPOSED additions to `AutoPilotProbes` (NOT YET BUILT)

These are the two genuinely-useful concepts from the original doc, **reframed as
headless fleet oracles** that live in `AutoPilotProbes` (or a sibling armed by
`AutoPilotDriver`) and `FlowTrace.Fail` tagged `[Flow:AutoTest]`. They are **PROPOSED /
not yet implemented** — there is no code for them today. They follow the existing
probe shape: armed by the driver, throttled off a realtime timer, report-once per key.

### 3.1 PROPOSED — Seam-reachability oracle
**Status: PROPOSED. Not built.** Generalize the editor-only `CastleGateNavVerify`
(spawn→gate path + closest-reach ≤ radius) into a **runtime fleet check**: for every
`SceneTransitionTrigger` in every loaded scene, assert it is reachable from the hero —
`NavMesh.CalculatePath(hero → trigger)` is `Complete` AND closest-reach ≤
`ProximityRadius`. Fail (`[Flow:AutoTest]`) on a partial path or an out-of-reach radius,
with the per-seam closest distance, plus a `1/N reachable` summary line.

*Why it matters:* the castle→OuterWorld bridge-seam reachability bug was just fixed on
**2026-06-19**; this oracle would auto-catch any regression on the **next** fleet run
instead of a multi-hour geometry RCA. It complements the existing NAVMESH-LINK probe
(which proves the *bridge* exists) by proving the *trigger itself* is walk-reachable.

### 3.2 PROPOSED — Rig-integrity oracle
**Status: PROPOSED. Not built.** A fleet sweep over active `SkinnedMeshRenderer`s
flagging the broken-rig fingerprints: `bones == 0`, null `sharedMesh`, or a
suspiciously tiny rig (the teeth-bones-only / hair-only / missing-rig class seen on
Tripo/Blink bodies). Fail (`[Flow:AutoTest]`) naming the renderer + object.

*Why it matters:* complements the `HeroArmorVisual` render-verify gold standard — that
one self-protects the armor overlay; this oracle catches the **base bodies** it sits on
(the WO-430 "inverted priority" gap, §audit). Runs headless across every spawned body
the autopilot encounters, so a bad import surfaces on the fleet, not in the owner's eye.

### 3.3 PROPOSED (lower priority) — material/orientation + subscription oracles
**Status: PROPOSED, lower priority, API-unverified.** The original doc's Tripo
material/orientation detector and the `EconomyService` "0 subscribers" detector are
reasonable fleet-oracle ideas, but the original specs were written against **partly
phantom types and assumed APIs** (e.g. a `SubscriberCount` getter that may not exist, a
specific shader-name check). Before building either:
- Verify the actual public surface (`EconomyService` subscriber exposure;
  `TripoMaterialFixer` / renderer shader-name access) from the current code.
- Prefer extending the existing WO-430 choke-point verifies (`TripoMaterialFixer.Run`,
  `VisualFactory.Skin`) over a standalone probe, since those retro-cover dozens of callers.

Do not implement these from the original spec verbatim — re-derive the API first.

---

## Part 4 — turning diagnostics on during triage (the real controls)

There is **no per-probe hotkey.** The real controls:

1. **Master + category filters (console / dev panel):**
   `FlowTrace.Enabled = true; FlowTrace.Only("Seam");` to mute everything but one
   bounded context, or `FlowTrace.Mute("Enemy")` to silence a noisy one.
2. **F8 (the only hotkey):** flag a subjective/visual bug — screenshot + freeze + one
   typed note into `break-log.jsonl`. For everything code-detectable, the `FlowTrace.Fail`
   is already captured without a keypress.
3. **Headless fleet (the primary detector):**
   `powershell -File run-autopilot-fleet.ps1 -Count 20` runs N chaos bots, then
   `AutoPilotTickets.Emit` ranks the breaks by reproduction count into
   `Builds/autopilot-tickets.md`. This self-serves diagnosis on passive flows with no
   owner playtest (`CLAUDE.md §12.4`).
4. **Headless regression (data/logic):** `DataRegression.RunAll` for anything decidable
   from data + logic (catalog mapping, pricing, save round-trip) — `REGRESSION_OK` /
   `REGRESSION_FAIL` marker, gate-able in CI.

### Strip path
The whole runtime diagnostic layer is one folder (`Assets/_Modules/Core/Diagnostics/`),
static and `DeNelle.Core`-local, emitting only through FlowTrace. When a system is
proven stable: mute/strip its `Step` breadcrumbs, **keep every `Warn`/`Fail` and every
`Guard`** (the permanent no-silent-failure net). `AutoPilotProbes` is already
`#if DEVELOPMENT_BUILD || UNITY_EDITOR`, so it ships in no player release build.

---

## Part 5 — build priority (for the PROPOSED oracles only)

Everything in Part 0 is **already built** — do not rebuild it. The only open work is
the two PROPOSED fleet oracles, in this order:

1. **Seam-reachability oracle (3.1)** — highest ROI: turns the just-fixed
   castle→OuterWorld seam-reachability class into an auto-caught regression, reusing the
   proven `CastleGateNavVerify` logic. Add to `AutoPilotProbes`.
2. **Rig-integrity oracle (3.2)** — closes the WO-430 "base bodies don't self-verify"
   gap with a cheap headless sweep. Add to `AutoPilotProbes`.
3. **Material/orientation + subscription (3.3)** — lower priority; **re-verify the API
   first**, and prefer extending the existing TGVRU choke points over standalone probes.

**Already covered — do not build:** the UI dumps (`DebuggingController` +
`PointerInterceptDiagnostic`), the trace/guard/capture substrate, the existing five
`AutoPilotProbes`, the fleet + ranked emitter. The fake-null class is a lint, not a
debugger.

---

## Appendix — at-a-glance: real vs. PROPOSED

| Capability | Status | Where |
|---|---|---|
| `FlowTrace` Step/Warn/Fail/Throttle/Once/Measure/Enter/Try + sink | **BUILT** | `Core/Diagnostics/FlowTrace.cs` |
| `Guard.Try` / `Try<T>` / `TryEach` | **BUILT** | `Core/Diagnostics/Guard.cs` |
| F8 break-capture flight recorder | **BUILT** | `Core/Diagnostics/BreakCaptureHarness.cs` |
| ScreenOpenWatchdog (names every panel open) | **BUILT** | `Core/Diagnostics/ScreenOpenWatchdog.cs` |
| WebTrace / WebTraceSink (remote, dormant) | **BUILT** | `Core/Diagnostics/WebTrace*.cs` |
| `AutoPilotProbes`: UNEXPECTED-CROSS, COPLANAR-FLOOR, WALL-CLIP, DUAL-NAVMESH/STRANDED, NAVMESH-LINK | **BUILT** | `DevTools/AutoPilotProbes.cs` |
| Chaos autopilot bot + N-instance fleet + ranked ticket emitter | **BUILT** | `DevTools/AutoPilotDriver.cs`, `run-autopilot-fleet.ps1`, `Editor/AutoPilot/AutoPilotTickets.cs` |
| On-screen UI dump + pointer-intercept | **BUILT** | `HUD/DebuggingController.cs`, `HUD/PointerInterceptDiagnostic.cs` |
| Editor NavMesh verifiers (seam-oracle seed) | **BUILT (editor-only)** | `Editor/CastleGateNavVerify.cs`, `Editor/SpawnPathVerifier.cs` |
| Seam-reachability fleet oracle | **PROPOSED** | would extend `AutoPilotProbes` |
| Rig-integrity fleet oracle | **PROPOSED** | would extend `AutoPilotProbes` |
| Tripo material/orientation + subscription oracles | **PROPOSED (API-unverified)** | prefer TGVRU choke points |
| ~~`DebugProbe` base + F9–F12 per-probe hotkeys~~ | **NEVER BUILT — REJECTED PARADIGM** | superseded by TGVRU + the fleet |
| ~~`EnemyStrongholdGenerator_NavReady` MonoBehaviour~~ | **REJECTED** | recipe-driven `EnemyStrongholdBuilder` instead |
