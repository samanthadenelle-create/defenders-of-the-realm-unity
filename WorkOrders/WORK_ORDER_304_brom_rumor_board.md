<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — STALE (undated current-state assertion, CLAUDE.md §15)
> **Git first-add:** 2026-06-22 (the WO itself carries no date at all).
> **Evidence:** undated; asserts `**Branch:** feat/tower-core-loop` (live branch is `wip/village2-and-f8-tickets`). Part of the single WO-290→305 authoring burst.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*. This is a DATING problem, not a verdict on the design — the content may well still be wanted.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK_ORDER_304 — Brom's rumor board (quest-board UI)

**Status:** CLOSED — STALE: undated current-state assertion, needs re-dating (era sweep 2026-08-17)
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
