# WORK_ORDER_304 — Brom's rumor board (quest-board UI)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 12 · **Depends on:** 290 (QuestService) — may fold into 290
**Design source:** `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md` §2.7

## Context
Brom the Innkeeper (SE Housing) is the quest hub. His rumor board is how the player discovers available
vendor quests + overworld raid alerts without a heavy UI.

## Goal
A code-built rumor board that lists available/active quests (from QuestService) and overworld alerts.

## Files to edit / create
- New code-built panel (HUD or DialogueUI) opened from Brom (`NPC_Brom.yarn` → `<<command: OpenRumorBoard>>`
  via NPCCommandBridge), or fold into the WO-290 tracker UI as a "board" tab.
- Reads `QuestService` for available Stage-1 hooks + in-progress quests; reads overworld raid/alert state.

## Acceptance criteria
- [ ] Talking to Brom opens the board; it lists currently available vendor quest hooks + active quests with stages.
- [ ] Overworld raid/alerts appear when active.
- [ ] Selecting an entry shows objective/reward; closes cleanly (mobile-friendly, code-built, no UXML).
- [ ] Brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't duplicate QuestService data — read from it.
