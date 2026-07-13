# WO Close List — 2026-06-27 (for owner Notion bulk-close)

Code-confirmed at HEAD (see `BACKLOG_RECONCILIATION_2026-06-27.md`). Tick as you close on Notion.
**Two collisions to respect:** WO-431 → close the *mvvm-shop* variant only (raid-rewards 431 stays OPEN);
WO-333 → close the *dtt-sensitivity* variant only (`333_village_death_no_dtt_atb_trigger` stays OPEN).
WO-430 → close the canonical `ui_mvvm_seam` variant (the other four 430 files are dup-number noise).

## A. DTT / PatriciaLight → CLOSE (Deprecated) — module + scene removed 2026-06-09
- [ ] 46 — Tower Combat / DTT refinement (dead module)
- [ ] 47 — DTT (dead)
- [ ] 48 — DTT (dead)
- [ ] 96 — DTT mode feature (dead)
- [ ] 98 — DTT (dead)
- [ ] 99 — DTT (dead)
- [ ] 100 — DTT (dead)
- [ ] 209 — misfiled duplicate of WO-46 Tower Combat
- [ ] 221 — DTT (dead)
- [ ] 317 — DTT (dead)
- [ ] 318 — DTT (dead)
- [ ] 319 — DTT (dead)
- [ ] 320 — DTT (dead)
- [ ] 330 — DTT (dead)
- [ ] 331 — DTT (dead)
- [ ] 332 — DTT (dead)
- [ ] 333 (dtt-sensitivity variant ONLY) — DTT (dead); keep village_death_no_dtt_atb OPEN

## B. Dungeon → set FUTURE WORK (module exists, feature deferred)
- [ ] 23, 29, 30, 65, 165, 250, 324 — dungeon features deferred (19 & 59 already done)

## C. Done-in-code → CLOSE (deliverable shipped at HEAD)
- [ ] 290 — QuestService + tracker UI (Core/Quests/QuestService.cs)
- [ ] 304 — Rumor/quest board (RumorBoardPanel.cs; folds into 290)
- [ ] 339 — Quest save versioning (SaveSchema QuestProgress, schema v24)
- [ ] 293 — Crafting tiers (GearCraftingRecipeCatalog.cs)
- [ ] 295 — Aegis legendary set (same catalog)
- [ ] 297 — Pet acquisition/slots (Pets/PetDeployer.cs)
- [ ] 298 — Pet skill catalog (Pets/PetSkillTreeCatalog.cs)
- [ ] 299 — Pet bond questlines (pets + quests present)
- [ ] 108 — Build mode (BuildMode/BuildModeController.cs)
- [ ] 215 — Build preview (BuildPreviewModal.cs)
- [ ] 282 — Build-mode placement (shipped)
- [ ] 314 — Build mode (shipped)
- [ ] 131 — Economy service (EconomyService.cs)
- [ ] 228 — Harvest/claim (World/Camps/ClaimableCamp.cs)
- [ ] 424 — Economy/harvest (shipped)
- [ ] 258 — ATB combat manager (BattleATB/ATBCombatManager.cs)
- [ ] 276 — Battle controller (BattleController.cs)
- [ ] 430 (ui_mvvm_seam) — UI MVVM seam (Core/UI/Mvvm/*)
- [ ] 434 — Shop/inventory MVVM (RESULT.md present)
- [ ] 450 — OuterWorld boundary injector fixes
- [ ] 358 — pipeline ✓DONE (Yarn welcome)
- [ ] 368 — pipeline ✓DONE (camera regression)
- [ ] 380 — pipeline ✓DONE
- [ ] 382 — pipeline ✓DONE
- [ ] 387 — pipeline ✓DONE
- [ ] 465 — pipeline ✓DONE

## D. Stale / dead → CLOSE (premised on removed/abandoned/superseded systems)
- [ ] 101, 102, 103, 105, 126, 158, 167, 168, 177, 179, 183, 188, 247, 253, 256, 263, 278 — premised on the abandoned Village.unity (town role → Village2 + CastleHubBuilder)
- [ ] 110 — Yarn dialogue spec → superseded by WO-455 (custom dialogue)
- [ ] 307, 378, 411 — superseded by 403/405 (+437/438)
- [ ] 414 — superseded by 429 (store-stock-from-DB)
- [ ] 301 — party-persistence backbone → superseded by SaveSchema
- [ ] 291, 294 — Yarn-authoring specs stale (content exists; author in dialogues.json)
- [ ] 265 — castle doors, owner-cancelled
- [ ] 328 — closed, no-repro

## E. Closed THIS session → CLOSE (implemented / superseded 2026-06-27)
- [ ] 437 — input/battle-lock gate: built & wired (BattleLock + PanelManager gate)
- [ ] 438 — base-loop RCA fixes: all 4 root causes fixed in code
- [ ] 447 — hero-select polish: delivered via WO-503
- [ ] 439 — quest board: superseded by WO-454 + build-pipeline prefab ban
- [ ] 448 — hub↔OuterWorld seam: superseded by WO-467 runtime seam + GroundZFightFixer
- [ ] 451 — intro shorten: superseded by WO-446; comet removal done today
- [ ] 467 — RegionGate primitive: runtime variant shipped (region-gates.json + RuntimeRegionGate.cs)
- [ ] 440 — ATB item submenu + battle log: implemented + compile-gated today
- [ ] 452 — autopilot hardening (4 tranches): implemented + compile-gated today
- [ ] 454 — unified quest tabs/daily/type-pin: implemented today (follow-up: HUD-pinnable dailies need a SaveSchema bump — keep a sliver or re-file)
- [ ] 437/438 tech-skin file variants — "Tech hud elements" pack gone → superseded by UiStyle/Obsidian (if they carry their own board rows)

## STILL OPEN (do NOT close)
- 455 (content conversion — owner-directed), 468 (seam Phase 2 — RCA captured, see `WO-468_SEAM_RCA_2026-06-27.md`),
  431-raid-rewards, 435, 442, 453, 454-faction-base (renumber), T-OFFSETS (blocked on owner offsets).
