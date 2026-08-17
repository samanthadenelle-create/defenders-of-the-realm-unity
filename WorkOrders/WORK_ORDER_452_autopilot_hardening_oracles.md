<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 452 — AutoPilot hardening: oracle assertions + visual log-scans

**Status: READY TO IMPLEMENT** · Lane: QA/Tooling (DevTools — isolated, no gameplay scene files) · P1
**Date:** 2026-06-13 · **Owner directive + CLI thesis (agreed):** assertion quality is the
multiplier, not coverage breadth. Harden the bot against the exact bug classes that bit us this
session (magenta materials, duplicate input-eating panels, economy desync, save-state drift).
**WO# 452 is provisional** — reconcile against the numbering authority
(`MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`) before minting; filesystem
max is 451, master doc is the authority. Slot into the QA/Tooling lane.

**Scope discipline:** this is the *Do-Now* tranche only. NO behavior trees, chaos fuzzing,
`Time.timeScale` sim, CI→Notion auto-comment, or rendered-bot fleet — those are deferred
(post-grant). Keep the harness lean; it serves the playtest loop, not the other way around.
**Headless constraint:** the parallel fleet is license-free + logic/flow/crash + oracles only.
Visual checks (magenta, duplicate panels) are **log-scan based** — never assert a pixel headless.

---

## A. Magenta / missing-material + duplicate-UIDocument log scanner (HIGHEST VALUE)

**New file:** `Assets/_Modules/DevTools/AutoPilotLogGuards.cs` (DeNelle.DevTools asmdef, or wherever
`AutoPilotDriver` lives — same assembly).

1. **Magenta/missing-material scan.** Subscribe to `Application.logMessageReceived`. Flag any line
   matching the Unity missing-shader/material signatures: `"Shader ... not supported"`,
   `"is missing"` + `"material"`, `"Hidden/InternalErrorShader"`, `"_Pink"`/magenta fallback, and
   the URP `"Material doesn't have a texture property"` family. On a hit → `FlowTrace.Fail("BotVisual",
   "<scene>: magenta/missing-material — <message>")` so it lands in `break-log.jsonl` and becomes a
   ticket. De-dupe identical messages per run.
2. **Duplicate-UIDocument / panel-raycaster guard.** On each `sceneLoaded`, scan
   `FindObjectsByType<UIDocument>(Include)`; group by `panelSettings.name`. If >1 ENABLED document
   shares a PanelSettings whose name is NOT expected-multiple, OR any document bound to
   `OnboardingPanelSettings` is enabled+pickable in a non-onboarding scene → `FlowTrace.Fail(
   "BotUI", ...)`. This is the headless detector for the dev-tools-dead-after-Yarn class
   (complements the runtime `OnboardingPanelGuard` fix — the guard *prevents*, this *catches a
   regression*).

**Why log-scan:** Unity emits these as warnings/errors even in `-nographics`, so the parallel fleet
catches them without a rendered instance.

---

## B. Economy oracle (extend, don't duplicate)

**File:** `Assets/_Modules/DevTools/AutoPilotDriver.cs` — extend the existing `AssertEconomyDeduct`.

- Before a buy/sell: snapshot `EconomyService.Instance.Snapshot` (W/I/F/C) **and** read the HUD's
  displayed numbers (the bot already resolves `VillageHudController` — read its labels via the same
  reflection seam `HeartHudBridge` uses, or a test-only getter).
- After the transaction: assert `wallet_after == wallet_before - price` **exactly** for the spent
  resource, and assert the **HUD label == wallet** (catches the push/desync, not just the wallet).
- Negative case: attempt a buy with insufficient funds → assert wallet **unchanged** + no purchase +
  a graceful decline (no NRE, no softlock). `FlowTrace.Fail("BotEco", ...)` on any mismatch.

---

## C. Combat oracle

**File:** `AutoPilotDriver.cs` — new `AssertCombatInvariants` phase (runs during a triggered wave).

- **Hero HP never < 0 unless GameOver fired:** sample `HeroHealth.Current` each tick; if it goes
  negative while no game-over/defeat state is active → `Fail`.
- **Towers actually fire:** assert a placed tower registers ≥1 shot/hit within N seconds of an enemy
  entering range. Cheapest signal = a `FlowTrace.Step("Tower","fired …")` already emitted by the
  tower fire path (add one if absent — single line), and the bot asserts it appeared. Don't build a
  damage simulator; assert the *fired* event exists.
- **Enemy variety:** assert a wave spawned ≥2 distinct enemy type ids (already on the checklist;
  bot reads the spawn log).

---

## D. Save round-trip oracle

**Files:** bot phase in `AutoPilotDriver.cs` + reuse `Assets/Editor/Regression/SessionRegression.cs`
round-trip helpers.

- Bot performs a few state-mutating actions (gain resources, place a tower, advance wave), then:
  **quicksave → reload the scene/state → assert** hero position (within ε), resources (exact),
  inventory/equipped, and wave phase all match the pre-reload snapshot. Any drift → `Fail` with the
  field that drifted. This is the bot-driven complement to the headless `SessionRegression` data
  round-trip (which already guards the schema; this guards the *live* play→save→reload path).

---

## E. Reproducibility

**File:** `Assets/Editor/AutoPilot/AutoPilotTickets.cs` (emitter).

- Every emitted ticket includes: `seed`, `runId`, the **full ordered action trace** leading to the
  failure (the bot already records phase transitions — serialize them into the ticket), and the
  triggering `[Flow:*]` line. Goal: any ticket is replayable by re-running `--seed=<n> --run=<id>`.

---

## Acceptance criteria
- [ ] A scene with a deliberately-broken material (test fixture) produces a `BotVisual` ticket headless.
- [ ] Forcing a second enabled `OnboardingPanelSettings` doc in a gameplay scene produces a `BotUI` ticket.
- [ ] A buy/sell asserts exact wallet delta AND HUD-label==wallet; an underfunded buy asserts no change.
- [ ] A wave run asserts hero-HP≥0-unless-defeated, ≥1 tower-fired event, ≥2 enemy types.
- [ ] A play→quicksave→reload asserts position/resources/wave match; a seeded drift is caught.
- [ ] Every ticket carries seed + runId + action trace + the Flow line.
- [ ] `COMPILE_GATE_OK`; a smoke fleet run (`-Count 4`) completes and emits at least the fixture tickets.

## Do NOT touch
- No gameplay scene files (DevTools/Editor only). No new behavior-tree/fuzzing/CI infra.
- Don't duplicate `VendorStockContract`/`SessionRegression`/`OnboardingPanelGuard` — extend/assert against them.
- Don't add visual/pixel assertions to the headless fleet (log-scan only).
