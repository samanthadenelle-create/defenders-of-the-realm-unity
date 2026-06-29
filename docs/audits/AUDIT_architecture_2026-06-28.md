# Architecture & Catalog Freshness Audit — 2026-06-28

**Scope:** READ-ONLY. Cross-check `docs/MASTER_CATALOG.md` + the README system
(`PROJECT_INDEX.md`, `Assets/README.md`, `Assets/_Modules/README.md`, `docs/README.md`)
against the actual code on branch `wip/village2-and-f8-tickets`: new/renamed/removed
classes, assemblies, scenes, and the CLAUDE.md §5 cross-assembly rules.

**Method:** enumerated the live asmdefs, scenes, module READMEs; diffed `git log
--since=2026-06-12 --diff-filter=A` (the catalog's own compile date) for added `.cs`;
spot-read the affected README/catalog sections. No files edited.

**Headline:** the catalog section files were compiled **2026-06-12** and claim "the
per-area code mechanics below remain trustworthy." That claim is now **substantially
false** — **159 new `.cs` files** landed since that date, including several **entire
new subsystems** with zero catalog/README coverage (real-time Arena combat, Troops,
the Raid loop, the custom MVVM Dialogue that replaced Yarn, the MVVM UI stack, Echo
workforce, Core/Diagnostics instrumentation). The top-level catalog banner correctly
flags the *hero-identity/party/Defend-the-Tower* framing as stale, but does **not**
warn that whole code areas are now missing.

---

## TOP DRIFT ITEMS (prioritized)

### D1 — Dialogue catalog describes the WRONG system (Yarn, not the live custom runner)
- `docs/MASTER_CATALOG/dialogue.md` is built entirely around "ONE shared **Yarn**
  runner" (50 `yarn` mentions); the index row (`MASTER_CATALOG.md` line 35) says the
  same. **Yarn was dropped** (owner decision, memory `drop-yarnspinner-custom-dialogue`).
- Live system: `Assets/_Modules/Core/Dialogue/` — `DialogueRunner.cs`, `DialogueService.cs`,
  `DialogueModel.cs`, `DialogueViewModel.cs` (+ `Assets/_Modules/HUD/DialogueView.cs`,
  `Village/Tutorial/DialogueCommandSink.cs`). Data-driven JSON nodes + MVVM uGUI view.
- A reader trusting the catalog will look for Yarn nodes/`DialogueCommandBridge` that are
  being retired. **Flag/rewrite `dialogue.md`** to the Core/Dialogue MVVM system.

### D2 — Whole live combat subsystem (Arena) absent from catalog + Village README
- The single-Knight pivot's real-time combat now lives in `Assets/_Modules/Village/Arena/`:
  `BattleArena.cs`, `BattleArenaHud.cs`, `BattleHud9Zone.cs`, `BattleStarRating.cs`,
  `EncounterParams.cs`, `ArenaBiomeDressing.cs`, `ArenaDeathCam.cs`
  (+ `Core/Arena/ArenaContracts.cs`, `Core/Combat/BattleLock.cs`,
  `Village/Enemies/OverworldEncounterSpawner.cs`).
- Not in `MASTER_CATALOG.md` index, no `Arena/` row in `Assets/_Modules/Village/README.md`.
  The catalog's "Battle/ATB" section still frames ATB as "the breach/dungeon encounter
  combat" without noting ATB is now flat/secondary and BattleArena is the live loop
  (per `CANON_GROUND_TRUTH_2026-06-26.md`).

### D3 — Troops + Raid loop subsystems undocumented
- New `Assets/_Modules/Village/Troops/` (10 files: `TroopController`, `TroopFactory`,
  `TroopDef`, `TroopCatalog`, `TroopDeployer`, `TroopRally`, `RaidDeployController`,
  `TroopDialogueCommands`, …) and new Raid loop in `Village/World/Camps/`
  (`RaidClaimService`, `RaidGarrisonSpawner`, `RaidVictoryController`,
  `Village2RaidController`, `OutpostVictoryController`, `GarrisonTurretArmer`,
  `GarrisonStatBlocks`) + `Village/Hero/RaidDeployScreen.cs`, `RaidSelectionScreen.cs`,
  `RaidEntryBridge.cs`.
- No `Troops/` row and no Raid entries in the Village README "World/Camps" row (which
  still lists only the old Camp/Outpost content). Not in the catalog.

### D4 — New scenes not in catalog (14 → 20)
- `docs/MASTER_CATALOG/scenes.md` claims "14 `Assets/Scenes/*.unity`". Live = **20**.
- Undocumented additions: `RaidBase_IronBastion.unity`, `RaidBase_fortified_garrison.unity`,
  `RaidBase_mage_enclave.unity`, `RaidBase_raider_camp_small.unity`,
  `Garrison_village2_stronghold.unity`, `Dungeon_Demo.unity`.
- The RaidBase scenes are wired (`SceneRouter`, `HubScenes`, `FeatureFlags`,
  `Editor/RaidSceneRegistrar.cs`, `WallTools/RaidBaseGenerator.cs`) — a real system, not
  scratch. Catalog boot/flow diagram (2b) mentions `Garrison_*` but no `RaidBase_*`.

### D5 — Large undocumented MVVM UI + supporting subsystems
- New MVVM stack across `Village/Hero/`, `Village/Items/`, `Village/Talents/`,
  `Village/Buildings/Progression/`: `EquipVM`, `InventoryVM`, `InventoryGrid`,
  `InventoryPaperDoll`, `ShopVM`, `PartyShopVM`, `CraftingVM`, `JewelerVM`,
  `HeroSkillTreeVM`/`HeroLoadoutVM`, `BuildingUpgradeVM`/`BuildingUpgradeService`/
  `VillageTierService`, plus `*PanelMvvm`/`*Bootstrap` partners.
- New `Village/Harvest/` Echo workforce: `EchoService`, `EchoWorkforceBootstrap`,
  `EchoWorkforceHud`, `EchoWaveUnlockBridge` (memory `echo-workforce-drag-drop`) — the
  Village README "Harvest/" row still describes only Worker/WorkerManager offline accrual.
- New `Assets/_Modules/Core/Diagnostics/` (`FlowTrace`, `Guard`, `ScreenOpenWatchdog`,
  `WebTrace`, `FloorDeepDiag`) — the §12 instrumentation spine; present in CLAUDE.md prose
  but not in the Core catalog section.
- New data-tier seam `Core/Data/ICatalogSource.cs` + `LocalJsonCatalogSource.cs`
  (memory `data-architecture-hybrid-db-direction`) — uncatalogued.

### D6 — `Assets/_Modules/Characters/` is an orphan empty module
- Directory exists with a tracked `Assets/_Modules/Characters.meta` but **0 files** inside.
  Not listed in `Assets/_Modules/README.md` module table. Either populate + document or
  delete the orphan folder+meta.

### D7 — CLAUDE.md §5 assembly table is incomplete + its cross-asm rule is over-stated
- §5 lists only 6 assemblies (Core/Village/HUD/Audio/BattleATB/Editor). Live = **18**
  asmdefs: also `DeNelle.AI`, `DeNelle.Data`, `DeNelle.Pets`, `DeNelle.Wallet`,
  `DeNelle.Web3`, `DeNelle.Cosmetics`, `DeNelle.Onboarding`, `DeNelle.Dungeons`,
  `DeNelle.Settings`, `DeNelle.DialogueUI`, `DeNelle.DevTools` (+ 3 Tests). The MASTER_CATALOG
  §2a graph is accurate; CLAUDE.md §5 is the stale copy.
- §5 states "**Village → Core only. HUD → Core only.**" The live asmdefs do not match the
  literal rule:
  - `DeNelle.Village` references Core, **AI, Cosmetics, Data, Pets, Wallet, Audio**.
  - `DeNelle.HUD` references Core **+ Data**.
  The *intent* (no Village↔HUD, no HUD/BattleATB→Village) **is upheld** — HUD does not ref
  Village, BattleATB refs only Core+Data, DevTools is the gated exception. But the rule as
  written is violated in letter; §5 should be reworded to "Village/HUD → Core (+ pure
  Data/leaf modules); never Village↔HUD, never HUD/BattleATB→Village." No true layering
  violation found.

