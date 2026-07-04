# ⚠ WORK ORDER 48 — Tower Core Loop (GROUP 0) — **SUPERSEDED 2026-07-04**

> **SUPERSEDED:** The Defend-the-Tower / PatriciaLight system was removed 2026-06-09. This WO was part of that system's feature arc.

**Status:** CLOSED — SUPERSEDED (system removed 2026-06-09)
**Date:** 2026-05-26 (overnight)
**Branch:** `feat/tower-core-loop` (off `feat/patricia-light` b356a23 — has everything)

> ✅ **UPDATE — real specs are in Linear** (team "Defenders of the Realm"): DEF-78, DEF-73,
> DEF-74, DEF-75, DEF-76, DEF-77, each with binding **Correction Pass / Clarifications** comments.
> The agents now read the Linear issues DIRECTLY and implement to spec. My earlier overnight
> synthesis (a static Core EconomyService + new-Input-System brief) **diverged and was discarded**
> (branch reset); DEF-78 was rebuilt verbatim to its Linear spec (MonoBehaviour singleton
> `EconomyService.Instance` in DeNelle.Village). The synthesized scope below is superseded by Linear.

## Key spec→codebase reconciliations the agents apply
- **No new asmdefs**: `DeNelle.Core.Data` / `DeNelle.Core.Progression` are namespaces inside the
  existing `DeNelle.Core` asmdef (not separate assemblies). Files go under `Assets/_Modules/Core/Data`
  & `/Core/Progression`; `DeNelle.Village` already refs `DeNelle.Core`.
- **EconomyService** = the DEF-78 spec verbatim (`Instance`, `CanAfford(int)`/`Spend(int)`, Wood-only
  stub) + a self-bootstrap so Instance is non-null at runtime. SkillSystem gets the same bootstrap.
- **Legacy Input** (`Input.GetMouseButtonDown`/`mousePosition`) per the DEF-73 clarification — NOT the new Input System.
- **Code-built UI Toolkit** for TowerUpgradeButton (UXML renders empty in builds; uGUI needs a Canvas).
- Tower `visualPrefab` null → procedural placeholder (no authored tower art yet).

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

## Pipeline status (verified AM 2026-05-27 from batchmode compile logs)
- [x] Stage 1: DEF-78 EconomyService — compiled clean (Builds/def78-compile.log, 22:29)
- [x] Stage 2: DEF-73 placement + SkillSystem + shared types — compiled clean (def73-75-compile.log, 22:49)
- [~] Stage 3: DEF-74 upgrades **DONE** (Tower.cs, TowerUpgradeButton.cs — in the 22:49 compile) ∥ DEF-76 construction queue **NOT STARTED** (no TowerConstruction/Queue/ProgressBar/Billboard files)
- [~] Stage 4: DEF-75 VFX **DONE** (folded into Tower.cs: TriggerUpgradeVFX + CameraShakeBridge) ∥ DEF-77 skill popup **NOT STARTED** (no LevelUpSkillPopup; HeroProgression.OnLevelUp delta absent)

> Overnight run stopped after the DEF-73/74/75 compile gate (22:49, 2026-05-26). DEF-76 and DEF-77
> were not reached. All implemented code is **uncommitted** on `feat/tower-core-loop` (last commit
> 8cc1754 is the plan only). Bonus not in original scope: `Assets/Editor/TowerDataSeeder.cs`
> (Defenders → Seed Tower Data) seeds 3 sample TowerData assets so the loop is testable without
> hand-authoring SOs.
