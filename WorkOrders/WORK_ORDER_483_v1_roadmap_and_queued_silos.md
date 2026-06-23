# WORK_ORDER_483 — V1 Roadmap + Queued Silos (resume point, 2026-06-23)

**Purpose:** durable hand-off so a fresh session (post `/clear`) resumes WITHOUT the chat transcript.
Pairs with memory `overworld-encounter-isolated-battle` + `follow-canon-orchestrate-not-solo-guess` (BINDING).

## DONE + COMMITTED this session (local, NOT pushed — awaits owner felt-verify)
- `22081724` — WO-482 overworld encounter → isolated **MonsterFamily** BattleArena (orc family, OrcHumanoid rig),
  single-hero cleanup (companions gated under `ff.singlehero`, hero no longer named "Grom"), **armored Tripo Knight**
  promoted to `Resources/Heroes`, shop All/Armor/Weapons category selector, F8 ⚑ button + `f8-watch.sh` (CLAUDE.md §14).
- `fe58e4ce` — **real-path encounter fix PROVEN** (reps never spawned: additive-scene gate bug; now `repSpawned=True
  droppedToBattle=True` in the fleet oracle `AssertEncounterRealPath`), **light world** (RegionMobSpawner/RaidOutpostSystem/
  CampSystem/TribeManager gated OFF under `ff.overworldencounter`), **PetSelect removed** (`ff.bypasspetselect` default ON),
  **Knight heal+ranged skill tree + 4-skill loadout** (opens from a new **"Skills" inventory tab**; Q locked = basic attack;
  `HeroLoadout` + `AbilityCatalog.FindById` + `HeroAbilities.Resolve` indirection; code-built uGUI MVVM panels; retired the
  UIDocument `HeroTalentPanel`).
- Gates green: CompileGate, PROMOTE_KNIGHT_OK, build SUCCESS, fleet real-path PASS. EditMode = 349 pass / **8 pre-existing**
  failures only (BuildingCatalog ×3, ModalPanel, VillageStrayCleanup lint — NONE from these lanes; do not chase here).

## ARCHITECT ROADMAP (dependency-ordered chunks; critical path **C0→C1→C2→C7→C8**)
Silos (disjoint, parallel-safe unless noted): **S1** encounter/arena · **S2** hero kit+skill tree (serial within) ·
**S3** economy (Core, additive) · **S4** single-hero/Knight art (DONE) · **S5** world layout (**SERIAL builder bottleneck**) ·
**S6** reward/loop-close.
- **C0 (done by owner):** real-time BattleArena = V1 combat; ATB frozen/dormant (do not invest, do not delete).
- **C1:** `ff.overworldencounter` is OFF by default — flip ON for the V1 profile once felt-verified. (Encounter now real-path-proven.)
- **C2 (next, player-felt, S6):** real win reward — extend `BattleArena.GrantWinReward` (XP-only today) to grant **skill points
  (Wisdom) + light gear/resources** via the EnemyOutpost loot-table path; retire the dead "unlock next companion" reward in
  `Village2RaidController`/`RaidVictoryController`. Route through `EconomyService.Grant` + `WisdomCurrencyService`.
- **C7 (S3, Core):** NEW `LifeForceService` (Core writes GameState — Core can't ref Village). `lifeForce = f(encounters won/
  territory)`; raise on `BattleArena.OnBattleEnded(won)`. One meter, one cause, one effect. Save-additive (schema bump, nullable).
- **C8 (S3):** Echo **harvester** (autonomous, passive) — reuse `PetHarvester` + `OfflineHarvestService`; gather wood/iron/grain
  at a rate scaled by C7. ONE echo (wood) for V1. **Retire `EchoAutoDeployTrigger`** (the old combat-pet) under single-hero.
  Drag-drop of the 2 flex echoes = V2-gated.

## SKILL TREE — next slices (S2, built spine = committed)
- **Slice 2:** add `costWood`/`costIron` to `HeroTalentNodeDef` + `EconomyService.TrySpend` in `WisdomCurrencyService.Unlock`
  (so wood/iron funds the tree). Fold the dead `SkillSystem.AvailablePoints` in as the tier-1 unlock gate.
- **Slice 3:** real per-slot signature mechanics (true Q-pierce, E-regen, low-HP clutch, W→ranged-AoE) — extend
  `HeroTalentModifiers` from two class-wide scalars to per-slot/per-effect. Defense/shield-block = Armorer building research (WO-432).
- UI landmine fixed: panels are code-built uGUI (mirrors `BuildingUpgradeVM`/`BuildingUpgradePanelMvvm`); no UXML.

## WORLD LAYOUT (S5 — SERIAL builder bottleneck; one owner; editor-closed bakes)
- **Core finding (data-proven):** geometry desync — `ZoneManager` is origin-centered (regions fan from 0,0,0; nodes ±70–92);
  `ExteriorTerrainBuilder` terrain is centered at **Z=−572** (1000×1000). They contradict → nodes/region/seam land off the terrain.
- **Decisive fix:** keep ZoneManager (Core contract) as truth; **re-center terrain to origin + shrink to ~460u** (covers the node
  ring; WO-482 made the giant terrain unnecessary). Add a single `DeNelle.Core.World.WorldGeometry` constant (terrain center/size,
  village half-extents, region anchors, south-gate seam landing) that ZoneManager/OuterWorldBuilder/ExteriorTerrainBuilder/
  CastleHubBuilder all read (stops re-desync). Re-bake OuterWorld navmesh (batchmode, editor closed).
- **Seam:** keep ONE south castle→OuterWorld lane (strip W/N/E OutpostConnectors, already `ff.outposttravel` OFF); repair its bake
  so `ConfirmMinRadius` can shrink toward 12; wire the OuterWorld→castle RETURN seam. Do NOT attempt the WO-453 seamless cross-zone
  walk (the encounter loop made it unnecessary). The WO-453 castle-gate AttemptExitCastle timeout in the fleet is THIS unfixed seam.
- Reps roam relative to the hero; danger gradient maps via `ZoneManager.ThreatLevel`; red-skull tell = `ThreatSkullPlate`.

## DISPATCH GUIDANCE (next wave — orchestrate per CLAUDE.md §11)
- Parallel, code-only, disjoint NOW: **C2** (S6 reward) · **C7** (S3 Core LifeForce) · skill-tree **Slice 2** (S2).
- Single-owner serial: **S5** world (scene/bake) — never two agents in a builder/bake.
- Tests are the permission gate for C5/skill-tree changes (§2c). Verify the REAL path, never a direct-call bypass
  (the lazy-verify lesson — memory `follow-canon-orchestrate-not-solo-guess`).

## OPEN PINS for the owner
- Flip `ff.overworldencounter` ON in the default V1 profile (after felt-verify)?
- Push the two local commits (`22081724`, `fe58e4ce`) after felt-verify?
- World re-center (S5) is the next big serial chunk — owner-led world architecture; confirm before baking.
