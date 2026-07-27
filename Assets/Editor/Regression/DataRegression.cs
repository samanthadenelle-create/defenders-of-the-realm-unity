// =============================================================================
// DataRegression — headless "pass the real data object in, see the real response"
// regression harness. Owner directive 2026-06-13: instrument + run headless; this is
// the start of a robust regression script.
//
// Runs in batchmode (Unity closed) via:
//   run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log
//
// It loads the REAL canonical catalogs through the SAME code path the game uses
// (GearCatalog -> CanonicalJson -> Newtonsoft), enumerates the resulting OBJECTS, and
// validates the response — so a silent JSON->object mapping break (wrong top-level key,
// renamed field, parse-to-empty) becomes a hard REGRESSION FAIL line instead of an
// empty store at runtime with no error. Prints a single authoritative marker:
//   REGRESSION_OK   (all checks passed)  /  REGRESSION_FAIL: <n> failure(s)
// =============================================================================
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using DeNelle.Village;
using DeNelle.Village.Arena;
using DeNelle.Village.Items;
using DeNelle.Village.Population;
using DeNelle.Core.State;
using DeNelle.Core.Catalog;

namespace DeNelle.Editor
{
    public static class DataRegression
    {
        public static void RunAll()
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== DataRegression: real catalog objects in, real response out ===");

            // --- GEAR (the active 'empty store' suspect) ---------------------------
            // Force a fresh read through the real loader (CanonicalJson, Resources-first).
            GearCatalog.Reload();

            var weapons = new List<WeaponDef>(GearCatalog.AllWeapons());
            var armors  = new List<ArmorDef>(GearCatalog.AllArmors());

            log.AppendLine($"weapons.json -> {weapons.Count} WeaponDef objects");
            log.AppendLine($"armor.json   -> {armors.Count} ArmorDef objects");

            // Response check 1: did the JSON map to objects AT ALL? (catches the silent
            // parse-to-empty: file present but top-level key / field names mismatch.)
            if (weapons.Count == 0) failures.Add("weapons.json deserialized to 0 objects (mapping break or empty 'weapons' array)");
            if (armors.Count == 0)  failures.Add("armor.json deserialized to 0 objects (mapping break or empty 'armor' array)");

            // Response check 2: did the DISPLAY fields populate? A row renders blank if
            // name/id came through null/empty even when the count is right. This is exactly
            // the 'rows exist but look empty' case the owner suspected.
            int badWeapon = 0, badArmor = 0;
            foreach (var w in weapons)
            {
                bool ok = w != null && !string.IsNullOrEmpty(w.id) && !string.IsNullOrEmpty(w.name);
                if (!ok) badWeapon++;
                log.AppendLine($"  W {(w != null ? w.id : "<null>")} | name='{(w != null ? w.name : "<null>")}' " +
                               $"| dmg={(w != null ? w.damageMult : 0f):0.00} | cost={CostStr(GearCatalog.GetBuyCost(w))}");
            }
            foreach (var a in armors)
            {
                bool ok = a != null && !string.IsNullOrEmpty(a.id) && !string.IsNullOrEmpty(a.name);
                if (!ok) badArmor++;
                log.AppendLine($"  A {(a != null ? a.id : "<null>")} | name='{(a != null ? a.name : "<null>")}' " +
                               $"| def={(a != null ? a.defense : 0f):0.00} | cost={CostStr(GearCatalog.GetBuyCost(a))}");
            }
            if (badWeapon > 0) failures.Add($"{badWeapon} weapon(s) have null/empty id or name (would render as blank rows)");
            if (badArmor  > 0) failures.Add($"{badArmor} armor(s) have null/empty id or name (would render as blank rows)");

            // Response check 3: store would have NON-EMPTY stock for a general vendor.
            int generalStock = weapons.Count + armors.Count;
            if (generalStock == 0) failures.Add("general vendor stock is EMPTY (no weapons + no armors)");
            else log.AppendLine($"general vendor stock = {generalStock} gear rows (+ potions added at runtime)");

            // --- ABILITIES (abilities.json -> AbilityCatalog) ----------------------
            // Same shape as the gear checks: load through the REAL loader, assert the
            // JSON mapped to objects and every entry's DISPLAY fields populated. There
            // is no Resources PATH to resolve here on purpose: AbilityDef.Icon is a HUD
            // GLYPH (e.g. "✦"), NOT a Resources path (see AbilityCatalog.cs Icon doc +
            // HeroAbilities), and Color is a hex string — neither is Resources.Load'able,
            // so asserting a path on them would INVENT an expectation. We validate only
            // what the catalog actually declares.
            CheckAbilities(failures, log);

            // --- ENEMIES (enemies.json -> EnemyCatalog) ----------------------------
            // This is the catalog that carries the #22 archer->lumber CLASS of bug: an
            // entry's id resolves (via EnemyFactory.ModelForEnemy) to a MODEL PATH, and
            // a wrong/missing path silently degrades to a tinted capsule at runtime
            // (EnemyFactory.cs:100-114 fallback) — varied ids, one look, no error. We
            // load the catalog through the same CanonicalJson bytes WaveDataLoader reads,
            // then for EVERY enemy resolve its model the way the factory does and assert
            // Resources.Load<GameObject>("Enemies/<model>") returns a real prefab.
            CheckEnemies(failures, log);

            // --- WAVE-SCALING (CITY-01 / CITY-06) + KILL REWARDS (BLIND-03-01) -----
            // Proves the most-played mode (a) escalates per wave (the runtime DEFAULT
            // WaveScalingCurve applies a multiplier >1 past wave 1 - the fallback
            // WaveManager.EnsureScalingCurve now always creates) and (b) pays progression
            // (every enemies.json row carries xp+coin rewards). Both were DEAD in the audit.
            CheckWaveScaling(failures, log);

            // --- STRUCTURES (structures-catalog.json -> CatalogRegistry) -----------
            // The build-mode tower/structure catalog. Each CatalogEntry.visualPrefabPath
            // is a Resources-relative prefab path that StructureFactory.Create feeds to
            // VisualFactory.Skin -> Resources.Load<GameObject>. A path that loads null is
            // EXACTLY the archer->lumber class (a tower wired to the wrong/missing visual)
            // — caught here as a FAIL naming the entry + path. Parsed identically to the
            // real CatalogBootstrap.LoadFromJson (StringEnumConverter + ignore-null/miss).
            CheckStructures(failures, log);

            // --- BUILDINGS (buildings.json -> BuildingCatalog) ---------------------
            // Load through the real loader; assert non-zero + non-empty id/displayName.
            // NOTE (conservative): BuildingDef.Model is a KayKit mesh KEY, NOT a
            // Resources path — gameplay buildings render through the structures catalog /
            // build pipeline, never via Resources.Load(Model). So we do NOT assert a path
            // load on Model (that would invent an expectation the catalog doesn't declare).
            CheckBuildings(failures, log);

            // --- POPULATION MILESTONES (population-milestones.json -> PopulationMilestonesCatalog) ---
            // WO-587: the milestone TABLE that drives Echo workforce slot unlocks. Assert the JSON
            // maps to >0 milestones, echo slots ascend 2..5 with NO gaps, and every entry carries at
            // least one real condition — so an owner edit can't silently break the unlock cadence.
            CheckPopulationMilestones(failures, log);

            // --- BARRACKS & TROOP UPGRADE PROGRESSION (WO-771.9) -------------------
            // Source-lint the committed barracks.json + troop-upgrades.json against
            // troops.json: the barracks ladder is contiguous (level 1 free, no gaps),
            // every unlocksTroopId resolves, the unlock encodings RECONCILE (barracks
            // level N lists exactly the troop whose UnlockBarracksTier == N), and every
            // upgrade curve starts at the 1.0 baseline. Emits BARRACKS_PROGRESSION_OK.
            CheckBarracksProgression(failures, log);

            // --- GAME GUIDE (guide-content.json -> GuideContentCatalog) ------------
            // WO-588: the opt-in tutorial codex content. Load through the real loader; assert
            // the JSON maps to >0 sections and every section carries a non-empty id/tab/title
            // and at least one non-empty body paragraph — so a content edit can't ship a blank
            // tab or an empty body to the guide panel.
            CheckGuideContent(failures, log);

            // --- DIALOGUE SPEAKER CARDS (dialogues.json speakers block) -------------
            // Owner-ratified card standard (2026-07-02 audit): every NPC dialogue card shows
            // name + guild/shop AFFILIATION + portrait. Assert every spoken line's speaker
            // resolves to a speakers-block record with a non-empty name + affiliation, and
            // every DECLARED portrait path (speakers block AND legacy per-node `portrait`
            // command args) loads a NON-NULL sprite — a dangling portrait path fails the gate.
            // An EMPTY portrait is legal by design (styled silhouette fallback in DialogueView).
            CheckDialogueSpeakers(failures, log);

            // --- ITEM-MODEL CAPABILITY INVARIANTS (WO-Item-1, docs/ITEM_MODEL.md §2c) ---
            // OWNER-RATIFIED 2026-06-18: the model invariants live in the regression test,
            // not just the doc — so every change/regen is gated by data, not faith. HARD
            // asserts on the resolved capability flags + a SOFT prefabPath coverage count
            // (WO-Item-2's generator fills those — do NOT fail on them yet).
            CheckItemCapabilities(weapons, armors, failures, log);
            CheckCraftingChain(failures, log);
            CheckJewelerChain(failures, log);
            CheckTalentLayout(failures, log);

            // --- ARMED-HERO INVARIANT (WO-Item Addressables equip) -----------------
            // At scale (433+ weapons, Blink Addressable-keyed) BestWeapon(job,1) may now
            // return a weapon whose prefab is an Addressable key. If neither the Addressable
            // key resolves NOR the EquipmentController's Resources map yields an attachable
            // mesh, the hero spawns UNARMED (WO-425 regression). This is the permission gate
            // that the armed-hero invariant holds at scale: for each class the level-1 auto-
            // equip is non-null AND its prefab reference resolves.
            CheckArmedHeroInvariant(failures, log);

            // --- HAND-SLOT EQUIP RULES (owner 2026-06-18, docs/STORE_EQUIP_SPEC.md) -
            // Drive the REAL GearLoadout equip flow on a throwaway GameObject and assert the
            // mutually-exclusive main-hand/off-hand rules hold: a 2H clears the off-hand; an
            // off-hand clears a 2H main; a 1H + shield coexist; the swap never leaves the hero
            // unarmed when a 1H exists. Exercises the actual enforcement, not a re-derivation.
            CheckHandSlotRules(failures, log);

            // --- BATTLE CLOSING (WO-505) — victory/defeat audio + star rating ------
            // Two provable bones from the silent-climax gap: (a) the victory + defeat
            // music clips resolve to a NON-NULL AudioClip through the SAME Resources path
            // AudioBootstrap uses (Resources.Load<AudioClip>("victory"/"defeat")) — this
            // catches the silent-track bug class (e.g. Resources.Load("dungeon") == null);
            // (b) BattleStarRating computes the right tier + multiplier for sample durations.
            CheckBattleClosing(failures, log);

            // --- WEAPON SWING-TRAIL VFX (WO-504 slice 3) ---------------------------
            // The Knight swings one shared mesh, so the rarity must read through the
            // swing-trail color/width. Assert the pure WeaponVfxMap resolver returns a
            // DISTINCT color per band, the gold const at legendary, the steel default
            // for null, and a MONOTONICALLY escalating width. Bones — owner felt-tunes
            // the exact colors later; this gates the MAPPING, not the aesthetic.
            CheckWeaponVfx(failures, log);

            // --- ACCESSORIES (accessories.json -> GearCatalog.Accessories) ---------
            // WO-543: the third gear category (rings + amulets). Assert the JSON maps to 10
            // AccessoryDef objects, the (additive) stat bonuses stay within the non-legendary
            // caps (damageMult < 0.20, defense < 0.15), and every entry carries an iconPath
            // (the shop/equip sprite) — the same display-field gate the weapon/armor checks use.
            CheckAccessories(failures, log);

            // --- VENDOR STOCK QUERIES (vendors.json -> VendorRegistry/VendorStockResolver) ---
            // WO-598 "the honest shelf": every registered vendor's query must resolve >=1 item
            // OR carry an authored emptyLine (never a raw empty grid); no roster-unobtainable
            // class (Mage under Knight-only V1) may appear in ANY vendor result; and each
            // trade's result stays inside its declared bands (Market never weapons, Jeweler
            // never armor, Forge never consumables).
            CheckVendorStock(failures, log);

            // --- ARMOR/ACCESSORY RIM-LIGHT VFX (WO-543 ArmorVfxMap) ----------------
            // The armor/accessory rarity must read through the hero rim-light glow. Assert the
            // pure ArmorVfxMap resolver returns a DISTINCT color per band, the gold const at
            // legendary, common == OFF (intensity 0), and a MONOTONICALLY escalating intensity.
            CheckArmorVfx(failures, log);

            // --- ENEMY STRUCTURE-AWARE SWEEP (ff.enemystructureaware) ---------------
            // Closes the UNVERIFIED targeting item (commit 8aa24c32): the verify-capture
            // showed 0 sweep acquires. Construct a REAL Enemy + a side structure (so the
            // forward probe misses and only the all-direction sweep can catch it) and drive
            // the REAL ProbeForStructure across three cases — proving from data, not faith,
            // that the sweep fires (no hero), stays suppressed (hero in aggro), and is inert
            // when the flag is off (reversible).
            CheckEnemyStructureSweep(failures, log);

