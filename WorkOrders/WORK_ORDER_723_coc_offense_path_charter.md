# WORK ORDER 723 — CoC Offense Path Charter + Flag Map

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at TroopDef.cs:124 + TroopUnlock.cs:34-80 + TroopTrainingPanel.cs:103-445 + TroopRosterRegression wired at DataRegression.cs:313.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Priority:** P0 (program gate)  
**Silo:** Architecture / Combat  
**Type:** Decision + thin wiring (no new combat systems)  
**Depends on:** —  
**Blocks:** 724–731  
**Program:** `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md`  
**Effort:** S  

---

## Goal

One written law so CLI never re-forks Barracks vs ArenaAttack vs RaidOutpost. Lock Path A/B, flag map, and the single product entry story.

---

## Context

Most of the CoC spine is **built but dual-pathed and flag-gated**:

- Path A: `ArmyStorage` + Barracks + `RaidDeployController` (CoC deploy)
- Path B: `ArenaAttackRecruitController` (50-pt budget squad) + `ArenaMode`
- Walk-to: `RaidOutpostSystem` + `EnemyOutpost`
- Legacy: `RaidSelectionScreen` → teleport `GoRaid`

**Recommendation:** Path A is product spine; Path B parked or thin optional.

---

## Deliverables

1. **Owner-pinned Path A/B** in RESULT (default recommend A).
2. **Flag map** (current → program end-state):

| Flag | Today | End-state |
|------|-------|-----------|
| `ff.barracks` | OFF | ON when WO-724 closed |
| `ff.arena` | OFF | ON when WO-725 closed |
| `ff.colosseum` | OFF | ON only if colosseum is the chosen landmark |
| `ff.raid` | ON | stays ON; soft-lock proven by 726 |
| `ff.raidwalk` | ON | ON if walk-to is a target entry |
| `ff.basebuilding` | OFF | not required for AI-camp PvE |

3. **Single entry product story** (one sentence), e.g.  
   *“From Elarion, open Raid Map / Arena Herald → pick AI camp → deploy army → clear → return.”*
4. **Deprecation list:** primary vs secondary vs dead for:
   - `ArenaAttackRecruitController`
   - legacy `RaidSelectionScreen` teleport
   - walk-to `RaidOutpostSystem`
5. If path ≠ recommendation: note amendments for 724–731 in RESULT.

---

## Tasks

1. Read key files (below) + `docs/ARENA_SOLUTION.md`.
2. Present Path A vs B + entry options to owner if not already pinned.
3. Write RESULT with pins + flag map + deprecation list.
4. Optional: debug-only comments / DevPanel labels — **no production flag flips**.

---

## Acceptance

- [ ] Owner-pinned Path A/B in RESULT.
- [ ] Flag table + entry story in RESULT.
- [ ] Deprecation list complete.
- [ ] No code feature work beyond optional debug toggles/comments.
- [ ] Downstream WOs amended only if path diverges from Path A recommendation.

---

## Not in scope

- Flip production flags ON.
- Delete alternate systems (flag-gate / document only).
- Train UI, deploy tray, AI recipes, PvP netcode.

---

## Key files (read)

- `Assets/_Modules/Core/FeatureFlags.cs`
- `Assets/_Modules/Village/Arena/ArenaMode.cs`
- `Assets/_Modules/Village/Arena/ArenaAttackRecruitController.cs`
- `Assets/_Modules/Village/Troops/RaidDeployController.cs`
- `Assets/_Modules/Village/World/Camps/RaidOutpostSystem.cs`
- `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs`
- `docs/ARENA_SOLUTION.md`

---

## RESULT

`WorkOrders/WORK_ORDER_723_coc_offense_path_charter.RESULT.md`
