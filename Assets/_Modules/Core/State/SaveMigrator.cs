// =============================================================================
// SaveMigrator — the persisted-save migration chain v1 → v34 (spec §2.4)
// -----------------------------------------------------------------------------
// C# port of `migratePersistedState` from src/state/gameStore.ts. One shared
// entry point used by BOTH the boot loader AND a future Save-Import path.
//
// IMPROVEMENT #3 (adopted): a registry-based migrator. Each step is a
// Dictionary<int,Func<...>> entry keyed by its TARGET version; Migrate() applies
// every entry from fromVersion+1..CurrentVersion in ascending order. Behaviour
// is identical to the original React stacked-`if` cascade, but each step is
// independently unit-testable. (Steps now run to v33; the chain has grown well
// past the original nine — many later versions are additive-default-on-read.)
//
// Every step is ADDITIVE — it seeds new fields with empty defaults and never
// mutates data a save already carries. Three fields (inventory.torches,
// dungeons.deathsByDungeon, dungeons.loreReadByDungeon) were added WITHOUT a
// version bump — they are simply optional/default-on-read, no migration step.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Jobs;
using UnityEngine;

namespace DeNelle.Core.State
{
    using PersistedState = SaveSchema.PersistedState;

    /// <summary>Registry-based port of <c>migratePersistedState</c> (v1 → v34).</summary>
    public static class SaveMigrator
    {
        /// <summary>
        /// One migration step: target version N → the function that upgrades a
        /// vN-1 payload to vN. Applied in ascending key order.
        /// </summary>
        private static readonly SortedDictionary<int, Func<PersistedState, PersistedState>> Steps =
            new SortedDictionary<int, Func<PersistedState, PersistedState>>
            {
                { 2, MigrateToV2 },
                { 3, MigrateToV3 },
                { 4, MigrateToV4 },
                { 5, MigrateToV5 },
                { 6, MigrateToV6 },
                { 7, MigrateToV7 },
                { 8, MigrateToV8 },
                { 9, MigrateToV9 },
                { 10, MigrateToV10 },
                // v11→v13 were additive-default-on-read (aetherCrystals / lastHarvestClaimMs /
                // buildJobs+adSkip): nullable fields defaulted on load, no Steps entry needed.
                { 14, MigrateToV14 },
                // v15 (DEF-121/WO-230) added `magic` — additive-default-on-read (a
                // nullable field defaulted to 0 on load, like aetherCrystals v11), so
                // NO Steps entry is needed.
                // v16 (WO-301) added `partyMemberIds` — additive-default-on-read (a
                // nullable list defaulted to empty on load), so NO Steps entry is needed.
                { 17, MigrateToV17 },
                { 18, MigrateToV18 },
                // v19 (arenaDefense, WO-389) + v20 (gearInventory) were additive-
                // default-on-read (a nullable list/dict defaulted to empty on load),
                // so NO Steps entry was needed for them.
                { 21, MigrateToV21 },
                { 22, MigrateToV22 },
                { 23, MigrateToV23 },
                { 24, MigrateToV24 },
                { 25, MigrateToV25 },
                { 26, MigrateToV26 },
                { 27, MigrateToV27 },
                { 28, MigrateToV28 },
                { 29, MigrateToV29 },
                { 30, MigrateToV30 },
                { 31, MigrateToV31 },
                { 32, MigrateToV32 },
                { 33, MigrateToV33 },
                { 34, MigrateToV34 },
                { 35, MigrateToV35 },
            };

        /// <summary>
        /// Migrates a parsed <see cref="PersistedState"/> from <paramref name="fromVersion"/>
        /// up to <see cref="SaveSchema.CurrentVersion"/>. Additive only. A save
        /// already at the current version is a no-op pass-through.
        /// </summary>
        public static PersistedState Migrate(PersistedState state, int fromVersion)
        {
            var s = state ?? new PersistedState();
            foreach (var step in Steps)
            {
                // Cumulative `if (fromVersion < N)` — an ancient save runs every
                // step in ascending order; a recent save skips the early ones.
                if (fromVersion < step.Key)
                    s = step.Value(s);
            }
            return s;
        }