            // --- monetization covenant gate (LB-5) + tower upgrade perks (overnight silos C/E) ---
            if (!MonetizationCovenantRegression.Run(out var covReason)) failures.Add(covReason); else log.AppendLine("[covenant] " + covReason);
            if (!TowerPerkRegression.Run(out var towerPerkReason)) failures.Add(towerPerkReason); else log.AppendLine("[tower-perks] " + towerPerkReason);
            // --- F8 open-ticket oracles (data-decidable roots, seconds-fast) ------
            if (!TowerRespawnRegression.Run(out var towerRespawnReason)) failures.Add(towerRespawnReason); else log.AppendLine("[tower-respawn] " + towerRespawnReason);
            if (!DefenseTargetableRegression.Run(out var defTargetReason)) failures.Add(defTargetReason); else log.AppendLine("[def-target] " + defTargetReason);
            if (!ArenaPrefabAuditRegression.Run(out var arenaReason)) failures.Add(arenaReason); else log.AppendLine("[arena-prefab] " + arenaReason);
            // --- Wave-1 full-coverage oracles (docs/FULL_COVERAGE_PLAN_2026-07-08.md) ---
            if (!CoreDataHubRegression.Run(out var coreDataHubReason)) failures.Add(coreDataHubReason); else log.AppendLine("[core-datahub] " + coreDataHubReason);
            if (!CoreCatalogRegression.Run(out var coreCatalogReason)) failures.Add(coreCatalogReason); else log.AppendLine("[core-catalog] " + coreCatalogReason);
            if (!CoreWorldLogicRegression.Run(out var coreWorldReason)) failures.Add(coreWorldReason); else log.AppendLine("[core-world] " + coreWorldReason);
            if (!CoreSaveContractRegression.Run(out var coreSaveReason)) failures.Add(coreSaveReason); else log.AppendLine("[core-save] " + coreSaveReason);
            if (!HeroProgressionRegression.Run(out var heroProgReason)) failures.Add(heroProgReason); else log.AppendLine("[hero-prog] " + heroProgReason);
            if (!AegisSetReachabilityRegression.Run(out var aegisReason)) failures.Add(aegisReason); else log.AppendLine("[aegis] " + aegisReason);
            if (!BuildingUpgradeRegression.Run(out var buildUpgReason)) failures.Add(buildUpgReason); else log.AppendLine("[build-upgrade] " + buildUpgReason);
            if (!OfflineHarvestRegression.Run(out var offlineReason)) failures.Add(offlineReason); else log.AppendLine("[offline-harvest] " + offlineReason);
            if (!VillageEconomyRegression.Run(out var villEconReason)) failures.Add(villEconReason); else log.AppendLine("[village-econ] " + villEconReason);
            if (!ArenaCatalogRegression.Run(out var arenaCatReason)) failures.Add(arenaCatReason); else log.AppendLine("[arena-cat] " + arenaCatReason);
            if (!CompanionRosterRegression.Run(out var compRosterReason)) failures.Add(compRosterReason); else log.AppendLine("[companion-roster] " + compRosterReason);
            // --- WO-736: Barracks 7-type troop roster + tier-unlock ladder (program 732-737 close) ---
            if (!TroopRosterRegression.Run(out var troopRosterReason)) failures.Add(troopRosterReason); else log.AppendLine("[troop-roster] " + troopRosterReason);
            // --- WO-771.6/771.11: raid V1 win/stars/loot + live HUD (LOCKED teleport/deploy loop) ---
            if (!RaidScoringRegression.Run(out var raidScoringReason)) failures.Add(raidScoringReason); else log.AppendLine("[raid-scoring] " + raidScoringReason);
            if (!TownsfolkDialogueRegression.Run(out var townsfolkReason)) failures.Add(townsfolkReason); else log.AppendLine("[townsfolk] " + townsfolkReason);
            if (!AtbEngineRegression.Run(out var atbReason)) failures.Add(atbReason); else log.AppendLine("[atb-engine] " + atbReason);
            if (!EconomyMetaCatalogRegression.Run(out var econMetaReason)) failures.Add(econMetaReason); else log.AppendLine("[econ-meta] " + econMetaReason);
            if (!GlimmerEconomyRegression.Run(out var glimmerReason)) failures.Add(glimmerReason); else log.AppendLine("[glimmer] " + glimmerReason);
            if (!SceneRoutingRegression.Run(out var sceneRouteReason)) failures.Add(sceneRouteReason); else log.AppendLine("[scene-route] " + sceneRouteReason);
            if (!ArtResourceRegression.Run(out var artResReason)) failures.Add(artResReason); else log.AppendLine("[art-resource] " + artResReason);
            // --- WO-682: Sfx WebGL import invariant (no divergent WebGL overrides -> no FSB decode failures) ---
            if (!SfxWebglAudioRegression.Run(out var sfxWebglReason)) failures.Add(sfxWebglReason); else log.AppendLine("[sfx-webgl] " + sfxWebglReason);
            // --- 2026-07-12 SME suites (owner: "a SME per architect path, full suite each") ---
            if (!CoreSaveRegression.Run(out var coreSaveSmeReason)) failures.Add(coreSaveSmeReason); else log.AppendLine("[core-save-sme] " + coreSaveSmeReason);
            if (!BuildEconomyRegression.Run(out var buildEconReason)) failures.Add(buildEconReason); else log.AppendLine("[build-econ] " + buildEconReason);
            if (!ObsidianQueueRegression.Run(out var obsidianQueueReason)) failures.Add(obsidianQueueReason); else log.AppendLine("[obsidian-queue] " + obsidianQueueReason);
            // --- WO-781: wounded-troop recovery advance (TickRecovery live+offline callers) ---
            if (!ArmyRecoveryRegression.Run(out var troopRecoveryReason)) failures.Add(troopRecoveryReason); else log.AppendLine("[troop-recovery] " + troopRecoveryReason);
            if (!DataWebRegression.Run(out var dataWebReason)) failures.Add(dataWebReason); else log.AppendLine("[data-web] " + dataWebReason);
            if (!HudUiRegression.Run(out var hudUiSmeReason)) failures.Add(hudUiSmeReason); else log.AppendLine("[hud-ui-sme] " + hudUiSmeReason);
            if (!CombatAtbRegression.Run(out var combatAtbReason)) failures.Add(combatAtbReason); else log.AppendLine("[combat-atb] " + combatAtbReason);
            if (!DialogueRegression.Run(out var dialogueReason)) failures.Add(dialogueReason); else log.AppendLine("[dialogue] " + dialogueReason);
            if (!EnemyRigColorRegression.Run(out var enemyRigColorReason)) failures.Add(enemyRigColorReason); else log.AppendLine("[enemy-rig-color] " + enemyRigColorReason);
            // --- WO-772 Phase 1: EnemyResolver id->family->DISTINCT model (generic-skeleton fix, ENEMY_RESOLVER_OK) ---
            if (!EnemyResolverRegression.Run(out var enemyResolverReason)) failures.Add(enemyResolverReason); else log.AppendLine("[enemy-resolver] " + enemyResolverReason);
            // --- 2026-07-26: retired walk-up outpost (ff.raidwalk) + ambient region roam (ff.regionroam OFF) ---
            if (!OverworldCombatGateRegression.Run(out var owCombatReason)) failures.Add(owCombatReason); else log.AppendLine("[overworld-combat-gate] " + owCombatReason);
            // --- destroyed-structure owner ruling (repair no-op + exclusion predicates; play-mode remove is note-only) ---
            if (!DestroyedStructureRegression.Run(out var destroyedStructReason)) failures.Add(destroyedStructReason); else log.AppendLine("[destroyed-structure] " + destroyedStructReason);
            if (!OrcRigBindingAudit.Run(out var orcBindingReason)) failures.Add(orcBindingReason); else log.AppendLine("[orc-binding] " + orcBindingReason);
            if (!HeroLocomotionClipRegression.Run(out var heroLocoClipReason)) failures.Add(heroLocoClipReason); else log.AppendLine("[hero-loco-clips] " + heroLocoClipReason);
            // --- UI-Obsidian conformance (style-everything-obsidian LAW): flags NEW hand-rolled uGUI vs baseline debt ---
            if (!UiObsidianConformanceRegression.Run(out var uiObsidianReason)) failures.Add(uiObsidianReason); else log.AppendLine("[ui-obsidian] " + uiObsidianReason);
            if (!UiMvvmConformanceRegression.Run(out var uiMvvmReason)) failures.Add(uiMvvmReason); else log.AppendLine("[ui-mvvm] " + uiMvvmReason);
            if (!HudPostureRegression.Run(out var hudPostureReason)) failures.Add(hudPostureReason); else log.AppendLine("[hud-posture] " + hudPostureReason);
            // --- WO-673 strategic placement — the §5 permission gates (flag-off parity,
            // migration round-trip, one-per-id, save v30, repair chain, 45° yaw + claim) ---
            if (!StrategicPlacementRegression.Run(out var stratPlaceReason)) failures.Add(stratPlaceReason); else log.AppendLine("[strategic-placement] " + stratPlaceReason);
            // --- WO-676 skill-tree strategic redesign — §C gates G1-G3 (data/dual-copy/
            // vocabulary + StatSum stacking/clamps + NO DEAD NODES consumer registry) ---
            if (!TalentStrategyRegression.Run(out var talentStratReason)) failures.Add(talentStratReason); else log.AppendLine("[talent-strategy] " + talentStratReason);
            // --- WO-738 echo per-echo specialization — §2c permission gate (roster identity +
            // balance dual-copy + token/legacy round-trip + bonus math + save v33 + EchoLaneBonuses) ---
            if (!EchoSpecializationRegression.Run(out var echoSpecReason)) failures.Add(echoSpecReason); else log.AppendLine("[echo-spec] " + echoSpecReason);
            // --- WO-745 Room Forge pipeline gate (catalog/dual-copy/mate/seal/drift/overlap + spine+demo green) ---
            if (!DeNelle.Editor.Regression.RoomForgeRegression.Run(out var roomForgeReason)) failures.Add(roomForgeReason); else log.AppendLine("[room-forge] " + roomForgeReason);

            // --- P1 FIX-PROOF SUITES (14) -- each PROVES one architect-plan P1 fix headless.
            // Guard.Try-wrapped so one bad suite logs + is skipped, never aborting the batch.
            // FAIL-BY-DESIGN suites (crystal-production, dungeon-dressing, modal-registration,
            // ftue-honesty) fail TRUTHFULLY today and flip green when their fix lands.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "wave-scaling suite", () => { if (!WaveScalingRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wave-scaling] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-rewards suite", () => { if (!EnemyRewardRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[enemy-rewards] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "wall-mitigation suite", () => { if (!WallHeartMitigationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wall-mitigation] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "pack-grant suite", () => { if (!PackGrantRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[pack-grant] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "upgrade-authority suite", () => { if (!BuildingUpgradeAuthorityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[upgrade-authority] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "crystal-production suite", () => { if (!CrystalProductionRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[crystal-production] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "sfx-resolve suite", () => { if (!SfxResolveRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[sfx-resolve] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-exit suite", () => { if (!DungeonExitRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-exit] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-dressing suite", () => { if (!DungeonDressingRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-dressing] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-return suite", () => { if (!DungeonReturnSceneRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-return] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-lore suite", () => { if (!DungeonLoreReadableRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-lore] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-state-reset suite", () => { if (!DungeonStateResetRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-state-reset] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-defeat suite", () => { if (!DungeonDefeatEndsRunRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-defeat] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-exit suite", () => { if (!DungeonExitReachableRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-exit] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-defeat-realtime suite", () => { if (!DungeonRealtimeSettleRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-defeat-realtime] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-toast suite", () => { if (!DungeonToastRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-toast] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-fpv suite", () => { if (!DungeonFpvRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-fpv] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "modal-registration suite", () => { if (!ModalArbiterRegistrationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[modal-registration] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "founding-reach suite", () => { if (!FoundingReachabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[founding-reach] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "ftue-honesty suite", () => { if (!FtueHonestyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[ftue-honesty] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "echo-card-copy suite", () => { if (!EchoCardCopyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[echo-card-copy] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "shader-pin suite", () => { if (!ShaderPinRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[shader-pin] " + r); });
            // --- WO-761: fire leaves a lingering burn on <=50% structures until repaired/destroyed ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "structure-burn suite", () => { if (!StructureBurnRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[structure-burn] " + r); });
            // --- audit P1 closers (owner 2026-07-20): EW-3 waves.json schema guard + ECON-1 pack->cosmetic grantability integrity ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "waves-schema suite", () => { if (!WavesSchemaRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[waves-schema] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "pack-cosmetic-integrity suite", () => { if (!PackCosmeticIntegrityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[pack-cosmetic-integrity] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tower-wall-los suite", () => { if (!TowerWallLosRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tower-wall-los] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "vfx-aura-diff suite", () => { if (!VfxAuraDifferentiationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[vfx-aura-diff] " + r); });
            // --- owner VfxManualPicks per-tier tower projectiles: archer tier ladder + arcane base/upgraded wired + every key catalogued ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tower-proj-map suite", () => { if (!TowerProjectileMapRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tower-proj-map] " + r); });

            // --- Store/Inventory icon coverage (key data: real art vs glyph fallback) ---
            CheckItemIconCoverage(weapons, armors, failures, log);

            // --- TUTORIAL V2 REGISTRY (WO-T1, spec §2.5.4) ---------------------------
            // tutorial-steps.json invariants: steps parse; every dialogue id exists in
            // dialogues.json; every highlight id is a known registry key; every completion
            // signal is a known bus id/prefix; mandatory order strictly increasing; every
            // speaker used by tut_* dialogues has a speaker record with a portrait (the
            // yellow-disc class of bug becomes a build failure).
            CheckTutorialSteps(failures, log);

            // --- verdict -----------------------------------------------------------
            log.AppendLine("=== verdict ===");
            if (failures.Count == 0)
            {
                log.AppendLine("REGRESSION_OK");
                Debug.Log(log.ToString());
            }
            else
            {
                log.AppendLine($"REGRESSION_FAIL: {failures.Count} failure(s):");
                foreach (var f in failures) log.AppendLine("  - " + f);
                // LogError so it also lands in break-log.jsonl and fails loudly in the log scan.
                Debug.LogError(log.ToString());
            }
        }

