<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — STALE (undated current-state assertion, CLAUDE.md §15)
> **Git first-add:** 2026-06-22 (the WO itself carries no date at all).
> **Evidence:** undated; asserts `**Branch:** feat/tower-core-loop` (live branch is `wip/village2-and-f8-tickets`). Part of the single WO-290→305 authoring burst.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*. This is a DATING problem, not a verdict on the design — the content may well still be wanted.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK_ORDER_291 — Vendor Yarn pack (9 NPCs) + NPCCommandBridge quest verbs

**Status:** CLOSED — STALE: undated current-state assertion, needs re-dating (era sweep 2026-08-17)
**Branch:** feat/tower-core-loop · **Lane:** 12 · **Depends on:** 290 (QuestService); WO-110 (blue-button fix) applies
**Design source:** `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md`

## Context
Stationed vendors (WO-107/108/109) use `DialogueRunner` + `NPCCommandBridge`
(`Assets/_Modules/DialogueUI/NPCCommandBridge.cs`, already has OpenShop/OpenCraft/OpenUpgrade/OpenEquip).
We need each vendor's stage-aware Talk storyline + new quest commands.

## Goal
Author the 9 vendor Yarn files with the Stage 0→END "stemming" model and add quest commands to the bridge.

## Files to edit / create
- `Assets/Dialogue/NPCs/` — `NPC_Borin.yarn` (Forge), `NPC_Halvard.yarn` (Armorer), `NPC_Pell.yarn`
  (Lumbermill), `NPC_Wren.yarn` (Windmill), `NPC_Sable.yarn` (Jeweler), `NPC_Coppin.yarn` (Market),
  `NPC_Brom.yarn` (Inn), `NPC_Fenn.yarn` (Pets), `NPC_Alric.yarn` (Steward).
- `NPCCommandBridge.cs` — add `StartQuest`, `AdvanceQuest`, `CompleteQuest`, `SetFlag`, `GiveKeystone`
  handlers routing to `QuestService` (null-conditional).

## Scope
- Each Talk node branches on Yarn vars (`$q_*_stage`, `$villageLevel`, `$cleared_region_*`) for Stage 0..END.
- Stage-END Talk offers the upgraded shop/craft + a forward rumor pointing at another vendor.

## Acceptance criteria
- [ ] All 9 vendors have stage-aware Talk that changes with quest progress.
- [ ] New quest verbs work from Yarn and drive QuestService (verified start→advance→complete).
- [ ] Existing OpenShop/OpenCraft/OpenUpgrade still work; no blue continue button (WO-110).
- [ ] No new System.Reflection in the bridge; null-conditional cross-service calls; brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't hardcode quest state in Yarn beyond vars mirrored from QuestService.
