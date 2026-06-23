# WORK_ORDER_305 — Relic-recovery quests (lost Elarion blades)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 5 (World/Exploration) · **Depends on:** 290 (QuestService), region content
**Design source:** `LORE_ELARION_WEAPONSMITHING.md` §4–5, `DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md` §2 (Act III)

## Context
Famous Elarion arms are scattered across the realm. Recovering them unlocks lore + missing crafting
techniques/components that feed the Forgemasters' Saga.

## Goal
Author region relic-recovery quests that grant lore + saga components/Keystones.

## Files to create / edit
- Quest data (QuestService): "The Dawnedge" (a region temple), "Grom's Garrison Blades" (Frost Peaks),
  "The Journeyman's Pattern-Blade" / Threefold Fold (Stone Mountains), plus 1–2 more.
- Place relic objectives in the world via `OuterWorldBuilder`/world runtime (not VillageSceneBuilder).
- Reward hooks: lore unlock (LORE doc) + the matching saga component/Keystone (WO-292/294).

## Acceptance criteria
- [ ] At least 3 relic quests are completable in their regions and grant the lore + the matching component/Keystone.
- [ ] Recovered relics feed the saga (Act III) and/or crafting (Threefold Fold etc.).
- [ ] Quest state persists; placed via world builder (no Village.unity edits, no hand-edited scenes).
- [ ] Brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- VillageSceneBuilder (Lane 1 only). No hand-edited `.unity`. Serialize OuterWorldBuilder edits with other Lane-5 WOs.