        // =====================================================================
        //  TUTORIAL V2 REGISTRY — tutorial-steps.json invariants (WO-T1)
        // =====================================================================
        private static void CheckTutorialSteps(List<string> failures, StringBuilder log)
        {
            log.AppendLine("=== tutorial-steps.json (Tutorial V2 registry) ===");
            DeNelle.Core.Tutorial.TutorialStepCatalog.Reload();
            DeNelle.Core.Dialogue.DialogueCatalog.Reload();

            var all = DeNelle.Core.Tutorial.TutorialStepCatalog.All;
            var mandatory = DeNelle.Core.Tutorial.TutorialStepCatalog.MandatorySteps();
            var contextual = DeNelle.Core.Tutorial.TutorialStepCatalog.ContextualSteps();
            log.AppendLine($"tutorial-steps.json -> {all.Count} steps ({mandatory.Count} mandatory, {contextual.Count} contextual)");

            if (mandatory.Count == 0)
            { failures.Add("tutorial-steps.json deserialized to 0 mandatory steps (mapping break or empty)"); return; }
            // Owner ruling 2026-07-24 (end-after-defend): the founding arc ENDS at the
            // defend/survive beat = exactly 7 mandatory steps (greet, hollow, stores, town,
            // echo, defense, defend). The venture-out back half (world_encounter, return_home,
            // freedom) was SCRAPPED — players learn to venture out automatically, and the guide
            // lives under Settings -> Game Guide. Supersedes the WO-702 (2026-07-13) 10-step chain.
            if (mandatory.Count != 7)
                failures.Add($"tutorial mandatory chain has {mandatory.Count} steps — the owner-decided founding flow ends after defend at exactly 7 (2026-07-24)");

            // Known highlight-registry ids + completion-signal vocabulary.
            var knownHighlights = new HashSet<string>(DeNelle.Core.UI.TutorialHighlightRegistry.KnownIds);
            bool KnownSignal(string s) =>
                !string.IsNullOrEmpty(s) && (
                    s == DeNelle.Core.Tutorial.TutorialSignals.BuildModeEntered ||
                    s == DeNelle.Core.Tutorial.TutorialSignals.TowerPlaced ||
                    s == DeNelle.Core.Tutorial.TutorialSignals.WaveCleared ||
                    s == DeNelle.Core.Tutorial.TutorialSignals.ArenaWin ||
                    s == DeNelle.Core.Tutorial.TutorialSignals.ArenaLoss ||
                    s == DeNelle.Core.Tutorial.TutorialSignals.CanAffordUpgrade ||
                    s == DeNelle.Core.Tutorial.TutorialSignals.EchoBornSecond ||
                    s == DeNelle.Core.Tutorial.TutorialSignals.FirstGearAdded ||
                    s == DeNelle.Core.Tutorial.TutorialSignals.FirstSkillPoint ||
                    s.StartsWith(DeNelle.Core.Tutorial.TutorialSignals.DialogueEndedPrefix) ||
                    s.StartsWith(DeNelle.Core.Tutorial.TutorialSignals.HeroReachedPrefix) ||
                    s.StartsWith(DeNelle.Core.Tutorial.TutorialSignals.PanelOpenedPrefix) ||
                    // WO-702: per-item placement signals (build.structure_placed:<entryId>)
                    s.StartsWith(DeNelle.Core.Tutorial.TutorialSignals.StructurePlacedPrefix));

            int lastOrder = int.MinValue;
            var tutSpeakers = new HashSet<string>();
            foreach (var s in mandatory)
            {
                if (s.Order <= lastOrder)
                    failures.Add($"tutorial step '{s.Id}' order {s.Order} is not strictly increasing");
                lastOrder = s.Order;
            }

            foreach (var s in all)
            {
                if (s == null || string.IsNullOrEmpty(s.Id))
                { failures.Add("tutorial step with null/empty id"); continue; }

                // Completion signal present + known vocabulary.
                string sig = s.Completion != null ? s.Completion.Signal : null;
                if (!KnownSignal(sig))
                    failures.Add($"tutorial step '{s.Id}' completion signal '{sig ?? "<null>"}' is not a known bus id");

                // Dialogue ids resolve in dialogues.json.
                foreach (var did in new[] { s.Dialogue?.Intro, s.Dialogue?.Outro })
                {
                    if (string.IsNullOrEmpty(did)) continue;
                    var def = DeNelle.Core.Dialogue.DialogueCatalog.Find(did);
                    if (def == null)
                    { failures.Add($"tutorial step '{s.Id}' dialogue '{did}' does not exist in dialogues.json"); continue; }
                    foreach (var node in def.Nodes)
                        if (node?.Lines != null)
                            foreach (var line in node.Lines)
                                if (line != null && !string.IsNullOrEmpty(line.Speaker))
                                    tutSpeakers.Add(line.Speaker);
                }

                // Highlight ids come from the registry's build-time contract.
                if (s.Highlight != null)
                    foreach (var h in s.Highlight)
                        if (!string.IsNullOrEmpty(h) && !knownHighlights.Contains(h))
                            failures.Add($"tutorial step '{s.Id}' highlight '{h}' is not a known TutorialHighlightRegistry id");

                // WO-780: first taught tower must be prepaid so the player can place it.
                if (s.Id == "founding_defense" && (s.Grant == null || !s.Grant.PrepaidTower))
                    failures.Add("tutorial step 'founding_defense' must have grant.prepaidTower:true (WO-780 — taught tower must be affordable on ENTER)");

                // Contextual rules: oneShot + never pausePressure (a hint never gates).
                if (s.IsContextual)
                {
                    if (!s.OneShot) failures.Add($"contextual step '{s.Id}' must be oneShot:true");
                    if (s.PausePressure) failures.Add($"contextual step '{s.Id}' must never pausePressure");
                    if (s.Trigger == null || s.Trigger.Type != "signal" || string.IsNullOrEmpty(s.Trigger.Signal))
                        failures.Add($"contextual step '{s.Id}' must trigger on a signal");
                }
            }

            // Every tut_* speaker resolves to a card record WITH a portrait (or an
            // explicit-silhouette empty) — the yellow-disc bug class becomes a failure.
            foreach (var sp in tutSpeakers)
            {
                var rec = DeNelle.Core.Dialogue.DialogueCatalog.FindSpeaker(sp);
                if (rec == null)
                    failures.Add($"tutorial dialogue speaker '{sp}' has no speaker record in dialogues.json (blank NPC card)");
                else if (string.IsNullOrEmpty(rec.Portrait))
                    log.AppendLine($"  note: speaker '{sp}' has an empty portrait — renders the styled silhouette (deliberate).");
                else log.AppendLine($"  speaker '{sp}' -> portrait '{rec.Portrait}' ok");
            }
        }

        // =====================================================================
        //  ENEMY STRUCTURE-AWARE SWEEP — real Enemy.ProbeForStructure, 3 cases
        // =====================================================================
        // A minimal live IDamageableStructure stand-in (a "tower/wall") for the sweep to
        // acquire. Only the two interface members + a collider are needed.
        private sealed class OracleStructure : MonoBehaviour, DeNelle.Core.Combat.IDamageableStructure
        {
            public bool Alive = true;
            public bool IsAlive => Alive;
            public void ApplyContactDamage(float amount) { }
        }

        private static void SetPrivateField(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(obj, value);
        }

        // =====================================================================
        //  VENDOR STOCK QUERIES (WO-598) — the honest shelf, gated by data
        // =====================================================================
        // Resolves every registered vendor's stock query through the REAL resolver
        // (vendors.json -> VendorRegistry -> VendorStockResolver, the same path the
        // shop VM binds) with the roster PINNED to Knight-only (the V1 canon) and
        // asserts:
        //   1. vendors.json maps to >=1 VendorDef and covers the shoppable set
        //      (forge/armorer/market/jeweler — buildings.json isShoppable).
        //   2. every vendor resolves >=1 item OR carries an authored emptyLine
        //      (never a raw empty grid — flag_11).
        //   3. NO roster-unobtainable item leaks: under Knight-only no weapon with a
        //      non-knight job and no light-weight armor may appear (flag_08's Mage
        //      wands at the Forge, as a permanent gate).
        //   4. each trade stays inside its bands: goods vendors never surface
        //      weapons/armor/accessories; the jeweler never weapons/armor/consumables;
        //      gear vendors never consumables/materials (flag_03).
        //   5. the Forge resolves >=1 ELIGIBLE item for a level-1 Knight (the V1
        //      player can actually shop on day one).
        private static void CheckVendorStock(List<string> failures, StringBuilder log)
        {
            log.AppendLine("--- VENDOR STOCK (vendors.json + VendorStockResolver, WO-598) ---");

            VendorRegistry.Reload();
            GearCatalog.Reload();
            var vendors = VendorRegistry.All;
            log.AppendLine($"vendors.json -> {vendors.Count} VendorDef objects");
            if (vendors.Count == 0)
            {
                failures.Add("vendors.json deserialized to 0 vendors (mapping break or missing file)");
                return;
            }

            // 1. Coverage: every shoppable storefront id has a registry row.
            foreach (var required in new[] { "forge", "armorer", "market", "jeweler" })
            {
                bool found = false;
                foreach (var v in vendors)
                    if (v != null && string.Equals(v.Id, required, System.StringComparison.OrdinalIgnoreCase))
                    { found = true; break; }
                if (!found)
                    failures.Add($"vendors.json is missing the shoppable vendor '{required}' (buildings.json isShoppable)");
            }

            var knightOnly = new[] { "knight" };
            foreach (var v in vendors)
            {
                if (v == null || string.IsNullOrEmpty(v.Id)) { failures.Add("vendors.json entry with null/empty id"); continue; }

                // 2. Authored empty line — required so a 0-item resolve can never render raw.
                if (string.IsNullOrEmpty(v.EmptyLine))
                    failures.Add($"vendor '{v.Id}' has no authored emptyLine (raw empty grid would render)");

                // Resolve as the V1 shopper (Knight, generous level so level gates don't hide leaks),
                // roster PINNED to knight-only so the assert is deterministic regardless of flags.
                var wares = DeNelle.Village.Hero.VendorStockResolver.Resolve(v.Id, "knight", 99, knightOnly);
                var layout = DeNelle.Village.Hero.VendorStockResolver.LayoutFor(v.Id);
                log.AppendLine($"  vendor '{v.Id}' (layout={layout}) resolved {wares.Count} ware(s)");

                if (wares.Count == 0 && string.IsNullOrEmpty(v.EmptyLine))
                    failures.Add($"vendor '{v.Id}' resolves 0 items AND has no authored emptyLine");

                foreach (var ware in wares)
                {
                    // 3. Roster leak gate: under Knight-only, no non-knight weapon / light armor.
                    if (ware.Kind == DeNelle.Village.Hero.VendorWareKind.Weapon)
                    {
                        var w = GearCatalog.FindWeapon(ware.Id);
                        if (w == null) { failures.Add($"vendor '{v.Id}' weapon '{ware.Id}' resolves to no def"); continue; }
                        if (!GearCatalog.WeaponFitsClass(w, "knight"))
                            failures.Add($"vendor '{v.Id}' stocks '{w.id}' (job='{w.job}') — roster-unobtainable under Knight-only (the flag_08 Mage-wand class of bug)");
                    }
                    else if (ware.Kind == DeNelle.Village.Hero.VendorWareKind.Armor)
                    {
                        var a = GearCatalog.FindArmor(ware.Id);
                        if (a == null) { failures.Add($"vendor '{v.Id}' armor '{ware.Id}' resolves to no def"); continue; }
                        if (!GearCatalog.ArmorFitsClass(a, "knight"))
                            failures.Add($"vendor '{v.Id}' stocks '{a.id}' (weight='{a.weight}') — roster-unobtainable under Knight-only");
                    }

                    // 4. Trade-band gate: a ware outside the vendor's layout is a wrong-shelf leak.
                    bool isGear = ware.Kind == DeNelle.Village.Hero.VendorWareKind.Weapon ||
                                  ware.Kind == DeNelle.Village.Hero.VendorWareKind.Armor;
                    bool isGoods = ware.Kind == DeNelle.Village.Hero.VendorWareKind.Consumable ||
                                   ware.Kind == DeNelle.Village.Hero.VendorWareKind.Material;
                    bool isJewel = ware.Kind == DeNelle.Village.Hero.VendorWareKind.Ring ||
                                   ware.Kind == DeNelle.Village.Hero.VendorWareKind.Amulet ||
                                   ware.Kind == DeNelle.Village.Hero.VendorWareKind.Gem;
                    switch (layout)
                    {
                        case DeNelle.Village.Hero.VendorLayout.Goods:
                            if (isGear || isJewel)
                                failures.Add($"GOODS vendor '{v.Id}' surfaced a {ware.Kind} ('{ware.Id}') — the Market must never sell gear/jewelry (flag_03)");
                            break;
                        case DeNelle.Village.Hero.VendorLayout.Jeweler:
                            if (isGear || ware.Kind == DeNelle.Village.Hero.VendorWareKind.Consumable)
                                failures.Add($"JEWELER vendor '{v.Id}' surfaced a {ware.Kind} ('{ware.Id}') — the Jeweler must never sell weapons/armor (flag_11)");
                            break;
                        case DeNelle.Village.Hero.VendorLayout.Gear:
                            if (isGoods || isJewel)
                                failures.Add($"GEAR vendor '{v.Id}' surfaced a {ware.Kind} ('{ware.Id}') — outside its trade");
                            break;
                    }
                }
            }

            // 5. The V1 day-one Knight can actually buy at the Forge (>=1 ELIGIBLE ware at Lv 1).
            var forgeLv1 = DeNelle.Village.Hero.VendorStockResolver.Resolve("forge", "knight", 1, knightOnly);
            int eligible = 0;
            foreach (var wr in forgeLv1) if (wr.Eligible) eligible++;
            log.AppendLine($"  forge @ Knight Lv1 -> {forgeLv1.Count} ware(s), {eligible} eligible");
            if (eligible == 0)
                failures.Add("the Forge resolves 0 ELIGIBLE items for a level-1 Knight — the V1 player can't shop on day one");
        }

        // =====================================================================
        //  STORE / INVENTORY ICON COVERAGE — does each real item resolve ART?
        // =====================================================================
        // Answers the felt bug ("items show letters") with DATA, no screenshot:
        // call ItemIconCatalog on every real WeaponDef/ArmorDef and count real-sprite
        // vs glyph-fallback. wand/staff/censer -> null is BY DESIGN (no art); the V1
        // failure case is a KNIGHT sword resolving to glyph (sword art exists).
        private static void CheckItemIconCoverage(List<WeaponDef> weapons, List<ArmorDef> armors,
                                                  List<string> failures, StringBuilder log)
        {
            log.AppendLine("--- ICON COVERAGE (store/inventory) ---");

            int wReal = 0, wGlyph = 0; var wGlyphSample = new List<string>();
            foreach (var w in weapons)
            {
                if (w == null) continue;
                if (ItemIconCatalog.ForWeapon(w) != null) wReal++;
                else { wGlyph++; if (wGlyphSample.Count < 24) wGlyphSample.Add((w.id ?? "?") + "/" + (w.name ?? "?")); }
            }
            int aReal = 0, aGlyph = 0; var aGlyphSample = new List<string>();
            foreach (var a in armors)
            {
                if (a == null) continue;
                if (ItemIconCatalog.ForArmor(a) != null) aReal++;
                else { aGlyph++; if (aGlyphSample.Count < 24) aGlyphSample.Add((a.id ?? "?") + "/" + (a.name ?? "?")); }
            }
            log.AppendLine($"[icon-coverage] weapons: {wReal} real / {wGlyph} glyph   armors: {aReal} real / {aGlyph} glyph");
            if (wGlyph > 0) log.AppendLine("  weapon glyphs: " + string.Join(", ", wGlyphSample));
            if (aGlyph > 0) log.AppendLine("  armor glyphs:  " + string.Join(", ", aGlyphSample));

            // V1-critical: the Knight's actual starting weapon is a sword and MUST resolve art.
            try
            {
                var knightWeapon = GearCatalog.BestWeapon("knight", 1);
                if (knightWeapon != null && ItemIconCatalog.ForWeapon(knightWeapon) == null)
                    failures.Add($"icon: Knight starting weapon '{knightWeapon.id}/{knightWeapon.name}' falls to GLYPH — sword art should resolve (real bug, not the by-design staff/censer glyph)");
                else
                    log.AppendLine($"[icon-coverage] Knight start weapon '{(knightWeapon?.id ?? "null")}' -> {(knightWeapon != null && ItemIconCatalog.ForWeapon(knightWeapon) != null ? "REAL art OK" : "no weapon/glyph")}");
            }
            catch (System.Exception e) { log.AppendLine("[icon-coverage] knight-weapon probe threw: " + e.Message); }
        }