        /// <summary>
        /// Version gate (from <c>applySaveImport</c>): rejects a save newer than
        /// this build or with a non-finite version; an older save migrates, an
        /// equal version is a no-op. Returns the migrated payload or a failure.
        /// </summary>
        public static SaveMigrationResult MigrateForImport(PersistedState state, double storeVersion)
        {
            if (double.IsNaN(storeVersion) || double.IsInfinity(storeVersion))
                return SaveMigrationResult.Failure($"Unsupported save version ({storeVersion}).");

            var version = (int)storeVersion;
            if (version > SaveSchema.CurrentVersion)
                return SaveMigrationResult.Failure(
                    $"Save is from a newer version ({version}) than this build supports.");

            // An older save migrates; an equal version skips the chain entirely.
            var migrated = version < SaveSchema.CurrentVersion
                ? Migrate(state, version)
                : (state ?? new PersistedState());
            return SaveMigrationResult.Success(migrated);
        }

        // =====================================================================
        //  Migration steps — verbatim ports of the React cascade
        // =====================================================================

        /// <summary>v1→v2: seed resources = STARTER_RESOURCES and ownedItemIds = [].</summary>
        private static PersistedState MigrateToV2(PersistedState s)
        {
            s.Resources = ResourceBalance.Starter;
            s.OwnedItemIds = new List<string>();
            return s;
        }

        /// <summary>
        /// v2→v3: seed heroClass = heroClass ?? Mage — pre-hero-select saves
        /// default to Mage so their existing ability set / model stay valid.
        /// </summary>
        private static PersistedState MigrateToV3(PersistedState s)
        {
            if (!s.HeroClass.HasValue) s.HeroClass = HeroClass.Mage;
            return s;
        }

        /// <summary>
        /// v3→v4: seed wood ?? 15, buildingCooldowns ?? {}, tutorialStep ?? 'done'
        /// (an in-progress save skips the first-time tutorial).
        /// </summary>
        private static PersistedState MigrateToV4(PersistedState s)
        {
            if (!s.Wood.HasValue) s.Wood = 15;
            if (s.BuildingCooldowns == null) s.BuildingCooldowns = new Dictionary<string, double>();
            if (!s.TutorialStep.HasValue) s.TutorialStep = TutorialStep.Done;
            return s;
        }

        /// <summary>
        /// v4→v5: seed towerAbilities ?? [0]×TOWER_SLOTS — a v4 save with a built
        /// tower would crash on render without it.
        /// </summary>
        private static PersistedState MigrateToV5(PersistedState s)
        {
            if (s.TowerAbilities == null)
            {
                s.TowerAbilities = new List<double>(Constants.TowerSlots);
                for (var i = 0; i < Constants.TowerSlots; i++) s.TowerAbilities.Add(0);
            }
            return s;
        }

        /// <summary>
        /// v5→v6: seed the whole ATB + dungeon block — inventory, atbLossStreak,
        /// breachStyle, buildingDamage, dungeons, activeDungeonRun, quests. (The
        /// React step also seeds the transient prepTimerLocked; that field is
        /// runtime-only in Unity and is simply not carried.)
        /// </summary>
        private static PersistedState MigrateToV6(PersistedState s)
        {
            if (!s.Inventory.HasValue) s.Inventory = AtbInventory.Empty;
            if (!s.AtbLossStreak.HasValue) s.AtbLossStreak = 0;
            if (!s.BreachStyle.HasValue) s.BreachStyle = BreachStyle.Ask;
            if (s.BuildingDamage == null) s.BuildingDamage = new Dictionary<string, double>();
            if (s.Dungeons == null) s.Dungeons = DungeonProgress.Empty();
            // activeDungeonRun ?? null — already null on a fresh PersistedState.
            if (s.Quests == null) s.Quests = QuestProgress.Empty();
            return s;
        }

        /// <summary>
        /// v6→v7: non-destructively merge the starter dungeon (healers_cottage)
        /// into dungeons.discovered so the Dungeon Select screen always has ≥1
        /// entry. (The React step also force-clears prepTimerLocked — runtime-only
        /// in Unity, so nothing to clear.)
        /// </summary>
        private static PersistedState MigrateToV7(PersistedState s)
        {
            var prev = s.Dungeons ?? DungeonProgress.Empty();
            if (prev.Discovered == null) prev.Discovered = new Dictionary<string, bool>();
            prev.Discovered[SaveSchema.StarterDungeonId] = true;
            s.Dungeons = prev;
            return s;
        }

