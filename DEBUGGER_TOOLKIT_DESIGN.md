# Debugger Toolkit Design — custom, easily-attachable debuggers per recurring bug class

**Date:** 2026-06-13 · **Type:** ARCHITECT design doc (read-only on code; this is the only file written).
**Branch:** feat/tower-core-loop

Owner framing: *"determine the most common issues and add custom debuggers you can easily attach"*;
*"never hurts to sit there with a flag turned off, but priceless during triage"*; *"tools HP depends on daily."*

The model is `Assets/_Modules/HUD/DebuggingController.cs` — a flag-gated, dormant-when-off,
**self-bootstrapping** on-screen tool that dumps full state on a click. This doc designs a small
suite of siblings, **one per recurring bug class**, that all FIT the existing
FlowTrace / Guard / BreakCaptureHarness culture (`docs/INSTRUMENTATION_STANDARD.md`) rather than
duplicating it.

**Hard rule honored throughout:** these are *observers*. They never change game behaviour — they
read state and emit `[Flow:*]`-tagged lines that the F8 `BreakCaptureHarness` already captures to
`break-log.jsonl` + Player.log. Every one is default-dormant.

---

## Part 0 — what already exists (do NOT duplicate)

| Concern | Existing tool | File | Verdict |
|---|---|---|---|
| Full UI click-intercept dump (uGUI + UITK), capture-next-click | `DebuggingController` | `Assets/_Modules/HUD/DebuggingController.cs:52` (bootstrap `:63`, `DumpAll` `:202`, `AppendUguiStack` `:250`, `AppendUitkStack` `:267`) | **COVERS bug class #1 + #2's symptom.** Reuse, do not rebuild. |
| Pointer-intercept while a dev/settings overlay is open | `PointerInterceptDiagnostic` (+ Bootstrap) | `Assets/_Modules/HUD/PointerInterceptDiagnostic.cs:36`, `...Bootstrap.cs` | Narrower auto-armed sibling of the above. Already shipped. |
| NavMesh path spawn→gate (editor, MainCastle_Hall only) | `CastleGateNavVerify` | `Assets/Editor/CastleGateNavVerify.cs:30` (`Verify` `:34`, batch `Diagnose` `:42`) | Editor-only, scene-hardcoded. **Generalize into a runtime probe** (class #3) — see SeamReachabilityProbe. |
| Spawn→target path check | `SpawnPathVerifier` | `Assets/Editor/SpawnPathVerifier.cs` | Editor-only enemy-spawn variant. Same generalization target. |
| Trace / guard / flight-recorder primitives | `FlowTrace`, `Guard`, `BreakCaptureHarness` | `Assets/_Modules/Core/Diagnostics/*` | **The substrate every debugger below emits through.** Never re-invent. |
| Economy-subscription *fix* (not a detector) | `HeartHudBridgeBootstrap` | `Assets/_Modules/Village/Heart/HeartHudBridgeBootstrap.cs:44` | Fixes class #5 but gives no *triage signal* when it regresses elsewhere. Class #5 debugger = the missing detector. |

So the toolkit below proposes **5 new debuggers**, explicitly skips the 2 UI classes already covered
by `DebuggingController`, and **generalizes** the editor NavMesh verifier into a runtime probe.

---

## Part 1 — ranked recurring failure modes (evidence-cited)

Ranked by **frequency × hand-diagnosis cost** (how often it bit us this session × how long it took to
find by hand). Each row cites the code/RCA that proves the class is real and recurring.

| Rank | Bug class | Times seen / cost | Evidence (file:line) | Already tooled? |
|---|---|---|---|---|
| **1** | **UITK `panel=<null>` / borrowed-PanelSettings teardown** | High freq, very high cost — HelpMenu Settings + AdminOverlay dev-tools both dead; "Store needs own PanelSettings"; recurred across ≥3 sessions ("THREE prior fixes all RAN yet click eaten") | `SETTINGS_PANELSETTINGS_RCA`: HelpMenu borrows by `panelSettings!=null` (`HelpMenu.cs:57-66`), guard kills it by asset *name* (`OnboardingPanelGuard.cs:164,177-191`); `PointerInterceptDiagnostic.cs:1-23` (3 prior fixes ran, click still eaten) | Symptom visible via `DebuggingController` (`DumpAll` lists every UIDocument's `panelSettings`/`livePanel`, `:223-231`). **No proactive panel-health watcher** → new debugger. |
| **2** | **NavMesh seam reachability — agent stalls N m short of a proximity trigger** | High cost — only 1 of 4 castle gate lanes bakes through; cost a full geometry RCA; "fixes get lost & re-derived" (gate-exit fixed once already, WO-168) | `SEAM_RCA`: West 1.4 m reachable, N/E/S stall ~35 m vs 12 m radius (`SEAM_RCA §1`); hero is a NavMeshAgent that stops at the mesh edge (`SceneTransitionTrigger.cs:16-20`); `ProximityRadius` at `CastleHubBuilder.cs:1784,1173` | Editor-only `CastleGateNavVerify` (scene-hardcoded). **No runtime, any-scene probe** → generalize. |
| **3** | **UI click-intercept (Canvas sortingOrder / UITK raycaster eats the click)** | High freq historically (Settings gear, top-right HUD pair, Start, dev-tools-after-Yarn) | `DebuggingController.cs:6-9`; `PointerInterceptDiagnostic.cs:6-19` | **FULLY tooled** — `DebuggingController` + `PointerInterceptDiagnostic`. No new build. |
| **4** | **Tripo asset — raw `FbxSurfacePhong` (magenta in URP) and/or mis-oriented (no -90° yaw)** | Recurring every new creature/building import — Demon + OgreMage this session; orcs/heroes before | `WIGHT_TRIPO_FIX`: `Demon.fbx.meta:6`/`OgreMage.fbx.meta:6` `externalObjects:{}` + Phong; rotation only on OrcWarband (`EnemyFactory.cs:87-90`); fixer `TripoMaterialFixer.cs:6-12` | Runtime fixer exists; **no detector that flags un-fixed Tripo at spawn** → new debugger. |
| **5** | **Service-subscription missing in scene (cross-asmdef bootstrap gap)** | Med freq, sneaky — `EconomyService.OnChanged subscribers=0` because the bridge only attaches in Village, not castle/OuterWorld | `HeartHudBridgeBootstrap.cs:5-32` (root cause + the log line "OnChanged fired W754 … (subscribers=0)") | Fix shipped; **no generic "this service has 0 subscribers in this scene" watcher** → new debugger. |
| **6** | **Scene-teardown hero-destroy (Single load nuked the hero → black screen)** | Med freq, very high cost when it hits (outpost black screen) | `SceneRouter.cs:62-66` (Single load "tears the whole world down"); 16 `LoadScene` call sites (grep) — additive-vs-single is the discriminator | None → new debugger. |
| **7** | **Unity fake-null — `??`/`?.` on a `UnityEngine.Object` returns fake-null not the fallback** | High latent cost (one occurrence masqueraded as ~6 HUD bugs) but it's a *code-authoring* defect, not a runtime-observable state | memory `unity-object-null-coalescing-trap`; fix = `TryGetComponent` sweep | **Not a runtime debugger problem** — it's a static/lint concern. See Part 4. |

**Headline:** the two most expensive recurring classes this session were **#1 (panel=null)** and
**#2 (seam reachability)** — both already triggered multi-hour RCAs. They are the top build priorities.
#3 is done. #4/#5/#6 are cheap, high-leverage detectors. #7 is not a runtime debugger.

---

## Part 2 — per-class debugger specs

All share the **shared base** in Part 3 (`DebugProbe` convention). Each spec gives: name · watches ·
attaches · dumps · flag · assembly · how it ties into FlowTrace/Guard/BreakCaptureHarness.

---

### Debugger A — `PanelHealthProbe`  (bug class #1, **PRIORITY**)

- **What it watches:** every `UIDocument` in the scene — does its `rootVisualElement` have a **live
  `IPanel`** (`root != null && root.panel != null`), is its `panelSettings` asset **shared** with
  another doc, and is any doc's panel `display=Flex`/`Pick`-able but `panel=<null>` (the exact
  built-but-invisible fingerprint from the Settings RCA).
- **How it attaches:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` self-bootstrap host (same
  pattern as `DebuggingController.cs:63`). No wiring. Runs a sweep **on each `sceneLoaded`** and on a
  hotkey; does NOT poll per-frame.
- **What it dumps** (per UIDocument):
  - `name`, `enabled`, `panelSettings.name`, `panelSettings.sortingOrder`,
    `livePanel` (root.panel != null), `pickingMode`, `display` (`root.style.display`).
  - **Shared-asset map:** group docs by `panelSettings` *instance* — any group with >1 member is
    flagged `SHARED panelSettings='X' held by [docA, docB]` (this is the HelpMenu-borrows-Onboarding
    smoking gun, `HelpMenu.cs:57-66`).
  - **The killer line:** any doc with `display==Flex && livePanel==false` → `Fail` "doc 'HelpMenu'
    open but panel=<null> (built-but-invisible — borrowed/torn-down PanelSettings)".
  - **Guard-name watch:** if `OnboardingPanelGuard` is present in a non-onboarding scene, list which
    docs match its `panelSettings.name=="OnboardingPanelSettings"` predicate (`OnboardingPanelGuard.cs:164`)
    — i.e. *predicts* which panels the guard will tear down, before the click fails.
- **Flag/toggle:** `public static bool PanelHealthProbe.Enabled = false;` (default OFF). Turn on at
  console / dev panel, or a hotkey (suggest **F9** — F8 is the break harness). When OFF, the bootstrap
  early-returns exactly like `DebuggingController.Bootstrap` (`:66`).
- **Assembly:** **`DeNelle.HUD`** (UIElements auto-referenced; no game deps — same as
  `DebuggingController`). It must NOT reference Village/Onboarding; the `OnboardingPanelGuard` name
  match is done by *string* (`d.panelSettings.name`), never a type reference, preserving the asmdef law.
- **Ties into existing layer:** emits via `FlowTrace.Step/Warn/Fail("Panel", …)` so lines land in
  `break-log.jsonl`. The live-panel-false case uses `Fail` (error-level → flight recorder, per
  STANDARD §5). Complements `DebuggingController.DumpAll` (which already lists docs at `:223`) by
  adding the *shared-asset* + *guard-prediction* analysis that the generic dump doesn't compute.

---

### Debugger B — `SeamReachabilityProbe`  (bug class #2, **PRIORITY**)

Generalizes the editor-only `CastleGateNavVerify` (`Assets/Editor/CastleGateNavVerify.cs:30`) into a
**runtime, any-scene** probe — so the 1-of-4-lanes asymmetry is provable in a normal/headless play
session, not only via the hardcoded editor menu.

- **What it watches:** for every `SceneTransitionTrigger` (and `OutpostConnector`) in the live scene,
  it runs `NavMesh.CalculatePath(hero.position → trigger.markerPosition)` and reports:
  `status` (Complete / **Partial** / Invalid), the **`closestEver`** distance the path actually
  reaches, and the trigger's **`ProximityRadius`** — the exact two-condition gate the seam RCA needed
  (`SEAM_RCA §1` table; `CastleGateNavVerify.cs:9-13` "(a) complete path AND (b) closest ≤ radius").
- **How it attaches:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` host. On demand (hotkey or
  `SeamReachabilityProbe.RunNow()`), it sweeps all seams once. Optionally a low-rate (`Throttle`, ~1/s)
  live mode that logs the hero's current distance vs each radius while you walk — to *watch* the stall
  happen. Default = on-demand only (no per-frame cost).
- **What it dumps** (per seam):
  `seam='WorldGate…' side=South path=Partial closestEver=34.4m radius=12m REACHABLE=NO`
  plus the **1-of-N summary line**: `seams reachable: 1/4 (West) — N/E/S stall ~35m (bake asymmetry,
  not a uniform collider)` — the headline the seam RCA had to derive by hand.
- **Flag/toggle:** `SeamReachabilityProbe.Enabled = false` default; hotkey to fire a sweep; a
  `liveTrace` bool for the throttled walk-mode.
- **Assembly:** **`DeNelle.Village`** — `SceneTransitionTrigger` lives in
  `Assets/_Modules/Village/World/` and the probe needs to read its `ProximityRadius`/marker. (The
  editor verifier reaches these by reflection; a Village-assembly probe reads them directly.)
- **Ties into existing layer:** `FlowTrace.Step/Warn("Seam", …)` (the seam RCA already used `[Flow:Seam]`
  / "live build SeamTrace" — this *is* that trace, packaged as a reusable probe). `Partial` path →
  `Warn`; a seam the hero is standing inside-radius-of but path is Partial → `Fail`. Mirrors
  `CastleGateNavVerify`'s `GATE_NAV_OK/FAIL` marker so the headless regression can assert on it.

---

### Debugger C — `TripoAssetProbe`  (bug class #4)

- **What it watches:** every spawned creature/structure renderer for the two Tripo failure
  fingerprints: **(a)** a material whose shader is a non-URP/Phong/`Standard` shader or whose
  shader is null/`Hidden/InternalErrorShader` (the magenta case `TripoMaterialFixer.cs:6-12`), and
  **(b)** a body whose forward axis is 90° off travel (the missing `-90°` yaw,
  `EnemyFactory.cs:87-90`; proven value `HeroBodySwapper.cs:96-106`).
- **How it attaches:** TWO entry points, owner's choice:
  1. **Passive sweep** — `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` host that, on a hotkey,
     walks all active `SkinnedMeshRenderer`/`MeshRenderer` under enemies/structures and flags any with
     a non-URP shader. Zero wiring.
  2. **At-spawn hook** — a one-line `TripoAssetProbe.Inspect(go, modelName)` call dropped into
     `VisualFactory.Skin` (`VisualFactory.cs:90`) so EVERY skinned body is checked at creation and a
     bad one logs immediately with its model name (catches Demon/OgreMage the instant they spawn).
- **What it dumps:** `model='Demon' renderer='body' shader='FBX/Phong' → MAGENTA RISK (not URP/Lit)`
  and `model='Demon' forwardDot=0.02 vs travel → likely 90° off (missing LocalRotation -90°)`. Lists
  the working-sibling fix path (extract material like `Orc_Berserker.fbx.meta:6-11`, or
  `FixTripoMaterials=true`).
- **Flag/toggle:** `TripoAssetProbe.Enabled = false` default. The at-spawn `Inspect` call itself early-
  returns when disabled (`if (!Enabled) return;` first line) so it's free in normal play.
- **Assembly:** **`DeNelle.Village`** (it inspects enemy/structure renderers created by `EnemyFactory`
  / `VisualFactory`, both Village-assembly). The shader-name check is pure `Renderer`/`Material` API,
  no Core dependency.
- **Ties into existing layer:** `FlowTrace.Warn("Tripo", …)` per bad renderer (a fallback/anomaly, not
  a hard stop). Pairs naturally with a `DataRegression` check (Part 4) that asserts no shipped
  `Resources/Enemies/*.fbx.meta` has `externalObjects:{}` + Phong — catching it pre-commit, headless.

---

### Debugger D — `SubscriptionProbe`  (bug class #5)

- **What it watches:** the **subscriber count** of the cross-scene services whose "0 subscribers in
  this scene" failure we hit — primarily `EconomyService.OnChanged`
  (`HeartHudBridgeBootstrap.cs:14` log "OnChanged fired … (subscribers=0)"). Generalizes to any service
  that exposes its handler list.
- **How it attaches:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` host; on each `sceneLoaded`
  it checks "is there a live HUD in this scene AND does `EconomyService.OnChanged` have ≥1 subscriber?"
  If HUD-present but subscribers==0 → the bridge-missing fingerprint. Idempotent, runs once per load.
- **What it dumps:**
  `scene='MainCastle_Hall' HUD=present EconomyService.OnChanged subscribers=0 → resource bar will
  freeze (HeartHudBridge not attached — cross-asmdef bootstrap gap)`. When healthy:
  `subscribers=1 (HeartHudBridge)` as a one-line all-clear.
- **Flag/toggle:** `SubscriptionProbe.Enabled = false` default. Hotkey to re-check on demand.
- **Assembly:** **`DeNelle.Village`** — *mandatory*, because it must read `EconomyService` and know
  about `HeartHudBridge`, both Village-assembly (a HUD-assembly probe could see the HUD but not the
  Village service → exactly the asmdef gap that caused the bug, per
  `HeartHudBridgeBootstrap.cs:10-13`). Requires `EconomyService` to expose a `SubscriberCount` (it
  already counts subscribers for its own log line — surface it as an internal getter; no behaviour
  change).
- **Ties into existing layer:** `FlowTrace.Warn("Economy", …)` when the count is 0 with a HUD present.
  This is the *detector* that `HeartHudBridgeBootstrap` (the *fix*) lacks — if a NEW scene reintroduces
  the gap, the probe flags it instead of the owner noticing a frozen bar.

---

### Debugger E — `HeroPresenceProbe`  (bug class #6)

- **What it watches:** the lifecycle of the tagged hero across scene loads — specifically whether a
  `LoadScene` in **Single** mode (`LoadSceneMode.Single`, `SceneRouter.cs:62-66`) destroyed the hero
  with nothing replacing it (the outpost black-screen fingerprint).
- **How it attaches:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` host that subscribes to
  `SceneManager.activeSceneChanged` + `sceneUnloaded`. On each transition it records the load **mode**
  (Single vs Additive) and, **one frame after the new scene is live**, checks
  `GameObject.FindWithTag("Player")` (the canonical hero tag, CLAUDE.md §7).
- **What it dumps:**
  - On transition: `transition '<from>' → '<to>' mode=Single — Single load WILL destroy the hero
    unless the target scene spawns one`.
  - Post-load verify: hero present → `Step` all-clear; **hero absent** → `Fail` "no GameObject tagged
    'Player' after load of '<to>' (mode=Single) — hero destroyed, black-screen risk" (error-level →
    flight recorder).
