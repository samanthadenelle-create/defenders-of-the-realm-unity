# WORK_ORDER_296 — Reforge choice (Heart vs cleansed regions) → finale/ending wiring

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 12 · **Depends on:** 293/295 (legendary craft), 292 (finale), 290 (flags)
**Design source:** `DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md` §2 (Act IV)

## Context
The legendary quench needs an aether catalyst. The player chooses: draw from the **Heart** (immediate,
full-power Legendary, Tree dims further → harder/somber finale) or gather from **cleansed regions**
(slower final gather/defense, Tree spared → "true" ending).

## Goal
Implement the branching choice and route it into finale difficulty + ending + NG+ seed.

## Files to edit / create
- Yarn `<<command: ReforgeChoice heart|regions>>` handler in `NPCCommandBridge` → sets a persisted flag.
- Branch: `heart` → reforge completes immediately, set Heart-dim modifier (affects finale difficulty/tone);
  `regions` → spawn a final gather/defense objective across the 4 cleansed nodes before reforge completes.
- Finale (WO-292) + ending screens (WO-235) read the flag for difficulty + ending variant + NG+ seed.

## Acceptance criteria
- [ ] The choice is presented at Act IV and is irreversible for that save.
- [ ] `heart` path: instant full-power legendary + dim modifier applied to the finale.
- [ ] `regions` path: final gather/defense objective gates the reforge; Tree spared.
- [ ] Ending screen + NG+ seed reflect the choice; flag persists (QuestService/GameState).
- [ ] Brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't fork ending screens — extend WO-235's.
