# WORK_ORDER_292 — Keystone → Spire finale wiring (Rebuild Elarion convergence)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 11/12 · **Depends on:** 290 (Keystones), DEF-37/38 (Spire), WO-190 (Necromancer)
**Design source:** `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md` §2.9, `DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md`

## Context
Warden Alric's "Rebuild Elarion" master quest consumes Keystones earned from vendor/saga questlines.
At ≥6 Keystones the Spire awakens → Spire Defense Mode → Orc Necromancer confrontation → Heart relit.

## Goal
Wire the Keystone count to the village-tier gates and the finale trigger.

## Files to edit / create
- `NPC_Alric.yarn` (or extend) — master quest stages gated on `QuestService.KeystoneCount` + specific Keystones.
- Finale trigger: when ≥6 Keystones + Act IV done → enable **Spire Defense Mode** (DEF-37/38) and the
  Necromancer encounter (WO-190 enemy), then a Heart-relit resolution + NG+ seed flag.
- Tie village-tier raises (WO-151) to Keystone groups (granary+lumbermill, forge+armory, wards).

## Acceptance criteria
- [ ] Keystone count drives Alric's stages; finale only unlocks at the designed threshold.
- [ ] Reaching the threshold triggers Spire Defense → Necromancer → Heart-relit outcome.
- [ ] Village tiers raise as the matching Keystone groups are delivered.
- [ ] NG+ seed flag persists (QuestService/GameState); brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits (Spire/scene changes go through the proper builder/WO). Don't fork WaveManager — extend.
