# WORK ORDER 733 — Troop Unlock UX + Train Refuse Gate — RESULT

**Status:** IMPLEMENTED (edit-only; orchestrator batch-gates + builds)
**Date:** 2026-07-16
**Depends on:** WO-732 (landed — `TroopDef.UnlockBarracksTier` + roster of 7)
**Paired with:** WO-737 (Obsidian layout — same panel edit)

---

## 1. Shared unlock query API (the ONE tier authority)

New file: `Assets/_Modules/Village/Troops/TroopUnlock.cs` (namespace `DeNelle.Village`).
No other code compares a troop's unlock tier — every train path calls these.

| API | Location | Behavior |
|-----|----------|----------|
| `int EffectiveBarracksTier()` | `TroopUnlock.cs:33` | `ModifierService.TierOf("barracks")` floored to 1 (a barracks that exists but was never written a tier still trains day-one Footman + Archer). |
| `bool IsTrainable(TroopDef def)` | `TroopUnlock.cs:44` | `def.UnlockBarracksTier <= EffectiveBarracksTier()`; null def → false. |
| `string LockedReason(TroopDef def)` | `TroopUnlock.cs:56` | `"Unlocks at Barracks Tier {n} - {TierName}"`; degrades to just the tier number when the barracks ladder is not authored in `building-tiers.json`. ASCII (no em-dash). |
| `string TierName(int tier)` | `TroopUnlock.cs:71` | `BuildingTierCatalog.TierOf("barracks", tier)?.Name`, or null. |

Note: `BuildingTierCatalog` building ids today are arcane-tower/armorer/forge/lumbermill/windmill
(no `barracks`), so `TierName` returns null and `LockedReason` cleanly prints just the tier number
until a barracks ladder is authored — no crash, no blank.

## 2. Hard refuse gate on `TroopDialogueCommands.Train`

`Assets/_Modules/Village/Troops/TroopDialogueCommands.cs:105-127` — inserted BEFORE the `TrainNow`
loop (before any spend), after the army-null check:

```
var gateDef = TroopCatalog.Find(troopId);
if (gateDef == null) { FlowTrace.Warn("TroopTrain","refuse-unknown id=..."); return 0; }
if (!TroopUnlock.IsTrainable(gateDef)) {
    FlowTrace.Warn("TroopTrain","refuse-locked id=.. needTier=.. haveTier=..");
    return 0;   // NO spend, NO army mutation
}
```

- `refuse-locked` returns **0** → the `TrainNow` loop never runs → **0 resources spent, 0 army members added**.
- `train-ok id=.. qty=..` FlowTrace.Step added after the loop when `trained > 0` (`TroopDialogueCommands.cs:132`).
- **Single chokepoint verified:** the ONLY caller of `ArmyStorage.TrainNow` is this method; every train
  entry point (training panel `TrainAndRefresh`, Yarn `<<StartTraining>>` → `Train`, DevPanel cheat)
  funnels through it, so the gate covers all paths. DevPanel trains footman/archer (tier 1) — allowed.

## 3. Panel UX (detailed in WO-737 RESULT)

`TroopTrainingPanel` now shows **all 7 troops** sorted by `UnlockBarracksTier` then catalog order;
locked troops stay visible + selectable with a dim icon + `T{n} LOCK` chip; the detail card shows the
`LockedReason` plate and disables the Train CTAs. See `WORK_ORDER_737_...RESULT.md`.

## 4. Deploy tray safety

No change required — locked types can only be trained through the now-gated `Train`, so no locked
`PlayerTroop` can ever be minted into the roster. No cheat path trains locked types.

## 5. Instrumentation

FlowTrace system `"TroopTrain"`: `refuse-unknown` / `refuse-locked` (Warn) + `train-ok` (Step).
FlowTrace system `"Barracks"`: panel open + rebuild steps.

---

## Acceptance

- [x] Fresh barracks tier 1: only Footman + Archer trainable; other 5 visible + locked copy.
- [x] At tier N, all troops with `unlockBarracksTier <= N` trainable (single `EffectiveBarracksTier` compare).
- [x] Locked train (panel or Yarn) spends 0 resources, adds 0 army members (returns 0 before `TrainNow`).
- [x] `TroopUnlock` is the ONLY tier compare used for troops.
- [x] `ff.barracks` OFF still blocks whole panel open (existing `BarracksUnlock.IsUnlocked` guard kept; now also toasts).
- [ ] CompileGate green — orchestrator batch-gates (edit-only agent).
- [x] No UXML introduced.

## Files touched
- ADD `Assets/_Modules/Village/Troops/TroopUnlock.cs` (braces 6/6, NUL 0)
- EDIT `Assets/_Modules/Village/Troops/TroopDialogueCommands.cs` (braces 18/18, NUL 0)
- EDIT `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs` (braces 56/56, NUL 0) — see WO-737
