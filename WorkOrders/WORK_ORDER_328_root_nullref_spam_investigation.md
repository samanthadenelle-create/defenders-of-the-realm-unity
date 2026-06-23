# WORK_ORDER_328 — Investigate + fix the recurring NullReferenceException spam (likely root cause)

**Status: READY TO IMPLEMENT** · **PRIORITY: HIGH (suspected root of several "nothing happens" bugs)**
**Branch:** feat/tower-core-loop · **Lane:** 0 (NOW) · **Origin:** owner playtest 2026-06-06 (seen in nearly every screenshot)

## Problem
The dev console floods with **`NullReferenceException: Object reference not set to an instance of an object`**
continuously across town, DTT, nodes, build preview, and the admin panel. A per-frame (Update/LateUpdate) or
common-path null is throwing every frame. This very likely **causes or compounds** multiple reported failures:
WO-314 (build preview), WO-317 (DTT), WO-325 (node does nothing), WO-327 (trigger wave) — an exception mid-handler
aborts the rest of the action.

## Goal
Identify the **single root null** (or the few) behind the spam and fix/guard it, clearing the console flood and
ideally unblocking the dependent interactions.

## Scope
- Reproduce with full stack traces (Open Log File / Player.log) — get the **class + line + method** of the
  top recurring NRE (the console truncates; the log has the stack).
- Fix the root (a missing reference/binding) at source; add null-guards on the offending per-frame/common path.
- Re-test the dependent bugs (314/317/325/327) — note in the RESULT which ones the root fix resolves.

## Acceptance criteria
- [ ] Root NRE identified with stack trace (class/method/line) and documented in the RESULT.
- [ ] Console no longer floods with NullReferenceException in town/DTT/node/build-preview/admin.
- [ ] Per-frame/common path is null-guarded so a missing optional can't spam/abort.
- [ ] RESULT notes which of WO-314/317/325/327 the fix also resolves (re-verify them).
- [ ] Brace check; CompileGate OK; Windows build SUCCESS; verify in a play session.

## Root cause (triage 2026-06-06)
**Confidence: Hypothesis — needs the Player.log stack to confirm (per this WO's own scope step 1).**
A static read of the entire always-on per-frame layer found it **uniformly null-guarded**, so no single
unconditional per-frame NRE was reproducible from source. Components audited and found guarded:
`VillageHudController` (no Update at all), `HeartHudBridge` (`Assets/_Modules/Village/Heart/HeartHudBridge.cs`),
`HeroAbilitiesHudBridge`, `PartyHudBridge`, `CompassHud`, `AdminOverlay`, `MineNode`, `CrystalMineNode`,
`FloatingHealthBar`, `XPBarController`, `PatriciaLightController.Update` (`:267` early-out),
`BuildPreviewModal.Update` (`:278` early-out).

**Important corrections to the WO's dependency claims (the "single root unblocks 314/317/325/327" premise is
mostly FALSE):**
- **WO-325** node interact is already null-guarded — its "does nothing" root is `CrystalEconomy.Instance` null /
  player-tag, NOT a thrown NRE. 328 will not fix it.
- **WO-327** admin path is null-safe (`InvokeMethod` guards null) — not the NRE source. 328 will not fix it.
- **WO-317** DTT `Update` is guarded; any DTT throw is in the spawn path (`VisualFactory.Skin`), not per-frame.
- **WO-314** is the ONE genuinely plausible build-path NRE: `VisualFactory.Skin` during
  `BuildPreviewModal.SetupPreview3D` (`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:231`) when an
  entry's `visualPrefabPath` is missing. Re-verify this under 328.

**Best remaining static candidate for a recurring per-frame NRE:** `HeroTargetIndicator.LateUpdate` dereferences
`_reticle` unguarded (`Assets/_Modules/Village/Hero/HeroTargetIndicator.cs:178` and `:186`) whenever a live
target exists — but `_reticle` is reliably built in `Awake` (`:104` → `BuildReticle` always assigns), so this
is low-probability. (Every OTHER `_reticle` access in the file IS null-checked, e.g. `:112`, `:297`.)

**Recommended action:** do NOT proceed on the assumption of a single root. Execute this WO's scope step 1 first —
capture `Player.log` with full stack traces (class/method/line) of the top recurring NRE — then fix that exact
member. Likely outcome: 2–3 distinct context-specific NREs rather than one global one. Add the missing per-frame
guard at the confirmed site; do not blanket try/catch.

## Do NOT touch
- No `.unity` edits. Fix at source + guard; don't blanket try/catch to hide it. Coordinate with 314/317/325/327 owners.