- **Flag/toggle:** `HeroPresenceProbe.Enabled = false` default. Cheap enough to leave on during raid/
  outpost testing specifically.
- **Assembly:** **`DeNelle.Core`** — it only uses `SceneManager` + the `"Player"` tag string, no
  Village types; Core is the right home (same neutrality as `FlowTrace`/`SceneRouter`, both Core).
- **Ties into existing layer:** `FlowTrace.Step/Fail("Scene", …)`. The absent-hero `Fail` is exactly
  the kind of "no silent failure" line the standard mandates for a screen that goes blank
  (STANDARD §2 trace-point 5, the data-empty-vs-built-but-invisible split).

---

## Part 3 — the shared "easily attachable" pattern

So all five are uniform, dormant-when-off, and zero-wiring — a `DebuggingController`-shaped base.

### 3.1 The `DebugProbe` convention (a tiny optional base, `DeNelle.Core.Diagnostics`)

Not an inheritance straitjacket — a **shared shape** every probe follows so they look and toggle
identically:

```csharp
// Assets/_Modules/Core/Diagnostics/DebugProbe.cs   (new — Core, sibling to FlowTrace)
namespace DeNelle.Core.Diagnostics
{
    /// Convention base for the attachable debuggers. Each concrete probe:
    ///  • has a `public static bool Enabled = false;`  (default DORMANT)
    ///  • self-bootstraps via [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] guarded by Enabled
    ///  • runs on a HOTKEY and/or sceneLoaded — never an unguarded per-frame loop
    ///  • emits ONLY through FlowTrace.Step/Warn/Fail("<system>", …)  (→ BreakCaptureHarness)
    ///  • NEVER mutates game state (read-only observer)
    public abstract class DebugProbe : MonoBehaviour
    {
        protected abstract string System { get; }          // FlowTrace category, e.g. "Panel"
        protected abstract KeyCode Hotkey { get; }         // on-demand sweep key
        protected abstract void Sweep(string reason);      // the dump

        protected void Update()
        {
            if (!ProbeEnabled) return;                     // each probe wires its own static flag
            if (Input.GetKeyDown(Hotkey)) Guard.Try(System, "sweep", () => Sweep($"hotkey {Hotkey}"));
        }
        protected abstract bool ProbeEnabled { get; }      // returns the concrete static Enabled
    }
}
```

