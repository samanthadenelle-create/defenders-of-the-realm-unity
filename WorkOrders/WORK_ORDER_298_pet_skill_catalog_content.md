# WORK_ORDER_298 — Pet skill catalog content + balance (4 branches + signatures)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 6 · **Depends on:** 297 (species), existing PetSkillTreeCatalog
**Design source:** `DESIGN_PET_SYSTEM.md` §4

## Context
`PetUnlockTracker` (level 1–20, 1 skill point/level, starter auto-grant) + `PetSkillTreeCatalog`
(skills + prereqs + canUnlock) + `PetSkillTreePanel` (HUD) already exist. They need real content.

## Goal
Populate the skill trees: four branches + a per-species signature node, with XP sources per role and sane balance.

## Files to edit / create
- `PetSkillTreeCatalog` data — branches:
  - **Harvest:** Yield+, Gather Speed+, Auto-range+, Offline Cap+, Dual-node.
  - **Combat:** Attack, Anti-ranged screen (WO-128), Taunt/Guard, Pack Tactics.
  - **Utility:** Carry Cap, Move Speed, Rare-node Scent, Revive-assist.
  - **Aura (Warden):** Heal-over-time, Yield aura, Speed aura, Aether aura (late-gated).
  - **Signature** apex node per species (Level ~15 + a quest item).
- XP sources: Harvester per bank, Striker per assist/kill, Guardian per damage soaked, Warden per aura uptime.
- Respec at Stables for Food/Glimmer.
- `PetSkillTreePanel` — surface the new nodes + roster/collection tab + slot assignment.

## Acceptance criteria
- [ ] All four branches + signatures appear in the tree with working prereqs/unlock costs.
- [ ] Skills produce their effects (yield/speed/anti-ranged/aura) in play.
- [ ] XP accrues per role; level 20 cap; 1 point/level; respec works.
- [ ] Effects route through EconomyService (yields) / existing combat + aura systems (no fork).
- [ ] Brace check; CompileGate OK; build SUCCESS.

## Do NOT touch
- No `.unity` edits. Don't add new System.Reflection (PetUnlockTracker's existing bridge stays as-is).