        /// <summary>
        /// v7→v8: four-cardinal-gates rename — the south gate id moved
        /// gate-0 → gate-2. If buildingDamage has a gate-0 key, copy its value to
        /// gate-2 and delete gate-0. Wrapped in try/catch — on failure, drop the
        /// orphan gate-0 (worst case the south gate loads at 0 damage).
        /// </summary>
        private static PersistedState MigrateToV8(PersistedState s)
        {
            var bd = s.BuildingDamage;
            if (bd == null) return s;
            try
            {
                if (bd.ContainsKey("gate-0"))
                {
                    bd["gate-2"] = bd["gate-0"];
                    bd.Remove("gate-0");
                }
            }
            catch (Exception ex)
            {
                // §12 TGVRU: was a SILENT catch. R (drop the orphan gate-0 → south gate
                // loads at 0 damage) is kept, but a failed migration step must be LOUD —
                // silently losing data is the worst case.
                FlowTrace.Warn("Save", $"v7→v8 gate-id migration FAILED — dropping orphan gate-0 (south gate loads at 0 damage). {ex.GetType().Name}: {ex.Message}");
                bd.Remove("gate-0");
            }
            return s;
        }

        /// <summary>
        /// v8→v9: (1) seed pendingBuilds ?? []. (2) Migrate audio/difficulty prefs
        /// out of the legacy standalone store keyed 'realm-defenders-settings':
        /// read+parse that PlayerPrefs key, then set muted/musicVolume/sfxVolume/
        /// difficulty/voiceOvers via state ?? legacy ?? default
        /// (defaults: muted=false, music=70, sfx=80, difficulty=normal,
        /// voiceOvers=false). Then delete the legacy key. Existing v8 players
        /// fall back to muted=false — only brand-new players get muted-by-default.
        /// </summary>
        private static PersistedState MigrateToV9(PersistedState s)
        {
            if (s.PendingBuilds == null) s.PendingBuilds = new List<PendingTowerBuild>();

            LegacySettings legacy = null;
            try
            {
                if (PlayerPrefs.HasKey(SaveSchema.LegacySettingsKey))
                {
                    var raw = PlayerPrefs.GetString(SaveSchema.LegacySettingsKey);
                    if (!string.IsNullOrEmpty(raw))
                        legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<LegacySettings>(
                            raw, SaveSchema.JsonSettings);
                }
            }
            catch (Exception ex)
            {
                // §12 TGVRU: was a SILENT catch. R (legacy=null → fall back to per-field
                // defaults below) is kept, but a failed legacy-settings parse must report
                // so a silently-lost audio/difficulty preference reaches the break-log.
                FlowTrace.Warn("Save", $"v8→v9 legacy-settings parse FAILED — audio/difficulty prefs fall back to defaults. {ex.GetType().Name}: {ex.Message}");
                legacy = null;
            }

            // state.<f> ?? legacy.<f> ?? <default>
            if (!s.Muted.HasValue) s.Muted = legacy?.Muted ?? false;
            if (!s.MusicVolume.HasValue) s.MusicVolume = legacy?.MusicVolume ?? 70;
            if (!s.SfxVolume.HasValue) s.SfxVolume = legacy?.SfxVolume ?? 80;
            if (!s.Difficulty.HasValue) s.Difficulty = legacy?.Difficulty ?? Difficulty.Normal;
            if (!s.VoiceOvers.HasValue) s.VoiceOvers = legacy?.VoiceOvers ?? false;

            try
            {
                if (PlayerPrefs.HasKey(SaveSchema.LegacySettingsKey))
                    PlayerPrefs.DeleteKey(SaveSchema.LegacySettingsKey);
            }
            catch (Exception ex)
            {
                // §12 TGVRU: was a silent ignore. The legacy key is ignored regardless,
                // so this is non-fatal — but report it (a failed PlayerPrefs delete can
                // signal a deeper prefs corruption worth seeing in the break-log).
                FlowTrace.Warn("Save", $"v8→v9 legacy-settings key delete FAILED (non-fatal — key is ignored regardless). {ex.GetType().Name}: {ex.Message}");
            }
            return s;
        }

        /// <summary>
        /// v9→v10: seed the Realm Map — regions ?? emptyRegionProgress()
        /// ({discovered:{}, cleared:{}}). (activeRegionRun is NOT persisted.)
        /// </summary>
        private static PersistedState MigrateToV10(PersistedState s)
        {
            if (s.Regions == null) s.Regions = RegionProgress.Empty();
            return s;
        }