Key properties this guarantees (all matching `DebuggingController`'s proven shape):

- **Default dormant.** `Enabled = false` + the bootstrap early-return = literally zero cost when off
  ("never hurts to sit there with a flag turned off").
- **Self-bootstrapping, zero wiring.** One `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` per probe
  spawns its `DontDestroyOnLoad` host iff `Enabled` (copy `DebuggingController.Bootstrap`, `:63-80`).
  Nothing references the probe; you flip its flag and it appears.
- **Uniform output.** Everything goes through `FlowTrace.<System>` → already captured by F8
  `BreakCaptureHarness` → `break-log.jsonl`. No probe invents its own logging or file writer.
- **Read-only, Guard-wrapped.** The sweep runs inside `Guard.Try` so a bad object in the scan logs and
  is skipped, never throwing inside a debugger (STANDARD §3). A debugger must never crash a playtest.
- **Per-category mute reuse.** Because each probe uses a distinct FlowTrace category (`Panel`, `Seam`,
  `Tripo`, `Economy`, `Scene`), the existing `FlowTrace.Only("Panel")` / `FlowTrace.Mute("Seam")`
  console controls (`FlowTrace.cs:35-54`) filter probe output for free — no new toggle UI.

### 3.2 Turning them on during triage

Three identical paths for every probe (smallest blast radius first, mirrors STANDARD §1):

1. **Console / dev panel:** `PanelHealthProbe.Enabled = true;` then `FlowTrace.Only("Panel");` to mute
   everything else.
2. **Hotkey:** each probe owns one key (F9/F10/… — never F8, the break harness). Press to fire a sweep
   on the live scene.
3. **Headless / batchmode:** a static `RunNow()` per probe, callable from
   `run-unity-method.ps1 -Method …` so a passive flow (seam reachability, panel health, tripo scan)
   self-serves a capture with no owner playtest (CLAUDE.md §12.4).

### 3.3 Strip path

Same one-folder story as the rest of the diagnostic layer: the probes are static + `DeNelle.Core`/
HUD/Village-local, emit only through FlowTrace. When a class is proven stable, set its `Enabled=false`
permanently or delete the single file — no cross-module coupling to unwind (STANDARD §1.4).

---

## Part 4 — bug class #7 (Unity fake-null) is NOT a runtime debugger

`??`/`?.` on a `UnityEngine.Object` returning fake-null (memory `unity-object-null-coalescing-trap`)
is an **authoring-time defect**, not an observable runtime *state* — by the time it bites, the object
already silently took the wrong branch. A "debugger" can't watch for it meaningfully. The right tools
are:

- **A headless lint / `DataRegression` grep** (STANDARD §4) that flags `?? ` / `?.` applied to a
  `Component`/`GameObject`/`UnityEngine.Object` expression and recommends `TryGetComponent` / explicit
  `== null`. Lives in `Assets/Editor/Regression/` (editor asmdef), emits `REGRESSION_FAIL` on a hit.
- This pairs with the **`TripoAssetProbe` regression** (Part 2C) — both are headless asset/code
  asserts, not on-screen probes.

Listing it here so it isn't mistaken for an omission: it's covered, just by the *lint/regression* arm
of the standard, not the attachable-debugger arm.

---

## Part 5 — prioritized build order

**Build first (max triage ROI) — the two classes that already cost multi-hour RCAs this session:**

1. **`PanelHealthProbe`** (class #1) — the single most expensive recurring class (Settings + dev-tools
   dead, "THREE prior fixes all RAN yet click eaten"). Detects the shared-PanelSettings teardown
   *before* the click fails, and predicts which docs `OnboardingPanelGuard` will kill. **DeNelle.HUD.**
2. **`SeamReachabilityProbe`** (class #2) — generalizes the proven editor `CastleGateNavVerify` into a
   runtime/headless any-scene probe; turns a multi-hour geometry RCA into a one-run
   `1/4 reachable` line. **DeNelle.Village.** High reuse of existing code.

**Build second (cheap, high-leverage detectors):**

3. **`SubscriptionProbe`** (class #5) — tiny; surfaces `EconomyService` subscriber count; the missing
   *detector* for the gap `HeartHudBridgeBootstrap` already had to *fix* blind. **DeNelle.Village.**
4. **`TripoAssetProbe`** (class #4) — flags magenta/un-rotated Tripo at spawn; recurs on every new
   import, so it pays for itself fast. Add the headless `.fbx.meta` regression alongside.
   **DeNelle.Village.**
5. **`HeroPresenceProbe`** (class #6) — Single-load hero-destroy watcher; cheapest of all, very high
   value on the raid/outpost lane. **DeNelle.Core.**

**Already done — do not build:** class #3 (UI click-intercept) is fully covered by
`DebuggingController` + `PointerInterceptDiagnostic`.

**Lint arm (not an attachable debugger):** class #7 fake-null → headless regression/lint in
`Assets/Editor/Regression/`.

**Foundation (do alongside #1):** add the small `DebugProbe` convention base
(`Assets/_Modules/Core/Diagnostics/DebugProbe.cs`) so #1–#5 are uniformly flag-gated and self-attaching
from the first one.

---

## Appendix — one-line attach/flag summary

| Debugger | Class | Attaches via | Default flag | Hotkey | Assembly | FlowTrace cat |
|---|---|---|---|---|---|---|
| `PanelHealthProbe` | #1 | RIOLM + sceneLoaded sweep | `Enabled=false` | F9 | DeNelle.HUD | `Panel` |
| `SeamReachabilityProbe` | #2 | RIOLM + `RunNow()` | `Enabled=false` | F10 | DeNelle.Village | `Seam` |
| `TripoAssetProbe` | #4 | RIOLM sweep **or** `Inspect()` in `VisualFactory.Skin` | `Enabled=false` | F11 | DeNelle.Village | `Tripo` |
| `SubscriptionProbe` | #5 | RIOLM + sceneLoaded check | `Enabled=false` | F12 | DeNelle.Village | `Economy` |
| `HeroPresenceProbe` | #6 | RIOLM + `activeSceneChanged` | `Enabled=false` | — | DeNelle.Core | `Scene` |
| `DebuggingController` *(exists)* | #1/#3 | RIOLM + on-screen 🐞 button | `Enabled=true` | on-screen | DeNelle.HUD | `[DBG]` |
| `PointerInterceptDiagnostic` *(exists)* | #1/#3 | component, overlay-gated | n/a (overlay-gated) | — | DeNelle.HUD | pointer dump |

*RIOLM = `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` self-bootstrap (the `DebuggingController.cs:63` pattern).*
