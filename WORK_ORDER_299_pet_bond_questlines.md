# WORK_ORDER_299 — Pet bond questlines (Fenn "Wild Hearts" + per-species)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 12 · **Depends on:** 290 (QuestService), 291 (vendor Yarn), 297 (acquisition)
**Design source:** `DESIGN_PET_SYSTEM.md` §5, `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md` §2.8

## Context
Fenn Wildmane (Stablemaster, SW Pet district) is the umbrella. Each species is unlocked by a short bond quest.

## Goal
Author the "Wild Hearts" umbrella + per-species bond quests that gate pet acquisition at a sane pace.

## Files to edit / create
- `Assets/Dialogue/NPCs/NPC_Fenn.yarn` — stage-aware Talk + quest hooks (StartQuest/AdvanceQuest commands).
- Quest definitions (via QuestService data, WO-290): Bond Sproutling, Craghound, Frostkit/Emberpup,
  Hatch-any, Rescue Stoneback Calf, Bond Aether Fox (late, gated on Heart-restoration progress).
- Wire each quest completion → `PetAcquisitionService` unlock + slot grants; signature-skill capstone errands
  (e.g. feed Glimmermoth a flawless crystal from Sable) cross-link to other vendors.

## Acceptance criteria
- [ ] Talking to Fenn starts "Wild Hearts"; completing a species bond quest unlocks that species.
- [ ] Quests gate by region-clear / village level so pets arrive in order; Aether Fox only after Heart progress.
- [ ] Slot unlocks (2nd, 3rd) granted at the designed steps.
- [ ] Quest state persists (QuestService, wallet-keyed); Yarn has no blue-button (WO-110 fix applies).
- [ ] Brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't hardcode quest state — use QuestService.