        /// <summary>
        /// v13→v14 (WO-108 player build mode): seed baseLayout ?? [] — an empty
        /// base layout. Additive: an existing player keeps the default
        /// VillageSceneBuilder village (the loader falls through on an empty layout)
        /// until they first enter build mode and save their own layout.
        /// </summary>
        private static PersistedState MigrateToV14(PersistedState s)
        {
            if (s.BaseLayout == null) s.BaseLayout = new List<PlacedStructureData>();
            return s;
        }

        /// <summary>
        /// v16→v17 (WO-164 zone persistence): seed zones ?? the default zone graph
        /// (5 zones). A pre-v17 save had no persisted zone graph; seed it so the
        /// world's discovery/clear/destination spine round-trips. Idempotent (only
        /// seeds when null/empty), matching <see cref="GameStateService"/>'s
        /// EnsureZoneGraph so the 5 zones can't duplicate across call sites.
        /// </summary>
        private static PersistedState MigrateToV17(PersistedState s)
        {
            if (s.Zones == null || s.Zones.Count == 0)
                s.Zones = new List<DeNelle.Core.World.ZoneState>(
                    DeNelle.Core.World.ZoneManager.DefaultZoneGraph());
            return s;
        }

        /// <summary>
        /// v17→v18 (crystal unification): a one-time fold of the orphan
        /// <c>aetherCrystals</c> balance into <c>resources.crystals</c> — the single
        /// source of truth. After the fold, zero out aetherCrystals (the field is
        /// kept for back-compat but no longer written). ResourceBalance is a struct →
        /// read-modify-write-assign-back.
        /// </summary>
        private static PersistedState MigrateToV18(PersistedState s)
        {
            if (s.AetherCrystals.HasValue && s.AetherCrystals.Value > 0)
            {
                var r = s.Resources ?? ResourceBalance.Starter;
                r.Crystals += (int)s.AetherCrystals.Value;
                s.Resources = r;
            }
            s.AetherCrystals = 0;
            return s;
        }

        /// <summary>
        /// v20→v21 (WO-159 node-settlement persistence): seed settlements ?? [] — an
        /// empty settlement list. Additive: a pre-v21 save had no persisted node
        /// settlements, so it loads with none claimed (the claim/HP/3-day-razed-lockout
        /// state now round-trips going forward). Idempotent (only seeds when null).
        /// </summary>
        private static PersistedState MigrateToV21(PersistedState s)
        {
            if (s.Settlements == null)
                s.Settlements = new List<DeNelle.Core.World.SettlementState>();
            return s;
        }

        /// <summary>
        /// v21→v22 (WO-453 army persistence): seed army ?? a fresh empty cap-10
        /// <see cref="ArmyStorage"/> — a pre-v22 save had no persisted army, so it loads
        /// with no owned troops (the owned roster + cap + wounded/recovery/veterancy now
        /// round-trip going forward). Additive + idempotent (only seeds when null).
        /// </summary>
        private static PersistedState MigrateToV22(PersistedState s)
        {
            if (s.Army == null)
                s.Army = new ArmyStorage();
            return s;
        }

        // v23 — building upgrade tiers (WO-430). Seed an empty dict on older saves so the
        // city-upgrade system reads tier 0 (locked) for every building until the player buys one.
        private static PersistedState MigrateToV23(PersistedState s)
        {
            if (s.BuildingTiers == null)
                s.BuildingTiers = new System.Collections.Generic.Dictionary<string, int>();
            return s;
        }

        // v24 — Village/Stronghold tier + owned building-research perks (WO-432). VillageTier defaults
        // to 0 (int); seed an empty perks list on older saves so the research system reads "none owned".
        private static PersistedState MigrateToV24(PersistedState s)
        {
            if (s.OwnedBuildingPerks == null)
                s.OwnedBuildingPerks = new System.Collections.Generic.List<string>();
            return s;
        }

        // v25 — Echo Workforce V1 (ECHO_WORKFORCE_SPEC). An existing player should keep their
        // starter Echo on first load of this build, so seed echoCount = 1 when absent (a fresh
        // PersistedState leaves it null). siloResources + wavesCompleted are additive-default-on-read
        // (null → 0 on load), so they need no explicit seed; we set them for clarity/round-trip.
        private static PersistedState MigrateToV25(PersistedState s)
        {
            if (!s.EchoCount.HasValue) s.EchoCount = 1;
            if (!s.SiloResources.HasValue) s.SiloResources = 0;
            if (!s.WavesCompleted.HasValue) s.WavesCompleted = 0;
            return s;
        }

