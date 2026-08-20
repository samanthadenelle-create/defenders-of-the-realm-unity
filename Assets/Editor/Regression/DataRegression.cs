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
//   REGRESSION_OK <n>/<n> suites          (all checks passed)
//   REGRESSION_FAIL: <n> failure(s) ...   (>=1 check failed)
//
// THE MARKER IS THE VERDICT (project law: judge by marker, never exit code) — so it
// must say WHICH suite produced it. Until 2026-08-02 THREE classes emitted a bare
// `REGRESSION_OK` (this file, SessionRegression, and the 22-case legacy
// Assets/Editor/RegressionSuite.cs) and the check-in gate ran the LEGACY one while
// every RESULT file read its marker as this one's. Distinct markers now:
//   DataRegression.RunAll    -> REGRESSION_OK <n>/<n> suites   (THE gate)
//   RegressionSuite.RunAll   -> CHECKIN_SUITE_OK <p>/<n> cases (legacy smoke battery)
//   SessionRegression.RunAll -> SESSION_GUARDS_OK
// RegressionMarkerRegression [regression-marker] keeps that invariant true.
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
            // then for EVERY enemy resolve its model the way the factory does and assert the
            // runtime seam DeNelle.Core.EnemyAssetLoader.LoadEnemyPrefab("<model>") returns a
            // real prefab. The seam (Addressables-FIRST, Resources-FALLBACK) is asked instead
            // of Resources.Load directly so this assertion stays true across the
            // Assets/EnemyContent -> Addressables migration; a raw Resources.Load would
            // null out for the whole roster the day the art moves (a false red), and asking
            // the loader proves MORE: that the real load path resolves.
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

            // --- SINGLETON + BAKED-TWIN INTEGRITY (StructureSingleton v2) ----------
            // Owner only-ever-one ruling: a catalog row with repo.singleton=true (plus
            // repo.bakedTwins when a legacy baked twin exists) must be FULLY enforced
            // with ZERO code. Gates: (a) bakedTwins shape (non-empty, unique across the
            // catalog, only on singleton rows); (b) singleton+bakedTwins field parity
            // between the StreamingAssets source and the Resources copy; (c) every
            // migration-census (bakedName,itemId) pair on a singleton row is listed in
            // that row's bakedTwins (+ the barracks pin); (d) CatalogRegistry.All()
            // exists and BarracksNpcInjector carries no bespoke standdown seam.
            CheckSingletons(failures, log);

            // --- BLANK-TOWN BAKED-TWIN GATE (WO-834) -------------------------------
            // Owner F8 seq 592: a "Build Your Own" founding loaded FULL of baked
            // default-town structures. Gates: (a) the pure surfacing rule's truth
            // table (blank+migrated suppresses; pre-migration and ever-built surface);
            // (b) the v35->v36 migrator seed (blank save seeds EMPTY; established save
            // gets BaseLayout + FreeBuildsUsed + the template grant); (c) source-lint
            // that every surfacing path carries the gate.
            CheckBlankTownGate(failures, log);

            // --- NPC MODEL BINDING (WO-818: repo.npcModel -> KayKit body) ----------
            // Owner mapping table 2026-08-01: 12 structure rows author repo.npcModel
            // (a KayKit slug) that the NPC injectors resolve as Resources/NPCs/KayKit/
            // <slug>. Gates: (a) npcModel field parity between the two catalog copies;
            // (b) every authored slug resolves to a STAGED FBX (a typo would warn +
            // People-fallback every load); (c) the 12 owner-approved rows carry the
            // owner's slugs VERBATIM (creative pick is owner-only).
            CheckNpcModels(failures, log);

            // --- ASSET-MOVE MANIFEST (Addressables migration, 2026-08-17) ----------
            // The generated manifest is the single source of truth for what moved out of Resources
            // and where it went. Registered HERE, not left as a menu item, because the whole point
            // is that a stale manifest FAILS A GATE rather than silently blanking a building — and
            // a suite that only runs when someone remembers is exactly the stale-second-source-of-
            // truth problem it exists to prevent. No-ops when no migration has run.
            DeNelle.Editor.Regression.AssetMoveManifestRegression.Verify(failures, log);

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

            // --- WO-996: armor dual-copy — Resources (curated) ⊆ StreamingAssets (library) ---
            CheckArmorDualCopy(failures, log);

            // --- WO-975: Gear.asset GUIDs resolve on disk (not dangling / gitignored hollow) ---
            CheckGearAddressableGroup(failures, log);

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

            // =====================================================================
            //  >>> REGISTERED ORACLE SUITES — START FENCE <<<
            // ---------------------------------------------------------------------
            // Everything between this fence and the END fence is ONE registered
            // oracle suite per line: `Class.Run(out reason)` -> failures.Add(reason)
            // on red, `log.AppendLine("[tag] " + reason)` on green.
            //
            // The two counters below make the verdict marker SELF-DESCRIBING
            // (REGRESSION_OK <n>/<n> suites). Without a count in the marker, a small
            // suite's log reads identically to this one's — which is exactly how the
            // check-in gate ran the 22-case legacy battery for months while every
            // RESULT file claimed the full set had passed. See RegressionMarkerRegression.
            //
            // *** ADD NEW SUITE REGISTRATIONS ABOVE THE END FENCE, NOT BELOW IT. ***
            // A line added below the end fence still RUNS but is not COUNTED.
            // =====================================================================
            int suiteTagLinesBefore = CountOracleTagLines(log);
            int suiteSkipLinesBefore = CollectSkippedSuiteTags(log).Count;
            int suiteFailuresBefore = failures.Count;

            // --- monetization covenant gate (LB-5) + tower upgrade perks (overnight silos C/E) ---
            if (!MonetizationCovenantRegression.Run(out var covReason)) failures.Add(covReason); else log.AppendLine("[covenant] " + covReason);
            if (!TowerPerkRegression.Run(out var towerPerkReason)) failures.Add(towerPerkReason); else log.AppendLine("[tower-perks] " + towerPerkReason);
            // --- F8 open-ticket oracles (data-decidable roots, seconds-fast) ------
            if (!TowerRespawnRegression.Run(out var towerRespawnReason)) failures.Add(towerRespawnReason); else log.AppendLine("[tower-respawn] " + towerRespawnReason);
            if (!DeNelle.Editor.Regression.HubSceneLiteralRegression.Run(out var hubLiteralReason)) failures.Add(hubLiteralReason); else log.AppendLine("[hub-scene-literal] " + hubLiteralReason);
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
            if (!OfflineClaimFanOutRegression.Run(out var offlineFanOutReason)) failures.Add(offlineFanOutReason); else log.AppendLine("[offline-fanout] " + offlineFanOutReason);
            // --- Dev queue time-skip (owner 2026-08-04): the skip is exact/additive/resettable,
            //     isolated from the WO-120 ServerOffsetMs lane, forward-only, release-stripped —
            //     and, the load-bearing one, COMBAT STILL READS NO TimeSource (so it can never
            //     warp the battle timer, which is the owner's whole constraint). ---
            if (!DevTimeSkipRegression.Run(out var devSkipReason)) failures.Add(devSkipReason); else log.AppendLine("[dev-time-skip] " + devSkipReason);
            if (!VillageEconomyRegression.Run(out var villEconReason)) failures.Add(villEconReason); else log.AppendLine("[village-econ] " + villEconReason);
            if (!ArenaCatalogRegression.Run(out var arenaCatReason)) failures.Add(arenaCatReason); else log.AppendLine("[arena-cat] " + arenaCatReason);
            if (!CompanionRosterRegression.Run(out var compRosterReason)) failures.Add(compRosterReason); else log.AppendLine("[companion-roster] " + compRosterReason);
            // --- WO-736: Barracks 7-type troop roster + tier-unlock ladder (program 732-737 close) ---
            if (!TroopRosterRegression.Run(out var troopRosterReason)) failures.Add(troopRosterReason); else log.AppendLine("[troop-roster] " + troopRosterReason);
            // --- WO-771.6/771.11: raid V1 win/stars/loot + live HUD (LOCKED teleport/deploy loop) ---
            if (!RaidScoringRegression.Run(out var raidScoringReason)) failures.Add(raidScoringReason); else log.AppendLine("[raid-scoring] " + raidScoringReason);
            // --- WO-912 sec.10.5: the ad provider stays BEHIND IAdService (registered BEFORE any SDK) ---
            if (!AdServiceSeamRegression.Run(out var adSeamReason)) failures.Add(adSeamReason); else log.AppendLine("[ad-seam] " + adSeamReason);
            if (!AndroidContentTargetRegression.Run(out var androidTargetReason)) failures.Add(androidTargetReason); else log.AppendLine("[android-content-target] " + androidTargetReason);
            if (!BattleQuiescenceRegression.Run(out var quiescenceReason)) failures.Add(quiescenceReason); else log.AppendLine("[battle-quiescence] " + quiescenceReason);
            if (!StructureSeatRegression.Run(out var seatReason)) failures.Add(seatReason); else log.AppendLine("[structure-seat] " + seatReason);
            if (!StructureCadenceRegression.Run(out var cadenceReason)) failures.Add(cadenceReason); else log.AppendLine("[structure-cadence] " + cadenceReason);
            if (!StructureLoadBoundedRegression.Run(out var loadBoundedReason)) failures.Add(loadBoundedReason); else log.AppendLine("[structure-load-bounded] " + loadBoundedReason);
            if (!SheathePoseRegression.Run(out var sheatheReason)) failures.Add(sheatheReason); else log.AppendLine("[sheathe-pose] " + sheatheReason);
            if (!OfflinePullRegression.Run(out var offlinePullReason)) failures.Add(offlinePullReason); else log.AppendLine("[offline-pull] " + offlinePullReason);
            if (!EnemyLoadBoundedRegression.Run(out var enemyBoundedReason)) failures.Add(enemyBoundedReason); else log.AppendLine("[enemy-load-bounded] " + enemyBoundedReason);
            if (!ContentPackingRegression.Run(out var packingReason)) failures.Add(packingReason); else log.AppendLine("[content-packing] " + packingReason);
            // --- WO-912 sec.9.3 + D4/D7: no ad reward may ever grant a real-money currency ---
            if (!AdPlacementCovenantRegression.Run(out var adCovReason)) failures.Add(adCovReason); else log.AppendLine("[ad-covenant] " + adCovReason);
            // --- WO-976: the `hasSurface` false green stays dead — each of the four visibility
            //     failure classes must still be able to FIRE, and the named skip is not a pass ---
            if (!UiSurfaceProbeRegression.Run(out var uiSurfaceReason)) failures.Add(uiSurfaceReason); else log.AppendLine("[ui-surface-probe] " + uiSurfaceReason);
            // --- WO-935/991/910/994: CombatCast + caravan mobility + Hunter mark + shield port ---
            if (!CombatCastCaravanMarkRegression.Run(out var castCaravanReason)) failures.Add(castCaravanReason); else log.AppendLine("[combat-cast-caravan-mark] " + castCaravanReason);
            if (!TownsfolkDialogueRegression.Run(out var townsfolkReason)) failures.Add(townsfolkReason); else log.AppendLine("[townsfolk] " + townsfolkReason);
            if (!AtbEngineRegression.Run(out var atbReason)) failures.Add(atbReason); else log.AppendLine("[atb-engine] " + atbReason);
            if (!EconomyMetaCatalogRegression.Run(out var econMetaReason)) failures.Add(econMetaReason); else log.AppendLine("[econ-meta] " + econMetaReason);
            if (!GlimmerEconomyRegression.Run(out var glimmerReason)) failures.Add(glimmerReason); else log.AppendLine("[glimmer] " + glimmerReason);
            if (!SceneRoutingRegression.Run(out var sceneRouteReason)) failures.Add(sceneRouteReason); else log.AppendLine("[scene-route] " + sceneRouteReason);
            // --- WO-1109: the raid hero is the CARRIED town hero, not the emergency fallback.
            //     Every raid entry used to land an "EMERGENCY pill spawned" FlowTrace.Fail in the
            //     break-log (SceneRouter.GoRaid carried nothing), which trained every seat to
            //     ignore Hero Fails. Pins the carry, the DDOL re-home (leak guard), and — just as
            //     hard — that the Fail alarm and its fallback both SURVIVED the fix. ---
            if (!RaidHeroCarryRegression.Run(out var raidHeroCarryReason)) failures.Add(raidHeroCarryReason); else log.AppendLine("[raid-hero-carry] " + raidHeroCarryReason);
            if (!ComposedDungeonRunRegression.Run(out var composedRunReason)) failures.Add(composedRunReason); else log.AppendLine("[composed-dungeon-run] " + composedRunReason);
            if (!ArtResourceRegression.Run(out var artResReason)) failures.Add(artResReason); else log.AppendLine("[art-resource] " + artResReason);
            // --- WO-682: Sfx WebGL import invariant (no divergent WebGL overrides -> no FSB decode failures) ---
            if (!SfxWebglAudioRegression.Run(out var sfxWebglReason)) failures.Add(sfxWebglReason); else log.AppendLine("[sfx-webgl] " + sfxWebglReason);
            // --- 2026-07-12 SME suites (owner: "a SME per architect path, full suite each") ---
            if (!CoreSaveRegression.Run(out var coreSaveSmeReason)) failures.Add(coreSaveSmeReason); else log.AppendLine("[core-save-sme] " + coreSaveSmeReason);
            if (!BuildEconomyRegression.Run(out var buildEconReason)) failures.Add(buildEconReason); else log.AppendLine("[build-econ] " + buildEconReason);
            if (!ObsidianQueueRegression.Run(out var obsidianQueueReason)) failures.Add(obsidianQueueReason); else log.AppendLine("[obsidian-queue] " + obsidianQueueReason);
            // --- WO-897: army composition musters the whole build-out onto the EXISTING Train queue
            //     (no second queue) and never silently drops what does not fit the five-per-line cap ---
            if (!ArmyMusterRegression.Run(out var armyMusterReason)) failures.Add(armyMusterReason); else log.AppendLine("[army-muster] " + armyMusterReason);
            // --- 2026-08-07: two fixes that shipped WITHOUT a pin, both "must never come back":
            //     the rewarded-ad stub that GRANTED THE REWARD with no SDK (a free timer skip on
            //     every channel), and the arena home-return that lived on a UI object three paths
            //     destroy without firing - which stranded the owner 7km out on BOTH platforms ---
            if (!AdGateAndArenaReturnRegression.Run(out var adArenaReason)) failures.Add(adArenaReason); else log.AppendLine("[ad-gate-arena] " + adArenaReason);
            // --- WO-781: wounded-troop recovery advance (TickRecovery live+offline callers) ---
            if (!ArmyRecoveryRegression.Run(out var troopRecoveryReason)) failures.Add(troopRecoveryReason); else log.AppendLine("[troop-recovery] " + troopRecoveryReason);
            if (!DataWebRegression.Run(out var dataWebReason)) failures.Add(dataWebReason); else log.AppendLine("[data-web] " + dataWebReason);
            if (!HudUiRegression.Run(out var hudUiSmeReason)) failures.Add(hudUiSmeReason); else log.AppendLine("[hud-ui-sme] " + hudUiSmeReason);
            if (!CombatAtbRegression.Run(out var combatAtbReason)) failures.Add(combatAtbReason); else log.AppendLine("[combat-atb] " + combatAtbReason);
            if (!DialogueRegression.Run(out var dialogueReason)) failures.Add(dialogueReason); else log.AppendLine("[dialogue] " + dialogueReason);
            if (!EnemyRigColorRegression.Run(out var enemyRigColorReason)) failures.Add(enemyRigColorReason); else log.AppendLine("[enemy-rig-color] " + enemyRigColorReason);
            // --- WO-772 Phase 1: EnemyResolver id->family->DISTINCT model (generic-skeleton fix, ENEMY_RESOLVER_OK) ---
            if (!EnemyResolverRegression.Run(out var enemyResolverReason)) failures.Add(enemyResolverReason); else log.AppendLine("[enemy-resolver] " + enemyResolverReason);
            // --- Resources -> Addressables migration guard: the enemy ADDRESSES must be in the
            //     catalog once Assets/EnemyContent is gone. Migration-state aware (a progress
            //     note pre-move, a hard assertion post-move); a DANGLING entry fails in either
            //     state. Without it a quietly-unmarked group ships a roster of tinted capsules ---
            if (!EnemyAddressableCatalogRegression.Run(out var enemyAddrCatalogReason)) failures.Add(enemyAddrCatalogReason); else log.AppendLine("[enemy-addr-catalog] " + enemyAddrCatalogReason);
            // --- 2026-07-26: retired walk-up outpost (ff.raidwalk) + ambient region roam (ff.regionroam OFF) ---
            if (!OverworldCombatGateRegression.Run(out var owCombatReason)) failures.Add(owCombatReason); else log.AppendLine("[overworld-combat-gate] " + owCombatReason);
            // --- destroyed-structure owner ruling (repair no-op + exclusion predicates; play-mode remove is note-only) ---
            if (!DestroyedStructureRegression.Run(out var destroyedStructReason)) failures.Add(destroyedStructReason); else log.AppendLine("[destroyed-structure] " + destroyedStructReason);
            if (!OrcRigBindingAudit.Run(out var orcBindingReason)) failures.Add(orcBindingReason); else log.AppendLine("[orc-binding] " + orcBindingReason);
            if (!HeroLocomotionClipRegression.Run(out var heroLocoClipReason)) failures.Add(heroLocoClipReason); else log.AppendLine("[hero-loco-clips] " + heroLocoClipReason);
            // --- UI-Obsidian conformance (style-everything-obsidian LAW): flags NEW hand-rolled uGUI vs baseline debt ---
            if (!UiObsidianConformanceRegression.Run(out var uiObsidianReason)) failures.Add(uiObsidianReason); else log.AppendLine("[ui-obsidian] " + uiObsidianReason);
            if (!UiMvvmConformanceRegression.Run(out var uiMvvmReason)) failures.Add(uiMvvmReason); else log.AppendLine("[ui-mvvm] " + uiMvvmReason);
            // --- UI-capture FIDELITY guard (2026-08-05): the headless capture harness was
            // geometry-BLIND — RenderCanvasToPng only rewrote canvas.scaleFactor and never
            // Screen.*, while the kit computes zone geometry AT BUILD TIME from Screen.*, so
            // every PNG shared ONE layout and the resolution in the filename was a LABEL, not
            // a layout. Two panels shipped broken behind a green UI_CAPTURE_OK. This is the
            // source-text ratchet that stops the fix being silently reverted; the live
            // geometry assertions run inside the harness itself. ---
            // Fully qualified: this suite lives in DeNelle.Editor.Regression, not DeNelle.Editor
            // (same as RuntimeSpawnVisualRegression below).
            if (!DeNelle.Editor.Regression.UiCaptureFidelityRegression.Run(out var uiCapFidelityReason)) failures.Add(uiCapFidelityReason); else log.AppendLine("[ui-capture-fidelity] " + uiCapFidelityReason);
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
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "queue-full-surface suite", () => { if (!UpgradeQueueFullSurfaceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[queue-full-surface] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "upgrade-family suite", () => { if (!UpgradeFamilyPrecedenceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[upgrade-family] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dualfamily-level-reset suite", () => { if (!DualFamilyLevelResetRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dualfamily-level-reset] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "crystal-production suite", () => { if (!CrystalProductionRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[crystal-production] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "sfx-resolve suite", () => { if (!SfxResolveRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[sfx-resolve] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-exit suite", () => { if (!DungeonExitRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-exit] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-dressing suite", () => { if (!DungeonDressingRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-dressing] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-return suite", () => { if (!DungeonReturnSceneRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-return] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-lore suite", () => { if (!DungeonLoreReadableRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-lore] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-state-reset suite", () => { if (!DungeonStateResetRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-state-reset] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-defeat suite", () => { if (!DungeonDefeatEndsRunRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-defeat] " + r); });
            // NOTE: tag was a DUPLICATE of the DungeonExitRegression line above ("[dungeon-exit]"),
            // so two different suites reported under one tag and one of them was invisible in the
            // log. Renamed to [dungeon-exit-reachable]. (The two suites ALSO share the
            // DUNGEON_EXIT_OK marker literal inside their own bodies — that is tracked as known
            // debt in RegressionMarkerRegression's allowlist; fixing it means editing those files.)
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-exit-reachable suite", () => { if (!DungeonExitReachableRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-exit-reachable] " + r); });
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
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "wave-authoring suite", () => { if (!WaveAuthoringLiveRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wave-authoring] " + r); });
            // --- WO-808 Option A: gear power-level ladder data integrity ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "gear-levels suite", () => { if (!GearLevelsRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[gear-levels] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "pack-cosmetic-integrity suite", () => { if (!PackCosmeticIntegrityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[pack-cosmetic-integrity] " + r); });
            // --- WO-1037 single-resource impulse packs (legalised by the WO-947 §12 amendment): exactly ONE economy key per SKU, $5 ceiling, resources-only, smallest-sufficient resolver, no grant route ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "impulse-pack suite", () => { if (!DeNelle.Editor.Regression.ImpulsePackRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[impulse-pack] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tower-wall-los suite", () => { if (!TowerWallLosRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tower-wall-los] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "vfx-aura-diff suite", () => { if (!VfxAuraDifferentiationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[vfx-aura-diff] " + r); });
            // --- owner VfxManualPicks per-tier tower projectiles: archer tier ladder + arcane base/upgraded wired + every key catalogued ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tower-proj-map suite", () => { if (!TowerProjectileMapRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tower-proj-map] " + r); });
            // --- WO-869 dungeon portal rebuild: robust shader resolve + MagentaGuard widening (protected primitive art + deferred re-sweep) + real additive state + arch structure ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "portal-rebuild suite", () => { if (!PortalRebuildRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[portal-rebuild] " + r); });
            // --- WO-826 Realm Map: realm-map.json dual-copy field parity + RealmMapCatalog loader oracle ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "realm-map suite", () => { if (!RealmMapRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[realm-map] " + r); });
            // --- WO-839 raid deploy screen: FrameCore footer/subHeader zones + F8 harness dev-guard + ScoutReport honesty ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "raid-deploy-ui suite", () => { if (!RaidDeployUiRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[raid-deploy-ui] " + r); });
            // --- WO-766 wallet provider: Android-only SOLANA_SDK define + real-provider selection + transfer confinement ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "wallet-provider suite", () => { if (!WalletProviderSelectionRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wallet-provider] " + r); });
            // --- wallet session (2026-08-17): the MWA grant survives a relaunch (she force-quit and was asked to connect again), is SEALED not plaintext, is BOUND to its wallet, is cleared on disconnect, and is never logged ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "wallet-session suite", () => { if (!DeNelle.Editor.Regression.WalletSessionPersistenceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wallet-session] " + r); });
            // --- login gate (2026-08-18): her wallet auto-resumed at boot and the SIGN IN wall was presented anyway 5s later. The gate read Firebase ONLY on a wallet-first build; it must continue for connected OR attested-bound OR signed-in, and still present on a genuine first run ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "login-gate suite", () => { if (!DeNelle.Editor.Regression.LoginGateRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[login-gate] " + r); });
            // --- promo redeem door: the Realm Store's ungated Redeem-a-Code entry routes through PromoCodeService, never logs the code, gives every failure its own canon sentence in both copies, and grants on the uncapped pack seam ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "promo-redeem-entry suite", () => { if (!PromoRedeemEntryRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[promo-redeem-entry] " + r); });
            // --- WO-835 action bar: Core applicability model invariants + View purity ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "hud-actionbar suite", () => { if (!HudActionBarRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hud-actionbar] " + r); });
            // --- WO-1008 raids discoverability: a built Barracks ALWAYS shows the Raids face. She played a save with a Barracks and an empty army, the face was absent entirely, and she reported "I do not see a way to start a raid" - a feature that hides itself is indistinguishable from a broken one. Zero troops is now a greyed face with a WORDED reason (she is red/green colourblind, so hue carries nothing), and the full-army gate underneath is untouched. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "raids-discoverability suite", () => { if (!RaidsDiscoverabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[raids-discoverability] " + r); });
            // --- WO-830 echo resource picker: picker/token/affinity contract (sibling to the echo-spec suite) ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "echo-picker suite", () => { if (!EchoResourcePickerRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[echo-picker] " + r); });
            // --- WO-797 dungeon room ownership: encounter schema + wake/confine math + exit beacon (F8 seq 622) ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-room-ownership suite", () => { if (!DeNelle.Editor.Regression.DungeonRoomOwnershipRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-room-ownership] " + r); });
            // --- WO-854 quest completability: EVERY story stage must have a reachable completion. This suite existed for four days and was registered NOWHERE - its own header carried this exact line as un-applied text, QUEST_REACH_OK appears in no log on disk, and four commits ratcheted MinCompletableStages up to 63 while nothing checked it. A ratchet defended by nothing is worse than no ratchet: it reads as proof. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "quest-reach suite", () => { if (!DeNelle.Editor.Regression.QuestCompletabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[quest-reach] " + r); });
            // --- WO-1001 slices 1b-8 composed pillars: the baker places EVERY pillar through FindType reflection (Editor cannot reference DeNelle.Dungeons), so a rename WARNs and places nothing while the bake still says saved=True; plus the bake-time-Configure-must-survive-SaveScene pin, authored-vs-placed parity in the baked scenes, the key bag, darkness actually feeding the roll, and a lock whose key is never granted (an unwinnable run) ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-composed-pillars suite", () => { if (!DeNelle.Editor.Regression.DungeonComposedPillarsRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-composed-pillars] " + r); });
            // --- WO-1001 slice 1 multi-level dungeons: StairDown/StairUp sockets oppose and carry half a floor each, a vertical mate drops exactly one floor, a stacked pair is not an overlap (that abort is what made descents impossible), doors keep the planar-only nudge, and the GENERATED stair prefabs on disk actually carry the poses ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-multilevel suite", () => { if (!DeNelle.Editor.Regression.DungeonMultiLevelRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-multilevel] " + r); });
            // --- WO-957 egress trim (owner F8 seq 2508: "Should be single entry point in maybe 2 total out"): a CONTENT dungeon authors AT MOST ONE extract - the BACK exit, seated in the room DungeonTreasureCache resolves as deepest - plus the one injected front exit. Nothing asserted the count before, which is how 13 per-stairwell pads accreted and gave dg_ember_deep SIX ways out. Also pins the WO-930 control-group exemption and the Resources/StreamingAssets dual-copy hash. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-egress suite", () => { if (!DeNelle.Editor.Regression.DungeonEgressRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-egress] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "biome-roads suite", () => { if (!DeNelle.Editor.Regression.BiomeRoadsRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[biome-roads] " + r); });
            // --- WO-1101 ground textures: every terrain layer carries CURATED (tracked) BaseColor + Normal art, measured Rec.709 luminance matches the WO-1044 per-march value targets, ADJACENT marches on the compass cycle separate by ΔL >= 0.15 (the colourblind gate as arithmetic - today's tints are ΔL 0.074 and fail it), Ashwood's ground stays PALE ("ink on ash", not the shipped inverted L=0.176), and the layer-index contract has exactly ONE authority (TerrainLayerSet) that both the bake and the DEF-108 runtime repaint read. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "terrain-layer suite", () => { if (!DeNelle.Editor.Regression.TerrainLayerRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[terrain-layer] " + r); });
            // --- WO-850 dungeon treasure cache: fixed-bundle validity against materials.json, deepest-room BFS (undirected + ordinal tie-break), per-dungeon first-clear one-shot, panel single-exit ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-treasure suite", () => { if (!DeNelle.Editor.Regression.DungeonTreasureRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-treasure] " + r); });
            // --- WO-852 Echo card layout: chip rows at/above MinTouchPx, fixed-pixel bands (no 1f/n fraction slicing), scroll well, per-frame rebuild guard ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "echo-card-layout suite", () => { if (!DeNelle.Editor.Regression.EchoCardLayoutRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[echo-card-layout] " + r); });
            // --- WO-866 rumor board layout: every filter tab fits the list well at the touch floor (the clipped "Gear"), the tab band is X-bounded by the list column so the detail pane cannot cross it, and the detail stack + a two-line body fits the pane (the -11px culled body) ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "rumor-board-layout suite", () => { if (!DeNelle.Editor.Regression.RumorBoardLayoutRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[rumor-board-layout] " + r); });
            // --- WO-878 build menu layout: every band that carries a button is at the kit touch floor (so ClampMinTouch cannot grow it into a neighbour - the root verbs, "< Back" and the Upgrade CTA all overlapped), the ladder fits the derived body at every capture aspect, and the cost/preview/CTA strings are the VM's ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "buildmenu-layout suite", () => { if (!DeNelle.Editor.Regression.BuildMenuLayoutRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[buildmenu-layout] " + r); });
            // --- WO-880 tower manager: the VM reads the SAME stat source the game builds from (the catalog repo block StructureFactory copies onto DefenseTower), every catalog tower row resolves to non-zero rng/dmg, a stat-less row says "(building)"/"(no stats)" instead of a fabricated "rng 0, dmg 0", and the list well is an exact whole number of row pitches (the half-clipped third row) ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tower-manager suite", () => { if (!DeNelle.Editor.Regression.TowerManagerRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tower-manager] " + r); });
            // --- WO-882 help menu: the VM emits only entries that are AVAILABLE and RENDERABLE, so the View can never build a blank button. The capture's "blank" row was two defects -- a Dev Tools label force-painted ElarionUi.Ink (near-black) onto what has resolved to a dark grey plate since 2026-07-16, and a third row clipped to a 36px sliver because the well's fraction-anchored height was not a whole multiple of the row pitch. Wired here by the committer: the authoring lane was fenced out of this file while five siblings held it. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "help-menu-entry suite", () => { if (!DeNelle.Editor.Regression.HelpMenuEntryRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[help-menu-entry] " + r); });
            // --- WO-860 starter loadout + shelf: new game clears dotr-equip-*, Knight starts sword+shield (not the stale axe / not auto-best Flameblade), shelf capped + equippable-only + no blink_* ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "starter-loadout suite", () => { if (!DeNelle.Editor.Regression.StarterLoadoutRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[starter-loadout] " + r); });
            // --- shields: every shield carries a real defense value, the ladder climbs with req.level, and GearLoadout actually SUMS the off-hand (all three were missing - shields were pure decoration) ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "shield-defense suite", () => { if (!DeNelle.Editor.Regression.ShieldDefenseRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[shield-defense] " + r); });
            // --- tower empowerment reachability: Tower.TryEmpower (tower-perks tier 4 + TowerCombat's GlacialCore/TrueAim/ManaSurge/EternalEmber) is gated by ONE affordance. This suite resolves the path outward from the gate - callers, then their referrers, then scene/prefab placements - and declares whether any of it is anchored in shipping code. It PINS today's orphan state, so wiring the affordance fails the suite until the expectation flag is flipped (which is what forces the owner's felt-verify of the new power). ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tower-empower-reach suite", () => { if (!DeNelle.Editor.Regression.TowerEmpowermentReachabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tower-empower-reach] " + r); });

            // --- 2026-08-02 oracle wave: suites written by the parallel lanes tonight.
            // Each class was VERIFIED to exist on disk with a public static bool Run(out string)
            // before being registered here (a phantom registration is a compile break).
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "modifier-key-coverage suite", () => { if (!DeNelle.Editor.Regression.ModifierKeyCoverageRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[modifier-key-coverage] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "hub-foliage suite", () => { if (!HubFoliageRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hub-foliage] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "glossary suite", () => { if (!DeNelle.Editor.Regression.GlossaryRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[glossary] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "item-identity suite", () => { if (!DeNelle.Editor.Regression.ItemIdentityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[item-identity] " + r); });

            // Second wave of the 2026-08-02 program (PM spec). Each class + its declared
            // tag were read off disk before registering; tags are the ones the suite
            // headers themselves declare, not the ones the spec guessed.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-pool-reset suite", () => { if (!DeNelle.Editor.Regression.EnemyPoolResetRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[enemy-pool-reset] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tutorial-reach suite", () => { if (!DeNelle.Editor.Regression.TutorialStepReachabilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tutorial-reach] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "runtime-spawn-visual suite", () => { if (!DeNelle.Editor.Regression.RuntimeSpawnVisualRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[runtime-spawn-visual] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "wallet-identity suite", () => { if (!DeNelle.Editor.Regression.WalletIdentityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wallet-identity] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "loot-class-gate suite", () => { if (!DeNelle.Editor.Regression.LootClassGateRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[loot-class-gate] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "shader-predicate-authority suite", () => { if (!DeNelle.Editor.Regression.ShaderPredicateSingleAuthorityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[shader-predicate-authority] " + r); });
            // --- dynamic difficulty: neutral lands EXACTLY on the authored target, both rails reachable, spike expires at read time, no dead authored key ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dynamic-difficulty suite", () => { if (!DeNelle.Editor.Regression.DynamicDifficultyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dynamic-difficulty] " + r); });
            // --- raid arena shape: footprint is a real fraction of the plane (the 2.4% square can never return), the spire is reachable by the HERO's seam, navmesh present ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "raid-arena-shape suite", () => { if (!DeNelle.Editor.Regression.RaidArenaShapeRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[raid-arena-shape] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "reset-full-clear suite", () => { if (!DeNelle.Editor.Regression.ResetToNewGameFullClearRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[reset-full-clear] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "cathedral-cumulative suite", () => { if (!DeNelle.Editor.Regression.CathedralCumulativeRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[cathedral-cumulative] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "hero-equip-hub suite", () => { if (!DeNelle.Editor.Regression.HeroEquipHudHubRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hero-equip-hub] " + r); });
            // The gear lane FOLDED shield-improvement + defense-cap into this one file rather
            // than shipping the three classes the spec named - registered as it actually landed.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "armed-hero suite", () => { if (!DeNelle.Editor.Regression.ArmedHeroInvariantRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[armed-hero] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "buildmenu-economy suite", () => { if (!DeNelle.Editor.Regression.BuildMenuRealEconomyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[buildmenu-economy] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "hero-death-pin suite", () => { if (!DeNelle.Editor.Regression.HeroDeathPinRebaseRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hero-death-pin] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "wall-build-l1 suite", () => { if (!DeNelle.Editor.Regression.WallBuildL1Regression.Run(out var r)) failures.Add(r); else log.AppendLine("[wall-build-l1] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "castle-plans suite", () => { if (!DeNelle.Editor.CastlePlansUnlockRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[castle-plans] " + r); });
            // WO-1105: the SEAT half (where the drop stands), sister to the unlock half above.
            // Distinct marker CASTLE_PLANS_SEAT_OK -- a shared marker is how a 22-case pass once
            // read as the full suite's pass (canon sec 8).
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "castle-plans-seat suite", () => { if (!DeNelle.Editor.CastlePlansSeatRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[castle-plans-seat] " + r); });
            // 2026-08-16: three player-visible defects pinned together - the talent panel must
            // resolve the LIVE hero class (a Ranger was spending Wisdom on the knight tree), the
            // ranger bow de-dupe must not be defeatable by component-add order (two bows), and an
            // unaffordable upgrade tap must not log as an F8 error. Distinct marker
            // LIVE_CLASS_BOW_AFFORD_OK (canon sec 8: a shared marker hides which suite really ran).
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "live-class-bow-afford suite", () => { if (!DeNelle.Editor.Regression.LiveClassBowAndAffordSeverityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[live-class-bow-afford] " + r); });

            // --- WO-853 structures are targetable: Faction derived (never serialized) on every
            // IDamageable, walls stay on layer Structure (towers must not shoot through them),
            // a wall at 100 damage drops its solid colliders, and DefenseTower's two IsAlive
            // answers stay deliberately different (player seam = liveness, enemy seam = +PlayerOwned) ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "structure-targetable suite", () => { if (!DeNelle.Editor.Regression.StructureTargetableRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[structure-targetable] " + r); });

            // --- THE QUEUED TOWER THAT FOUGHT (owner F8, 2026-08-04): a structure with an
            // in-flight build job must not acquire, fire or damage. The WO-612 scaffold silenced
            // exactly ONE component type (DefenseTower), so 'tower_arcane_spire' -> ArcaneTower
            // slipped the gate entirely and defended live waves for its whole timer -- five spires
            // at remaining=270s in the owner's own capture, a hole WO-855 Phase 4 stretched from
            // 15s to up to 2h. Pins: the gate silences EVERY combat family, Reveal restores exactly
            // what it silenced, baked/EnemyOwned towers with no scaffold stay untouched, and the
            // job key survives the save round trip so a pending tower reloads inert ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "under-construction-gate suite", () => { if (!DeNelle.Editor.Regression.UnderConstructionGateRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[under-construction-gate] " + r); });

            // --- PHANTOM COLLECTOR INCOME (2026-08-04): an empty town must earn ZERO. The
            // harvest tick's only guard was `GetLevel(id) < 1`, and GetLevel defaults to 1
            // and never asks whether the building exists, so all three resource buildings
            // paid out from t=0 - straight to the wallet, uncapped, no Collect tap. Pins the
            // existence gate (WO-834 everBuiltStructureIds / a live collector), the deleted
            // direct-grant fallback, and the zero-seed founding bootstrap it must not break ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "collector-income suite", () => { if (!DeNelle.Editor.Regression.CollectorIncomeRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[collector-income] " + r); });

            // --- TOWN BANK CAP (WO-857 / WO-901 Phase F, 2026-08-04): the first UPPER clamp ever
            // put on EconomyService.Grant -- the single path every income source in the game flows
            // through. Get it wrong and resources silently vanish or a fresh save soft-locks, so this
            // suite is the permission gate (ARCHITECTURE_PRINCIPLES §2c): crystals+coins uncapped BY
            // DESIGN, baseCap can never resolve to 0, a fresh 0-wood/0-iron save can still found and
            // buy, a spend is never upper-clamped, every clamped grant EMITS THE WARN (the only thing
            // between the player and vaporised resources), capacity scales with container level,
            // fill/drain is ONE pure capacity-ascending function, and an over-cap save is grandfathered ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "town-bank-cap suite", () => { if (!DeNelle.Editor.Regression.TownBankCapRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[town-bank-cap] " + r); });

            // --- ECON-SWEEP 2026-08-16: the four economy-silo defects from the cross-silo sweep ---
            // (1) no spend/grant may move the UNSAVED _wood/_iron pool during play without a hard,
            // F8-visible FlowTrace.Fail; (2) a bank-cap-clamped grant logs and pops the APPLIED amount,
            // never the request (the Echo silo dump popped pre-clamp numbers for resources the player
            // never got); (3) a cancel notice never says "Nothing to refund." when a currency outside
            // the refundable basket WAS taken (research is gold-priced and JobCost has no coins lane —
            // the MESSAGE was the defect, the refund policy is the owner's call); (4) the Echo "Lv N"
            // readout stays off the card/roster while EchoAssignments.SetLevel has no production
            // caller, with the level DATA axis untouched ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "econ-sweep suite", () => { if (!DeNelle.Editor.Regression.EconomySweepRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[econ-sweep] " + r); });

            // --- THE EMPTY UI REVIEW (owner, 2026-08-04): INDEX.html showed "mostly just the
            // blank templates and nothing else". Not an un-fed review -- a POISONED one. The exe
            // was built 21:18:09 and at 21:21:06 an AutoPilot fleet running in its DEFAULT
            // -nographics mode rewrote 35 panel_*.png review shots at exactly 33150 bytes each
            // (flat black), because CaptureRawShot fired ScreenCapture with no graphics device
            // and its own comment called that acceptable. build-ui-review.ps1 then badged the
            // blanks "PAIR COMPLETE". Pins: neither capture path can write an unmeasured frame,
            // every _mapping.json panelId has a real AutoPilot route or an argued exemption, and
            // each route writes the EXACT deliveredShot filename the review reads ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "ui-capture-coverage suite", () => { if (!DeNelle.Editor.Regression.UiCaptureCoverageRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[ui-capture-coverage] " + r); });

            // --- WO-865 Skills panel layout: the body is three DISJOINT fixed-pixel bands
            // (columns / ability / action), never fractions of a ~493px body well. The 2026-08-04
            // Seeker capture showed a 32px fraction action row that ClampMinTouch grew to the
            // 112px touch floor SYMMETRICALLY -- straight over the graph well and quick-slot 4 --
            // plus a centre-pivoted graph content rect sliced at both mask edges, a section band
            // 15.6px from a node row, and a 23px name band that ellipsized "Emberbrand Throw".
            // Pins: every tappable band >= MinTouchPx and every text band >= a TMP line box; the
            // stack replayed at the reference body leaves a positive graph well + a two-line
            // description; the graph pad covers half a node plate and the fixed px-per-unit
            // lattice clears the tightest gap authored in hero-talents.json; the longest catalog
            // word/name fits at the FontFloor; and the source laws (RectMask2D, top-left pivot,
            // band pins, reserved section row, no 1/n slicing, no green ButtonConfirm overlay) ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "skills-panel-layout suite", () => { if (!DeNelle.Editor.Regression.SkillsPanelLayoutRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[skills-panel-layout] " + r); });

            // --- TALENT FOCUS SINGLETON (WO-1021 sec 2.1d, owner "Still Messy" at WIS 252):
            // SkillNodeState.Next is a PER-TRACK signal, so a view that renders it oversized
            // grows one shouting gold plate per track. Pins: at most ONE plate above NodeSizePx
            // on a multi-track board, a per-track NEXT cue that is normal-size and separable in
            // GREYSCALE, and the sec.2.1b lattice solver holding the minimum pitch ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "talent-focus suite", () => { if (!DeNelle.Editor.Regression.TalentFocusSingletonRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[talent-focus] " + r); });

            // --- TALENT TREE SHAPE (owner ruling 2026-08-16): "start with three and they can
            // branch wider", "common or specialty should still start from a few simple then
            // really refine to the playstyle of the user". The shared pool was reshaped to
            // 3/4/4 while the three CLASS trees still fanned five-to-eight flat across the
            // bottom rank, and ranger/mage carried no authored position at all -- so the
            // runtime auto-placer, not the designer, decided the tree the player looked at.
            // Pins: at most THREE root, cheapest-cost nodes on every bottom row (classes AND
            // the common pool), a strictly wider row above it and no funnel above that, every
            // node positioned inside 0..1, no orphan / cycle / unreachable node, and no
            // visible node stranded behind a hidden prerequisite ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "talent-tree-shape suite", () => { if (!DeNelle.Editor.Regression.TalentTreeShapeRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[talent-tree-shape] " + r); });

            // --- NUMERAL LEGIBILITY (owner defect 2026-08-05, QueueCardRail_2670x1200.png):
            // no typographic role may render its numeral 1 as a bare vertical stroke. The chip
            // font (Alata) drew '1' at 7.23 ink units against its own 'l' 6.84 and '|' 6.14, so
            // "Builders 1/2 | Train 1" read as three identical marks with three meanings. This
            // measures the LIVE glyph metrics of the font each role actually renders with ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "numeral-legibility suite", () => { if (!DeNelle.Editor.Regression.NumeralLegibilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[numeral-legibility] " + r); });

            // --- WO-879 daily-quest empty state: ONE empty-state fact owned by DailyQuestVM (assigned at a single site, IsEmpty projects it), proven on a live null-source VM, and DailyQuestHud reads it once + renders it once in one chrome (no BuildParchmentDetailEmpty second column, no View-authored copy, no View-side emptiness test), on fixed-pixel bands ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "daily-quest-empty suite", () => { if (!DeNelle.Editor.Regression.DailyQuestEmptyStateRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[daily-quest-empty] " + r); });

            // --- VFX LOOP FLAGS (2026-08-05): a catalog row's IsLoop must equal what its
            // prefab's emission actually does. IsLoop was a sticky manual checkbox in
            // VfxCasterWindow (force-set true for the Projectile/Aura roles), so 95 of 135
            // HovlVfxCatalog rows read IsLoop:1 -- including a pile of rate-0 burst prefabs
            // (PP_BigExplosion, PP_MuzzleFlash, PP_EarthShatter ...). A loop row never
            // auto-returns its pool slot (VFXManager.Hovl.cs ~283-288 registers no reclaim
            // deadline; the only loop reclaim frees DESTROYED hosts, which pooled objects
            // never are), so each fire-and-forget play permanently burned one of the 20
            // slots -- six F8 captures caught the cap saturated at 20/20 ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "vfx-loop-flag suite", () => { if (!DeNelle.Editor.Regression.VfxLoopFlagRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[vfx-loop-flag] " + r); });

            // --- VFX SELF-CONTAINMENT (2026-08-05): the shipped Resources/VFX prefabs
            // were committed with a message claiming the tracked copy is what ships.
            // FALSE: AssetDatabase.CopyAsset duplicates the PREFAB ONLY, so all 28
            // prefabs kept pointing their materials/textures/shaders/meshes at
            // Assets/UnityTechnologies (.gitignore:399) and Assets/Spells Pack
            // (.gitignore:214) -- 73 distinct gitignored assets, Boss_FireBreath alone
            // reaching 6. On a fresh clone / the laptop / CI those resolve to nothing
            // and the effects render MAGENTA or untextured, which is exactly the
            // "no magenta leak through, no missing shaders" criterion that work was
            // signed off against. Latent only because this machine has the packs.
            // Fixed by DeNelle.Editor.VfxResourceArtMirror; this oracle is what stops
            // it silently coming back ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "vfx-self-contained suite", () => { if (!DeNelle.Editor.Regression.VfxResourceSelfContainmentRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[vfx-self-contained] " + r); });

            // --- VFX NULL SLOTS (WO-1100, 2026-08-16): a catalogued prefab carrying an
            // ENABLED ParticleSystemRenderer with ALL material slots null draws
            // engine-default MAGENTA, the runtime deliberately refuses to repaint a
            // particle slot (the 08-05 white-blob lesson), and every spawn F8-spams a
            // MagentaProbe M2 FAIL -- 12 owner captures per session for the portal
            // threshold aura, whose slot-level shape NO existing gate asserted (the
            // self-containment gate measures gitignored REACH, and this prefab's reach
            // is owner-baselined on purpose). DISABLED all-null renderers are the
            // vendor container pattern -- noted, normalized at spawn by VFXManager,
            // never failed. Ratcheted over the 5 known ParticlePack offenders. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "vfx-null-slot suite", () => { if (!DeNelle.Editor.Regression.VfxParticleNullSlotRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[vfx-null-slot] " + r); });

            // --- ENEMY RIG <-> CONTROLLER COHERENCE (2026-08-09): a Humanoid model on a
            // Generic-clip controller (or the reverse) T-poses and slides. The runtime
            // ALREADY detects this -- but only for an enemy that actually spawns in a play
            // session, so a rarely-spawned boss ships broken in silence. This asks the same
            // question statically, over every model in Resources/Enemies, and fails the gate.
            // It is what stopped the seven AccuRig intakes from being wired to Boss/LargeEnemy. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "enemy-rig-coherence suite", () => { if (!DeNelle.Editor.Regression.EnemyRigControllerCoherenceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[enemy-rig-coherence] " + r); });

            // --- BUILD CARD ART (WO-1010, 2026-08-09): a catalog row whose art does not
            // resolve renders as a bare LETTER on the card. The capture caught "Lumberyard"
            // as an "L" among illustrated neighbours — a content gap no gate could see, on
            // the exact screen testers called unreadable. Ratcheted: today's artless rows are
            // recorded debt, any NEW one fails. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "build-card-art suite", () => { if (!DeNelle.Editor.Regression.BuildCardArtRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[build-card-art] " + r); });

            // --- DUNGEON ENCOUNTER FAMILY (WO-1001 slice 2): EncounterSpec.kind was
            // compared ONLY to "none" by DungeonBaker, and OutpostEnemyGroupSpawner's
            // id picker was four hardcoded hollow-* literals whose hand-written stats
            // ignored enemies.json outright -- so authoring "orc-group" SILENTLY SPAWNED
            // HOLLOWS. This oracle pins that every family table emits REAL non-boss
            // roster ids, that hollow-group still reproduces the retired picker's stream
            // exactly, that an unknown kind falls back LOUDLY, and that the baker/binder
            // still write the serialized kind the bake depends on ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-encounter-family suite", () => { if (!DeNelle.Editor.Regression.DungeonEncounterFamilyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-encounter-family] " + r); });

            // --- TOWNSFOLK BODIES (owner ruling 2026-08-07): the whole town wandered on
            // TWO People-pack peasants; CastleTownsfolkInjector.BodyPool now names the 14
            // CraftPix bodies. Every failure in that chain is SILENT - a bad Resources path,
            // an unbuilt prefab, a body with no visible mesh, or a URP material with no
            // albedo bound all produce a warning + a grey capsule or a flat grey person, not
            // an error. The albedo half is the one that has actually shipped here before
            // (WO-719's white spire, the white Knight, the 73 gitignored VFX dependencies of
            // 2026-08-05), because a shader-only check passes an untextured URP mesh ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "townsfolk-bodies suite", () => { if (!DeNelle.Editor.Regression.TownsfolkBodyPoolRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[townsfolk-bodies] " + r); });

            // --- COLLECTOR UPGRADE LADDER (WO-936 Finding C, 2026-08-09): a placed
            // collector's tier tree is authored on the row its repo.collectorBuildingId
            // points at, not on itself — the live Lumber Mill upgrades through
            // 'lumbermill', which build-categories lockedIds RETIRES from the palette.
            // It therefore reads as dead content while being the sole home of a live
            // building's progression. Deleting it would not crash: BuildingUpgradeVM
            // falls through to ResourceBuildingProgression's legacy level curve and
            // silently draws a DIFFERENT ladder, with no log line and no symptom. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "collector-ladder suite", () => { if (!DeNelle.Editor.Regression.CollectorLadderRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[collector-ladder] " + r); });

            // --- 2026-08-10 wave-3 lanes. Each lane authored its oracle but left the
            // registration to the committer on purpose (this file is lane-fenced, so
            // nine agents editing it in parallel would collide). Registered here in the
            // same commit as the lane work, which is also what keeps [regression-marker]
            // green - an oracle written and never registered is a FAIL by design. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "barracks-blanktown suite", () => { if (!DeNelle.Editor.BarracksBlankTownRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[barracks-blanktown] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "echo-hollow-route suite", () => { if (!DeNelle.Editor.EchoHollowRouteRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[echo-hollow-route] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "harvest-drip suite", () => { if (!DeNelle.Editor.HarvestDripRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[harvest-drip] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "hostile-green suite", () => { if (!DeNelle.Editor.HostileGreenCueRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hostile-green] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "aggro-leash suite", () => { if (!DeNelle.Editor.AggroLeashRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[aggro-leash] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-cam-958 suite", () => { if (!DeNelle.Editor.DungeonCameraTightRoomRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-cam-958] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "gear-aura-carry suite", () => { if (!DeNelle.Editor.Regression.GearAuraCarryGateRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[gear-aura-carry] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "armor-store-window suite", () => { if (!DeNelle.Editor.Regression.ArmorStoreLockedWindowRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[armor-store-window] " + r); });

            // --- 2026-08-10 evening, minted from two live F8 captures while the owner played.
            // Same fencing reason as the block above: each lane authored its oracle, the
            // committer registers it. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tutorial-anchor-latch suite", () => { if (!DeNelle.Editor.Regression.TutorialAnchorLatchRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tutorial-anchor-latch] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tutorial-watchdog-bound suite", () => { if (!DeNelle.Editor.Regression.TutorialWatchdogBoundRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tutorial-watchdog-bound] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "build-carousel-order suite", () => { if (!DeNelle.Editor.Regression.BuildCarouselTutorialOrderRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[build-carousel-order] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "founding-guide-wolf suite", () => { if (!DeNelle.Editor.Regression.FoundingGuideWolfBodyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[founding-guide-wolf] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "hub-tree-aura suite", () => { if (!DeNelle.Editor.Regression.HubTreeAuraWithholdRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hub-tree-aura] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "hud-class-fallback suite", () => { if (!DeNelle.Editor.Regression.HudHeroClassFallbackRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hud-class-fallback] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "tutorial-guide-identity suite", () => { if (!DeNelle.Editor.Regression.TutorialGuideIdentityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tutorial-guide-identity] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "endstate-handoff suite", () => { if (!DeNelle.Editor.Regression.EndStateTransitionHandoffRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[endstate-handoff] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "town-suspend-floor suite", () => { if (!DeNelle.Editor.Regression.TownSuspendSceneFloorRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[town-suspend-floor] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "equipment-screen-layout suite", () => { if (!DeNelle.Editor.Regression.EquipmentScreenLayoutRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[equipment-screen-layout] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-mover-ownership suite", () => { if (!DeNelle.Editor.Regression.DungeonMoverOwnershipRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-mover-ownership] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "hero-bar-rebind suite", () => { if (!DeNelle.Editor.Regression.HeroBarClassRebindRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hero-bar-rebind] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "mage-spell-kit suite", () => { if (!DeNelle.Editor.Regression.MageSpellKitAuthoringRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[mage-spell-kit] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "guide-lead-move suite", () => { if (!DeNelle.Editor.Regression.GuideLeadMovementRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[guide-lead-move] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "town-movement-floor suite", () => { if (!DeNelle.Editor.Regression.TownMovementFloorRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[town-movement-floor] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "one-guide-body suite", () => { if (!DeNelle.Editor.Regression.OneGuideBodyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[one-guide-body] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "wall-adjacency suite", () => { if (!DeNelle.Editor.Regression.WallAdjacencyRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wall-adjacency] " + r); });
            // --- WO-1105: the ranged primary is DERIVED (strike-shaped effect whose authored range
            //     far exceeds measured melee reach), never a per-class table; ranger.q is a costed
            //     cooldown'd ranged strike; and the runtime weapons catalog carries NO crossbow
            //     while the R4a exclusion stands (the Generate Gear Catalog menu would import 125). ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "ranged-primary suite", () => { if (!DeNelle.Editor.Regression.RangedPrimaryRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[ranged-primary] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "ranged-facing suite", () => { if (!DeNelle.Editor.Regression.RangedFacingLockRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[ranged-facing] " + r); });
            // --- ICON_CATALOG 2026-08-16: no MAGE ability may paint KNIGHT art. Resolves every mage
            //     ability through the real ResolveKey(id, effect) -> Resolve -> DefaultSprite chain and
            //     compares the resulting Sprite REFERENCE to attack_sword / icon_shield / icon_combat. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "mage-ability-icons suite", () => { if (!DeNelle.Editor.Regression.MageAbilityIconRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[mage-ability-icons] " + r); });
            // --- WO-1104: the spire-plans moment subscribes to the PlansCollected seam, plays ONCE
            //     ever, registers with the arbiter, never touches roster/unlock state, and reads its
            //     speaker from EchoRosterCatalog rather than a name literal. ---
            // NOTE: this oracle declares `namespace DeNelle.Editor` (not .Regression) -- cite it as
            // written rather than "correcting" the file to match its neighbours mid-wave.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "spire-celebration suite", () => { if (!DeNelle.Editor.SpirePlansCelebrationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[spire-celebration] " + r); });
            // --- Owner VFX bans (2026-08-16: "Spell_Fire_6 - Do Not use anywhere"): Spell_Fire_6 +
            //     'Magic circle sun loop' stay dead - source lint over all runtime+editor code plus a
            //     baked-catalog GUID scan (both VFXCatalog.asset and HovlVfxCatalog.asset). Colour
            //     Variants are deliberately NOT banned (scope note in the suite header). Replacement
            //     pick = BigExplosion (owner-tagged the same day).
            //     ⚠ THE ONLY REGISTRATION OF THIS SUITE (audit 2026-08-15). It was registered TWICE
            //     here, so every run emitted two [banned-vfx] lines and inflated the suite count; the
            //     duplicate is now pinned by BannedVfxRegression's own single-registration case, which
            //     FAILS if a second call site reappears in this file.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "banned-vfx suite", () => { if (!DeNelle.Editor.Regression.BannedVfxRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[banned-vfx] " + r); });
            // --- WO-997 class resource economy: every playable class authors a valid resource block,
            //     every cost fits its owning class's pool, at least one costed non-ultimate per kit
            //     (kills the "everything is cooldown-gated" gap), both abilities.json copies identical. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "class-resource suite", () => { if (!DeNelle.Editor.Regression.ClassResourceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[class-resource] " + r); });
            // --- WO-973 Bryn bubble legibility: code defaults small + scene copy agrees + ratio vs the
            //     shipped TownsfolkBubble. Case 2 is a DRIFT CATCHER that stays red until the
            //     Dungeon_HealersCottage bake rewrites the serialised copy — an honest red, not noise. ---
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "wanderer-bubble suite", () => { if (!DeNelle.Editor.Regression.WandererBubbleLegibilityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wanderer-bubble] " + r); });

            // --- COST-BASKET SEPARATION (WO-947, owner economy ruling 2026-08-10): regular
            // structures cost wood + iron and NEVER crystals; magical/ethereal structures are
            // crystal-based; no basket ever touches all three. Enforced on the AUTHORED
            // baskets so any NEW catalog row obeys the ruling from day one. Five rows are
            // carried as a dated, cited pending-pin list because their classification is an
            // OPEN OWNER call (WO-947 section 4) -- the list can only shrink; the oracle FAILS
            // if a listed row stops violating without being removed.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "cost-basket suite", () => { if (!DeNelle.Editor.Regression.CostBasketSeparationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[cost-basket] " + r); });

            // --- VFX POOL SHAPE (WO-955, 2026-08-10): a pooled host was DESTROYED while it
            // still sat in a free list, and the next Acquire dereferenced it -- captured twice
            // in one session (HeroHpStateAura in town after arena deaths, then EnemyAuraVFX in
            // dg_ember_deep, a scene the first caller never touched: the pool hangs off the
            // DDOL singleton, so a poisoned list outlives the scene that poisoned it). Asserts
            // both halves of VfxPoolGuard: drain past corpses on the way out, and refuse to
            // enqueue any host that is not parked under the pool root on the way in.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "vfx-pool-shape suite", () => { if (!DeNelle.Editor.Regression.VfxPoolShapeRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[vfx-pool-shape] " + r); });

            // --- TALENT ICON MAP (WO-1023, 2026-08-15): talent-icon-map.json had NO
            // oracle -- 83/83 coverage, unique art per talent, resolvable iconPaths and
            // the byte-identical Resources/StreamingAssets twin were all true by care
            // alone (the WO-996 armor.json shape: Resources wins at runtime, so drift
            // is invisible in the Editor). Also pins the two WO-1023 re-tags (ranger
            // Venomcraft off Rogue7, shared Arcane Bolt off Arcanist1) so the
            // duplicate-icon defect cannot return.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "talent-icons suite", () => { if (!DeNelle.Editor.Regression.TalentIconMapRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[talent-icons] " + r); });

            // --- SHIPPED SURFACE GATE (2026-08-16): three surfaces that SHIPPED in the release
            // APK while believing they were dev-only, off, or wired up. (a) HeroGaitForensics
            // gated on a RAW PlayerPrefs key that DEFAULTED ON and was in no flag table, so a
            // per-frame boxed string.Format + CSV write ran on players' devices with no reachable
            // off-switch -- now a declared flag, default OFF (flagged off, NOT stripped: §12).
            // (b) The Settings screen-shake toggle was INERT IN BOTH DIRECTIONS -- it wrote a key
            // nothing read while the gameplay bridge read a key nothing wrote -- so a visible
            // accessibility control moved no shake at all. (c) JupiterSwapBootstrap auto-spawned a
            // crypto swap CTA gated by NOTHING, in the build store-hardening had stripped every
            // other crypto surface out of. All three defaults are pinned as SOURCE TEXT on purpose:
            // FeatureFlags.Get reads PlayerPrefs first, so a runtime read describes the gate
            // machine, never what ships.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "shipped-surface-gate suite", () => { if (!DeNelle.Editor.Regression.ShippedSurfaceGateRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[shipped-surface-gate] " + r); });

            // (BANNED VFX used to be registered a SECOND time here - removed 2026-08-15. The suite
            //  is registered ONCE, above with the other VFX oracles; two call sites emitted two
            //  [banned-vfx] lines per run and inflated the suite count. Do not re-add.)

            // --- HERO DEATH SEVERITY (audit 2026-08-15): a NORMAL hero death must not raise an F8
            // ERROR. HeroHealth's death-freeze state dump was a FlowTrace.Fail because break-log was
            // errors-only on device, so the most common event in the game woke a live triage seat
            // every time the owner died. Pins the new FlowTrace.Capture channel (INFO severity +
            // kind:"note" straight into break-log.jsonl) and the F8 daemon's skip of note rows --
            // two-sided, so DELETING the dump fails just as loudly as restoring the Fail.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "hero-death-severity suite", () => { if (!DeNelle.Editor.Regression.HeroDeathSeverityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[hero-death-severity] " + r); });

            // --- DEV GRANT UNCAPPED (audit 2026-08-15): the dev resource grants resolved
            // GetMethod("GrantSpendable") BY STRING -- the TownBankCapacity-clamped path -- so a
            // 50,000 wood dev grant into a 2,500 bank silently lost ~95% of itself. Reflection by
            // string is invisible to the compiler and to ordinary source lint, so this oracle reads
            // the literal method-name strings the dev surfaces pass to GetMethod.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dev-grant-uncapped suite", () => { if (!DeNelle.Editor.Regression.DevGrantUncappedRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dev-grant-uncapped] " + r); });

            // --- ECHO ENGAGE DIALOGUE (WO-1030, 2026-08-16): options were clipped by a
            // text+options sum clamp (48px rows under the touch floor, fraction overlay) and
            // Echo portraits fell to silhouette for want of speaker records. Pins the
            // reserve-first sizing tokens, the speaker-record portraits, and the fit math.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "echo-engage-dialogue suite", () => { if (!DeNelle.Editor.Regression.EchoEngageDialogueRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[echo-engage-dialogue] " + r); });

            // --- ATTACHMENT OFFSETS (WO-994, 2026-08-16): the 08-16 harness audit found
            // NOTHING covered AttachmentOffsetRegistry or seated-prop transforms - the
            // owner's dialed shield seat could vanish (row lost, fullOverride flipped,
            // mirror unparseable) with every marker green. Pins the shield_A rows through
            // the real Resources-first read path + the WO-994 seat-drift tripwire wiring.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "attachment-offset suite", () => { if (!DeNelle.Editor.Regression.AttachmentOffsetRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[attachment-offset] " + r); });

            // --- GEAR PROP RENDERS (owner report 2026-08-18, build 2026.08.19.331306:
            // "shield is missing and sword is now wrong"). The device trace carried
            // "parent-scale compensate: ... -> worldBounds=(0, 0, 0)" — a held prop with no
            // volume at all. Every existing gate was green for that build, because they all
            // ask "did the pipeline RUN" and none asks "does what it produced have VOLUME".
            // Also pins the address half: c072e5736 records weapons.json naming
            // "gear/weapon/ShieldWithItemLogic" while no group published it, so the load
            // failed and the equip fell back to the legacy mesh — the swap looked done and
            // changed nothing, silently, in a shippable tree.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "gear-prop-renders suite", () => { if (!DeNelle.Editor.GearPropRendersRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[gear-prop-renders] " + r); });

            // --- ECHO WORLD PRESENCE (WO-1108 Lane B, 2026-08-16): the owner's rule is
            // "it takes you to the gate, gives you your dialogue, then it disappears... The
            // only time it reappears is after your battle." Until this WO there was NO
            // despawn path for a pet anywhere, and TWO independent appearance owners. Drives
            // the real state machine (body present during the escort -> gone on completion ->
            // back exactly once after a battle) and pins the single-owner rule by scanning
            // every runtime file for a second PetDeployer.SummonAt caller.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "echo-world-presence suite", () => { if (!DeNelle.Editor.Regression.EchoWorldPresenceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[echo-world-presence] " + r); });

            // WO-1110: the raid's three non-victory exits must PAY THE SAME (death used to
            // forfeit razing credit that retreat paid), Start must bind the clock-expiry exit
            // BEFORE it builds the HUD (an unguarded presentation throw was the raid's only
            // exitless state), and the four named raid catches must not swallow silently again.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "raid-exit-parity suite", () => { if (!DeNelle.Editor.Regression.RaidExitParityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[raid-exit-parity] " + r); });

            // --- COLLECTOR STACK PROPS (2026-08-16): CollectorStackPropCatalog.cs told
            // everyone to "place the asset at Assets/Resources/Collectors/..." and nobody
            // ever did - the folder did not exist and git history shows the asset was
            // never added on any branch. So EnsureCatalog() resolved null on every run and
            // every farm/lumbermill/forge silently drew the abstract fill bar instead of
            // its diegetic prop pile, for months, with nothing red. A graceful degradation
            // with no gate over it is indistinguishable from working software. Pins BOTH
            // branches: the asset exists with the owner's 2026-08-16 picks committed as
            // GUIDs (a TEXT assertion - the KayKit pack is gitignored, so a loaded-
            // reference check would go red on a pack-less machine for a non-defect), AND
            // TryGet still reports null-prop / unmapped rows as NOT FOUND so that machine
            // keeps the fill bar rather than reaching Instantiate(null).
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "collector-props suite", () => { if (!DeNelle.Editor.Regression.CollectorStackPropCatalogRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[collector-props] " + r); });

            DeNelle.Core.Diagnostics.Guard.Try("Regression", "placed-upgrade-page suite", () => { if (!DeNelle.Editor.PlacedUpgradePageTruthRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[placed-upgrade-page] " + r); });

            // WO-1041/WO-1042 — the dungeon's justification, pinned. Gems must stay unbuyable
            // (the Jeweler was SELLING all three for gold before this ticket, which voided the
            // pillar's whole thesis); the polish job must stay unbuyable-to-completion (a paid
            // instant resolve of a random outcome is a loot box, owner ruling 2026-08-16); every
            // completed run must pay; the grade must move ODDS ONLY; free/ad/paid must share one
            // odds table with DISCLOSED percentages derived from it.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-gem-exclusivity suite", () => { if (!DeNelle.Editor.DungeonGemExclusivityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-gem-exclusivity] " + r); });

            // 2026-08-16 combat silo: every enemy must RESOLVE a non-null type-VFX set (the
            // per-prefab assignment never landed, so every telegraph/sound/hit cue was dead);
            // exactly ONE authority may field a wave's heavy (wave 5 fielded an authored 1050 HP
            // troll AND a generated elite); and a boss spawn id must resolve to a real marker or
            // fail LOUDLY (the hardcoded "spawn-0" could never match, so the boss entered from an
            // arbitrary gate behind a Debug.LogWarning F8 never saw).
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "combat-cue-authority suite", () => { if (!DeNelle.Editor.Regression.CombatCueAuthorityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[combat-cue-authority] " + r); });

            // 2026-08-16: the ranger's Attack/Cast/CastUpper all bound Ranger_Aim_Idle (a static pose) so every shot froze the hero mid-aim; pins the real bow clip in the BUILT controller AND its generator.
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "ranger-bow-fire suite", () => { if (!DeNelle.Editor.Regression.RangerBowFireRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[ranger-bow-fire] " + r); });

            DeNelle.Core.Diagnostics.Guard.Try("Regression", "raid-repeat-clear suite", () => { if (!DeNelle.Editor.Regression.RaidRepeatClearRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[raid-repeat-clear] " + r); });
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "spawn-budget-vfx-warm suite", () => { if (!DeNelle.Editor.Regression.SpawnBudgetAndVfxWarmRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[spawn-budget-vfx-warm] " + r); });

            DeNelle.Core.Diagnostics.Guard.Try("Regression", "forge-shelf-kind suite", () => { if (!DeNelle.Editor.Regression.ForgeShelfClassKindRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[forge-shelf-kind] " + r); });

            // PROD-003 (2026-08-18): the Realm Store storefront is BAKED into the hub, so a gate on
            // the placer cannot see a stale bake — and there was one: an FBX axis re-import left the
            // saved collider describing a mesh shape that no longer existed, and every code-level
            // check stayed green. This pins the ARTIFACT: present exactly once, standing where the
            // producer says, fit to the town's height cadence, seated, with its door, NOT an
            // IDamageableStructure, and absent from both dual copies of structures-catalog.json and
            // build-categories.json (a catalog row is the failure the ticket exists to prevent).
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "realm-storefront suite", () => { if (!DeNelle.Editor.Regression.RealmStorefrontRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[realm-storefront] " + r); });

            // --- THE ORACLE THAT GUARDS THIS FILE: distinct markers, no unregistered
            // oracle, no gate script grepping a marker nobody emits. Registered LAST so
            // it sees the fully-built registry above it (it reads SOURCE, not runtime
            // state, so its own registration line is what satisfies its self-reference).
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "regression-marker suite", () => { if (!DeNelle.Editor.Regression.RegressionMarkerRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[regression-marker] " + r); });

            // =====================================================================
            //  >>> REGISTERED ORACLE SUITES — END FENCE <<<  (new lines go ABOVE)
            // =====================================================================
            // --- SKIPPED IS A THIRD STATE, NOT A PASS (2026-08-16 coverage audit) ---
            // A suite that stands down (GameStateService will not install headless, a
            // data file is absent) reports TRUE so a harness limitation is not read as a
            // product defect -- but the bool is the caller's only channel, so until now
            // that stand-down landed in the GREEN column. In an environment where the
            // state seam does not install, six economy oracles asserted NOTHING and this
            // marker still read full green. Those suites now stamp RegressionOutcome.
            // SkipToken into their reason, and the tally subtracts them here. The
            // denominator (suitesTotal) is unchanged on purpose: the suite was registered
            // and it DID run, so it must stay in the denominator -- what changed is that
            // it no longer counts as evidence in the numerator.
            int suiteTagLines = CountOracleTagLines(log) - suiteTagLinesBefore;
            var skippedTags   = CollectSkippedSuiteTags(log);
            int suitesSkipped = skippedTags.Count - suiteSkipLinesBefore;
            if (suitesSkipped < 0) suitesSkipped = 0;
            int suitesGreen = suiteTagLines - suitesSkipped;
            int suitesRed   = failures.Count - suiteFailuresBefore;
            int suitesTotal = suiteTagLines + suitesRed;

            // --- G1: the denominator must be PINNED, not self-reported -------------
            // suitesTotal is derived from what the run PRODUCED (tag lines + failures).
            // A suite registered inside Guard.Try that THROWS produces neither, so it
            // silently leaves this total and the marker still reads green at a smaller
            // number. Joining the runtime count against the count of registration
            // call-sites in SOURCE makes a vanished suite loud. Both sides are measured;
            // neither is a literal (a hardcoded expectation would be audit finding G8).
            int expectedSuites;
            string expectDetail;
            if (!DeNelle.Editor.Regression.RegressionMarkerRegression.TryGetExpectedSuiteCount(out expectedSuites, out expectDetail))
            {
                failures.Add("[suite-count] could not derive the expected registered-suite count from source (" +
                             expectDetail + "). The denominator is therefore UNPINNED: a suite that throws " +
                             "inside Guard.Try would vanish from the total and the marker would still read green.");
            }
            else if (expectedSuites != suitesTotal)
            {
                failures.Add("[suite-count] SUITE VANISHED FROM THE DENOMINATOR: source registers " +
                             expectedSuites + " oracle suite(s) between the fences, but this run only " +
                             "accounted for " + suitesTotal + " (" + suitesGreen + " green + " + suitesRed +
                             " red). The difference threw inside its Guard.Try, which swallows the exception " +
                             "and returns false, so it emitted no [tag] line and no failure. Search the log " +
                             "for 'FAILED at' to find it. " + expectDetail);
                // Deliberately NOT adjusting suitesRed/suitesTotal here: those are the
                // measurement of what this run produced, and doctoring them to match the
                // expectation would erase the very discrepancy being reported. The marker
                // keeps reporting the honest (smaller) number; this failure explains it.
            }
            else
            {
                log.AppendLine("[suite-count] denominator pinned: source registers " + expectedSuites +
                               " suite(s) and the run accounted for all " + suitesTotal + ".");
            }

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
            // THE marker is SELF-DESCRIBING: it carries the registered-suite count on the
            // SAME line, so a log can never be mistaken for a different (smaller) suite's
            // pass. Consumers grep the shaped form  REGRESSION_OK <n>/<n> suites  — see
            // tools/regression/checkin_gate.ps1 and RegressionMarkerRegression.
            log.AppendLine("=== verdict ===");
            log.AppendLine($"registered oracle suites: {suitesTotal} ({suitesGreen} green, {suitesRed} red, {suitesSkipped} skipped)");
            if (suitesSkipped > 0)
            {
                // Named, not just counted: "7 skipped" is only actionable if the log says WHICH.
                log.AppendLine("SKIPPED SUITES (asserted nothing this run): " +
                               string.Join(", ", skippedTags.GetRange(suiteSkipLinesBefore, suitesSkipped).ToArray()));
            }
            if (failures.Count == 0)
            {
                log.AppendLine($"REGRESSION_OK {suitesGreen}/{suitesTotal} suites -- {suitesGreen} green, {suitesRed} red, {suitesSkipped} skipped");
                Debug.Log(log.ToString());
            }
            else
            {
                log.AppendLine($"REGRESSION_FAIL: {failures.Count} failure(s) ({suitesGreen}/{suitesTotal} registered suites green, {suitesSkipped} skipped):");
                foreach (var f in failures) log.AppendLine("  - " + f);
                // LogError so it also lands in break-log.jsonl and fails loudly in the log scan.
                Debug.LogError(log.ToString());
            }
        }

        // =====================================================================
        //  Registered-suite counter (feeds the self-describing REGRESSION_OK marker)
        // =====================================================================
        // Every registered oracle suite reports green by appending a line that STARTS
        // with its "[tag] " prefix, so counting lines that begin with '[' between the
        // START and END fences yields the exact number of suites that reported green —
        // without touching (and churning) the ~90 registration lines themselves.
        private static int CountOracleTagLines(StringBuilder log)
        {
            if (log == null) return 0;
            string s = log.ToString();
            int n = 0;
            if (s.Length > 0 && s[0] == '[') n++;
            for (int i = 0; i + 1 < s.Length; i++)
                if (s[i] == '\n' && s[i + 1] == '[') n++;
            return n;
        }

        // =====================================================================
        //  Skipped-suite collector (the THIRD state)
        // =====================================================================
        // A stand-down rides in the reason string as RegressionOutcome.SkipToken, so a
        // suite reporting it still appends its "[tag] " line (it did not fail) but is
        // subtracted from the green numerator and NAMED in the verdict. Returns the tags
        // rather than a bare count so the log can say WHICH suites asserted nothing --
        // "7 skipped" with no names is the same unactionable number the old "125/125" was.
        private static List<string> CollectSkippedSuiteTags(StringBuilder log)
        {
            var tags = new List<string>();
            if (log == null) return tags;
            foreach (var line in log.ToString().Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length == 0 || line[0] != '[') continue;
                if (line.IndexOf(DeNelle.Editor.Regression.RegressionOutcome.SkipToken,
                                 System.StringComparison.Ordinal) < 0) continue;
                int close = line.IndexOf(']');
                tags.Add(close > 1 ? line.Substring(1, close - 1) : "unnamed-suite");
            }
            return tags;
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
            // WO-1012 (owner-ruled arc, 2026-08-09/10): the founding flow is the 8-beat
            // pet-Echo-guided arc — ARRIVE, WALK, BUILD ONE, ACK, ONE CANNON, TIMERS,
            // ENEMIES AT THE GATE, WIN+HANDOFF. Supersedes the 2026-07-24 end-after-defend
            // 7-step pin (that ruling's substance — no venture-out back half, no whole-village
            // build — survives inside the 8 beats; the count moved because ACK/WIN became
            // structural beats, not because scope grew).
            if (mandatory.Count != 8)
                failures.Add($"tutorial mandatory chain has {mandatory.Count} steps — the WO-1012 owner-ruled arc is exactly 8 beats (2026-08-10)");

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
                    // WO-1012 P3: the scripted teaching band's repelled signal (ENEMIES beat).
                    s == DeNelle.Core.Tutorial.TutorialSignals.TutorialBandRepelled ||
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
                // then attempt the same load the factory's VisualFactory.Skin call performs —
                // through DeNelle.Core.EnemyAssetLoader (Addressables-first, Resources-fallback),
                // NOT a raw Resources.Load, so the check survives the Addressables migration.
                // A null load means this enemy ships as a tinted-capsule fallback at runtime —
                // a silent regression.
                string model = EnemyFactory.ModelForEnemy(e);
                string path = "Enemies/" + model;
                var prefab = DeNelle.Core.EnemyAssetLoader.LoadEnemyPrefab(model);
                if (prefab == null)
                {
                    failures.Add($"enemies.json: '{e.Id}' resolves to model '{model}' but EnemyAssetLoader could not resolve \"{path}\" via Addressables OR Resources (would spawn as a tinted capsule — wrong/missing prefab)");
                    log.AppendLine($"  EN {e.Id} -> model '{model}' | PREFAB UNRESOLVABLE via EnemyAssetLoader ('{path}': neither Addressables nor Resources)");
                }
                else
                {
                    log.AppendLine($"  EN {e.Id} -> model '{model}' | prefab OK via EnemyAssetLoader ('{path}')");
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
                var prefab = DeNelle.Core.StructureAssetLoader.LoadStructurePrefab(entry.visualPrefabPath);
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
        //  SINGLETON + BAKED-TWIN INTEGRITY - StructureSingleton v2 (owner
        //  only-ever-one ruling): a repo.singleton row + repo.bakedTwins must be
        //  fully enforced with ZERO code, so the DATA must hold these invariants.
        // =====================================================================
        private static void CheckSingletons(List<string> failures, StringBuilder log)
        {
            int before = failures.Count;
            log.AppendLine("[singletons] repo.singleton + bakedTwins integrity (StructureSingleton v2):");

            var settings = new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter() },
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };

            StructuresCatalogFile Load(string path, string label)
            {
                if (!System.IO.File.Exists(path))
                {
                    failures.Add($"[singletons] {label} catalog copy MISSING at '{path}'");
                    return null;
                }
                try
                {
                    return JsonConvert.DeserializeObject<StructuresCatalogFile>(
                        System.IO.File.ReadAllText(path), settings);
                }
                catch (System.Exception ex)
                {
                    failures.Add($"[singletons] {label} catalog copy failed to parse: {ex.Message}");
                    return null;
                }
            }

            // Raw file reads on purpose: the dual-copy contract is between the two
            // COMMITTED files, not whatever CanonicalJson happens to resolve first.
            string srcPath = System.IO.Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/structures-catalog.json");
            string resPath = System.IO.Path.Combine(Application.dataPath, "Resources/Data/Canonical/structures-catalog.json");
            var src = Load(srcPath, "StreamingAssets");
            var res = Load(resPath, "Resources");
            if (src == null || res == null || src.Entries == null || res.Entries == null) return;

            // (a) shape: every bakedTwins entry non-empty; twin names UNIQUE across the
            //     catalog (a twin represents exactly one row); bakedTwins only on
            //     singleton-flagged rows (the enforcement sweep only walks those).
            var twinOwner = new Dictionary<string, string>();
            var srcById = new Dictionary<string, CatalogEntry>();
            int singletonRows = 0, twinRows = 0;
            foreach (var e in src.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id) || e.repo == null) continue;
                srcById[e.id] = e;
                if (e.repo.singleton) singletonRows++;
                var twins = e.repo.bakedTwins;
                if (twins == null || twins.Length == 0) continue;
                twinRows++;
                if (!e.repo.singleton)
                    failures.Add($"[singletons] '{e.id}' authors bakedTwins but is NOT flagged repo.singleton - " +
                                 "twin standdown/resurface only runs for singleton rows (StructureSingleton.EnforceAll)");
                foreach (var name in twins)
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        failures.Add($"[singletons] '{e.id}' has a null/empty bakedTwins entry");
                        continue;
                    }
                    if (twinOwner.TryGetValue(name, out var owner))
                        failures.Add($"[singletons] baked twin '{name}' is claimed by BOTH '{owner}' and '{e.id}' - " +
                                     "a baked twin must represent exactly ONE catalog row");
                    else
                        twinOwner[name] = e.id;
                }
            }
            log.AppendLine($"  {singletonRows} singleton row(s); {twinRows} row(s) author bakedTwins " +
                           $"({twinOwner.Count} unique twin name(s))");

            // (b) dual-copy parity on the singleton + bakedTwins fields (the two files
            //     must stay byte-identical in these fields; compare the parsed values).
            var resById = new Dictionary<string, CatalogEntry>();
            foreach (var e in res.Entries)
                if (e != null && !string.IsNullOrEmpty(e.id)) resById[e.id] = e;
            foreach (var e in src.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id) || e.repo == null) continue;
                if (!resById.TryGetValue(e.id, out var r) || r.repo == null)
                {
                    failures.Add($"[singletons] row '{e.id}' present in the StreamingAssets copy but missing " +
                                 "(or repo-less) in the Resources copy");
                    continue;
                }
                if (e.repo.singleton != r.repo.singleton)
                    failures.Add($"[singletons] '{e.id}' repo.singleton differs between copies " +
                                 $"(StreamingAssets={e.repo.singleton}, Resources={r.repo.singleton})");
                string a = e.repo.bakedTwins == null ? "" : string.Join("|", e.repo.bakedTwins);
                string b = r.repo.bakedTwins == null ? "" : string.Join("|", r.repo.bakedTwins);
                if (a != b)
                    failures.Add($"[singletons] '{e.id}' repo.bakedTwins differs between copies " +
                                 $"(StreamingAssets='{a}', Resources='{b}')");
            }

            // (c) migration-census coverage: every (bakedName,itemId) the WO-673 census
            //     maps onto a SINGLETON row must appear in that row's bakedTwins - else
            //     StructureSingleton (catalog-only in v2) cannot see the twin the
            //     migration/injector lane manages. Plus the explicit barracks pin.
            foreach (var (bakedName, itemId) in StrategicPlacementMigration.BakedStorefronts())
            {
                if (!srcById.TryGetValue(itemId, out var row) || row.repo == null) continue;   // row not authored yet
                if (!row.repo.singleton) continue;                                             // census row not singleton - out of scope
                bool listed = row.repo.bakedTwins != null &&
                              System.Array.IndexOf(row.repo.bakedTwins, bakedName) >= 0;
                if (!listed)
                    failures.Add($"[singletons] migration census maps baked '{bakedName}' -> singleton row '{itemId}' " +
                                 "but that row's repo.bakedTwins does not list it (StructureSingleton v2 reads ONLY the catalog)");
            }
            {
                bool barracksPinned = srcById.TryGetValue("barracks", out var barracksRow) &&
                                      barracksRow.repo != null && barracksRow.repo.bakedTwins != null &&
                                      System.Array.IndexOf(barracksRow.repo.bakedTwins, "CastleBarracks") >= 0;
                if (!barracksPinned)
                    failures.Add("[singletons] 'barracks'.repo.bakedTwins must contain 'CastleBarracks' " +
                                 "(the v1 SupplementalBaked row moved to data - losing it revives the two-barracks leak)");
            }

            // (d) seam asserts: CatalogRegistry.All() exists (EnforceAll depends on it),
            //     and BarracksNpcInjector no longer carries its bespoke baked-twin
            //     standdown (source-lint - reflection cannot see method bodies) but DOES
            //     subscribe the SingletonResolved reseat seam.
            var allMethod = typeof(CatalogRegistry).GetMethod("All", BindingFlags.Public | BindingFlags.Static);
            if (allMethod == null)
                failures.Add("[singletons] CatalogRegistry.All() is MISSING - StructureSingleton.EnforceAll sweeps it");
            string injPath = System.IO.Path.Combine(Application.dataPath, "_Modules/Village/NPCs/BarracksNpcInjector.cs");
            if (!System.IO.File.Exists(injPath))
            {
                failures.Add($"[singletons] BarracksNpcInjector.cs not found at '{injPath}' (seam lint skipped = FAIL)");
            }
            else
            {
                string injSrc = System.IO.File.ReadAllText(injPath);
                if (injSrc.Contains("SetActive(false)"))
                    failures.Add("[singletons] BarracksNpcInjector still contains a 'SetActive(false)' bespoke " +
                                 "baked-twin standdown - StructureSingleton.Enforce owns twin standdown in v2");
                if (!injSrc.Contains("SingletonResolved"))
                    failures.Add("[singletons] BarracksNpcInjector does not subscribe StructureSingleton.SingletonResolved - " +
                                 "the placed-barracks reseat seam is missing");
            }

            log.AppendLine(failures.Count == before
                ? "  SINGLETON_TWINS_OK"
                : $"  SINGLETON_TWINS_FAIL ({failures.Count - before} failure(s))");
        }

        // =====================================================================
        //  BLANK-TOWN BAKED-TWIN GATE - WO-834 (owner F8 seq 592): a baked twin
        //  may only surface for an id the player has EVER built (or while the
        //  WO-673 marker is unset - the bake still owns the town). Pins the pure
        //  rule, the v35->v36 seed, and the gate's presence at every surfacing seam.
        // =====================================================================
        private static void CheckBlankTownGate(List<string> failures, StringBuilder log)
        {
            int before = failures.Count;
            log.AppendLine("[blankTown] WO-834 baked-twin surface gate:");

            // (a) The pure rule's truth table (StructureSingleton.MayBakedTwinSurface).
            var none = new List<string>();
            var farmOnly = new List<string> { "collector_farm" };
            if (StructureSingleton.MayBakedTwinSurface("collector_farm", none, true))
                failures.Add("[blankTown] migrated save + empty everBuilt must SUPPRESS the baked twin (the seq-592 fix)");
            if (StructureSingleton.MayBakedTwinSurface("collector_farm", null, true))
                failures.Add("[blankTown] migrated save + NULL everBuilt must suppress (null-tolerant blank town)");
            if (!StructureSingleton.MayBakedTwinSurface("collector_farm", none, false))
                failures.Add("[blankTown] marker-false save must SURFACE (legacy pre-migration + Default-Town founding load)");
            if (!StructureSingleton.MayBakedTwinSurface("collector_farm", farmOnly, true))
                failures.Add("[blankTown] ever-built id must SURFACE on a migrated save (WO-819 sell-resurface leg)");
            if (!StructureSingleton.MayBakedTwinSurface("COLLECTOR_FARM", farmOnly, true))
                failures.Add("[blankTown] ever-built compare must be OrdinalIgnoreCase (catalog-id convention)");

            // (b) The v35->v36 migrator seed (SaveMigrator.MigrateToV36 via the chain).
            //     BLANK save (the owner's seq-592 shape: empty layout, no freebies burned)
            //     must seed an EMPTY list - present-but-empty is the fix.
            var blank = new SaveSchema.PersistedState
            {
                BaseLayout = new List<PlacedStructureData>(),
                FreeBuildsUsed = new List<string>(),
            };
            blank = SaveMigrator.Migrate(blank, 35);
            if (blank.EverBuiltStructureIds == null)
                failures.Add("[blankTown] MigrateToV36 left everBuiltStructureIds NULL on a blank save (must seed empty list)");
            else if (blank.EverBuiltStructureIds.Count != 0)
                failures.Add($"[blankTown] MigrateToV36 seeded {blank.EverBuiltStructureIds.Count} id(s) on a BLANK save (must be 0 - blank founding stays blank)");

            //     ESTABLISHED save: BaseLayout + FreeBuildsUsed union + the frozen
            //     default-town template grant (incl. 'barracks' - the WO-724 unlock right).
            var estab = new SaveSchema.PersistedState
            {
                BaseLayout = new List<PlacedStructureData>
                {
                    new PlacedStructureData("collector_farm", 1, 1, 0, level: 1,
                        yawOffset: 0f, worldY: 0f, wallMounted: false),
                },
                FreeBuildsUsed = new List<string> { "workshop" },
            };
            estab = SaveMigrator.Migrate(estab, 35);
            foreach (var id in new[] { "collector_farm", "workshop", "barracks", "pet-house" })
                if (estab.EverBuiltStructureIds == null || !estab.EverBuiltStructureIds.Contains(id))
                    failures.Add($"[blankTown] MigrateToV36 established-save seed missing '{id}' (existing towns must keep today's baked twins)");

            //     SOLD-SINGLETON save: empty layout but a burned freebie - the id must
            //     survive (WO-819 sell-resurface) WITHOUT dragging the template grant in.
            var sold = new SaveSchema.PersistedState
            {
                BaseLayout = new List<PlacedStructureData>(),
                FreeBuildsUsed = new List<string> { "pet-house" },
            };
            sold = SaveMigrator.Migrate(sold, 35);
            if (sold.EverBuiltStructureIds == null || !sold.EverBuiltStructureIds.Contains("pet-house"))
                failures.Add("[blankTown] MigrateToV36 dropped a FreeBuildsUsed id (placed-then-sold twin would stop resurfacing)");
            else if (sold.EverBuiltStructureIds.Contains("barracks"))
                failures.Add("[blankTown] MigrateToV36 granted the template to an EMPTY-BaseLayout save (blank founding must stay blank)");

            // (c) Source-lint: every surfacing seam carries the gate (reflection cannot
            //     see method bodies - same contract as the CheckSingletons seam lint).
            void Lint(string relPath, string needle, string why)
            {
                string path = System.IO.Path.Combine(Application.dataPath, relPath);
                if (!System.IO.File.Exists(path))
                {
                    failures.Add($"[blankTown] {relPath} not found (gate lint skipped = FAIL)");
                    return;
                }
                if (!System.IO.File.ReadAllText(path).Contains(needle))
                    failures.Add($"[blankTown] {relPath} no longer references '{needle}' - {why}");
            }
            Lint("_Modules/Village/BuildMode/StructureSingleton.cs", "MayBakedTwinSurface",
                "the Enforce resurface branch must be gated (WO-834)");
            Lint("_Modules/Village/BuildMode/StrategicPlacementMigration.cs", "MayBakedTwinSurface",
                "StanddownActiveForBaked must stand never-built bakes down at scene load");
            Lint("_Modules/Village/BuildMode/StrategicPlacementMigration.cs", "MarkEverBuilt",
                "the migration writer must grant the default-town template ids");
            Lint("_Modules/Village/NPCs/CastleVendorNpcInjector.cs", "MayBakedTwinSurface",
                "the Lever-1 baked-anchor fallback must not resurface/staff a suppressed store");
            Lint("_Modules/Village/NPCs/BarracksNpcInjector.cs", "MayBakedTwinSurface",
                "the WO-724 unlock poll must not resurface the barracks on a blank town");
            Lint("_Modules/Village/HubStructureVisualInjector.cs", "MayBakedTwinSurface",
                "EnsureBarracksSurfaced must respect the blank-town gate");
            Lint("_Modules/Village/BuildMode/BuildModeController.cs", "MarkEverBuilt",
                "the placement commit seam must grow the ever-built ledger");

            log.AppendLine(failures.Count == before
                ? "  BLANK_TOWN_GATE_OK"
                : $"  BLANK_TOWN_GATE_FAIL ({failures.Count - before} failure(s))");
        }

        // =====================================================================
        //  NPC MODEL BINDING - WO-818 (owner mapping table 2026-08-01): mapped
        //  structure rows author repo.npcModel (a KayKit slug) that the Village
        //  NPC injectors resolve as Resources/NPCs/KayKit/<slug>. The DATA must
        //  hold: (a) dual-copy field parity; (b) every authored slug resolves to
        //  a staged FBX; (c) the 12 owner-approved rows carry the owner's slugs
        //  VERBATIM (creative pick is owner-only - drift here is a code pick).
        // =====================================================================
        private static void CheckNpcModels(List<string> failures, StringBuilder log)
        {
            int before = failures.Count;
            log.AppendLine("[npcModel] repo.npcModel -> KayKit body binding (WO-818):");

            var settings = new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter() },
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };

            StructuresCatalogFile Load(string path, string label)
            {
                if (!System.IO.File.Exists(path))
                {
                    failures.Add($"[npcModel] {label} catalog copy MISSING at '{path}'");
                    return null;
                }
                try
                {
                    return JsonConvert.DeserializeObject<StructuresCatalogFile>(
                        System.IO.File.ReadAllText(path), settings);
                }
                catch (System.Exception ex)
                {
                    failures.Add($"[npcModel] {label} catalog copy failed to parse: {ex.Message}");
                    return null;
                }
            }

            // Raw file reads on purpose (same contract as CheckSingletons): the dual-copy
            // guarantee is between the two COMMITTED files.
            string srcPath = System.IO.Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/structures-catalog.json");
            string resPath = System.IO.Path.Combine(Application.dataPath, "Resources/Data/Canonical/structures-catalog.json");
            var src = Load(srcPath, "StreamingAssets");
            var res = Load(resPath, "Resources");
            if (src == null || res == null || src.Entries == null || res.Entries == null) return;

            var resById = new Dictionary<string, CatalogEntry>();
            foreach (var e in res.Entries)
                if (e != null && !string.IsNullOrEmpty(e.id)) resById[e.id] = e;

            // (a) dual-copy parity + (b) every authored slug resolves to a staged FBX
            //     under the TRACKED Resources/NPCs/KayKit (a typo'd slug would warn +
            //     People-fallback on every load - catch it at the gate instead).
            // PROD-002: npcModel may now be FOLDER-QUALIFIED ("CraftPixPeople/NPC_Peasant_1") or a
            // bare legacy slug ("Ranger" -> the KayKit stage). This check MIRRORS
            // KayKitNpcBody.Load's rule deliberately: if the gate resolved paths differently from
            // the runtime, it would pass on bodies the game cannot load, which is worse than having
            // no gate. It also accepts .prefab OR .fbx — KayKit bodies are staged as raw FBXs,
            // the purchased CraftPix bodies are built prefabs, and both are legitimate answers to
            // "does Resources.Load<GameObject> find a body here".
            string npcRootDir = System.IO.Path.Combine(Application.dataPath, "Resources/NPCs");
            int authored = 0;
            var srcModelById = new Dictionary<string, string>();
            foreach (var e in src.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                string a = (e.repo != null) ? e.repo.npcModel : null;
                srcModelById[e.id] = a;
                string b = (resById.TryGetValue(e.id, out var r) && r.repo != null) ? r.repo.npcModel : null;
                if ((a ?? "") != (b ?? ""))
                    failures.Add($"[npcModel] '{e.id}' repo.npcModel differs between copies " +
                                 $"(StreamingAssets='{a ?? "<null>"}', Resources='{b ?? "<null>"}')");
                if (string.IsNullOrEmpty(a)) continue;
                authored++;
                // Same rule as KayKitNpcBody.Load: a '/' means the slug names its own pack folder.
                string rel  = a.Contains("/") ? a : "KayKit/" + a;
                string stem = System.IO.Path.Combine(npcRootDir, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
                string found = null;
                foreach (var ext in new[] { ".prefab", ".fbx" })
                    if (System.IO.File.Exists(stem + ext)) { found = stem + ext; break; }

                if (found == null)
                    failures.Add($"[npcModel] '{e.id}' npcModel '{a}' has NO body at '{stem}.prefab' or '{stem}.fbx' " +
                                 "(the injector would warn + fall back to the People chain every load)");
                else
                    log.AppendLine($"  NM {e.id} -> '{a}' | body OK ({System.IO.Path.GetExtension(found)})");
            }

            // (c) the 12 owner-approved rows (WO-818 mapping table, VERBATIM - a change
            //     here is an owner retag applied to BOTH this table and the catalog,
            //     never a code-side pick).
            // PROD-002 (2026-08-17) — RETAGGED FROM KAYKIT PLACEHOLDERS TO THE OWNER-PURCHASED
            // CraftPix bodies. Owner: "can we replace the placeholder kay kat with the people i
            // purchased?" and, on the casting itself, "so you can pick". The picks below read
            // STATUS against what each post sells or does, which is why they are legible rather
            // than arbitrary: the two skilled trades that sell gear are CityDwellers, the
            // high-value/high-status posts are RichCitizens, and everyone working the land or a
            // production building is a Peasant.
            //   ⛔ NPC_King and NPC_Queen are DELIBERATELY UNCAST. They are the two most
            //      distinctive bodies in the set and would read as absurd behind a shop counter;
            //      holding them back keeps royalty available for a throne-room or quest beat
            //      instead of spending it on a vendor.
            // 12 rows, 12 non-royal bodies, strict 1:1 — no body appears twice, which matters
            // because a duplicated body reads to the player as the same person working two jobs.
            // This table stays VERBATIM-pinned: a change is a retag applied to BOTH this table and
            // the catalog, never a code-side pick.
            var expected = new Dictionary<string, string>
            {
                { "barracks",             "CraftPixPeople/NPC_RichCitizen_4" },  // officer
                { "workshop",             "CraftPixPeople/NPC_RichCitizen_2" },  // master artisan
                { "forge",                "CraftPixPeople/NPC_CityDweller_1" },  // SELLS WEAPONS
                { "armorer",              "CraftPixPeople/NPC_CityDweller_2" },  // SELLS ARMOUR
                { "jeweler",              "CraftPixPeople/NPC_RichCitizen_1" },  // rings/gems - highest value
                { "market",               "CraftPixPeople/NPC_Peasant_1" },      // Coppin, produce
                { "arcane-tower",         "CraftPixPeople/NPC_RichCitizen_3" },  // scholar
                { "pet-house",            "CraftPixPeople/NPC_Peasant_6" },      // Echo keeper
                { "collector_farm",       "CraftPixPeople/NPC_Peasant_3" },
                { "mill",                 "CraftPixPeople/NPC_Peasant_2" },
                { "collector_lumbermill", "CraftPixPeople/NPC_Peasant_4" },
                { "healing_caravan",      "CraftPixPeople/NPC_Peasant_5" },
            };
            foreach (var kv in expected)
            {
                if (!srcModelById.TryGetValue(kv.Key, out var actual))
                    failures.Add($"[npcModel] mapped structure row '{kv.Key}' is MISSING from the catalog");
                else if (actual != kv.Value)
                    failures.Add($"[npcModel] '{kv.Key}' npcModel '{actual ?? "<null>"}' != owner-approved '{kv.Value}' (WO-818 table)");
            }
            if (authored != expected.Count)
                failures.Add($"[npcModel] {authored} row(s) author npcModel but the WO-818 owner table maps exactly {expected.Count} " +
                             "- an extra/missing binding is not an owner-approved pick");

            // (d) WO-833: the shared idle controller KayKitNpcBody.ArmIdle arms on a body that
            //     arrives with NO controller must EXIST under Resources and reference >=1 clip -
            //     an empty/missing controller renders the FBX bind pose (owner F8 "NPC Stuck in
            //     T Pose"). Catch it at the gate, not the felt-test.
            //     ⚠ SCOPE NARROWED BY PROD-002, and the check is kept anyway. It no longer covers
            //     "all 12 structure NPCs": those now resolve to CraftPix prefabs that ship
            //     AC_CraftPixTownsfolk already bound, and ArmController leaves a bound controller
            //     alone rather than overwriting it. This controller still drives every KayKit body
            //     that remains live (the hero body-swap set, the construction worker), so deleting
            //     the check would drop cover on a T-pose bug that is still reachable - it simply
            //     protects fewer NPCs than the original comment claimed.
            const string idleCtrlPath = "Assets/Resources/NPCs/KayKit/KayKitNpcIdle.controller";
            var idleCtrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(idleCtrlPath);
            if (idleCtrl == null)
                failures.Add($"[npcModel] WO-833 idle controller MISSING at '{idleCtrlPath}' - staged KayKit NPCs " +
                             "would T-pose; run Defenders/Art/Build KayKit NPC Idle Controller " +
                             "(DeNelle.Editor.KayKitNpcAnimatorSetup.Build)");
            else
            {
                int clipCount = 0;
                string firstClip = null;
                var clips = idleCtrl.animationClips;
                if (clips != null)
                    foreach (var c in clips)
                        if (c != null) { clipCount++; if (firstClip == null) firstClip = c.name; }
                if (clipCount == 0)
                    failures.Add($"[npcModel] WO-833 idle controller at '{idleCtrlPath}' references NO animation clip - " +
                                 "the Idle state is motion-less, NPCs would still T-pose (rebuild via KayKitNpcAnimatorSetup.Build)");
                else
                    log.AppendLine($"  NM idle controller OK ({clipCount} clip(s), first '{firstClip}')");
            }

            log.AppendLine(failures.Count == before
                ? $"  NPC_MODELS_OK ({authored} bound row(s))"
                : $"  NPC_MODELS_FAIL ({failures.Count - before} failure(s))");
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
            // WO-850 (2026-08-02): "ember-resin" REMOVED from this set - the owner ruled the
            // torch ingredients be promoted into materials.json so the larder can hold them, so
            // it now has a real MaterialDef and no longer needs the exception. Leaving it here
            // would have been a comment that lies ("intentionally have NO MaterialDef") and
            // would mask a genuine future regression if the row were ever deleted.
            var legacy = new HashSet<string>
            {
                "wild-herb", "rare-essence", "monster-hide", "tattered-cloth"
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
                log.AppendLine("[jeweler] SOFT: " + DeNelle.Editor.Regression.RegressionOutcome.PartialSkip(
                    "simulated craft", "no iron/wood-only recipe to simulate without GameState"));
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
            // Tag disambiguated 2026-08-02: the registered ArmedHeroInvariantRegression suite
            // owns "[armed-hero]". This INLINE check keeps a distinct tag so a log grep names
            // exactly which of the two produced a line.
            log.AppendLine("[armed-hero-inline] BestWeapon(job,1) resolves an attachable prefab per class:");

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
                    var prefab = string.IsNullOrEmpty(path) ? null : DeNelle.Core.StructureAssetLoader.LoadStructurePrefab(path);
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

        // WO-996: after the library merge, every Resources armor id must exist in StreamingAssets
        // (same subset shape as weapons). Schema versions must agree.
        private static void CheckArmorDualCopy(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[armor-dual-copy] Resources armor ids ⊆ StreamingAssets (WO-996):");
            string rPath = "Assets/Resources/Data/Canonical/armor.json";
            string sPath = "Assets/StreamingAssets/Data/Canonical/armor.json";
            if (!System.IO.File.Exists(rPath) || !System.IO.File.Exists(sPath))
            {
                failures.Add("armor-dual-copy: one or both armor.json copies are missing on disk");
                return;
            }
            try
            {
                var rTok = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(rPath));
                var sTok = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(sPath));
                int rVer = rTok.Value<int?>("version") ?? 0;
                int sVer = sTok.Value<int?>("version") ?? 0;
                if (rVer != sVer)
                    failures.Add($"armor-dual-copy: schema version mismatch Resources=v{rVer} StreamingAssets=v{sVer}");
                var rIds = new HashSet<string>();
                var sIds = new HashSet<string>();
                foreach (var row in rTok["armor"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray())
                    if (row["id"] != null) rIds.Add(row["id"].ToString());
                foreach (var row in sTok["armor"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray())
                    if (row["id"] != null) sIds.Add(row["id"].ToString());
                int missing = 0;
                foreach (var id in rIds)
                {
                    if (!sIds.Contains(id))
                    {
                        missing++;
                        if (missing <= 8)
                            failures.Add($"armor-dual-copy: Resources id '{id}' missing from StreamingAssets library");
                    }
                }
                if (missing > 8)
                    failures.Add($"armor-dual-copy: …and {missing - 8} more Resources-only ids");
                log.AppendLine($"  Resources={rIds.Count} StreamingAssets={sIds.Count} version R={rVer}/S={sVer} missingFromLibrary={missing}");
            }
            catch (System.Exception ex)
            {
                failures.Add($"armor-dual-copy: parse threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        // WO-975: every Gear.asset serialize-entry GUID must resolve to an on-disk asset.
        private static void CheckGearAddressableGroup(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[gear-addressable-group] Gear.asset entry GUIDs resolve on disk (WO-975):");
            const string gearAsset = "Assets/AddressableAssetsData/AssetGroups/Gear.asset";
            if (!System.IO.File.Exists(gearAsset))
            {
                failures.Add("gear-addressable-group: Gear.asset missing");
                return;
            }
            var re = new System.Text.RegularExpressions.Regex(
                @"^\s+-\s+m_GUID:\s+([0-9a-fA-F]{32})\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            string text = System.IO.File.ReadAllText(gearAsset);
            var seen = new HashSet<string>();
            int total = 0, ok = 0, dangling = 0;
            foreach (System.Text.RegularExpressions.Match m in re.Matches(text))
            {
                string guid = m.Groups[1].Value.ToLowerInvariant();
                if (!seen.Add(guid)) continue;
                total++;
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) ||
                    (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path)))
                {
                    dangling++;
                    if (dangling <= 6)
                        failures.Add($"gear-addressable-group: dangling GUID {guid} path='{path}'");
                }
                else ok++;
            }
            if (total == 0)
                failures.Add("gear-addressable-group: zero serialize-entry GUIDs in Gear.asset");
            if (dangling > 6)
                failures.Add($"gear-addressable-group: …and {dangling - 6} more dangling GUIDs");
            log.AppendLine($"  entries={total} resolvable={ok} dangling={dangling}");
            // Soft note: AddressableKeyExists remains advisory for per-key probes (WO-975 §3).
            log.AppendLine("  note: AddressableKeyExists() is deliberately advisory for single-key probes; this suite is the hard fence.");
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
