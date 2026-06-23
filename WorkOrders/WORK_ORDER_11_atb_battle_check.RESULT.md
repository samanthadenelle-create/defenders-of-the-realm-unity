# WORK ORDER 11 — RESULT

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Outcome:** ATB scene loads + renders cleanly in the player build, and the whole Village→ATB→Village **transition chain is statically verified intact**. But ATB **combat cannot run**: the scene's `BattleController._runtimeState` reference was dropped (recovery casualty). Filed as **WO-21** (per owner: scene-ref fixes go to a follow-up WO with acceptance criteria, not an autonomous scene hand-edit). Combat playout itself remains owner-eyes-on.
**Editor:** Unity 6000.4.8f1

---

## Per-step results

| Step / AC | Result |
|---|---|
| **2.2 / AC1** — ATBBattle loads standalone, no errors, no magenta | ✅ **build-verified** via `-bootScene ATBBattle`: loads, **0 runtime errors**, renders sky/ground + hero (violet) & enemy (red) capsules, **no magenta** (`wo11-atb-bootscene.png`). `BattleController` has a single-enemy dev fallback for direct open. |
| **2.1** — transition wiring intact | ✅ **static-verified** (see chain below) — no broken refs in the code path |
| **2.3 / AC2** — Village→ATB transition (forced breach) | ⛔ **blocked by WO-21** (combat can't start) + ⚠️ eyes-on (needs a real breach). Wiring is correct; the runtime-state ref is the blocker. |
| **2.3 / AC3** — ATB→Village return | ✅ wiring intact (`ReturnAfterResult → ResolveReturnScene → LoadSceneWithFade`, BUG-008 fix); ⚠️ eyes-on after WO-21 |
| **2.4 / AC4** — apex dragon | ⚠️ observational (needs wave-4 playout) |
| **2.5 / AC5** — build flow | ✅ ATB loads in the player build; full breach→combat→return is gated on WO-21 + eyes-on |
| **AC6** — this RESULT | ✅ |

## Transition chain — verified intact (static trace)

```
WaveManager.TriggerBreach()                         ✓ builds BattleParams{Wave,BreachedIds,ParticipatingPetIds}
  → SceneRouter.GoBattle(params).Forget()           ✓ stashes PendingBattle, fades to ATBBattle
  → BattleController.Start → BuildSetup()            ✓ reads SceneRouter.PendingBattle (dev fallback when null)
      → _runtimeState.StartBattle(setup, Village)    ✗ _runtimeState is NULL → bails "battle cannot run"  ← WO-21
  → BattleController.ReturnAfterResult(result)       ✓ LoadSceneWithFade(ResolveReturnScene())
      → ResolveReturnScene() = BattleParams.ReturnScene (default Village; dungeon round-trip supported)  ✓
```

Every link is correctly wired **except** the dropped `_runtimeState` serialized reference in the scene.

## Bug found → filed as WO-21

`ATBBattle.unity`'s `BattleController` has `_runtimeState: {fileID: 0}` (null). `BattleController.Start` early-returns when it's null, so combat never starts. The target asset (`ATBRuntimeState.asset`, guid `2e5ba38cb6a90334898284f491fd675e`) exists and is tracked — this is purely a dropped link (same fresh-clone fragility class as WO-05/18). Also logged: `'attack-button' not found in BattleHUD` (action input inert).

**Per owner direction, these are NOT hand-edited here** — `WORK_ORDER_21_atb_scene_wiring.md` carries the fix as acceptance criteria (restore the `_runtimeState` ref + reconcile the BattleHUD button names). It's a small one-field GUID re-bake; do it in-editor or as a one-line YAML edit under controlled execution.

## Remaining (eyes-on, after WO-21)

Once WO-21 restores the ref: force a breach in Village playmode (AdminOverlay or let a Hollow Walker cross the inner ring), confirm the fade to ATB, that breachedIds map to combatants, that commands work, that it resolves and fades back to Village resuming the wave, and the apex dragon path (2.4). These need input/observation that `-bootScene` alone doesn't drive.