        // v26 — WO-543 accessory equip persistence. equippedRingId/equippedAmuletId are
        // additive-default-on-read (null → "none"); we seed "" explicitly for a clean round-trip,
        // mirroring the v25 silo seeds. An old save simply has no accessory equipped.
        private static PersistedState MigrateToV26(PersistedState s)
        {
            if (s.EquippedRingId == null) s.EquippedRingId = "";
            if (s.EquippedAmuletId == null) s.EquippedAmuletId = "";
            return s;
        }

        // v27 — wall-mounted defense seating. PlacedStructureData gained worldY (seat height) +
        // wallMounted (high-ground perk flag). Both are additive default-on-read: a pre-v27
        // baseLayout record deserializes with worldY = 0 and wallMounted = false, which is exactly
        // "ground placement, no elevation bonus" — the prior behaviour. So no per-record rewrite is
        // needed; this step is a documented no-op that records the schema bump (mirroring the v25/v26
        // default-on-read precedent). Listing it keeps the version chain explicit + unit-testable.
        private static PersistedState MigrateToV27(PersistedState s)
        {
            // No data rewrite: existing baseLayout entries keep worldY = 0 / wallMounted = false
            // (ground placement) on read, unchanged. New placements persist their seat height.
            return s;
        }

        // v28 — WO-587 Population & Echo growth. populationXp/populationQuests/populationOutposts are
        // additive-default-on-read (null → 0 on load); populationEchoSlots seeds 1 when absent so an
        // existing player keeps their starter Wood echo slot. We set each explicitly for a clean
        // round-trip, mirroring the v25/v26 seed precedent. Idempotent (only seeds when null).
        private static PersistedState MigrateToV28(PersistedState s)
        {
            if (!s.PopulationXP.HasValue) s.PopulationXP = 0;
            if (!s.PopulationQuests.HasValue) s.PopulationQuests = 0;
            if (!s.PopulationOutposts.HasValue) s.PopulationOutposts = 0;
            if (!s.PopulationEchoSlots.HasValue) s.PopulationEchoSlots = 1;
            return s;
        }

        // v29 — F8-47 hero level/XP persistence. A pre-v29 save never persisted the hero's
        // level (it lived only on the in-memory HeroProgression component), so seed the fresh-hero
        // defaults: level 1, no banked/lifetime XP — exactly what such a player had on every load.
        // heroXp/heroLifetimeXp are additive-default-on-read (null → 0); we set each explicitly for
        // a clean round-trip, mirroring the v25/v28 seed precedent. Idempotent (only seeds when null).
        private static PersistedState MigrateToV29(PersistedState s)
        {
            if (!s.HeroLevel.HasValue) s.HeroLevel = 1;
            if (!s.HeroXp.HasValue) s.HeroXp = 0;
            if (!s.HeroLifetimeXp.HasValue) s.HeroLifetimeXp = 0;
            return s;
        }

        // v30 — WO-673 strategic-placement migration marker. A pre-v30 save has never run the
        // one-shot bake→BaseLayout migration, so seed false: the baked storefronts + runtime
        // station injectors keep owning the functional structures (exactly the prior behaviour)
        // until the one-shot writer (StrategicPlacementMigration.RunIfNeeded) flips it once
        // on the next home-hub load (always-on since WO-682 removed ff.strategicplacement).
        // Additive + idempotent (only seeds when null), mirroring the v25/v29 seed precedent.
        private static PersistedState MigrateToV30(PersistedState s)
        {
            if (!s.StrategicPlacementMigrated.HasValue) s.StrategicPlacementMigrated = false;
            return s;
        }

        // v31 — WO-681/658 echo gather-lane assignments (per-Echo lane CSV, index 0 = the
        // starter Echo). A pre-v31 save has never assigned a lane, so seed the "wood" starter
        // default — exactly the prior behaviour (the starter Echo always gathered wood).
        // Additive + idempotent (only seeds when null), mirroring the v29/v30 seed precedent.
        private static PersistedState MigrateToV31(PersistedState s)
        {
            if (s.EchoLanes == null) s.EchoLanes = "wood";
            return s;
        }

