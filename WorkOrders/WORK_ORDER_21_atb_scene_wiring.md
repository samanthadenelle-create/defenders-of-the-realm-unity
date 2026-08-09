# WORK ORDER 21 — ATB scene wiring: restore BattleController refs

**Status:** CLOSED - SUPERSEDED (reconciled 2026-08-09 from the tree - the cited defect is already gone: `Assets/Scenes/ATBBattle.unity` now reads `_runtimeState: {fileID: 11400000, guid: 2e5ba38cb6a90334898284f491fd675e}`, not `{fileID: 0}`; and ATB is frozen/dormant per WO-483 sec.C0, with the real-time BattleArena as V1 combat)

**Date:** 2026-05-24 (filed by WO-11 ATB verification)
**Owner:** Samantha Denelle
**Authority:** Standing Authority #35 + WO-025 (safe-scene-edit).
**Priority:** High — ATB combat currently **cannot run** (core loop broken).
**Depends on:** WO-10.
**Expected runtime:** 20–40 minutes.

---

## 1. Problem statement (statically + runtime confirmed in WO-11)

Booting `ATBBattle.unity` directly (`-bootScene ATBBattle`) loads with 0 crashes but logs:

```
[BattleController] No ATBRuntimeState assigned — battle cannot run.
[BattleController] 'attack-button' not found in BattleHUD — input will be inert.
```

`BattleController.Start` bails immediately when `_runtimeState == null`, so **no battle ever runs** — every gate-breach encounter would dead-end. Root cause: the serialized `_runtimeState` reference was dropped from the scene (a GUID-remap / reimport casualty from the 2026-05-24 recovery sweep — exactly the risk WO-11 §1 flags). Confirmed in the scene:

```
# Assets/Scenes/ATBBattle.unity — the BattleController MonoBehaviour
_runtimeState: {fileID: 0}        # ← NULL, should reference ATBRuntimeState.asset
_hudDocument: {fileID: 1032066911}  # OK
_heroCapsule / _enemyCapsule        # OK
```

The target asset exists and is tracked: `Assets/_Modules/BattleATB/Generated/ATBRuntimeState.asset`, guid `2e5ba38cb6a90334898284f491fd675e`.

## 2. Fix (small, specific scene edit — explicitly permitted by WO-11 hard rule)

1. In `ATBBattle.unity`, set the `BattleController._runtimeState` field to the ATBRuntimeState asset:
   ```
   _runtimeState: {fileID: 11400000, guid: 2e5ba38cb6a90334898284f491fd675e, type: 2}
   ```
   (One-field GUID re-bake — the diff is a single line. Do it in-editor by dragging `ATBRuntimeState.asset` onto the BattleController's `_runtimeState` slot, or by the one-line YAML edit above.)
2. Investigate the `'attack-button' not found in BattleHUD` warning: confirm `Assets/_Modules/BattleATB/UI/BattleHUD.uxml` contains an element named `attack-button` (and the other action-button names `BattleController` queries). If the uxml element names drifted, reconcile them with the controller's query strings. (Action input is inert until this resolves.)

## 3. Acceptance criteria

1. `ATBBattle.unity` `BattleController._runtimeState` references `ATBRuntimeState.asset` (no longer `{fileID: 0}`).
2. Booting `-bootScene ATBBattle` no longer logs "No ATBRuntimeState assigned"; a battle starts (dev fallback combatant when no `PendingBattle`).
3. The `attack-button` warning is gone; action buttons are wired (clickable in the HUD).
4. Build clean; the Village→breach→ATB→return loop is exercisable (eyes-on).
5. `WORK_ORDER_21_atb_scene_wiring.RESULT.md` written.

## 4. Notes

- The rest of the ATB transition chain is **verified intact** (WO-11): `WaveManager.TriggerBreach → SceneRouter.GoBattle → PendingBattle → BattleController.BuildSetup → ReturnAfterResult → ResolveReturnScene`. Only the dropped `_runtimeState` ref (+ the HUD button names) block combat.
- Same fresh-clone fragility class as WO-05 / WO-18: a tracked scene referencing an asset whose link was lost. The asset itself is present and tracked, so this is purely re-linking.