        private static void CheckEnemyStructureSweep(List<string> failures, StringBuilder log)
        {
            log.AppendLine("--- ENEMY STRUCTURE SWEEP (ff.enemystructureaware) ---");

            // FeatureFlags are read-only properties backed by PlayerPrefs ("ff.<name>":
            // 0=off, 1=on, -1=default). Drive the flag via the SAME key Get() reads, and
            // restore the prior pref exactly (delete if it was unset).
            const string FlagKey = "ff.enemystructureaware";
            int prevPref = PlayerPrefs.GetInt(FlagKey, -1);
            var created = new List<GameObject>();
            try
            {
                PlayerPrefs.SetInt(FlagKey, 1);   // force ON

                // Enemy at origin facing +Z. Structure 2.5m to the +X SIDE so the forward
                // probe (a short SphereCast along +Z) misses — only the all-direction sweep
                // can acquire it. This models a marching enemy with a tower off to the side.
                var enemyGo = new GameObject("OracleEnemy");
                created.Add(enemyGo);
                enemyGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var enemy = enemyGo.AddComponent<Enemy>();   // auto-adds NavMeshAgent + EnemyDamageable

                var structGo = new GameObject("OracleTower");
                created.Add(structGo);
                structGo.transform.position = new Vector3(2.5f, 0f, 0f);
                structGo.AddComponent<BoxCollider>().size = Vector3.one;
                var structure = structGo.AddComponent<OracleStructure>();

                SetPrivateField(enemy, "_enemyId", "oracle-enemy");
                SetPrivateField(enemy, "_structureSweepRadius", 5f);
                SetPrivateField(enemy, "_contactProbeDistance", 1.1f);
                SetPrivateField(enemy, "_heroAggroRadius", 7f);
                SetPrivateField(enemy, "_heroAggroDropMargin", 2.5f);
                // _structureScanBuffer is a field initializer (non-null); ensure anyway.
                var bufF = typeof(Enemy).GetField("_structureScanBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
                if (bufF != null && bufF.GetValue(enemy) == null) bufF.SetValue(enemy, new Collider[16]);

                Physics.SyncTransforms();

                var probe = typeof(Enemy).GetMethod("ProbeForStructure", BindingFlags.NonPublic | BindingFlags.Instance);
                if (probe == null) { failures.Add("structure sweep: Enemy.ProbeForStructure not found (renamed?)"); return; }

                // CASE A — no hero present -> the sweep MUST acquire the side structure.
                var a = probe.Invoke(enemy, null) as DeNelle.Core.Combat.IDamageableStructure;
                if (!ReferenceEquals(a, structure))
                    failures.Add($"structure sweep CASE A (no hero): expected to acquire the side structure, got '{(a as MonoBehaviour)?.name ?? "null"}' — the ff.enemystructureaware sweep did NOT fire");
                else
                    log.AppendLine("  CASE A no-hero: sweep acquired side structure OK");

                // CASE B — hero within aggro -> sweep SUPPRESSED (hero stays primary).
                var heroGo = new GameObject("OracleHero");
                created.Add(heroGo);
                heroGo.transform.position = new Vector3(0f, 0f, 1.5f);   // inside aggro radius
                SetPrivateField(enemy, "_heroTransform", heroGo.transform);
                Physics.SyncTransforms();
                var b = probe.Invoke(enemy, null) as DeNelle.Core.Combat.IDamageableStructure;
                if (b != null)
                    failures.Add($"structure sweep CASE B (hero in aggro): should SUPPRESS, but returned '{(b as MonoBehaviour)?.name}' — hero-primary gate broken");
                else
                    log.AppendLine("  CASE B hero-in-aggro: sweep suppressed OK");

                // CASE C — flag OFF -> legacy forward-only; sweep must be inert (reversible).
                SetPrivateField(enemy, "_heroTransform", null);
                PlayerPrefs.SetInt(FlagKey, 0);   // force OFF (legacy)
                var c = probe.Invoke(enemy, null) as DeNelle.Core.Combat.IDamageableStructure;
                if (c != null)
                    failures.Add($"structure sweep CASE C (flag off): should be legacy forward-only, but sweep returned '{(c as MonoBehaviour)?.name}' — not reversible");
                else
                    log.AppendLine("  CASE C flag-off: sweep inert (legacy) OK");
            }
            catch (System.Exception ex)
            {
                failures.Add($"structure sweep oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (prevPref == -1) PlayerPrefs.DeleteKey(FlagKey);
                else PlayerPrefs.SetInt(FlagKey, prevPref);
                foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // =====================================================================
        //  ABILITIES — abilities.json via AbilityCatalog (the real loader)
        // =====================================================================
        private static void CheckAbilities(List<string> failures, StringBuilder log)
        {
            AbilityCatalog.Reload();

            // Enumerate every class loadout the catalog exposes. We probe the known
            // hero classes (the catalog is keyed by lowercase class id); the default
            // 'mage' is the v2-foundation class that MUST be present.
            string[] classes = { "mage", "knight", "ranger", "cleric" };
            int total = 0;
            int bad = 0;
            foreach (var cls in classes)
            {
                var loadout = AbilityCatalog.GetLoadout(cls);
                if (loadout == null || loadout.Count == 0) continue;   // class simply not authored yet
                foreach (var ab in loadout)
                {
                    total++;
                    bool ok = ab != null
                              && !string.IsNullOrEmpty(ab.Slot)
                              && !string.IsNullOrEmpty(ab.Name);
                    if (!ok) bad++;
                    log.AppendLine($"  AB [{cls}] slot='{(ab != null ? ab.Slot : "<null>")}' " +
                                   $"name='{(ab != null ? ab.Name : "<null>")}' " +
                                   $"effect='{(ab != null ? ab.Effect : "<null>")}'");
                }
            }

            log.AppendLine($"abilities.json -> {total} AbilityDef object(s) across {classes.Length} probed class(es)");

            // The default 'mage' loadout is the proven v2 content: if it is empty, the
            // JSON->object mapping broke (wrong top-level key 'classes', renamed slots,
            // or a parse-to-empty) exactly like the gear case.
            if (AbilityCatalog.GetLoadout(AbilityCatalog.DefaultClass).Count == 0)
                failures.Add($"abilities.json: default class '{AbilityCatalog.DefaultClass}' has 0 abilities (mapping break or empty 'classes')");
            if (total == 0)
                failures.Add("abilities.json deserialized to 0 AbilityDef objects (mapping break or empty 'classes')");
            if (bad > 0)
                failures.Add($"{bad} ability(ies) have null/empty slot or name (would render blank on the hotbar)");
        }

        // =====================================================================
        //  ENEMIES — enemies.json via EnemyCatalog + the FACTORY model-path resolve
        // =====================================================================
        private static void CheckEnemies(List<string> failures, StringBuilder log)
        {
            // Load the catalog through the same WebGL-safe bytes WaveDataLoader reads
            // (its step 1 is CanonicalJson.Read; we deserialize the same way it does so
            // a schema/key break here is the SAME break the game would hit). The async
            // WaveDataLoader.LoadEnemiesAsync isn't awaitable in this sync harness, so we
            // mirror its exact parse: CanonicalJson.Read -> JsonConvert<EnemyCatalog>.
            string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
            EnemyCatalog catalog = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json); }
                catch (System.Exception ex)
                {
                    failures.Add($"enemies.json failed to parse: {ex.Message}");
                    log.AppendLine($"enemies.json -> PARSE ERROR: {ex.Message}");
                    return;
                }
            }

            if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
            {
                failures.Add("enemies.json deserialized to 0 EnemyDef objects (mapping break or empty 'enemies')");
                log.AppendLine("enemies.json -> 0 EnemyDef objects");
                return;
            }

            log.AppendLine($"enemies.json -> {catalog.Enemies.Count} EnemyDef object(s)");

            int badField = 0;
            foreach (var e in catalog.Enemies)
            {
                // Skip the schema-doc placeholder row (its id is the field description, not
                // a real enemy) — be conservative, don't fail on a documented non-entry.
                if (e != null && e.Id != null && e.Id.Contains(" ")) continue;

                bool ok = e != null && !string.IsNullOrEmpty(e.Id) && !string.IsNullOrEmpty(e.Name);
                if (!ok) { badField++; continue; }

                // PREFAB-PATH CHECK (catches the archer->lumber class). Resolve the model
                // EXACTLY as the single enemy-creation path does (EnemyFactory.ModelForEnemy),
                // then attempt the same Resources.Load<GameObject>("Enemies/<model>") the
                // factory's VisualFactory.Skin call performs. A null load means this enemy
                // ships as a tinted-capsule fallback at runtime — a silent regression.
                string model = EnemyFactory.ModelForEnemy(e);
                string path = "Enemies/" + model;
                var prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    failures.Add($"enemies.json: '{e.Id}' resolves to model '{model}' but Resources.Load<GameObject>(\"{path}\") is NULL (would spawn as a tinted capsule — wrong/missing prefab)");
                    log.AppendLine($"  EN {e.Id} -> model '{model}' | PREFAB MISSING at '{path}'");
                }
                else
                {
                    log.AppendLine($"  EN {e.Id} -> model '{model}' | prefab OK ('{path}')");
                }
            }
            if (badField > 0)
                failures.Add($"{badField} enemy(ies) have null/empty id or name");
        }