        // v32 — first-build-free flags (owner ruling 2026-07-13 evening: first placement of
        // each catalog id is free; the flag burns on use and never resets; replaces the
        // resource seed, StartingBudget -> 0). A pre-v32 save has burned nothing, so the
        // correct default IS the fresh default: an EMPTY list (full freebies — the player
        // keeps whatever wood/iron they banked on top). Additive + idempotent.
        private static PersistedState MigrateToV32(PersistedState s)
        {
            if (s.FreeBuildsUsed == null) s.FreeBuildsUsed = new System.Collections.Generic.List<string>();
            return s;
        }

        // v33 (WO-738) — the echoLanes token grew from a bare lane ("wood") to a
        // "lane:level" grammar ("harvest:3,idle,crafting:1"). This is backward-compatible
        // READ-migrated by EchoAssignments at parse time (a legacy wood/iron/food token
        // reads as the Harvest lane at level 1; idle stays idle), so no field transform is
        // needed here. The step exists only to keep the version triple aligned
        // (SaveMigrator top step == SaveSchema.CurrentVersion) per the CORE_SAVE oracle —
        // the same reason recent additive bumps each carry a pass-through step.
        private static PersistedState MigrateToV33(PersistedState s)
        {
            return s;
        }

        // v34 (REDS #3/#4) — the four previously in-memory-only fields now round-trip.
        // A pre-v34 save never persisted them, so seed the fresh defaults — exactly what
        // such a player had on every load: no claimed tribe progress, no relit wards, a
        // zeroed arena W/L ledger, and no persisted pet slot map (PetAcquisitionService
        // then falls back to its legacy starter-in-slot-0 rebuild). tribes/wards/
        // petActiveSlots are additive-default-on-read (null → empty on load) and arena is
        // additive-default-on-read (null → the SO's Empty default); we seed each explicitly
        // for a clean round-trip, mirroring the v22/v25/v29 seed precedent. Additive +
        // idempotent (only seeds when null), so it never clobbers data a save already carries.
        private static PersistedState MigrateToV34(PersistedState s)
        {
            if (s.Tribes == null) s.Tribes = new List<DeNelle.Core.World.TribeState>();
            if (s.Wards == null) s.Wards = new List<DeNelle.Core.World.WardStoneState>();
            if (!s.Arena.HasValue) s.Arena = ArenaProgress.Empty;
            if (s.PetActiveSlots == null) s.PetActiveSlots = new List<string>();
            return s;
        }

        // v35 (WO-773) — the common "Obsidian" multi-channel work queue. Build the
        // ObsidianQueueState (Builder/Train/Research channels) and FOLD the legacy timed-state
        // into the BUILDER channel so nothing in flight is lost:
        //   • buildJobs (WO-172 active timers) → Builder.ActiveJobs, with Kind backfilled from
        //     jobType (an Upgrade jobType becomes JobKind.Upgrade; else Build) + channel = Builder.
        //   • pendingBuilds (legacy pet-assisted tower builds) → Builder.ActiveJobs as TowerBuild
        //     jobs, remaining time preserved from FinishAt.
        //   • buildingCooldowns whose ready-at is still in the FUTURE → Builder.ActiveJobs as Build
        //     jobs, remaining time preserved (expired cooldowns carry no in-flight work → skipped).
        // After folding, the legacy lists are CLEARED (buildJobs/pendingBuilds emptied, folded
        // cooldown keys removed) so the queue is the single source of truth going forward. Guarded
        // against id collision so a job can never be double-created. In THIS tree the legacy fields
        // are empty in practice (no runtime writer), so the common case is a clean seed of an empty
        // three-channel queue; the fold is the defensive no-loss path for any save that carried them.
        // Additive + idempotent (only builds the queue when absent).
        private static PersistedState MigrateToV35(PersistedState s)
        {
            var q = s.ObsidianQueue ?? new ObsidianQueueState();
            var builder = q.Channel(ChannelId.Builder);
            double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 1) buildJobs → Builder.ActiveJobs (Kind backfill + channel stamp).
            if (s.BuildJobs != null && s.BuildJobs.Count > 0)
            {
                foreach (var raw in s.BuildJobs)
                {
                    var job = raw;
                    if (job.Kind == 0)
                        job.Kind = job.JobType == (int)BuildJobType.Upgrade
                            ? (int)JobKind.Upgrade : (int)JobKind.Build;
                    job.Channel = (int)ChannelId.Builder;
                    if (!FoldGuardHasId(builder, job.StructureId))
                        builder.ActiveJobs.Add(job);
                }
                s.BuildJobs = new List<BuildJobData>();   // folded into the channel — cleared.
            }

