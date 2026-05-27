# WORK ORDER 48 — Tower Core Loop (GROUP 0) — SYNTHESIZED, review in AM

**Status:** In progress (autonomous overnight, agent pipeline)
**Date:** 2026-05-26 (overnight)
**Branch:** `feat/tower-core-loop` (off `feat/patricia-light` b356a23 — has everything)

> ⚠️ **The owner provided the DEF-78/73/74/75/76/77 dependency ORDER but the detailed
> DEF specs were NOT in the repo** (they live in the Claude-UI chat). Claude Code
> **synthesized** the per-DEF scope below from the DEF titles + [[WORK_ORDER_46_tower_combat]]
> + the existing economy/build code. **Review against the real DEF specs in the morning**
> and correct anything that diverged. All work is on a branch, compile-gated, reversible.

## Owner's execution order (verbatim)
GROUP 0 — Tower Core Loop (first priority, strict order):
1. **DEF-78** first — EconomyService, no dependencies, start immediately.
2. **DEF-73** second — placement system, SkillSystem, all shared types (after EconomyService).
3. **DEF-74 + DEF-76** in parallel — Tower upgrades + construction queue (after DEF-73).
4. **DEF-75 + DEF-77** last — VFX delta + skill popup (after their parents).

GROUP 1+ — existing wave/camera/AI correction loops, then world expansion, then systems.

## Synthesized scope (Claude Code interpretation)

- **DEF-78 — EconomyService:** a single source of truth for resource transactions, wrapping
  `GameStateService.State.Resources` (Crystals + Stone/Iron/Wood). `CanAfford(cost)`,
  `TrySpend(cost)`, `Grant(...)`, balance getters, a `ResourceCost` struct, a `Changed` event.
  Formalizes the economy currently patched ad-hoc in BuildMenu (`CrystalBalance`/`SpendCrystals`).
  Lives in `DeNelle.Core` (next to GameStateService) so every module routes through it. No deps.
- **DEF-73 — Placement + SkillSystem + shared types:** the tower **placement** flow (arm a
  tower from the build menu, ghost-preview at valid tiles, tap to place, spend via
  EconomyService) + the actual **Tower** combat component (targeting + firing — WO-46) + shared
  `TowerDef`/`TowerInstance` types. "SkillSystem" interpreted as per-tower skill/perk hooks
  (data-driven), kept minimal. Depends on DEF-78.
- **DEF-74 — Tower upgrades:** upgrade a placed tower (the BuildMenu upgrade stub → real:
  bump damage/range/HP per level, spend the upgrade cost via EconomyService). Depends on DEF-73.
- **DEF-76 — Construction queue:** towers take `BuildTimeSec` to raise (a per-tower build timer
  + a queue/HUD readout); a tower only fights once built. Depends on DEF-73.
- **DEF-75 — VFX delta:** tower fire/build-complete/upgrade VFX (reuse `AbilityVfxKit` element
  colours; "delta" = incremental polish on top of placement). Depends on DEF-73/74.
- **DEF-77 — Skill popup:** a code-built popup to view/choose a tower's skill/perk (ties to the
  DEF-73 SkillSystem). Depends on DEF-73. **Most ambiguous** — kept light; flag for AM.

## Hard constraints (carried from this session's hard-won lessons)
- **Code-built UI only** — UXML-sourced UIDocuments render EMPTY in this project's player builds
  (see [[uxml-uidocuments-dont-render-in-builds]]). Build all HUD/popups in C#.
- Reuse the real systems: `Enemy`/`IDamageable`/`DamageAttribution`, `HeartController`,
  `BuildMenu`, `BuildingCatalog`/`TowerVariantDef`, `GameStateService`. No parallel HP/economy.
- Damage routed through `IDamageable.TakeDamage` + `DamageAttribution.Record` (feeds XP).
- No `.unity` scene edits (runtime/code-built). No new asmdef cycles.
- Each stage compile-gated via `run-unity-method.ps1` before the next starts.

## Pipeline status
- [ ] Stage 1: DEF-78 EconomyService
- [ ] Stage 2: DEF-73 placement + Tower + SkillSystem + shared types
- [ ] Stage 3: DEF-74 upgrades ∥ DEF-76 construction queue
- [ ] Stage 4: DEF-75 VFX ∥ DEF-77 skill popup
