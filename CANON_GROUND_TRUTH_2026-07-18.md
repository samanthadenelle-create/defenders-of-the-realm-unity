# CANON GROUND TRUTH — 2026-07-18

> **Purpose:** the single anchor of *current reality*, verified from the working tree, HEAD, the
> gates, and owner rulings given live. **Supersedes `CANON_GROUND_TRUTH_2026-07-13.md`** (banner it).
> If a doc contradicts a line here, the doc is STALE. Read this → `KEY_FACTS.md` →
> `SESSION_CANON_LOADER.md` → `SAMANTHA.md` → `docs/HANDOVER.md` → `docs/MASTER_CATALOG.md`.

## ⭐ North Star (CORRECTED)
- **Pi Hackathon: WON** (owner, 2026-07-17). The "July-31 deadline / build mode IS the demo"
  framing is **RETIRED** — there is NO upcoming demo; the roadmap is OPEN for the next phase.
  Any doc still leaning on the hackathon deadline is STALE.
- Product unchanged: "Echoes of Elarion" in "Defenders of the Realm", mobile web (Pi Browser),
  V1 = one controllable Knight + player-built city; HP-B2B architecture; ten-year-old feel bar.

## Repo / git
- **Branch `wip/village2-and-f8-tickets`**, pushed to origin (github `defenders-unity`). As of the
  07-18 arc HEAD is at the MVVM + Room Forge landing (`9b38d058` after the canon-docs push; the
  MVVM landmines land on top). Local == origin after each push. **Prod UNTOUCHED** (promotion is the
  owner's separate call).
- **Save schema v33** (echoLanes `lane:level` token, WO-738). Every 21→33 bump has a SaveMigrator step.

## What landed this arc (07-17 → 07-18)
- **WO-744 — strict-MVVM whole-game migration.** Every View binds an `IPanelViewModel`; no runtime
  game-state reads. A conformance-oracle ratchet `UiMvvmConformanceRegression` (wired into
  `DataRegression` as `[ui-mvvm]`) enforces it. **Silos B/C/D/E/F/G-safe LANDED + gate-green**
  (~33 views on VMs, oracle debt 28→16). New shared seams: `GearIconCatalog`, `LiveWalletSource`
  (reuses the existing `Core.UI.Mvvm.WalletVM` DTO), promoted Core `CraftRecipeVM`,
  `ArenaPaletteVM`, `StructureCardVM`/`PlacedTowerListVM`. **Silo G landmines (BattleHudUgui behind
  `ff.battlehudvm` default-OFF; DialogueView with the WO-702 truce RELOCATED) + the last straggler
  panels (TroopTraining/PackStore/NPCUpgrade) are the final piece** — see the newest HANDOVER block
  for exact state. Spec: `docs/UI_MVVM_MIGRATION_PLAN.md`.
- **WO-740–745 — Room Forge into mainline.** The dungeon session's socketed-room pipeline merged to
  `wip`; 17 default room prefabs + shared KayKit materials; the demo compose layout bakes clean
  (`matesOk=2 matesFail=0 sealed=1`, NavMesh `PathComplete`); a 10-case `RoomForgeRegression`
  (`[room-forge]`) + `[Flow:DungeonBake]` instrumentation + two baker contract fixes (hard-gate,
  re-verify/overlap) + a doubled-path crash fix. RESULT: `WorkOrders/WORK_ORDER_745.RESULT.md`.
- **Repair-Wall dead button FIXED** — it was a silent no-op (reflection to a nonexistent method);
  now routes to `WallRepairController.SurfaceWorstRepair()`. Recurring owner ticket; owner felt-verify
  pending (surface-prompt vs RepairAll).

## Owner design rulings (captured; memories written)
- **Echo lanes (WO-738):** Defense = flat **+X% city defense** (NOT an offline-raid sim); new-player
  onboarding teaches the CLAIM loop first, defers lane-assignment; a **teaching conversation at every
  Echo unlock** (copy in WO-738). Only Harvest is wired; Crafting/Defense/Exploration are stubbed
  contracts on `EchoLaneBonuses`.
- **Dungeons = a POST-city-secure expansion pillar** (owner still determining the macro shape);
  torch/oil/darkness risk-reward is ~90% built (`Lantern.cs` + `RandomEncounterTable` darkness×1.6,
  `inDarkness` currently hardcoded false). Room Forge IS the "JSON dungeon layout editor" (not a
  GSpawn clone). Memory: `dungeon-pillar-roadmap`.

## Process notes / hazards
- **Two-session shared-tree hazard (live):** a dungeon session shares this working tree; it switched
  the branch out from under the MVVM work (caught a commit on the wrong branch, reconciled) and held
  the Unity editor (blocked headless gates). **Fix: the dungeon session should use a separate git
  worktree/clone.** §11 sole-committer reconciliation applied.
- **WO numbering:** banner `CLI_LANES_WO_NUMBERS.md` next-free = **746** (744 MVVM, 745 Room Forge
  regression, 740-743 Room Forge program). A UI-seat + CLI banner collision on 744/745 was reconciled.
- Gates: `CompileGate` → `COMPILE_GATE_OK`; `DataRegression.RunAll` baseline = **8 known reds**
  (arena ground, B2 dual-wallet, pet-slot, core-save Tribes/Wards/Arena, fountain L2/L3, DATAWEB
  drift, HUDUI, orc-raider) + the `[ui-mvvm]`/`[room-forge]` ratchets at 0 NEW. Unity licensing can
  intermittently error a batch run (`needs interactive Hub refresh`) — retry, don't kill processes.

## Open / owner's
- **Felt-verify** the converted MVVM screens + the repair button + the Room Forge menus/baked scene.
- **Image-pair screenshots** for the silos (in progress this arc).
- **Notion sync** — needs owner `/mcp` auth.
- **Push** is authorized on green (owner standing OK); prod promotion stays the owner's.