            // 2) pendingBuilds → Builder.ActiveJobs as TowerBuild jobs (remaining time preserved).
            if (s.PendingBuilds != null && s.PendingBuilds.Count > 0)
            {
                foreach (var pb in s.PendingBuilds)
                {
                    string id = $"legacy-tower-{pb.Slot}";
                    if (FoldGuardHasId(builder, id)) continue;
                    double rem = Math.Max(0.0, pb.FinishAt - now);
                    builder.ActiveJobs.Add(new BuildJobData
                    {
                        StructureId = id,
                        JobType = (int)BuildJobType.Build,
                        Kind = (int)JobKind.TowerBuild,
                        Channel = (int)ChannelId.Builder,
                        StartMs = now,
                        DurationMs = rem,
                        TargetTier = 0,
                    });
                }
                s.PendingBuilds = new List<PendingTowerBuild>();   // folded — cleared.
            }

            // 3) future-dated buildingCooldowns → Builder.ActiveJobs as Build jobs.
            if (s.BuildingCooldowns != null && s.BuildingCooldowns.Count > 0)
            {
                var keys = new List<string>(s.BuildingCooldowns.Keys);
                foreach (var k in keys)
                {
                    double readyAt = s.BuildingCooldowns[k];
                    if (readyAt <= now) continue;             // expired — nothing in flight.
                    if (FoldGuardHasId(builder, k)) continue; // already an active job for this id.
                    builder.ActiveJobs.Add(new BuildJobData
                    {
                        StructureId = k,
                        JobType = (int)BuildJobType.Build,
                        Kind = (int)JobKind.Build,
                        Channel = (int)ChannelId.Builder,
                        StartMs = now,
                        DurationMs = readyAt - now,
                        TargetTier = 0,
                    });
                    s.BuildingCooldowns.Remove(k);            // folded — key removed.
                }
            }

            // Ensure the other channels exist (empty) so the shape is complete.
            q.Channel(ChannelId.Train);
            q.Channel(ChannelId.Research);
            s.ObsidianQueue = q;
            return s;
        }

        /// <summary>True if the Builder channel already holds a job (active or pending) for
        /// <paramref name="id"/> — the fold guard so a job is never double-created.</summary>
        private static bool FoldGuardHasId(ChannelState ch, string id)
        {
            if (ch == null || string.IsNullOrEmpty(id)) return false;
            if (ch.ActiveJobs != null)
                for (int i = 0; i < ch.ActiveJobs.Count; i++)
                    if (ch.ActiveJobs[i].StructureId == id) return true;
            if (ch.PendingQueue != null)
                for (int i = 0; i < ch.PendingQueue.Count; i++)
                    if (ch.PendingQueue[i].StructureId == id) return true;
            return false;
        }

        /// <summary>
        /// The shape of the legacy 'realm-defenders-settings' standalone store
        /// (former useGameSettings) — read once by the v8→v9 step.
        /// </summary>
        [Serializable]
        private sealed class LegacySettings
        {
            [Newtonsoft.Json.JsonProperty("muted")] public bool? Muted;
            [Newtonsoft.Json.JsonProperty("musicVolume")] public double? MusicVolume;
            [Newtonsoft.Json.JsonProperty("sfxVolume")] public double? SfxVolume;
            [Newtonsoft.Json.JsonProperty("difficulty")] public Difficulty? Difficulty;
            [Newtonsoft.Json.JsonProperty("voiceOvers")] public bool? VoiceOvers;
        }
    }

    /// <summary>The result of <see cref="SaveMigrator.MigrateForImport"/>.</summary>
    public sealed class SaveMigrationResult
    {
        /// <summary>True when migration succeeded.</summary>
        public bool Ok { get; private set; }
        /// <summary>The migrated payload (only when <see cref="Ok"/>).</summary>
        public SaveSchema.PersistedState Data { get; private set; }
        /// <summary>The rejection reason (only when not <see cref="Ok"/>).</summary>
        public string Reason { get; private set; }

        public static SaveMigrationResult Success(SaveSchema.PersistedState data)
            => new SaveMigrationResult { Ok = true, Data = data };

        public static SaveMigrationResult Failure(string reason)
            => new SaveMigrationResult { Ok = false, Reason = reason };
    }
}