        // =====================================================================
        //  WAVE-SCALING (CITY-01 / CITY-06) + KILL REWARDS (BLIND-03-01)
        //  Two assertions that headlessly prove the core wave loop is no longer a
        //  no-progression, no-escalation treadmill:
        //   (1) the runtime DEFAULT WaveScalingCurve (the fallback WaveManager now
        //       ALWAYS creates when no asset is wired) applies a stat multiplier >1
        //       past wave 1 and keeps climbing - so wave 19 enemies are NOT wave-1
        //       enemies (the CITY-01 "no headless proof" gap, CITY-06).
        //   (2) every real enemies.json row carries xpReward>0 AND coinReward>0 - so
        //       the most-played mode pays hero XP + gold on every kill (BLIND-03-01).
        // =====================================================================
        private static void CheckWaveScaling(List<string> failures, StringBuilder log)
        {
            log.AppendLine("--- [wave-scaling] default-curve escalation + kill rewards ---");

            // (1) DEFAULT curve escalation. CreateInstance runs WaveScalingCurve's field
            //     initializers, which seed the default HP/speed/damage curves - the exact
            //     object WaveManager.EnsureScalingCurve now returns when no asset is wired.
            var curve = ScriptableObject.CreateInstance<WaveScalingCurve>();
            float hp1  = curve.HpMultiplier(1);
            float hp10 = curve.HpMultiplier(10);
            float hp19 = curve.HpMultiplier(19);
            float dmg19 = curve.DamageMultiplier(19);
            float spd19 = curve.SpeedMultiplier(19);
            log.AppendLine($"  default curve HP x{hp1:0.00}@w1 -> x{hp10:0.00}@w10 -> x{hp19:0.00}@w19; " +
                           $"dmg x{dmg19:0.00}@w19; spd x{spd19:0.00}@w19");
            if (!(hp10 > 1f))
                failures.Add($"[wave-scaling] default HP multiplier at wave 10 is {hp10:0.00} (expected >1 - wave scaling would be DEAD)");
            if (!(hp19 > hp1))
                failures.Add($"[wave-scaling] default HP multiplier does not increase by wave 19 ({hp19:0.00} <= wave-1 {hp1:0.00})");
            if (!(dmg19 > 1f))
                failures.Add($"[wave-scaling] default contact-damage multiplier at wave 19 is {dmg19:0.00} (expected >1)");
            UnityEngine.Object.DestroyImmediate(curve);

            // (2) enemies.json kill rewards. Parse through the same CanonicalJson bytes the
            //     game reads (WaveDataLoader path), then assert every real row pays out.
            string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
            EnemyCatalog catalog = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json); }
                catch (System.Exception ex)
                {
                    failures.Add($"[wave-scaling] enemies.json failed to parse for reward check: {ex.Message}");
                    return;
                }
            }
            if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
            {
                failures.Add("[wave-scaling] enemies.json produced 0 rows for the reward check");
                return;
            }

            int checkedRows = 0, noReward = 0;
            foreach (var e in catalog.Enemies)
            {
                // Skip the schema-doc placeholder row (id carries a space - see CheckEnemies).
                if (e == null || string.IsNullOrEmpty(e.Id) || e.Id.Contains(" ")) continue;
                checkedRows++;
                if (e.XpReward <= 0 || e.CoinReward <= 0)
                {
                    noReward++;
                    failures.Add($"[wave-scaling] enemy '{e.Id}' missing kill rewards " +
                                 $"(xpReward={e.XpReward}, coinReward={e.CoinReward}; both must be > 0)");
                }
            }
            log.AppendLine($"  reward coverage: {checkedRows - noReward}/{checkedRows} enemy rows carry xp+coin rewards");
        }

        // =====================================================================
        //  STRUCTURES — structures-catalog.json visualPrefabPath load + type check
        // =====================================================================
        [System.Serializable]
        private sealed class StructuresCatalogFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        private static void CheckStructures(List<string> failures, StringBuilder log)
        {
            // Parse identically to the production CatalogBootstrap.LoadFromJson so a
            // schema break shows up HERE the same way it would at startup.
            string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/structures-catalog.json");
            StructuresCatalogFile file = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        Converters = { new StringEnumConverter() },
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                    };
                    file = JsonConvert.DeserializeObject<StructuresCatalogFile>(json, settings);
                }
                catch (System.Exception ex)
                {
                    failures.Add($"structures-catalog.json failed to parse: {ex.Message}");
                    log.AppendLine($"structures-catalog.json -> PARSE ERROR: {ex.Message}");
                    return;
                }
            }

            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("structures-catalog.json deserialized to 0 CatalogEntry objects (mapping break or empty 'entries')");
                log.AppendLine("structures-catalog.json -> 0 CatalogEntry objects");
                return;
            }

            log.AppendLine($"structures-catalog.json -> {file.Entries.Count} CatalogEntry object(s)");

            int badField = 0;
            foreach (var entry in file.Entries)
            {
                bool ok = entry != null && !string.IsNullOrEmpty(entry.id) && !string.IsNullOrEmpty(entry.displayName);
                if (!ok) { badField++; continue; }

                // Composites have no own mesh (they bundle cell entries) and a sparse
                // decoration row may legitimately omit visualPrefabPath — only assert the
                // ones the catalog ACTUALLY declares a path for (conservative).
                if (string.IsNullOrEmpty(entry.visualPrefabPath))
                {
                    log.AppendLine($"  ST {entry.id} | no visualPrefabPath (composite/decoration) — skipped");
                    continue;
                }

                // PREFAB-PATH CHECK: StructureFactory.Create -> VisualFactory.Skin does
                // Resources.Load<GameObject>(visualPrefabPath). A null load = the structure
                // builds with NO mesh (StructureFactory.cs:88-90 warning) — the archer->
                // lumber class for towers/structures.
                var prefab = Resources.Load<GameObject>(entry.visualPrefabPath);
                if (prefab == null)
                {
                    failures.Add($"structures-catalog.json: '{entry.id}' visualPrefabPath '{entry.visualPrefabPath}' loads NULL (structure would build with no mesh — wrong/missing prefab)");
                    log.AppendLine($"  ST {entry.id} -> '{entry.visualPrefabPath}' | PREFAB MISSING");
                }
                else
                {
                    log.AppendLine($"  ST {entry.id} -> '{entry.visualPrefabPath}' | prefab OK");
                }
            }
            if (badField > 0)
                failures.Add($"{badField} structure entry(ies) have null/empty id or displayName");
        }

        // =====================================================================
        //  BUILDINGS — buildings.json via BuildingCatalog (the real loader)
        // =====================================================================
        private static void CheckBuildings(List<string> failures, StringBuilder log)
        {
            BuildingCatalog.Reload();
            var buildings = new List<BuildingDef>(BuildingCatalog.Buildings);

            log.AppendLine($"buildings.json -> {buildings.Count} BuildingDef object(s)");
            if (buildings.Count == 0)
                failures.Add("buildings.json deserialized to 0 BuildingDef objects (mapping break or empty 'buildings')");

            int bad = 0;
            foreach (var b in buildings)
            {
                // displayName is a canon-strings KEY (not a literal) but must be non-empty
                // so the build menu can resolve a name; id is the build/cooldown key.
                bool ok = b != null && !string.IsNullOrEmpty(b.Id) && !string.IsNullOrEmpty(b.DisplayName);
                if (!ok) bad++;
                log.AppendLine($"  BD {(b != null ? b.Id : "<null>")} | displayName='{(b != null ? b.DisplayName : "<null>")}' " +
                               $"| model='{(b != null ? b.Model : "<null>")}' (mesh key, not a Resources path)");
            }
            if (bad > 0)
                failures.Add($"{bad} building(s) have null/empty id or displayName");
        }

        // =====================================================================
        //  WO-771.9 — BARRACKS & TROOP UPGRADE PROGRESSION source-lint
        //  Emits BARRACKS_PROGRESSION_OK when the ladder + curves + reconcile pass.
        // =====================================================================
        private static void CheckBarracksProgression(List<string> failures, StringBuilder log)
        {
            int before = failures.Count;

            BarracksCatalog.Reload();
            TroopUpgradeCatalog.Reload();
            TroopCatalog.Reload();

            var levels = new List<BarracksDef>(BarracksCatalog.All);
            var troops = new List<TroopDef>(TroopCatalog.All);
            var upgrades = new List<TroopUpgradeDef>(TroopUpgradeCatalog.All);

            log.AppendLine($"barracks.json -> {levels.Count} level(s); troop-upgrades.json -> {upgrades.Count} row(s)");

            if (levels.Count == 0) failures.Add("barracks.json deserialized to 0 levels (mapping break or empty 'levels')");
            if (troops.Count == 0) failures.Add("troops.json deserialized to 0 troops (barracks progression needs the roster)");

            // Level 1 must be the free day-one baseline; the ladder must be contiguous 1..Max.
            int max = BarracksCatalog.MaxLevel;
            for (int lvl = 1; lvl <= max; lvl++)
            {
                var def = BarracksCatalog.Find(lvl);
                if (def == null) { failures.Add($"barracks.json is missing level {lvl} (ladder must be contiguous 1..{max})"); continue; }
                if (lvl == 1 && !def.Cost.IsZero)
                    failures.Add("barracks.json level 1 must be FREE (zero cost) — it is the day-one baseline");
                if (lvl == 1 && def.BuildTimeSeconds != 0f)
                    failures.Add("barracks.json level 1 must have zero build time (day-one baseline)");

                // Every unlocked troop id must resolve AND its UnlockBarracksTier must == this level (reconcile).
                if (def.UnlocksTroopIds != null)
                {
                    foreach (var id in def.UnlocksTroopIds)
                    {
                        var t = TroopCatalog.Find(id);
                        if (t == null) { failures.Add($"barracks.json level {lvl} unlocks unknown troop id '{id}'"); continue; }
                        if (t.UnlockBarracksTier != lvl)
                            failures.Add($"reconcile mismatch: '{id}' listed at barracks level {lvl} but troops.json UnlockBarracksTier={t.UnlockBarracksTier}");
                    }
                }
            }

            // Every troop should be unlocked by exactly the barracks level == its UnlockBarracksTier.
            foreach (var t in troops)
            {
                if (t == null) continue;
                bool unlockedAtTier = BarracksProgression.IsTroopUnlocked(t.Id, t.UnlockBarracksTier);
                bool lockedBelow = t.UnlockBarracksTier <= 1 || !BarracksProgression.IsTroopUnlocked(t.Id, t.UnlockBarracksTier - 1);
                if (!unlockedAtTier) failures.Add($"troop '{t.Id}' is NOT unlocked at its own UnlockBarracksTier {t.UnlockBarracksTier}");
                if (!lockedBelow) failures.Add($"troop '{t.Id}' is unlocked BELOW its UnlockBarracksTier {t.UnlockBarracksTier} (gate leak)");
            }

            // Every upgrade row: troop resolves; curves present + start at the 1.0 baseline.
            int abilitiesUnresolved = 0;
            foreach (var upg in upgrades)
            {
                if (upg == null) continue;
                if (TroopCatalog.Find(upg.TroopId) == null)
                    failures.Add($"troop-upgrades.json row '{upg.TroopId}' has no matching troop in troops.json");

                CheckCurveBaseline(failures, upg.TroopId, "reach", upg.Reach);
                CheckCurveBaseline(failures, upg.TroopId, "strength", upg.Strength);

                if (upg.SpecialAbilities != null)
                {
                    foreach (var a in upg.SpecialAbilities)
                    {
                        if (a == null) continue;
                        if (a.LevelThreshold < 1)
                            failures.Add($"'{upg.TroopId}' ability '{a.AbilityId}' has a non-positive levelThreshold");
                        // Ability id resolution is INFORMATIONAL only (abilities.json population is WO-771.14) — log, don't fail.
                        if (string.IsNullOrEmpty(a.AbilityId) || AbilityCatalog.FindById(a.AbilityId) == null)
                            abilitiesUnresolved++;
                    }
                }
            }
            if (abilitiesUnresolved > 0)
                log.AppendLine($"[barracks] note: {abilitiesUnresolved} upgrade ability id(s) not yet in abilities.json (informational; WO-771.14 owns ability wiring)");

            if (failures.Count == before)
                log.AppendLine("BARRACKS_PROGRESSION_OK");
            else
                log.AppendLine($"BARRACKS_PROGRESSION_FAIL: {failures.Count - before} issue(s)");
        }

        // A StatCurve must be authored and start at the 1.0 baseline (values[0] == 1.0).
        private static void CheckCurveBaseline(List<string> failures, string troopId, string which, StatCurve curve)
        {
            if (curve == null || curve.Values == null || curve.Values.Length == 0)
            {
                failures.Add($"'{troopId}' {which} curve is empty (must define per-level multipliers)");
                return;
            }
            if (System.Math.Abs(curve.Values[0] - 1.0f) > 0.001f)
                failures.Add($"'{troopId}' {which} curve must start at 1.0 baseline (values[0]={curve.Values[0]:0.###})");
        }

        // WO-588: validate the Game Guide codex content (guide-content.json).
        // =====================================================================
        //  DIALOGUE SPEAKER CARDS — name + affiliation + portrait all resolve
        // =====================================================================
        private static void CheckDialogueSpeakers(List<string> failures, StringBuilder log)
        {
            DeNelle.Core.Dialogue.DialogueCatalog.Reload();
            var dialogues = DeNelle.Core.Dialogue.DialogueCatalog.Dialogues;
            var speakers  = DeNelle.Core.Dialogue.DialogueCatalog.Speakers;
            log.AppendLine($"dialogues.json -> {dialogues.Count} DialogueDef, {speakers.Count} speaker record(s)");

            if (dialogues.Count == 0) { failures.Add("dialogues.json deserialized to 0 dialogues (mapping break)"); return; }
            if (speakers.Count == 0)  { failures.Add("dialogues.json has no 'speakers' block (card standard: name+affiliation+portrait per speaker)"); return; }

            // 1) Every speaker record carries a name + affiliation; a DECLARED portrait path loads.
            foreach (var s in speakers)
            {
                if (s == null || string.IsNullOrEmpty(s.Name))
                { failures.Add("speakers block contains a record with null/empty name"); continue; }
                if (string.IsNullOrEmpty(s.Affiliation))
                    failures.Add($"speaker '{s.Name}' has no affiliation (card standard requires guild/shop affiliation)");
                string portraitState = "silhouette";
                if (!string.IsNullOrEmpty(s.Portrait))
                {
                    var sp = Resources.Load<Sprite>(s.Portrait);
                    if (sp == null) failures.Add($"speaker '{s.Name}' declares portrait '{s.Portrait}' but Resources.Load<Sprite> returned null (dangling path)");
                    else portraitState = s.Portrait;
                }
                log.AppendLine($"  S {s.Name} | affiliation='{s.Affiliation}' | portrait={portraitState}");
            }

            // 2) Every spoken line's speaker resolves to a record (the card can always render
            //    name + affiliation); every legacy per-node `portrait` command arg loads.
            foreach (var d in dialogues)
            {
                if (d == null || d.Nodes == null) continue;
                foreach (var node in d.Nodes)
                {
                    if (node == null) continue;
                    if (node.Lines != null)
                        foreach (var line in node.Lines)
                        {
                            if (line == null || string.IsNullOrEmpty(line.Speaker)) continue; // narration is legal
                            if (DeNelle.Core.Dialogue.DialogueCatalog.FindSpeaker(line.Speaker) == null)
                                failures.Add($"dialogue '{d.Id}' node '{node.Id}': speaker '{line.Speaker}' has no speakers-block record (card cannot show affiliation)");
                        }
                    if (node.Commands != null)
                        foreach (var cmd in node.Commands)
                        {
                            if (cmd == null || cmd.Verb != "portrait") continue;
                            string path = (cmd.Args != null && cmd.Args.Count > 0) ? cmd.Args[0] : null;
                            if (string.IsNullOrEmpty(path) || Resources.Load<Sprite>(path) == null)
                                failures.Add($"dialogue '{d.Id}' node '{node.Id}': `portrait` command path '{path}' does not resolve a sprite");
                        }
                }
            }
        }

        private static void CheckGuideContent(List<string> failures, StringBuilder log)
        {
            GuideContentCatalog.Reload();
            var sections = new List<GuideSection>(GuideContentCatalog.Sections);

            log.AppendLine($"guide-content.json -> {sections.Count} GuideSection object(s)");
            if (sections.Count == 0)
            {
                failures.Add("guide-content.json deserialized to 0 sections (mapping break or empty 'sections')");
                return;
            }

            int badField = 0, emptyBody = 0;
            var seenIds = new HashSet<string>();
            foreach (var s in sections)
            {
                bool ok = s != null
                          && !string.IsNullOrEmpty(s.Id)
                          && !string.IsNullOrEmpty(s.Tab)
                          && !string.IsNullOrEmpty(s.Title);
                if (!ok) { badField++; continue; }

                if (!seenIds.Add(s.Id))
                    failures.Add($"guide-content.json: duplicate section id '{s.Id}'");

                // Body must have at least one non-empty paragraph (a blank body renders as an empty tab).
                bool hasBody = false;
                if (s.Body != null)
                    foreach (var p in s.Body)
                        if (!string.IsNullOrEmpty(p)) { hasBody = true; break; }
                if (!hasBody) emptyBody++;

                log.AppendLine($"  GG {s.Id} | tab='{s.Tab}' status='{s.Status}' " +
                               $"body={(s.Body != null ? s.Body.Count : 0)} tips={(s.Tips != null ? s.Tips.Count : 0)}");
            }
            if (badField > 0) failures.Add($"{badField} guide section(s) have null/empty id, tab, or title");
            if (emptyBody > 0) failures.Add($"{emptyBody} guide section(s) have an empty body (no non-empty paragraph)");
        }

        // WO-587: validate the Population milestone table that drives Echo slot unlocks.
        private static void CheckPopulationMilestones(List<string> failures, StringBuilder log)
        {
            PopulationMilestonesCatalog.Reload();
            var milestones = new List<PopulationMilestone>(PopulationMilestonesCatalog.Milestones);

            log.AppendLine($"population-milestones.json -> {milestones.Count} PopulationMilestone object(s)");
            if (milestones.Count == 0)
            {
                failures.Add("population-milestones.json deserialized to 0 milestones (mapping break or empty 'milestones')");
                return;
            }

            // Echo slots must ascend 2,3,4,... with NO gaps and a condition on every entry
            // (the catalog already sorts by EchoSlot ascending).
            int expected = 2;
            foreach (var m in milestones)
            {
                if (m == null) { failures.Add("population-milestones.json: a null milestone entry"); continue; }

                if (m.EchoSlot != expected)
                    failures.Add($"population-milestones.json: echoSlot {m.EchoSlot} out of order/gapped (expected {expected}; slots must ascend 2..5 with no gaps)");

                if (!m.HasAnyCondition)
                    failures.Add($"population-milestones.json: echoSlot {m.EchoSlot} has NO condition (needs at least one 'any' or 'all' threshold)");

                log.AppendLine($"  PM slot={m.EchoSlot} " +
                               $"any=[{CondStr(m.Any)}] all=[{CondStr(m.All)}]");
                expected++;
            }

            // Slots should reach the design max of 5 (3 organic + 2 flex).
            if (milestones[milestones.Count - 1].EchoSlot != 5)
                failures.Add($"population-milestones.json: top echoSlot is {milestones[milestones.Count - 1].EchoSlot}, expected 5 (3 organic + 2 flex cap)");
        }

        private static string CondStr(MilestoneCondition c)
        {
            if (c == null || c.IsEmpty) return "-";
            var parts = new List<string>();
            if (c.Xp > 0) parts.Add($"xp>={c.Xp}");
            if (c.QuestsCompleted > 0) parts.Add($"quests>={c.QuestsCompleted}");
            if (c.OutpostsCleared > 0) parts.Add($"outposts>={c.OutpostsCleared}");
            if (c.WavesCleared > 0) parts.Add($"waves>={c.WavesCleared}");
            if (c.VillageLevel > 0) parts.Add($"village>={c.VillageLevel}");
            return string.Join(",", parts);
        }

        // =====================================================================
        //  ITEM-MODEL CAPABILITIES — WO-Item-1 invariants (docs/ITEM_MODEL.md §2c)
        // -----------------------------------------------------------------------
        //  HARD (fail REGRESSION_FAIL when violated):
        //   - every Weapon entry resolves Carriable|Equippable
        //   - every Armor/Gear entry resolves Carriable|Equippable
        //   - every Consumable entry resolves Carriable|Usable
        //   - NO entry resolves both Carriable and AI (an item is never an enemy)
        //  SOFT (report a coverage count, do NOT fail — WO-Item-2's generator fills
        //  prefabPath; failing now would block the additive foundation):
        //   - how many Carriable entries resolve a non-null prefabPath
        // =====================================================================
        private static void CheckItemCapabilities(
            List<WeaponDef> weapons, List<ArmorDef> armors,
            List<string> failures, StringBuilder log)
        {
            const ItemCapability EQUIP = ItemCapability.Carriable | ItemCapability.Equippable;
            const ItemCapability USE   = ItemCapability.Carriable | ItemCapability.Usable;

            // Load consumables through the same real loader the game uses.
            ConsumableCatalog.Reload();
            var consumables = new List<ConsumableDef>(ConsumableCatalog.All);
            log.AppendLine($"consumables.json -> {consumables.Count} ConsumableDef object(s)");

            int carriableTotal = 0;     // SOFT denominator
            int prefabResolved = 0;     // SOFT numerator (prefabPath non-null on a Carriable)

            // --- Weapons: must resolve Carriable|Equippable, never AI ---
            foreach (var w in weapons)
            {
                if (w == null) continue;
                var cap = w.Capabilities;
                if ((cap & EQUIP) != EQUIP)
                    failures.Add($"weapons.json: '{w.id}' resolves {cap} — must retain Carriable|Equippable");
                if ((cap & ItemCapability.Carriable) != 0 && (cap & ItemCapability.AI) != 0)
                    failures.Add($"weapons.json: '{w.id}' resolves BOTH Carriable and AI (an item is never an enemy)");
                if ((cap & ItemCapability.Carriable) != 0)
                {
                    carriableTotal++;
                    if (!string.IsNullOrEmpty(w.prefabPath)) prefabResolved++;
                }
            }

            // --- Armor/Gear: must resolve Carriable|Equippable, never AI ---
            foreach (var a in armors)
            {
                if (a == null) continue;
                var cap = a.Capabilities;
                if ((cap & EQUIP) != EQUIP)
                    failures.Add($"armor.json: '{a.id}' resolves {cap} — must retain Carriable|Equippable");
                if ((cap & ItemCapability.Carriable) != 0 && (cap & ItemCapability.AI) != 0)
                    failures.Add($"armor.json: '{a.id}' resolves BOTH Carriable and AI (an item is never an enemy)");
                if ((cap & ItemCapability.Carriable) != 0)
                {
                    carriableTotal++;
                    if (!string.IsNullOrEmpty(a.prefabPath)) prefabResolved++;
                }
            }

            // --- Consumables: must resolve Carriable|Usable, never AI ---
            foreach (var c in consumables)
            {
                if (c == null) continue;
                var cap = c.Capabilities;
                if ((cap & USE) != USE)
                    failures.Add($"consumables.json: '{c.Id}' resolves {cap} — must retain Carriable|Usable");
                if ((cap & ItemCapability.Carriable) != 0 && (cap & ItemCapability.AI) != 0)
                    failures.Add($"consumables.json: '{c.Id}' resolves BOTH Carriable and AI (an item is never an enemy)");
                if ((cap & ItemCapability.Carriable) != 0)
                {
                    carriableTotal++;
                    if (!string.IsNullOrEmpty(c.PrefabPath)) prefabResolved++;
                }
            }

            // SOFT coverage line — WO-Item-2's generator populates prefabPath; until then
            // 0/N is EXPECTED and must NOT fail (the foundation is additive, no behavior change).
            log.AppendLine($"[item-model] capability invariants checked on " +
                           $"{weapons.Count}W + {armors.Count}A + {consumables.Count}C entries");
            log.AppendLine($"[item-model] SOFT prefabPath coverage: {prefabResolved}/{carriableTotal} " +
                           $"Carriable entries resolve a non-null prefabPath (WO-Item-2 fills the rest)");
        }

        // =====================================================================
        //  CRAFTING CHAIN — drops -> craft -> inventory data smoke (WO Consumable-
        //  Crafting-V1 + overnight stretch). The runtime transaction reuses the
        //  already-shipping VillageInventory larder (proven elsewhere); this oracle
        //  guards the DATA chain so new content can never break craftability:
        //   HARD (fail REGRESSION):
        //    - every recipe Output resolves in the consumable catalog
        //    - every recipe ingredient resolves in the material catalog (legacy ids OK)
        //    - every art-backed (ing_*) ingredient is DROPPABLE in >=1 loot table
        //      (so the ingredient is obtainable -> the recipe is actually craftable)
        //    - every loot-table drop materialId resolves in the material catalog
        //      (no phantom drop) — legacy scaffolding ids excepted
        //   SOFT (log only): ing_* materials used by no recipe (orphan ingredient)
        // =====================================================================
        private static void CheckCraftingChain(List<string> failures, StringBuilder log)
        {
            // Documented legacy scaffolding ids: referenced by the 4 pre-existing
            // recipes + the default loot tables, intentionally have NO MaterialDef
            // (glyph fallback). Must not fail the gate.
            var legacy = new HashSet<string>
            {
                "wild-herb", "rare-essence", "monster-hide", "tattered-cloth", "ember-resin"
            };

            MaterialCatalog.Reload();
            ConsumableCatalog.Reload();
            ConsumableCraftingCatalog.Reload();
            LootTableCatalog.Reload();
            VendorRegistry.Reload();

            var materialIds = new HashSet<string>();
            foreach (var m in MaterialCatalog.All)
                if (m != null && !string.IsNullOrEmpty(m.Id)) materialIds.Add(m.Id);

            // Union of every materialId dropped by any loot table (obtainable set).
            var droppable = new HashSet<string>();
            foreach (var t in LootTableCatalog.All)
            {
                if (t == null || t.Drops == null) continue;
                foreach (var d in t.Drops)
                {
                    if (d == null || string.IsNullOrEmpty(d.MaterialId)) continue;
                    droppable.Add(d.MaterialId);
                    if (!materialIds.Contains(d.MaterialId) && !legacy.Contains(d.MaterialId))
                        failures.Add($"loot-tables.json: table '{t.Id}' drops phantom material '{d.MaterialId}' (no MaterialDef)");
                }
            }

            // Union of every material/consumable id any vendor actually SELLS, resolved
            // through the REAL shelf path (vendors.json -> VendorStockResolver — the WO-598
            // Market surfaces every non-gem priced material). Buy-only ingredients are a
            // legitimate acquisition mode; "obtainable" = droppable OR purchasable.
            var purchasable = new HashSet<string>();
            foreach (var v in VendorRegistry.All)
            {
                if (v == null || string.IsNullOrEmpty(v.Id)) continue;
                foreach (var ware in DeNelle.Village.Hero.VendorStockResolver.Resolve(v.Id, "knight", 99, new[] { "knight" }))
                    if (ware.Kind == DeNelle.Village.Hero.VendorWareKind.Material ||
                        ware.Kind == DeNelle.Village.Hero.VendorWareKind.Consumable)
                        purchasable.Add(ware.Id);
            }

            var recipes = ConsumableCraftingCatalog.All;
            var usedIngredients = new HashSet<string>();
            int chainOk = 0;
            foreach (var r in recipes)
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) continue;

                if (string.IsNullOrEmpty(r.Output) || !ConsumableCatalog.IsConsumable(r.Output))
                    failures.Add($"consumable-recipes.json: '{r.Id}' output '{r.Output}' is not a known consumable");

                bool ingredientsOk = true;
                if (r.Ingredients != null)
                {
                    foreach (var ing in r.Ingredients)
                    {
                        if (ing == null || string.IsNullOrEmpty(ing.Id)) continue;
                        usedIngredients.Add(ing.Id);

                        bool isMat = materialIds.Contains(ing.Id);
                        if (!isMat && !legacy.Contains(ing.Id))
                        {
                            failures.Add($"consumable-recipes.json: '{r.Id}' needs unknown ingredient '{ing.Id}' (no MaterialDef)");
                            ingredientsOk = false;
                        }
                        // Art-backed ingredient must be obtainable — from a drop OR a vendor
                        // shelf — else the recipe is uncraftable in normal play. (Was drops-only;
                        // that false-failed the 5 Market buy-only herbs/liquids, WO-600 reclassed.)
                        if (isMat && ing.Id.StartsWith("ing_") && !droppable.Contains(ing.Id) && !purchasable.Contains(ing.Id))
                        {
                            failures.Add($"consumable-recipes.json: '{r.Id}' ingredient '{ing.Id}' has NO acquisition path (no loot drop AND no vendor shelf) (uncraftable)");
                            ingredientsOk = false;
                        }
                    }
                }
                if (ingredientsOk && ConsumableCatalog.IsConsumable(r.Output)) chainOk++;
            }

            // SOFT: art-backed materials that no recipe consumes (dead-end drops).
            int orphan = 0;
            foreach (var id in materialIds)
                if (id.StartsWith("ing_") && !usedIngredients.Contains(id)) orphan++;

            log.AppendLine($"[crafting] chain checked: {recipes.Count} recipe(s), {materialIds.Count} material(s), " +
                           $"{droppable.Count} droppable id(s); {chainOk} recipe(s) fully craftable drops->craft->consumable");
            if (orphan > 0)
                log.AppendLine($"[crafting] SOFT: {orphan} ing_* material(s) used by no recipe (dead-end drop)");
        }

        // =====================================================================
        //  JEWELER JEWELRY-CRAFTING CHAIN (WO-553) — guards the jeweler-recipes.json
        //  data + the atomic JewelerCraftingService loop:
        //   HARD per recipe: (a) OutputAccessoryId resolves in GearCatalog.FindAccessory;
        //         (b) base.id resolves as an accessory; (c) every gem.id resolves in
        //         MaterialCatalog.
        //   SOFT (log only): a gem id that drops from NO loot table yet — gem boss-drops
        //         are owned by a SEPARATE agent (owner decision 2026-06-28); not a fail.
        //   HARD simulated craft (first iron/wood-only recipe — no GameState dependency):
        //         seed VillageInventory with base + gems + the wallet, call
        //         JewelerCraftingService.Craft, assert success + base/gems consumed +
        //         wallet debited + output granted (+1); then a no-funds craft returns
        //         false and consumes nothing (rollback).
        // =====================================================================
        private static void CheckJewelerChain(List<string> failures, StringBuilder log)
        {
            GearCatalog.Reload();
            MaterialCatalog.Reload();
            LootTableCatalog.Reload();
            DeNelle.Village.Crafting.JewelerRecipeCatalog.Reload();

            var materialIds = new HashSet<string>();
            foreach (var m in MaterialCatalog.All)
                if (m != null && !string.IsNullOrEmpty(m.Id)) materialIds.Add(m.Id);

            var droppable = new HashSet<string>();
            foreach (var t in LootTableCatalog.All)
            {
                if (t == null || t.Drops == null) continue;
                foreach (var d in t.Drops)
                    if (d != null && !string.IsNullOrEmpty(d.MaterialId)) droppable.Add(d.MaterialId);
            }

            var recipes = DeNelle.Village.Crafting.JewelerRecipeCatalog.All;
            var gemIds = new HashSet<string>();
            int chainOk = 0;
            DeNelle.Village.Crafting.JewelerRecipeDef simRecipe = null;

            foreach (var r in recipes)
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) continue;

                bool ok = true;

                // (a) output resolves as a real accessory.
                if (string.IsNullOrEmpty(r.OutputAccessoryId) || GearCatalog.FindAccessory(r.OutputAccessoryId) == null)
                { failures.Add($"jeweler-recipes.json: '{r.Id}' output '{r.OutputAccessoryId}' is not a known accessory"); ok = false; }

                // (b) base resolves as a real accessory.
                if (r.Base == null || string.IsNullOrEmpty(r.Base.Id) || GearCatalog.FindAccessory(r.Base.Id) == null)
                { failures.Add($"jeweler-recipes.json: '{r.Id}' base '{r.Base?.Id}' is not a known accessory"); ok = false; }

                // (c) every gem resolves in MaterialCatalog; SOFT droppability.
                if (r.Gems != null)
                {
                    foreach (var g in r.Gems)
                    {
                        if (g == null || string.IsNullOrEmpty(g.Id)) continue;
                        gemIds.Add(g.Id);
                        if (!materialIds.Contains(g.Id))
                        { failures.Add($"jeweler-recipes.json: '{r.Id}' needs unknown gem '{g.Id}' (no MaterialDef)"); ok = false; }
                        else if (!droppable.Contains(g.Id))
                            log.AppendLine($"[jeweler] SOFT: gem '{g.Id}' drops from NO loot table yet (boss-drop lane pending — separate agent)");
                    }
                }

                if (ok)
                {
                    chainOk++;
                    // Earmark the first iron/wood-only recipe (no GameState-backed crystals/food)
                    // for the simulated craft so the wallet path needs only EconomyService.
                    if (simRecipe == null && (r.Cost == null || (r.Cost.Crystals == 0 && r.Cost.Food == 0)))
                        simRecipe = r;
                }
            }

            log.AppendLine($"[jeweler] chain checked: {recipes.Count} recipe(s), {gemIds.Count} gem(s); " +
                           $"{chainOk} fully craftable base+gems->output");

            // ── HARD simulated craft (atomic consume->grant + no-funds rollback) ──
            if (simRecipe == null)
            {
                log.AppendLine("[jeweler] SOFT: no iron/wood-only recipe to simulate without GameState — sim skipped");
                return;
            }

            var ecoGo = new GameObject("JewelerRegressionEconomy");
            var invGo = new GameObject("JewelerRegressionInventory");
            EconomyService eco = null;
            DeNelle.Village.Crafting.VillageInventory inv = null;
            try
            {
                eco = ecoGo.AddComponent<EconomyService>();
                inv = invGo.AddComponent<DeNelle.Village.Crafting.VillageInventory>();
                // Awake does not fire on AddComponent in edit mode — assign the singletons directly.
                SetStaticInstance(typeof(EconomyService), eco);
                SetStaticInstance(typeof(DeNelle.Village.Crafting.VillageInventory), inv);

                // Seed exactly the base + gems the recipe needs (iron/wood cost covered by the
                // in-session EconomyService pool defaults — wood 200 / iron 80).
                inv.Clear();
                if (simRecipe.Base != null) inv.Add(simRecipe.Base.Id, simRecipe.Base.Count);
                if (simRecipe.Gems != null)
                    foreach (var g in simRecipe.Gems)
                        if (g != null && !string.IsNullOrEmpty(g.Id)) inv.Add(g.Id, g.Count);

                int outBefore = inv.Get(simRecipe.OutputAccessoryId);
                int ironBefore = eco.Iron;

                var res = DeNelle.Village.Crafting.JewelerCraftingService.Craft(simRecipe.Id);
                if (!res.Success)
                    failures.Add($"[jeweler] sim '{simRecipe.Id}': Craft returned FAILURE ('{res.FailReason}') with inputs seeded");
                else
                {
                    if (simRecipe.Base != null && inv.Get(simRecipe.Base.Id) != 0)
                        failures.Add($"[jeweler] sim '{simRecipe.Id}': base '{simRecipe.Base.Id}' NOT consumed");
                    if (simRecipe.Gems != null)
                        foreach (var g in simRecipe.Gems)
                            if (g != null && inv.Get(g.Id) != 0)
                                failures.Add($"[jeweler] sim '{simRecipe.Id}': gem '{g.Id}' NOT consumed");
                    if (inv.Get(simRecipe.OutputAccessoryId) != outBefore + 1)
                        failures.Add($"[jeweler] sim '{simRecipe.Id}': output '{simRecipe.OutputAccessoryId}' not granted (+1)");
                    int ironCost = simRecipe.Cost?.Iron ?? 0;
                    if (ironCost > 0 && eco.Iron != ironBefore - ironCost)
                        failures.Add($"[jeweler] sim '{simRecipe.Id}': wallet iron not debited ({ironBefore}->{eco.Iron}, expected -{ironCost})");
                }

                // No-funds rollback: empty inventory -> Craft must fail + consume/grant nothing.
                inv.Clear();
                int outAfterClear = inv.Get(simRecipe.OutputAccessoryId);
                var res2 = DeNelle.Village.Crafting.JewelerCraftingService.Craft(simRecipe.Id);
                if (res2.Success)
                    failures.Add($"[jeweler] sim '{simRecipe.Id}': Craft SUCCEEDED with empty inventory (should fail)");
                if (inv.Get(simRecipe.OutputAccessoryId) != outAfterClear)
                    failures.Add($"[jeweler] sim '{simRecipe.Id}': failed craft still granted output (no rollback)");

                log.AppendLine($"[jeweler] sim '{simRecipe.Id}' -> consume base+gems, grant '{simRecipe.OutputAccessoryId}', " +
                               "debit wallet; no-funds craft rejected (rollback) OK");
            }
            catch (System.Exception ex)
            {
                failures.Add($"[jeweler] sim threw: {ex.Message}");
                log.AppendLine($"  jeweler sim EXCEPTION: {ex}");
            }
            finally
            {
                // Leave no durable state: clear inventory, reset singletons, destroy the GOs.
                if (inv != null) inv.Clear();
                SetStaticInstance(typeof(EconomyService), null);
                SetStaticInstance(typeof(DeNelle.Village.Crafting.VillageInventory), null);
                if (ecoGo != null) Object.DestroyImmediate(ecoGo);
                if (invGo != null) Object.DestroyImmediate(invGo);
            }
        }

        /// <summary>Assigns a MonoBehaviour-singleton's <c>public static Instance { get; private set; }</c>
        /// backing field by reflection — Awake (which normally sets it) does not fire on AddComponent in
        /// edit-mode batchmode. Null clears it. Best-effort (no-op if the field isn't found).</summary>
        private static void SetStaticInstance(System.Type type, object value)
        {
            if (type == null) return;
            var f = type.GetField("<Instance>k__BackingField",
                                  BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null) f.SetValue(null, value);
        }

        // =====================================================================
        //  TALENT NODE-GRAPH LAYOUT (Path B) — guards the authored graph data so a
        //  bad position/edge can't ship a broken tree:
        //   HARD: a node sets BOTH x and y or NEITHER; positions stay within 0..1;
        //         every prerequisite + edge id resolves to a real node (no dangling).
        //   SOFT (log): how many nodes carry an authored position.
        // =====================================================================
        private static void CheckTalentLayout(List<string> failures, StringBuilder log)
        {
            var checkNodes = new List<DeNelle.Village.Talents.HeroTalentNodeDef>();
            var allIds = new HashSet<string>();

            foreach (var slug in new[] { "knight", "ranger", "mage" })
            {
                var tree = DeNelle.Village.Talents.HeroTalentCatalog.GetTree(slug);
                if (tree?.Nodes == null) continue;
                foreach (var n in tree.Nodes)
                    if (n != null && !string.IsNullOrEmpty(n.Id)) { checkNodes.Add(n); allIds.Add(n.Id); }
            }
            var shared = DeNelle.Village.Talents.HeroTalentCatalog.SharedNodes;
            if (shared != null)
                foreach (var n in shared)
                    if (n != null && !string.IsNullOrEmpty(n.Id)) { checkNodes.Add(n); allIds.Add(n.Id); }

            int positioned = 0;
            foreach (var n in checkNodes)
            {
                bool xs = n.X >= 0f, ys = n.Y >= 0f;
                if (xs != ys)
                    failures.Add($"hero-talents.json: '{n.Id}' has only one of x/y set (x={n.X}, y={n.Y}) — set both or neither");
                if (n.HasPosition)
                {
                    positioned++;
                    if (n.X > 1f || n.Y > 1f)
                        failures.Add($"hero-talents.json: '{n.Id}' position out of 0..1 (x={n.X}, y={n.Y})");
                }
                if (n.Prerequisites != null)
                    foreach (var pr in n.Prerequisites)
                        if (!string.IsNullOrEmpty(pr) && !allIds.Contains(pr))
                            failures.Add($"hero-talents.json: '{n.Id}' prerequisite '{pr}' is not a known node");
                if (n.Edges != null)
                    foreach (var e in n.Edges)
                        if (!string.IsNullOrEmpty(e) && !allIds.Contains(e))
                            failures.Add($"hero-talents.json: '{n.Id}' edge '{e}' is not a known node");
            }
            log.AppendLine($"[talents] layout checked: {checkNodes.Count} node(s), {positioned} positioned; all prereq/edge ids resolve");
        }

        // =====================================================================
        //  ARMED-HERO INVARIANT — BestWeapon(job,1) non-null + prefab resolves
        // -----------------------------------------------------------------------
        //  For each playable class: the level-1 auto-equip MUST return a WeaponDef
        //  (never null → the hero would spawn unarmed), AND that def's prefab MUST
        //  resolve to something attachable:
        //    • Addressable def (loadVia=="addressable" / "gear/" prefabPath) → the key
        //      must be present in the Gear group (Addressables.LoadResourceLocations).
        //    • otherwise → the EquipmentController Resources map must yield a mesh
        //      (Resources.Load of "Heroes/Props/Weapons/<mesh>"). Resolve never returns
        //      null for a non-empty id, so this always yields a path; we assert the prop
        //      actually exists in Resources so the hero shows the real mesh, not just the
        //      tinted-primitive last-resort.
        //  HARD-fails REGRESSION_FAIL on a null pick or an unresolvable Addressable key.
        // =====================================================================
        private static void CheckArmedHeroInvariant(List<string> failures, StringBuilder log)
        {
            GearCatalog.Reload();
            string[] classes = { "knight", "mage", "ranger", "cleric" };
            log.AppendLine("[armed-hero] BestWeapon(job,1) resolves an attachable prefab per class:");

            foreach (var job in classes)
            {
                WeaponDef w = GearCatalog.BestWeapon(job, 1);
                if (w == null)
                {
                    failures.Add($"armed-hero: BestWeapon('{job}', 1) returned NULL — hero would spawn UNARMED");
                    log.AppendLine($"  AH [{job}] -> <null> | UNARMED");
                    continue;
                }

                if (EquipmentController.IsAddressableWeapon(w))
                {
                    // Blink Addressable weapon: the prefabPath must be a present key in the catalog.
                    bool keyPresent = AddressableKeyExists(w.prefabPath);
                    if (!keyPresent)
                    {
                        failures.Add($"armed-hero: BestWeapon('{job}', 1) = '{w.id}' is Addressable " +
                                     $"'{w.prefabPath}' but that key is NOT present in the Addressables " +
                                     "catalog (Gear group) — Blink prefab would fail to load");
                        log.AppendLine($"  AH [{job}] -> '{w.id}' | Addressable '{w.prefabPath}' | KEY MISSING");
                    }
                    else
                    {
                        log.AppendLine($"  AH [{job}] -> '{w.id}' | Addressable '{w.prefabPath}' | key OK");
                    }
                }
                else
                {
                    // Legacy/Tripo weapon: resolve the Resources mesh path the controller would load.
                    string path = EquipmentController.ResolveWeaponMeshResourcePath(w.id);
                    var prefab = string.IsNullOrEmpty(path) ? null : Resources.Load<GameObject>(path);
                    if (prefab == null)
                    {
                        failures.Add($"armed-hero: BestWeapon('{job}', 1) = '{w.id}' maps to Resources " +
                                     $"prop '{path ?? "<null>"}' which loads NULL — hero would show only the " +
                                     "tinted-primitive fallback (real weapon mesh missing from Resources)");
                        log.AppendLine($"  AH [{job}] -> '{w.id}' | Resources '{path}' | PROP MISSING (primitive fallback)");
                    }
                    else
                    {
                        log.AppendLine($"  AH [{job}] -> '{w.id}' | Resources '{path}' | prop OK");
                    }
                }
            }
        }

        // =====================================================================
        //  HAND-SLOT EQUIP RULES — main-hand / off-hand mutual exclusion
        // -----------------------------------------------------------------------
        //  Drives the REAL GearLoadout equip methods on a throwaway hero GO and asserts:
        //   1. equip 1H + shield -> BOTH slots filled (the allowed combo).
        //   2. equip 2H (over a 1H+shield) -> off-hand CLEARED (2H takes both hands).
        //   3. equip shield while a 2H is held -> 2H REMOVED, main falls back to a 1H
        //      (never left unarmed when a 1H exists — armed-hero invariant).
        //  Discovers test ids from the catalog (knight = the class with both a 1H and a 2H,
        //  shield = job 'any') so it stays valid as the catalog grows.
        // =====================================================================
        private static void CheckHandSlotRules(List<string> failures, StringBuilder log)
        {
            GearCatalog.Reload();
            log.AppendLine("[hand-slot] main-hand / off-hand mutual-exclusion rules:");

            const string Job = "knight";   // has BOTH a 1H main and a 2H in the catalog
            int level = 99;                // unlock everything for the test

            WeaponDef oneH   = GearCatalog.BestOneHandedWeapon(Job, level);
            WeaponDef twoH   = FindTwoHanded(Job, level);
            WeaponDef shield = FindShield(level);

            if (oneH == null)   { failures.Add("[hand-slot] no 1H weapon found for 'knight' — cannot test the rules"); return; }
            if (twoH == null)   { failures.Add("[hand-slot] no 2H weapon found for 'knight' — cannot test the rules"); return; }
            if (shield == null) { failures.Add("[hand-slot] no shield/off-hand item found in the catalog — cannot test the rules"); return; }

            log.AppendLine($"  test ids: 1H='{oneH.id}' 2H='{twoH.id}' shield='{shield.id}'");

            // Clear any persisted choices for this class so the test starts from a clean slate
            // and doesn't write durable state for the real game.
            string key = Job.ToLowerInvariant();
            PlayerPrefs.DeleteKey("dotr-equip-weapon-" + key);
            PlayerPrefs.DeleteKey("dotr-equip-offhand-" + key);
            PlayerPrefs.DeleteKey("dotr-equip-armor-" + key);

            var go = new GameObject("HandSlotRegressionHero");
            GearLoadout loadout = null;
            try
            {
                loadout = go.AddComponent<GearLoadout>();
                loadout.BindOwnerClass(Job);   // sets the class + runs an initial Refresh

                // --- 1. 1H + shield coexist ---
                loadout.EquipWeaponById(oneH.id);
                loadout.EquipOffHandById(shield.id);
                if (loadout.EquippedWeapon == null || loadout.EquippedWeapon.id != oneH.id)
                    failures.Add($"[hand-slot] 1H+shield: main-hand expected '{oneH.id}' but was '{loadout.EquippedWeapon?.id ?? "<null>"}'");
                if (loadout.EquippedOffHand == null || loadout.EquippedOffHand.id != shield.id)
                    failures.Add($"[hand-slot] 1H+shield: off-hand expected '{shield.id}' but was '{loadout.EquippedOffHand?.id ?? "<null>"}'");
                log.AppendLine($"  R1 1H+shield -> main='{loadout.EquippedWeapon?.id ?? "<null>"}' off='{loadout.EquippedOffHand?.id ?? "<null>"}'");

                // --- 2. equip 2H over 1H+shield -> off-hand cleared ---
                loadout.EquipWeaponById(twoH.id);
                if (loadout.EquippedWeapon == null || loadout.EquippedWeapon.id != twoH.id)
                    failures.Add($"[hand-slot] equip 2H: main-hand expected '{twoH.id}' but was '{loadout.EquippedWeapon?.id ?? "<null>"}'");
                if (loadout.EquippedOffHand != null)
                    failures.Add($"[hand-slot] equip 2H: off-hand should be CLEARED but was '{loadout.EquippedOffHand.id}' (2H takes both hands)");
                log.AppendLine($"  R2 equip 2H -> main='{loadout.EquippedWeapon?.id ?? "<null>"}' off='{loadout.EquippedOffHand?.id ?? "<null>"}'");

                // --- 3. equip shield while 2H held -> 2H removed, main falls back to a 1H ---
                loadout.EquipOffHandById(shield.id);
                if (loadout.EquippedOffHand == null || loadout.EquippedOffHand.id != shield.id)
                    failures.Add($"[hand-slot] shield-over-2H: off-hand expected '{shield.id}' but was '{loadout.EquippedOffHand?.id ?? "<null>"}'");
                if (loadout.EquippedWeapon != null && loadout.EquippedWeapon.IsTwoHanded)
                    failures.Add($"[hand-slot] shield-over-2H: 2H '{loadout.EquippedWeapon.id}' should have been REMOVED but is still in the main hand");
                // Armed-hero invariant: a 1H exists for this class, so the main hand must NOT be empty.
                if (loadout.EquippedWeapon == null)
                    failures.Add("[hand-slot] shield-over-2H: main hand left UNARMED though a 1H fallback exists (armed-hero invariant broken)");
                else if (loadout.EquippedWeapon.IsOffHandItem)
                    failures.Add($"[hand-slot] shield-over-2H: main hand holds an off-hand item '{loadout.EquippedWeapon.id}' (a shield can never be the main hand)");
                log.AppendLine($"  R3 shield-over-2H -> main='{loadout.EquippedWeapon?.id ?? "<null>"}' off='{loadout.EquippedOffHand?.id ?? "<null>"}'");
            }
            catch (System.Exception ex)
            {
                failures.Add($"[hand-slot] rule check threw: {ex.Message}");
                log.AppendLine($"  hand-slot check EXCEPTION: {ex}");
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                // Leave no durable test state behind.
                PlayerPrefs.DeleteKey("dotr-equip-weapon-" + key);
                PlayerPrefs.DeleteKey("dotr-equip-offhand-" + key);
                PlayerPrefs.DeleteKey("dotr-equip-armor-" + key);
            }
        }

        private static WeaponDef FindTwoHanded(string job, int level)
        {
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || !w.IsTwoHanded) continue;
                if (GearCatalog.WeaponFitsClass(w, job) && (w.req == null || level >= w.req.level)) return w;
            }
            return null;
        }

        private static WeaponDef FindShield(int level)
        {
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || !w.IsOffHandItem) continue;
                if (w.req == null || level >= w.req.level) return w;
            }
            return null;
        }

        // True when <paramref name="key"/> resolves to at least one Addressable resource
        // location (i.e. the address is registered in the content catalog — the Gear group
        // entries marked by BlinkAddressableMarker). Synchronous via WaitForCompletion; the
        // handle is released after the check so the locations probe never leaks.
        private static bool AddressableKeyExists(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            try
            {
                AsyncOperationHandle<IList<IResourceLocation>> h =
                    Addressables.LoadResourceLocationsAsync(key);
                IList<IResourceLocation> locs = h.WaitForCompletion();
                bool exists = locs != null && locs.Count > 0;
                if (h.IsValid()) Addressables.Release(h);
                return exists;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DataRegression] Addressable key probe threw for '{key}': {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        //  BATTLE CLOSING — WO-505: victory/defeat clip resolve + star-rating math
        // -----------------------------------------------------------------------
        //  (a) AUDIO: the win/loss climax must not be silent. The clips ship at
        //      Assets/Audio/Resources/{victory,defeat}.mp3 and AudioBootstrap loads
        //      them by short name via Resources.Load<AudioClip>("victory"/"defeat").
        //      We do the EXACT same load and FAIL if either returns null — that is the
        //      silent-track bug class (the known Resources.Load("dungeon") == null).
        //  (b) STARS: BattleStarRating.StarsForDuration must map sample durations to the
        //      right tier (60s->3, 100s->2, 200s->1) and MultiplierForStars must match
        //      (3->1.50x, 2->1.25x, 1->1.00x). Pure math; deterministic.
        //  Emits FlowTrace.Fail per violation so it lands in the break-log marker, in
        //  addition to the REGRESSION_FAIL line.
        // =====================================================================
        private static void CheckBattleClosing(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[battle-closing] victory/defeat clip resolve + star-rating tiers:");

            // (a) AUDIO — resolve through the same Resources path AudioBootstrap uses.
            string[] cueNames = { "victory", "defeat" };
            foreach (var name in cueNames)
            {
                var clip = Resources.Load<AudioClip>(name);
                if (clip == null)
                {
                    string msg = $"battle-closing: Resources.Load<AudioClip>(\"{name}\") is NULL — " +
                                 "the win/loss climax would play SILENT (clip missing from " +
                                 "Assets/Audio/Resources/ or not imported as an AudioClip)";
                    failures.Add(msg);
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                    log.AppendLine($"  AUDIO '{name}' -> NULL (SILENT CLIMAX)");
                }
                else
                {
                    log.AppendLine($"  AUDIO '{name}' -> clip OK ('{clip.name}', {clip.length:0.0}s)");
                }
            }

            // (b) STARS — sample durations -> expected tier, and the matching multiplier.
            // (duration, expectedStars, expectedMultiplier)
            var samples = new (float dur, int stars, float mult)[]
            {
                (60f,  3, 1.50f),   // fast clean win
                (90f,  3, 1.50f),   // exactly the 3-star boundary (inclusive)
                (100f, 2, 1.25f),   // mid
                (120f, 2, 1.25f),   // exactly the 2-star boundary (inclusive)
                (200f, 1, 1.00f),   // slow win
            };
            foreach (var s in samples)
            {
                int gotStars = BattleStarRating.StarsForDuration(s.dur);
                float gotMult = BattleStarRating.MultiplierForStars(gotStars);
                bool starOk = gotStars == s.stars;
                bool multOk = Mathf.Approximately(gotMult, s.mult);
                if (!starOk)
                {
                    string msg = $"battle-closing: StarsForDuration({s.dur:0}s) = {gotStars}, expected {s.stars}";
                    failures.Add(msg);
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                }
                if (!multOk)
                {
                    string msg = $"battle-closing: MultiplierForStars({gotStars}) = {gotMult:0.00}, expected {s.mult:0.00}";
                    failures.Add(msg);
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                }
                log.AppendLine($"  STARS dur={s.dur:0}s -> {gotStars} star(s) x{gotMult:0.00} " +
                               $"(expected {s.stars} x{s.mult:0.00}) {((starOk && multOk) ? "OK" : "FAIL")}");
            }
        }

        // =====================================================================
        //  WEAPON SWING-TRAIL VFX - WO-504 slice 3 (WeaponVfxMap pure resolver)
        // -----------------------------------------------------------------------
        //  Gates the rarity -> trail color/width MAPPING (not the aesthetic - the
        //  exact colors are owner-felt-tune bones). Asserts:
        //   1. each band resolves a DISTINCT color (common != legendary, etc.);
        //   2. legendary (and elarion) == the GoldColor const, common/null == SteelColor;
        //   3. a null weapon -> the steel common default (null-safe);
        //   4. trail WIDTH escalates MONOTONICALLY common < uncommon < rare < epic < legendary.
        //  Emits FlowTrace.Fail per violation so it lands in the break-log marker.
        // =====================================================================
        private static void CheckWeaponVfx(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[weapon-vfx] rarity -> swing-trail color/width mapping (WO-504 s3):");

            // (1) distinct color per band - build the per-band colors via the resolver.
            string[] bands = { "common", "uncommon", "rare", "epic", "legendary", "elarion" };
            var colors = new Dictionary<string, Color>();
            var widths = new Dictionary<string, float>();
            foreach (var b in bands)
            {
                var w = new WeaponDef { id = "vfx_" + b, name = b, rarity = b };
                var profile = WeaponVfxMap.Resolve(w);
                colors[b] = profile.TrailColor;
                widths[b] = profile.TrailWidth;
                log.AppendLine($"  VFX {b} -> color=({profile.TrailColor.r:0.00},{profile.TrailColor.g:0.00}," +
                               $"{profile.TrailColor.b:0.00},{profile.TrailColor.a:0.00}) width={profile.TrailWidth:0.000}");
            }

            // The five DISTINCT visual tiers (legendary & elarion intentionally SHARE the gold apex).
            string[] distinct = { "common", "uncommon", "rare", "epic", "legendary" };
            for (int i = 0; i < distinct.Length; i++)
                for (int j = i + 1; j < distinct.Length; j++)
                {
                    if (ApproxColor(colors[distinct[i]], colors[distinct[j]]))
                    {
                        string msg = $"weapon-vfx: bands '{distinct[i]}' and '{distinct[j]}' resolve the SAME trail color " +
                                     "(each rarity tier must read distinct)";
                        failures.Add(msg);
                        DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                    }
                }

            // common vs legendary must differ (the headline read).
            if (ApproxColor(colors["common"], colors["legendary"]))
            {
                string msg = "weapon-vfx: common and legendary resolve the same trail color (a legendary blade must read legendary)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }

            // (2) apex/default consts pinned by name.
            if (!ApproxColor(colors["legendary"], WeaponVfxMap.GoldColor))
            {
                string msg = "weapon-vfx: legendary color != WeaponVfxMap.GoldColor (the gold apex const)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }
            if (!ApproxColor(colors["elarion"], WeaponVfxMap.GoldColor))
            {
                string msg = "weapon-vfx: elarion mark color != WeaponVfxMap.GoldColor (top band shares the gold apex)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }

            // (3) null weapon -> steel common default (null-safe).
            var nullProfile = WeaponVfxMap.Resolve(null);
            if (!ApproxColor(nullProfile.TrailColor, WeaponVfxMap.SteelColor))
            {
                string msg = "weapon-vfx: Resolve(null) color != WeaponVfxMap.SteelColor (null weapon must fall back to the steel default)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }
            if (!Mathf.Approximately(nullProfile.TrailWidth, WeaponVfxMap.CommonWidth))
            {
                string msg = "weapon-vfx: Resolve(null) width != WeaponVfxMap.CommonWidth (null weapon must fall back to the common width)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }

            // (4) width escalates MONOTONICALLY common < uncommon < rare < epic < legendary.
            for (int i = 1; i < distinct.Length; i++)
            {
                float prev = widths[distinct[i - 1]];
                float cur  = widths[distinct[i]];
                if (!(cur > prev))
                {
                    string msg = $"weapon-vfx: trail width does not escalate '{distinct[i - 1]}'({prev:0.000}) -> " +
                                 $"'{distinct[i]}'({cur:0.000}) (must be monotonically increasing)";
                    failures.Add(msg);
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                }
            }
        }

        // =====================================================================
        //  ACCESSORIES — accessories.json via GearCatalog.Accessories (WO-543)
        // =====================================================================
        private static void CheckAccessories(List<string> failures, StringBuilder log)
        {
            var accessories = new List<AccessoryDef>(GearCatalog.Accessories);
            log.AppendLine($"accessories.json -> {accessories.Count} AccessoryDef object(s)");

            if (accessories.Count != 10)
                failures.Add($"accessories.json deserialized to {accessories.Count} objects, expected 10 (mapping break or roster drift)");

            int badField = 0, noIcon = 0, overDmg = 0, overDef = 0;
            foreach (var ac in accessories)
            {
                bool ok = ac != null && !string.IsNullOrEmpty(ac.id) && !string.IsNullOrEmpty(ac.name);
                if (!ok) { badField++; continue; }

                bool legendary = !string.IsNullOrEmpty(ac.rarity) &&
                                 ac.rarity.Trim().ToLowerInvariant() == "legendary";

                // Non-legendary balance caps (legendary is the apex and may exceed them).
                if (!legendary && ac.damageMult >= 0.20f) { overDmg++;
                    failures.Add($"accessories.json: '{ac.id}' damageMult {ac.damageMult:0.00} >= 0.20 cap (non-legendary)"); }
                if (!legendary && ac.defense >= 0.15f) { overDef++;
                    failures.Add($"accessories.json: '{ac.id}' defense {ac.defense:0.00} >= 0.15 cap (non-legendary)"); }

                if (string.IsNullOrEmpty(ac.iconPath)) { noIcon++;
                    failures.Add($"accessories.json: '{ac.id}' has no iconPath (would render with no shop sprite)"); }

                log.AppendLine($"  AC {ac.id} | name='{ac.name}' | slot={ac.slot} rarity={ac.rarity} " +
                               $"| dmg={ac.damageMult:0.00} def={ac.defense:0.00} hp={ac.hpBonus} " +
                               $"| icon='{ac.iconPath}' | cost={CostStr(GearCatalog.GetBuyCost(ac))}");
            }
            if (badField > 0) failures.Add($"{badField} accessory(ies) have null/empty id or name");
            log.AppendLine($"[accessories] caps: {overDmg} over-dmg, {overDef} over-def, {noIcon} missing-icon");
        }

        // =====================================================================
        //  ARMOR/ACCESSORY RIM-LIGHT VFX — WO-543 ArmorVfxMap pure resolver
        // =====================================================================
        private static void CheckArmorVfx(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[armor-vfx] rarity -> rim-light color/intensity mapping (WO-543):");

            string[] bands = { "common", "uncommon", "rare", "epic", "legendary", "elarion" };
            var colors = new Dictionary<string, Color>();
            var intensities = new Dictionary<string, float>();
            foreach (var b in bands)
            {
                var profile = ArmorVfxMap.Resolve(b);
                colors[b] = profile.RimColor;
                intensities[b] = profile.RimIntensity;
                log.AppendLine($"  AVFX {b} -> color=({profile.RimColor.r:0.00},{profile.RimColor.g:0.00}," +
                               $"{profile.RimColor.b:0.00}) intensity={profile.RimIntensity:0.000} burst={profile.LegendaryBurst}");
            }

            // Distinct color per visual tier (legendary & elarion share the gold apex).
            string[] distinct = { "common", "uncommon", "rare", "epic", "legendary" };
            for (int i = 0; i < distinct.Length; i++)
                for (int j = i + 1; j < distinct.Length; j++)
                    if (ApproxColor(colors[distinct[i]], colors[distinct[j]]))
                    {
                        string msg = $"armor-vfx: bands '{distinct[i]}' and '{distinct[j]}' resolve the SAME rim color";
                        failures.Add(msg);
                        DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                    }

            // legendary / elarion == gold apex.
            if (!ApproxColor(colors["legendary"], ArmorVfxMap.GoldColor))
            {
                string msg = "armor-vfx: legendary color != ArmorVfxMap.GoldColor (the gold apex const)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }
            if (!ApproxColor(colors["elarion"], ArmorVfxMap.GoldColor))
            {
                string msg = "armor-vfx: elarion color != ArmorVfxMap.GoldColor (top band shares the gold apex)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }

            // common == OFF (no glow) + null-safe default off.
            if (intensities["common"] != 0f)
            {
                string msg = $"armor-vfx: common intensity {intensities["common"]:0.00} != 0 (common must be OFF — no glow)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }
            if (ArmorVfxMap.Resolve((string)null).RimIntensity != 0f)
            {
                string msg = "armor-vfx: Resolve(null) intensity != 0 (no gear must be OFF)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }

            // intensity escalates MONOTONICALLY common < uncommon < rare < epic < legendary.
            for (int i = 1; i < distinct.Length; i++)
            {
                float prev = intensities[distinct[i - 1]];
                float cur  = intensities[distinct[i]];
                if (!(cur > prev))
                {
                    string msg = $"armor-vfx: rim intensity does not escalate '{distinct[i - 1]}'({prev:0.00}) -> " +
                                 $"'{distinct[i]}'({cur:0.00}) (must be monotonically increasing)";
                    failures.Add(msg);
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                }
            }

            // legendary drives the apex burst; lower bands do not.
            if (!ArmorVfxMap.Resolve("legendary").LegendaryBurst)
                failures.Add("armor-vfx: legendary band must set LegendaryBurst (the Burst_rings apex)");
            if (ArmorVfxMap.Resolve("rare").LegendaryBurst)
                failures.Add("armor-vfx: rare band must NOT set LegendaryBurst");

            // Dominant-rarity selection: an epic ring on common armor -> epic profile.
            var dom = ArmorVfxMap.Resolve(
                new ArmorDef { id = "a", rarity = "common" },
                new AccessoryDef { id = "r", rarity = "epic", slot = "ring" },
                null);
            if (!ApproxColor(dom.RimColor, ArmorVfxMap.EpicColor))
                failures.Add("armor-vfx: dominant-rarity pick wrong (epic ring + common armor should resolve EPIC)");
        }

        private static bool ApproxColor(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g)
                && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
        }

        private static string CostStr(DeNelle.Village.ResourceCost c)
        {
            var parts = new List<string>();
            if (c.Wood > 0) parts.Add(c.Wood + "W");
            if (c.Iron > 0) parts.Add(c.Iron + "I");
            if (c.Food > 0) parts.Add(c.Food + "F");
            if (c.Crystals > 0) parts.Add(c.Crystals + "C");
            return parts.Count == 0 ? "Free" : string.Join("+", parts);
        }
    }
}
