// =============================================================================
// Core State — SaveMigrator tests (EditMode)
// -----------------------------------------------------------------------------
// core-state-port.md §2.4: the nine-step migration chain v1->v2 ... v9->v10.
// Every step is ADDITIVE — it seeds new fields with empty defaults and never
// mutates data a save already carries.
//
// One test per migration step verifies a save authored at version N migrates to
// v10 with the correct seeded defaults; cumulative tests confirm an ancient
// (v1) save runs every step in order. The version-gate tests verify
// MigrateForImport rejects a save newer than this build / a non-finite version.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Core.Tests
{
    [TestFixture]
    public class SaveMigratorTest
    {
        [SetUp]
        public void SetUp()
        {
            TestSupport.ClearSave();
        }

        [TearDown]
        public void TearDown()
        {
            TestSupport.ClearSave();
        }

        // =====================================================================
        //  v1 -> v2 — seed resources + ownedItemIds
        // =====================================================================

        [Test]
        public void migrate_v1_to_v10_seeds_starter_resources_and_owned_items()
        {
            var s = new SaveSchema.PersistedState();
            var migrated = SaveMigrator.Migrate(s, 1);

            Assert.That(migrated.Resources.HasValue, Is.True, "v1->v2 seeds resources");
            Assert.That(migrated.Resources.Value.Crystals, Is.EqualTo(250));
            Assert.That(migrated.Resources.Value.Food, Is.EqualTo(80));
            Assert.That(migrated.Resources.Value.Coins, Is.EqualTo(15));
            Assert.That(migrated.OwnedItemIds, Is.Not.Null.And.Empty,
                "v1->v2 seeds ownedItemIds = []");
        }

        // =====================================================================
        //  v2 -> v3 — seed heroClass = heroClass ?? Mage
        // =====================================================================

        [Test]
        public void migrate_v2_to_v10_defaults_missing_hero_class_to_mage()
        {
            var s = new SaveSchema.PersistedState(); // no heroClass
            var migrated = SaveMigrator.Migrate(s, 2);

            Assert.That(migrated.HeroClass.HasValue, Is.True, "v2->v3 seeds heroClass");
            Assert.That(migrated.HeroClass.Value, Is.EqualTo(HeroClass.Mage),
                "pre-hero-select saves default to Mage");
        }

        [Test]
        public void migrate_v2_to_v10_keeps_an_existing_hero_class()
        {
            var s = new SaveSchema.PersistedState { HeroClass = HeroClass.Ranger };
            var migrated = SaveMigrator.Migrate(s, 2);

            Assert.That(migrated.HeroClass.Value, Is.EqualTo(HeroClass.Ranger),
                "v2->v3 must NOT overwrite a hero class the save already carries.");
        }

        // =====================================================================
        //  v3 -> v4 — seed wood, buildingCooldowns, tutorialStep = done
        // =====================================================================

        [Test]
        public void migrate_v3_to_v10_seeds_wood_cooldowns_and_done_tutorial()
        {
            var s = new SaveSchema.PersistedState();
            var migrated = SaveMigrator.Migrate(s, 3);

            Assert.That(migrated.Wood.HasValue, Is.True, "v3->v4 seeds wood");
            Assert.That((int)migrated.Wood.Value, Is.EqualTo(15));
            Assert.That(migrated.BuildingCooldowns, Is.Not.Null.And.Empty,
                "v3->v4 seeds buildingCooldowns = {}");
            Assert.That(migrated.TutorialStep.HasValue, Is.True);
            Assert.That(migrated.TutorialStep.Value, Is.EqualTo(TutorialStep.Done),
                "an in-progress (v3) save skips the first-time tutorial");
        }

        // =====================================================================
        //  v4 -> v5 — seed towerAbilities = [0]x9
        // =====================================================================

        [Test]
        public void migrate_v4_to_v10_seeds_nine_zero_tower_abilities()
        {
            var s = new SaveSchema.PersistedState();
            var migrated = SaveMigrator.Migrate(s, 4);

            Assert.That(migrated.TowerAbilities, Is.Not.Null, "v4->v5 seeds towerAbilities");
            Assert.That(migrated.TowerAbilities.Count, Is.EqualTo(Constants.TowerSlots),
                "towerAbilities length = TOWER_SLOTS (9)");
            foreach (var v in migrated.TowerAbilities)
                Assert.That(v, Is.EqualTo(0.0), "every seeded tower ability is 0");
        }

        // =====================================================================
        //  v5 -> v6 — seed the whole ATB + dungeon block
        // =====================================================================

        [Test]
        public void migrate_v5_to_v10_seeds_the_atb_and_dungeon_block()
        {
            var s = new SaveSchema.PersistedState();
            var migrated = SaveMigrator.Migrate(s, 5);

            Assert.That(migrated.Inventory.HasValue, Is.True, "v5->v6 seeds inventory");
            Assert.That(migrated.Inventory.Value.Potions, Is.EqualTo(0));
            Assert.That(migrated.Inventory.Value.ManaCrystals, Is.EqualTo(0));
            Assert.That(migrated.Inventory.Value.Cleanses, Is.EqualTo(0));
            Assert.That(migrated.AtbLossStreak.HasValue, Is.True, "v5->v6 seeds atbLossStreak");
            Assert.That((int)migrated.AtbLossStreak.Value, Is.EqualTo(0));
            Assert.That(migrated.BreachStyle.HasValue, Is.True, "v5->v6 seeds breachStyle");
            Assert.That(migrated.BreachStyle.Value, Is.EqualTo(BreachStyle.Ask));
            Assert.That(migrated.BuildingDamage, Is.Not.Null.And.Empty,
                "v5->v6 seeds buildingDamage = {}");
            Assert.That(migrated.Dungeons, Is.Not.Null, "v5->v6 seeds dungeons");
            Assert.That(migrated.Quests, Is.Not.Null, "v5->v6 seeds quests");
            Assert.That(migrated.ActiveDungeonRun, Is.Null, "activeDungeonRun stays null");
        }

        // =====================================================================
        //  v6 -> v7 — merge the starter dungeon into dungeons.discovered
        // =====================================================================

        [Test]
        public void migrate_v6_to_v10_discovers_the_starter_dungeon()
        {
            var s = new SaveSchema.PersistedState
            {
                Dungeons = new DungeonProgress
                {
                    Discovered = new Dictionary<string, bool>(),
                    Cleared = new Dictionary<string, int>(),
                    BestTime = new Dictionary<string, double>(),
                    NoHitClear = new Dictionary<string, bool>(),
                },
            };
            var migrated = SaveMigrator.Migrate(s, 6);

            Assert.That(
                migrated.Dungeons.Discovered.ContainsKey(SaveSchema.StarterDungeonId), Is.True,
                "v6->v7 merges healers_cottage into dungeons.discovered");
            Assert.That(migrated.Dungeons.Discovered[SaveSchema.StarterDungeonId], Is.True);
        }

        [Test]
        public void migrate_v6_to_v10_preserves_existing_discovered_dungeons()
        {
            var s = new SaveSchema.PersistedState
            {
                Dungeons = new DungeonProgress
                {
                    Discovered = new Dictionary<string, bool> { { "crystal_caverns", true } },
                    Cleared = new Dictionary<string, int>(),
                    BestTime = new Dictionary<string, double>(),
                    NoHitClear = new Dictionary<string, bool>(),
                },
            };
            var migrated = SaveMigrator.Migrate(s, 6);

            Assert.That(migrated.Dungeons.Discovered.ContainsKey("crystal_caverns"), Is.True,
                "v6->v7 merge is non-destructive");
            Assert.That(
                migrated.Dungeons.Discovered.ContainsKey(SaveSchema.StarterDungeonId), Is.True);
        }

        // =====================================================================
        //  v7 -> v8 — gate-0 -> gate-2 rename
        // =====================================================================

        [Test]
        public void migrate_v7_to_v10_renames_gate0_damage_to_gate2()
        {
            var s = new SaveSchema.PersistedState
            {
                BuildingDamage = new Dictionary<string, double> { { "gate-0", 40 } },
            };
            var migrated = SaveMigrator.Migrate(s, 7);

            Assert.That(migrated.BuildingDamage.ContainsKey("gate-0"), Is.False,
                "v7->v8 deletes the orphan gate-0 key");
            Assert.That(migrated.BuildingDamage.ContainsKey("gate-2"), Is.True,
                "v7->v8 copies the value to gate-2");
            Assert.That(migrated.BuildingDamage["gate-2"], Is.EqualTo(40.0));
        }

        [Test]
        public void migrate_v7_to_v10_is_a_no_op_when_no_gate0_key_exists()
        {
            var s = new SaveSchema.PersistedState
            {
                BuildingDamage = new Dictionary<string, double> { { "heart", 12 } },
            };
            var migrated = SaveMigrator.Migrate(s, 7);

            Assert.That(migrated.BuildingDamage.ContainsKey("gate-2"), Is.False,
                "v7->v8 must not invent a gate-2 key when there was no gate-0");
            Assert.That(migrated.BuildingDamage["heart"], Is.EqualTo(12.0));
        }

        // =====================================================================
        //  v8 -> v9 — seed pendingBuilds + migrate legacy audio settings
        // =====================================================================

        [Test]
        public void migrate_v8_to_v10_seeds_pending_builds_and_audio_defaults()
        {
            // No legacy 'realm-defenders-settings' key — fall back to defaults.
            var s = new SaveSchema.PersistedState();
            var migrated = SaveMigrator.Migrate(s, 8);

            Assert.That(migrated.PendingBuilds, Is.Not.Null.And.Empty,
                "v8->v9 seeds pendingBuilds = []");
            Assert.That(migrated.Muted.HasValue, Is.True);
            Assert.That(migrated.Muted.Value, Is.False,
                "v8 players fall back to muted=false (only new players are muted-by-default)");
            Assert.That((int)migrated.MusicVolume.Value, Is.EqualTo(70), "music default 70");
            Assert.That((int)migrated.SfxVolume.Value, Is.EqualTo(80), "sfx default 80");
            Assert.That(migrated.Difficulty.Value, Is.EqualTo(Difficulty.Normal),
                "difficulty default normal");
            Assert.That(migrated.VoiceOvers.Value, Is.False, "voiceOvers default false");
        }

        [Test]
        public void migrate_v8_to_v10_reads_audio_prefs_from_the_legacy_store()
        {
            // Seed the legacy standalone settings store the v8->v9 step migrates.
            const string legacyJson =
                "{\"muted\":true,\"musicVolume\":33,\"sfxVolume\":44," +
                "\"difficulty\":\"hard\",\"voiceOvers\":true}";
            PlayerPrefs.SetString(SaveSchema.LegacySettingsKey, legacyJson);
            PlayerPrefs.Save();

            var migrated = SaveMigrator.Migrate(new SaveSchema.PersistedState(), 8);

            Assert.That(migrated.Muted.Value, Is.True, "v8->v9 reads muted from legacy store");
            Assert.That((int)migrated.MusicVolume.Value, Is.EqualTo(33));
            Assert.That((int)migrated.SfxVolume.Value, Is.EqualTo(44));
            Assert.That(migrated.Difficulty.Value, Is.EqualTo(Difficulty.Hard));
            Assert.That(migrated.VoiceOvers.Value, Is.True);

            Assert.That(PlayerPrefs.HasKey(SaveSchema.LegacySettingsKey), Is.False,
                "v8->v9 must DELETE the legacy 'realm-defenders-settings' key.");
        }

        [Test]
        public void migrate_v8_to_v10_prefers_state_value_over_legacy_store()
        {
            PlayerPrefs.SetString(SaveSchema.LegacySettingsKey,
                "{\"musicVolume\":10}");
            PlayerPrefs.Save();

            var s = new SaveSchema.PersistedState { MusicVolume = 90 };
            var migrated = SaveMigrator.Migrate(s, 8);

            Assert.That((int)migrated.MusicVolume.Value, Is.EqualTo(90),
                "state.<f> ?? legacy.<f> — the state value wins.");
        }

        // =====================================================================
        //  v9 -> v10 — seed the Realm Map region progress
        // =====================================================================

        [Test]
        public void migrate_v9_to_v10_seeds_empty_region_progress()
        {
            var s = new SaveSchema.PersistedState();
            var migrated = SaveMigrator.Migrate(s, 9);

            Assert.That(migrated.Regions, Is.Not.Null, "v9->v10 seeds regions");
            Assert.That(migrated.Regions.Discovered, Is.Not.Null.And.Empty,
                "regions.discovered = {}");
            Assert.That(migrated.Regions.Cleared, Is.Not.Null.And.Empty, "regions.cleared = {}");
        }

        // =====================================================================
        //  Cumulative + no-op behaviour
        // =====================================================================

        [Test]
        public void migrate_from_v1_runs_every_step_in_order()
        {
            // An ancient v1 save must end up fully shaped for v10.
            var migrated = SaveMigrator.Migrate(new SaveSchema.PersistedState(), 1);

            Assert.That(migrated.Resources.HasValue, Is.True, "v2 step ran");
            Assert.That(migrated.HeroClass.HasValue, Is.True, "v3 step ran");
            Assert.That(migrated.Wood.HasValue, Is.True, "v4 step ran");
            Assert.That(migrated.TowerAbilities, Is.Not.Null, "v5 step ran");
            Assert.That(migrated.Inventory.HasValue, Is.True, "v6 step ran");
            Assert.That(migrated.Dungeons.Discovered.ContainsKey(SaveSchema.StarterDungeonId),
                Is.True, "v7 step ran");
            Assert.That(migrated.PendingBuilds, Is.Not.Null, "v9 step ran");
            Assert.That(migrated.Regions, Is.Not.Null, "v10 step ran");
        }

        [Test]
        public void migrate_at_current_version_is_a_no_op_passthrough()
        {
            var s = new SaveSchema.PersistedState { BestWave = 12 };
            var migrated = SaveMigrator.Migrate(s, SaveSchema.CurrentVersion);

            Assert.That(ReferenceEquals(migrated, s), Is.True,
                "a save already at CurrentVersion must pass straight through.");
            Assert.That((int)migrated.BestWave.Value, Is.EqualTo(12));
        }

        // =====================================================================
        //  Version gate — MigrateForImport
        // =====================================================================

        [Test]
        public void migrate_for_import_rejects_a_save_newer_than_this_build()
        {
            var s = new SaveSchema.PersistedState();
            var result = SaveMigrator.MigrateForImport(s, SaveSchema.CurrentVersion + 1);

            Assert.That(result.Ok, Is.False,
                "a save with storeVersion > CurrentVersion must be rejected.");
            Assert.That(result.Reason, Does.Contain("newer"));
        }

        [Test]
        public void migrate_for_import_rejects_a_non_finite_version()
        {
            var result = SaveMigrator.MigrateForImport(new SaveSchema.PersistedState(), double.NaN);
            Assert.That(result.Ok, Is.False, "a NaN storeVersion must be rejected.");
        }

        [Test]
        public void migrate_for_import_accepts_and_migrates_an_older_save()
        {
            var result = SaveMigrator.MigrateForImport(new SaveSchema.PersistedState(), 6);

            Assert.That(result.Ok, Is.True, "an older save must be accepted.");
            Assert.That(result.Data.Regions, Is.Not.Null,
                "an older save must come back fully migrated to v10.");
        }

        [Test]
        public void migrate_for_import_accepts_a_current_version_save_unchanged()
        {
            var s = new SaveSchema.PersistedState { BestWave = 3 };
            var result = SaveMigrator.MigrateForImport(s, SaveSchema.CurrentVersion);

            Assert.That(result.Ok, Is.True);
            Assert.That((int)result.Data.BestWave.Value, Is.EqualTo(3),
                "an equal-version save is a no-op pass-through.");
        }
    }
}
