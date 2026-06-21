# 12-Agent Openers — paste one per silo (2026-05-30)

> Companion to `PARALLEL_LANES.md`. Each block is a self-contained brief for one CLI silo.
> **Hard rule:** only Agent 1 edits `VillageSceneBuilder.cs`. Shared files needing coordination:
> `GameState.cs` (additive field-adds, one at a time) and `SaveSchema`/`SaveMigrator` (Agent 3 owns the bump).
> Every agent: **INSTRUMENT-FIRST — CLAUDE.md §12 HARD GATE: no fix on a non-trivial bug until CAPTURED
> DATA proves the cause (loggers step in/out → run headless → data pinpoints → fix that). Never guess /
> inference-fix; it's the OPENING move, unprompted.** brace-gate each `.cs`, commit by explicit path,
> Village→Core only, no UXML, editor closed for bakes.

## WAVE 1 — start these 7 now (zero deps, disjoint files)

**AGENT 1 — Village Builder (SOLE writer of VillageSceneBuilder.cs).**
Only you edit `Assets/Editor/VillageSceneBuilder.cs` + fire village bakes. In order: WO-166 (gates render+passable + 4 gates + walk-anim + pet T-pose) → WO-167 (gatehouse pillar clips ceiling — fold into 166) → WO-168 (navmesh gate openings) → WO-157 (strip magenta crystal veins) → WO-137 (castle/rampart rebake). Playable-village blocker — top priority.

**AGENT 2 — Zone Foundation (do first; 8/9/10/11 depend on you).**
WO-164: `ZoneManager.Depth`/`ThreatLevel` (danger tier × depth) + per-region `ZoneState` (discovered/cleared/neighbors/City-Horde) persisted. Files: `Core/World/*` + one GameState field (coordinate w/ 3,12). Pure Core, no Village ref.

**AGENT 3 — Wallet Merge + Economy (do first; keystone depends on you).**
RESOURCE_ECONOMY_DESIGN Step 0: collapse the 3-way wallet (GameState + ResourceBalance + EconomyService mirror + ManaCrystals) to ONE source of truth (GameState canonical). You OWN the SaveSchema/SaveMigrator version bump. Underlies WO-108/151/159.

**AGENT 4 — P1 Bug Fixes.**
WO-135: CrystalMine auto-upgrade spend, VFXManager counter drift, CrystalMine wave double-subscribe, WaveManager dict leak (+4 P2s). Files: CrystalMine/VFXManager/WaveManager. Reconcile WaveManager w/ existing work.

**AGENT 5 — Enemy AI / Tactics.**
WO-145/146/147: advanced tactics / formation / perception. `EnemyBrain.cs` + AI files only. No scene/builder.

**AGENT 6 — Camera.**
WO-156 in `SmartMobileCamera.cs` only: over-wall framing + orbit/pitch (two-finger / RMB) + clip-avoidance + per-wall transparency fade when a wall blocks the hero. Keep full wall height (don't lower); collision unaffected.

**AGENT 7 — UI / Music Polish.**
WO-162 (music jukebox on existing AudioService — selection UI + persisted pref, combat music overrides) + light BuildMenu UI polish. Code-built, no UXML.

## WAVE 2 — unlock after Agent 2 (zone) and/or Agent 3 (wallet) land

**AGENT 8 — Region Enemy Spawning** (after A2).
WO-155: region→enemy tables (living outer / Wound-tied deep per REGION_ENEMY_ROSTER) + level from ThreatLevel + red-skull nameplate. Reuse enemy defs (don't re-stat). Share roster w/ A11 — don't fork.

**AGENT 9 — World Crystal Mine + Dungeon Portals** (after A2).
WO-153 (renewable region-graded mine on MineNode) + WO-165 (hidden dungeon portals via the WO-154 spawner → DungeonController → return). Coordinate OuterWorldBuilder edits w/ A10/A11.

**AGENT 10 — Node Settlements** (after A2 + A3).
WO-159 phased: nodes→finite reserves; build Settlement to claim→auto-harvest→defend; empty→node gone/settlement stays; destroyed→3-game-day lockout; deep-region uneven terrain. New Settlement*.cs. Coordinate OuterWorldBuilder w/ A9/A11.

**AGENT 11 — Wandering Tribes** (after A2 + A5).
WO-160: radius-trigger spawn + state-save; randomized raid size in the region band (some easy/some brutal); reduced respawn on wipe; all hostile; the threat that razes undefended settlements (pairs w/ A10). New TribeManager.cs/TribeDef. Share roster w/ A8.

**AGENT 12 — Village Progression / Crafting** (after A3).
WO-151: one BuildingUpgrade component + VillageLevel meta-gate + BuildingEffects (Forge +dmg / Armory −dmg-taken / resource buildings +yield). New files in Village/Buildings/. Uses merged wallet (A3). Coordinate GameState field-adds w/ A2/A3. No builder edit.

## KEYSTONE — assign a freed agent the moment Agent 3 (wallet) finishes

**WO-108 — Player Build Mode** (the CREATE verb; highest-value build).
Read the build-ready header atop WO-108. Data spine first (PlacedStructureData + GameState.BaseLayout + v12 migration + BaseLayoutLoader), then PlacementGrid + BuildModeController + place/move/rotate/sell/upgrade. REUSE TowerPlacementSystem/StructureFactory/CatalogEntry/BuildMenu — don't fork. Charge-after-commit from the ONE merged wallet; NavMesh carving. Do NOT edit VillageSceneBuilder.

## The 5 collision rules (only ways 12 agents break each other)
1. `VillageSceneBuilder.cs` → Agent 1 ONLY.
2. `GameState.cs` field-adds (A2/A3/A12/WO-108) → additive, one at a time.
3. `OuterWorldBuilder.cs` (A9/A10/A11) → serialize those edits.
4. `SaveSchema`/`SaveMigrator` → Agent 3 owns the bump; others route through it.
5. Enemy spawning: A8 (region) vs A11 (tribes) share the roster — don't fork.

## Do-first: WO-164 (Agent 2) + wallet-merge (Agent 3) unblock Wave 2 + the keystone. Prioritize both.