### D8 — `HUDManager` documented as shipped but does not exist (still unfixed)
- `Assets/_Modules/HUD/README.md` (2 refs) and the entire `Assets/_Modules/HUD/README_HUD.md`
  (8 refs) + `PROJECT_INDEX.md` line 39 describe `HUDManager.cs` + `VirtualDPadLean.cs` as a
  shipped HUD. **`HUDManager.cs` does not exist** anywhere in `Assets/`. Live input is Village
  `VirtualJoystick`; the live HUD is `VillageHudController`. Catalog already flagged this
  (P3 #22) on 06-12; **still present 16 days later.** Delete `README_HUD.md` + the HUDManager
  rows, or restore the class.

### D9 — Village README still describes the retired party-of-4 / body-swap hero
- `Assets/_Modules/Village/README.md` "Hero/" row still narrates `HeroBodySwapper` + "class
  bodies" + party hero swap — superseded by the single Tripo Knight pivot
  (`combat-pivot-single-hero-northstar`). The README has a Defend-the-Tower SUPERSEDED
  banner at top but the body still teaches the old hero model.

### D10 — PROJECT_INDEX still points at retired/stale canonical docs
- `PROJECT_INDEX.md` line 53 still lists `CORE_ARCHITECTURE_PLAN.md` as "root-level
  canonical architecture for the Unity 6 mobile **TD + dungeon + Solana** game" — that
  framing is the demoted/stale one (the same file is listed as STALE two tables up, line 27).
  `Assets/_Modules/README.md` line 3 also points to `CORE_ARCHITECTURE_PLAN.md` as the "full
  plan" — stale pointer.

---

## What is NOT drifted (verified holding)
- **No real cross-assembly layering violation.** HUD never refs Village; BattleATB refs only
  Core+Data; DevTools (the documented exception) is the only gameplay-refing tooling asm and
  is `UNITY_EDITOR || DEVELOPMENT_BUILD`-gated. `DeNelle.Editor` lives at
  `Assets/Editor/DeNelle.Editor.asmdef` (not under `_Modules`, as cataloged).
- The MASTER_CATALOG **§2a dependency graph and §2c critical-path mechanics remain accurate**
  for the systems they cover (HeroLocomotion-is-a-NavMeshAgent, economy split, PanelManager,
  build/upgrade) — the drift is **omission of new systems**, not wrong description of old ones.
- Every shipped module under `_Modules/` has a README (20 READMEs / 20 module dirs) **except**
  the empty `Characters/` orphan (D6).

## Recommended canon actions (per CLAUDE.md §15 — flag, don't mass-rewrite)
1. Add `STALE:` banners to `docs/MASTER_CATALOG/dialogue.md` (Yarn→Core/Dialogue) and
   `scenes.md` (14→20, list RaidBase/Dungeon_Demo).
2. Add index rows to `MASTER_CATALOG.md` for Arena, Troops, Raid-loop, Echo-workforce,
   MVVM-UI, Core/Diagnostics, Core/Data catalog-source.
3. Update `Assets/_Modules/Village/README.md` "Subfolders" table: add `Arena/`, `Troops/`,
   `Items/` (MVVM), Raid entries under `World/Camps/`, Echo under `Harvest/`; fix the Hero row.
4. Delete `Assets/_Modules/HUD/README_HUD.md` + HUDManager rows (D8) or restore the class.
5. Reword CLAUDE.md §5 assembly table (add the 12 missing asms) + cross-asm rule wording (D7).
6. Remove the orphan `Assets/_Modules/Characters/` folder+meta or document it (D6).
7. Fix `PROJECT_INDEX.md` line 53 + `Assets/_Modules/README.md` line 3 stale
   `CORE_ARCHITECTURE_PLAN.md` pointers (D10).
