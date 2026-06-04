// =============================================================================
// SaveMigrator — the persisted-save migration chain v1 → v10 (spec §2.4)
// -----------------------------------------------------------------------------
// C# port of `migratePersistedState` from src/state/gameStore.ts. One shared
// entry point used by BOTH the boot loader AND a future Save-Import path.
//
// IMPROVEMENT #3 (adopted): a registry-based migrator. Each step is a
// Dictionary<int,Func<...>> entry keyed by its TARGET version; Migrate() applies
// every entry from fromVersion+1..CurrentVersion in ascending order. Behaviour
// is identical to the React nine-stacked-`if` cascade, but each step is
// independently unit-testable.
//
// Every step is ADDITIVE — it seeds new fields with empty defaults and never
// mutates data a save already carries. Three fields (inventory.torches,
// dungeons.deathsByDungeon, dungeons.loreReadByDungeon) were added WITHOUT a
// version bump — they are simply optional/default-on-read, no migration step.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.State
{
    using PersistedState = SaveSchema.PersistedState;

    /// <summary>Registry-based port of <c>migratePersistedState</c> (v1 → v10).</summary>
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
            catch (Exception)
            {
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
            catch (Exception)
            {
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
            catch (Exception)
            {
                // ignore — the legacy key is now ignored regardless.
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
