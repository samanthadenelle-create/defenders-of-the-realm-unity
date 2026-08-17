<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — STALE (undated current-state assertion, CLAUDE.md §15)
> **Git first-add:** 2026-06-22 (the WO itself carries no date at all).
> **Evidence:** undated; asserts `**Branch:** feat/tower-core-loop` (live branch is `wip/village2-and-f8-tickets`). Part of the single WO-290→305 authoring burst.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*. This is a DATING problem, not a verdict on the design — the content may well still be wanted.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK_ORDER_305 — Relic-recovery quests (lost Elarion blades)

**Status:** CLOSED — STALE: undated current-state assertion, needs re-dating (era sweep 2026-08-17)
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
