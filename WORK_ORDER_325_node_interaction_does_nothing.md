# WORK_ORDER_325 — Nothing happens at resource node (mine upgrade/harvest dead)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 6 (Economy/Progression) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Reconcile with:** `MineNode` / `CrystalMine` interaction + upgrade, `EconomyService.TrySpend`, NPCCommand/prompt input

## Problem
At a resource node the prompt shows **"[G] Upgrade Mine T1→2 (25 ◆)"**, but pressing **G / interacting does
nothing** — no upgrade, no harvest, no feedback. The dev console shows **NullReferenceException spam**
("Object reference not set to an instance of an object") — likely the interact handler throws and aborts.

## Goal
Interacting at a node works: the **G** action performs the upgrade (spends crystals via EconomyService,
applies the tier change + feedback), and harvest/interact generally functions — no NRE.

## Where to look
- The node interact path (`MineNode`/`CrystalMine`): the **G** input binding → TryUpgrade/Harvest; confirm
  it's wired and not null (the NRE suggests a missing reference — EconomyService instance? prompt target?
  upgrade data? visual?).
- `EconomyService.TrySpend(crystals: 25)` → tier change → `StructureTierVisual`/visual swap + SFX/feedback.
- Null-guard the interact handler so a missing optional (visual/audio) can't abort the whole action.

## Acceptance criteria
- [ ] Pressing G at the node performs the upgrade: 25 crystals spent (if affordable), tier T1→T2 applied, visible feedback.
- [ ] Insufficient resources gives a clear "can't afford" response (not silence).
- [ ] No NullReferenceException on approach/interact/upgrade.
- [ ] Harvest/interact at nodes works generally (not just upgrade).
- [ ] Costs via EconomyService; brace check; CompileGate OK; build SUCCESS; verify in play.

## Do NOT touch
- No `.unity` edits. Don't fork MineNode/EconomyService — fix the wiring + null-guard. Ties WO-228/229/266.
