// =============================================================================
// Core State — Save/Load round-trip tests (EditMode)
// -----------------------------------------------------------------------------
// The spec's Week-1 acceptance criterion (core-state-port.md §6):
//   "launch → New Game → quit → relaunch → save restored".
//
// Each test mutates the live GameState SO, calls Save() (writes the SaveFile
// envelope to PlayerPrefs 'dotr-save'), then SIMULATES A RESTART — discards the
// in-memory SO + service, spawns a fresh service, and calls Load() to rehydrate
// from PlayerPrefs. All 41 persisted fields must come back byte-for-byte,
// including the nested shapes and the dictionaries.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Core.Tests
{
    [TestFixture]
    public class SaveLoadRoundTripTest
    {
        private GameStateService _service;

        [SetUp]
        public void SetUp()
        {
            TestSupport.ClearSave();
        }

        [TearDown]
        public void TearDown()
        {
            TestSupport.DestroyService(_service);
            _service = null;
            TestSupport.ClearSave();
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>
        /// Simulates a quit + relaunch: destroys the current service and SO,
        /// spawns a fresh service, and Loads from PlayerPrefs.
        /// </summary>
        private GameState RestartAndLoad(out bool loaded)
        {
            TestSupport.DestroyService(_service);
            _service = TestSupport.SpawnService(out var fresh);
            loaded = _service.Load();
            return fresh;
        }

        // =====================================================================
        //  Round-trip — all 41 persisted fields
        // =====================================================================

        [Test]
        public void save_then_relaunch_restores_a_fully_populated_state()
        {
            // ── Author a rich save through the service's own Snapshot path ──
            _service = TestSupport.SpawnService(out var state);
            ApplyRichStateOntoSO(state, TestSupport.MakeRichState());
            _service.Save();
            Assert.That(PlayerPrefs.HasKey(SaveSchema.PlayerPrefsKey), Is.True,
                "Save() must write the dotr-save PlayerPrefs key.");

            // ── Quit + relaunch ─────────────────────────────────────────────
            var reloaded = RestartAndLoad(out var loaded);
            Assert.That(loaded, Is.True, "Load() must report an existing save was restored.");

            var expected = TestSupport.MakeRichState();
            AssertAll41FieldsMatch(expected, reloaded);
        }

        [Test]
        public void load_reports_false_when_no_save_exists()
        {
            _service = TestSupport.SpawnService(out _);
            Assert.That(_service.Load(), Is.False,
                "A brand-new game (no PlayerPrefs key) must Load() to false.");
        }

        [Test]
        public void fresh_game_reset_then_relaunch_restores_starter_state()
        {
            // launch → New Game → quit → relaunch (the literal Week-1 path).
            _service = TestSupport.SpawnService(out _);
            _service.ResetToNewGame();

            var reloaded = RestartAndLoad(out var loaded);
            Assert.That(loaded, Is.True);
            Assert.That(reloaded.Onboarded, Is.False);
            Assert.That(reloaded.BestWave, Is.EqualTo(0));
            Assert.That(reloaded.Resources.Crystals, Is.EqualTo(250));
            Assert.That(reloaded.Resources.Food, Is.EqualTo(80));
            Assert.That(reloaded.Resources.Coins, Is.EqualTo(15));
            Assert.That(reloaded.Voidshards, Is.EqualTo(5));
            Assert.That(reloaded.Stone, Is.EqualTo(20));
            // WO-682: strategic placement is always on — New Game seeds the core-kit budget.
            Assert.That(reloaded.Iron, Is.EqualTo(StartingBudget.StrategicIron));
            Assert.That(reloaded.Wood, Is.EqualTo(StartingBudget.StrategicWood));
            Assert.That(reloaded.TutorialStep, Is.EqualTo(TutorialStep.Step1));
            Assert.That(reloaded.SchemaVersion, Is.EqualTo(SaveSchema.CurrentVersion));
        }

        [Test]
        public void mutators_persist_immediately_and_survive_a_relaunch()
        {
            _service = TestSupport.SpawnService(out _);
            _service.FinishOnboarding();
            _service.RecordRun(13);
            _service.ChooseHero(HeroClass.Knight);
            _service.SetMuted(false);
            _service.SetDifficulty(Difficulty.Hard);
            _service.AdvanceTutorial(); // Step1 -> Step2

            var reloaded = RestartAndLoad(out var loaded);
            Assert.That(loaded, Is.True);
            Assert.That(reloaded.Onboarded, Is.True);
            Assert.That(reloaded.BestWave, Is.EqualTo(13));
            Assert.That(reloaded.HeroClass, Is.EqualTo(HeroClassOpt.Knight));
            Assert.That(reloaded.Muted, Is.False);
            Assert.That(reloaded.Difficulty, Is.EqualTo(Difficulty.Hard));
            Assert.That(reloaded.TutorialStep, Is.EqualTo(TutorialStep.Step2));
        }

        [Test]
        public void tutorial_done_round_trips_as_the_string_done()
        {
            // TutorialStep.Done serializes as "done", not the int 99.
            _service = TestSupport.SpawnService(out var state);
            state.TutorialStep = TutorialStep.Done;
            _service.Save();

            var json = PlayerPrefs.GetString(SaveSchema.PlayerPrefsKey);
            Assert.That(json, Does.Contain("\"tutorialStep\":\"done\""),
                "Done must serialize as the literal string \"done\".");
            Assert.That(json, Does.Not.Contain("\"tutorialStep\":99"));

            var reloaded = RestartAndLoad(out _);
            Assert.That(reloaded.TutorialStep, Is.EqualTo(TutorialStep.Done));
        }

        [Test]
        public void numeric_tutorial_step_round_trips_as_a_raw_number()
        {
            _service = TestSupport.SpawnService(out var state);
            state.TutorialStep = TutorialStep.Step4;
            _service.Save();

            var json = PlayerPrefs.GetString(SaveSchema.PlayerPrefsKey);
            Assert.That(json, Does.Contain("\"tutorialStep\":4"),
                "Steps 1..7 must serialize as raw numbers.");

            var reloaded = RestartAndLoad(out _);
            Assert.That(reloaded.TutorialStep, Is.EqualTo(TutorialStep.Step4));
        }

        [Test]
        public void string_enums_round_trip_through_their_kebab_wire_values()
        {
            _service = TestSupport.SpawnService(out var state);
            state.MovementStyle = MovementStyle.Tap;
            state.BreachStyle = BreachStyle.TowerSim;
            state.Difficulty = Difficulty.Easy;
            _service.Save();

            var json = PlayerPrefs.GetString(SaveSchema.PlayerPrefsKey);
            Assert.That(json, Does.Contain("\"breachStyle\":\"tower-sim\""),
                "BreachStyle.TowerSim must serialize to the kebab string.");
            Assert.That(json, Does.Contain("\"movementStyle\":\"tap\""));
            Assert.That(json, Does.Contain("\"difficulty\":\"easy\""));

            var reloaded = RestartAndLoad(out _);
            Assert.That(reloaded.MovementStyle, Is.EqualTo(MovementStyle.Tap));
            Assert.That(reloaded.BreachStyle, Is.EqualTo(BreachStyle.TowerSim));
            Assert.That(reloaded.Difficulty, Is.EqualTo(Difficulty.Easy));
        }

        [Test]
        public void null_hero_class_round_trips_as_json_null()
        {
            _service = TestSupport.SpawnService(out var state);
            state.HeroClass = HeroClassOpt.None;
            _service.Save();

            var reloaded = RestartAndLoad(out _);
            Assert.That(reloaded.HeroClass, Is.EqualTo(HeroClassOpt.None),
                "An unset hero class must survive a round-trip as None.");
        }

        [Test]
        public void empty_save_envelope_keeps_fresh_defaults()
        {
            // A save whose 'state' payload is null must not crash Load().
            PlayerPrefs.SetString(SaveSchema.PlayerPrefsKey, "");
            PlayerPrefs.Save();

            _service = TestSupport.SpawnService(out var state);
            Assert.That(_service.Load(), Is.False);
            Assert.That(state.Voidshards, Is.EqualTo(5),
                "An empty save must leave the fresh SO defaults intact.");
        }

        // =====================================================================
        //  Field-by-field assertions
        // =====================================================================

        /// <summary>
        /// Verifies every one of the 41 persisted fields survived the round-trip.
        /// Grouped to mirror core-state-port.md §1.3's numbered table.
        /// </summary>
        private static void AssertAll41FieldsMatch(SaveSchema.PersistedState e, GameState a)
        {
            // #1 pets
            Assert.That(a.Pets.Count, Is.EqualTo(e.Pets.Count), "#1 pets count");
            for (var i = 0; i < e.Pets.Count; i++)
            {
                var ep = e.Pets[i];
                var ap = a.Pets[i];
                Assert.That(ap.Id, Is.EqualTo(ep.Id), $"#1 pets[{i}].id");
                Assert.That(ap.OwnerId, Is.EqualTo(ep.OwnerId), $"#1 pets[{i}].ownerId");
                Assert.That(ap.Species, Is.EqualTo(ep.Species), $"#1 pets[{i}].species");
                Assert.That(ap.Nickname, Is.EqualTo(ep.Nickname), $"#1 pets[{i}].nickname");
                Assert.That(ap.Level, Is.EqualTo(ep.Level), $"#1 pets[{i}].level");
                Assert.That(ap.Xp, Is.EqualTo(ep.Xp), $"#1 pets[{i}].xp");
                Assert.That(ap.UnlockedSkillIds, Is.EqualTo(ep.UnlockedSkillIds),
                    $"#1 pets[{i}].unlockedSkillIds");
                Assert.That(ap.EquippedActiveIds, Is.EqualTo(ep.EquippedActiveIds),
                    $"#1 pets[{i}].equippedActiveIds");
            }

            // #2..#4 player
            Assert.That(a.StarterPetId, Is.EqualTo(e.StarterPetId), "#2 starterPetId");
            Assert.That(a.Onboarded, Is.EqualTo(e.Onboarded.Value), "#3 onboarded");
            Assert.That(a.BestWave, Is.EqualTo((int)e.BestWave.Value), "#4 bestWave");

            // #5 resources
            Assert.That(a.Resources.Crystals, Is.EqualTo(e.Resources.Value.Crystals),
                "#5 resources.crystals");
            Assert.That(a.Resources.Food, Is.EqualTo(e.Resources.Value.Food), "#5 resources.food");
            Assert.That(a.Resources.Coins, Is.EqualTo(e.Resources.Value.Coins), "#5 resources.coins");

            // #6..#8
            Assert.That(a.OwnedItemIds, Is.EqualTo(e.OwnedItemIds), "#6 ownedItemIds");
            Assert.That(a.PetBonds, Is.EqualTo(new List<int> { 4, 2, 1 }), "#7 petBonds");
            Assert.That(a.Voidshards, Is.EqualTo((int)e.Voidshards.Value), "#8 voidshards");

            // #9..#11 village
            Assert.That(a.Towers, Is.EqualTo(new List<int> { 2, 0, 1, 3, 0, 0, 0, 0, 0 }), "#9 towers");
            Assert.That(a.TowerAbilities, Is.EqualTo(new List<int> { 1, 0, 2, 0, 0, 0, 0, 0, 0 }),
                "#10 towerAbilities");
            Assert.That(a.WallLevel, Is.EqualTo((int)e.WallLevel.Value), "#11 wallLevel");

            // #12..#14
            Assert.That(a.Stone, Is.EqualTo((int)e.Stone.Value), "#12 stone");
            Assert.That(a.Iron, Is.EqualTo((int)e.Iron.Value), "#13 iron");
            Assert.That(a.Wood, Is.EqualTo((int)e.Wood.Value), "#14 wood");

            // #15 buildingCooldowns (dictionary)
            AssertDictEqual(e.BuildingCooldowns, a.BuildingCooldowns, "#15 buildingCooldowns");

            // #16 pendingBuilds
            Assert.That(a.PendingBuilds.Count, Is.EqualTo(e.PendingBuilds.Count),
                "#16 pendingBuilds count");
            for (var i = 0; i < e.PendingBuilds.Count; i++)
            {
                Assert.That(a.PendingBuilds[i].Slot, Is.EqualTo(e.PendingBuilds[i].Slot),
                    $"#16 pendingBuilds[{i}].slot");
                Assert.That(a.PendingBuilds[i].Ability, Is.EqualTo(e.PendingBuilds[i].Ability),
                    $"#16 pendingBuilds[{i}].ability");
                Assert.That(a.PendingBuilds[i].FinishAt, Is.EqualTo(e.PendingBuilds[i].FinishAt),
                    $"#16 pendingBuilds[{i}].finishAt");
            }

            // #17 tutorialStep
            Assert.That(a.TutorialStep, Is.EqualTo(e.TutorialStep.Value), "#17 tutorialStep");

            // #18..#24 settings
            Assert.That(a.JoystickSensitivity,
                Is.EqualTo((float)e.JoystickSensitivity.Value).Within(1e-4),
                "#18 joystickSensitivity");
            Assert.That(a.MovementStyle, Is.EqualTo(e.MovementStyle.Value), "#19 movementStyle");
            Assert.That(a.Muted, Is.EqualTo(e.Muted.Value), "#20 muted");
            Assert.That(a.MusicVolume, Is.EqualTo((float)e.MusicVolume.Value).Within(1e-4),
                "#21 musicVolume");
            Assert.That(a.SfxVolume, Is.EqualTo((float)e.SfxVolume.Value).Within(1e-4),
                "#22 sfxVolume");
            Assert.That(a.Difficulty, Is.EqualTo(e.Difficulty.Value), "#23 difficulty");
            Assert.That(a.VoiceOvers, Is.EqualTo(e.VoiceOvers.Value), "#24 voiceOvers");

            // #25 ownedPets
            Assert.That(a.OwnedPets, Is.EqualTo(e.OwnedPets), "#25 ownedPets");

            // #26 seenTutorials (dictionary)
            AssertDictEqual(e.SeenTutorials, a.SeenTutorials, "#26 seenTutorials");

            // #27..#28
            Assert.That(a.BoundWallet, Is.EqualTo(e.BoundWallet), "#27 boundWallet");
            Assert.That(a.HeroClass, Is.EqualTo(HeroClassOpt.Ranger), "#28 heroClass");

            // #29 inventory
            Assert.That(a.Inventory.Potions, Is.EqualTo(e.Inventory.Value.Potions),
                "#29 inventory.potions");
            Assert.That(a.Inventory.ManaCrystals, Is.EqualTo(e.Inventory.Value.ManaCrystals),
                "#29 inventory.manaCrystals");
            Assert.That(a.Inventory.Cleanses, Is.EqualTo(e.Inventory.Value.Cleanses),
                "#29 inventory.cleanses");
            Assert.That(a.Inventory.Torches, Is.EqualTo(e.Inventory.Value.Torches),
                "#29 inventory.torches (optional)");

            // #30..#31
            Assert.That(a.AtbLossStreak, Is.EqualTo((int)e.AtbLossStreak.Value), "#30 atbLossStreak");
            Assert.That(a.BreachStyle, Is.EqualTo(e.BreachStyle.Value), "#31 breachStyle");

            // #32 buildingDamage (dictionary)
            AssertDictEqual(e.BuildingDamage, a.BuildingDamage, "#32 buildingDamage");

            // #33 dungeons (nested ledger)
            AssertDictEqual(e.Dungeons.Discovered, a.Dungeons.Discovered, "#33 dungeons.discovered");
            AssertDictEqual(e.Dungeons.Cleared, a.Dungeons.Cleared, "#33 dungeons.cleared");
            AssertDictEqual(e.Dungeons.BestTime, a.Dungeons.BestTime, "#33 dungeons.bestTime");
            AssertDictEqual(e.Dungeons.NoHitClear, a.Dungeons.NoHitClear, "#33 dungeons.noHitClear");
            AssertDictEqual(e.Dungeons.DeathsByDungeon, a.Dungeons.DeathsByDungeon,
                "#33 dungeons.deathsByDungeon (optional)");
            Assert.That(a.Dungeons.LoreReadByDungeon, Is.Not.Null,
                "#33 dungeons.loreReadByDungeon (optional)");
            Assert.That(a.Dungeons.LoreReadByDungeon["healers_cottage"]["lore-1"], Is.True,
                "#33 dungeons.loreReadByDungeon nested value");

            // #34 activeDungeonRun (nested)
            Assert.That(a.ActiveDungeonRun, Is.Not.Null, "#34 activeDungeonRun");
            Assert.That(a.ActiveDungeonRun.DungeonId, Is.EqualTo(e.ActiveDungeonRun.DungeonId),
                "#34 activeDungeonRun.dungeonId");
            Assert.That(a.ActiveDungeonRun.AvatarNodeId, Is.EqualTo(e.ActiveDungeonRun.AvatarNodeId),
                "#34 activeDungeonRun.avatarNodeId");
            Assert.That(a.ActiveDungeonRun.VisitedNodes, Is.EqualTo(e.ActiveDungeonRun.VisitedNodes),
                "#34 activeDungeonRun.visitedNodes");
            Assert.That(a.ActiveDungeonRun.ClearedEncounters,
                Is.EqualTo(e.ActiveDungeonRun.ClearedEncounters),
                "#34 activeDungeonRun.clearedEncounters");
            Assert.That(a.ActiveDungeonRun.OpenedChests, Is.EqualTo(e.ActiveDungeonRun.OpenedChests),
                "#34 activeDungeonRun.openedChests");
            Assert.That(a.ActiveDungeonRun.ReadLore, Is.EqualTo(e.ActiveDungeonRun.ReadLore),
                "#34 activeDungeonRun.readLore");
            Assert.That(a.ActiveDungeonRun.StartedAt, Is.EqualTo(e.ActiveDungeonRun.StartedAt),
                "#34 activeDungeonRun.startedAt");
            Assert.That(a.ActiveDungeonRun.Loot.Crystals, Is.EqualTo(e.ActiveDungeonRun.Loot.Crystals),
                "#34 activeDungeonRun.loot.crystals");
            Assert.That(a.ActiveDungeonRun.Loot.Wood, Is.EqualTo(e.ActiveDungeonRun.Loot.Wood),
                "#34 activeDungeonRun.loot.wood");
            AssertDictEqual(e.ActiveDungeonRun.Loot.PetBondShards,
                a.ActiveDungeonRun.Loot.PetBondShards, "#34 activeDungeonRun.loot.petBondShards");
            AssertDictEqual(e.ActiveDungeonRun.Loot.SkillPoints,
                a.ActiveDungeonRun.Loot.SkillPoints, "#34 activeDungeonRun.loot.skillPoints");

            // #35 quests (nested ledger)
            AssertDictEqual(e.Quests.Completed, a.Quests.Completed, "#35 quests.completed");
            AssertDictEqual(e.Quests.Available, a.Quests.Available, "#35 quests.available");
            Assert.That(a.Quests.Active.ContainsKey("quest-1"), Is.True, "#35 quests.active key");
            Assert.That(a.Quests.Active["quest-1"].BeatIndex, Is.EqualTo(2),
                "#35 quests.active.quest-1.beatIndex");
            Assert.That(a.Quests.Active["quest-1"].Flags["met-healer"], Is.True,
                "#35 quests.active.quest-1.flags");

            // #36 regions
            AssertDictEqual(e.Regions.Discovered, a.Regions.Discovered, "#36 regions.discovered");
            AssertDictEqual(e.Regions.Cleared, a.Regions.Cleared, "#36 regions.cleared");

            // #37 myInviteCode
            Assert.That(a.MyInviteCode, Is.EqualTo(e.MyInviteCode), "#37 myInviteCode");

            // #38 contacts
            Assert.That(a.Contacts.Count, Is.EqualTo(e.Contacts.Count), "#38 contacts count");
            for (var i = 0; i < e.Contacts.Count; i++)
            {
                Assert.That(a.Contacts[i].Code, Is.EqualTo(e.Contacts[i].Code),
                    $"#38 contacts[{i}].code");
                Assert.That(a.Contacts[i].Nickname, Is.EqualTo(e.Contacts[i].Nickname),
                    $"#38 contacts[{i}].nickname");
            }

            // #38a blockedCodes
            Assert.That(a.BlockedCodes, Is.EqualTo(e.BlockedCodes), "#38a blockedCodes");

            // #38b inbox
            Assert.That(a.Inbox.Count, Is.EqualTo(e.Inbox.Count), "#38b inbox count");
            for (var i = 0; i < e.Inbox.Count; i++)
            {
                Assert.That(a.Inbox[i].Id, Is.EqualTo(e.Inbox[i].Id), $"#38b inbox[{i}].id");
                Assert.That(a.Inbox[i].SenderCode, Is.EqualTo(e.Inbox[i].SenderCode),
                    $"#38b inbox[{i}].senderCode");
                Assert.That(a.Inbox[i].RecipientCode, Is.EqualTo(e.Inbox[i].RecipientCode),
                    $"#38b inbox[{i}].recipientCode");
                Assert.That(a.Inbox[i].PhraseId, Is.EqualTo(e.Inbox[i].PhraseId),
                    $"#38b inbox[{i}].phraseId");
                Assert.That(a.Inbox[i].SentAt, Is.EqualTo(e.Inbox[i].SentAt),
                    $"#38b inbox[{i}].sentAt");
                Assert.That(a.Inbox[i].ReadAt, Is.EqualTo(e.Inbox[i].ReadAt),
                    $"#38b inbox[{i}].readAt");
            }

            // #38c lastInboxSyncAt
            Assert.That(a.LastInboxSyncAt, Is.EqualTo(e.LastInboxSyncAt), "#38c lastInboxSyncAt");

            // SchemaVersion is stamped current after a load.
            Assert.That(a.SchemaVersion, Is.EqualTo(SaveSchema.CurrentVersion), "schemaVersion");
        }

        private static void AssertDictEqual<TV>(
            IDictionary<string, TV> expected, IDictionary<string, TV> actual, string label)
        {
            Assert.That(actual, Is.Not.Null, $"{label} — dictionary must not be null");
            Assert.That(actual.Count, Is.EqualTo(expected.Count), $"{label} — count");
            foreach (var kv in expected)
            {
                Assert.That(actual.ContainsKey(kv.Key), Is.True, $"{label} — missing key '{kv.Key}'");
                Assert.That(actual[kv.Key], Is.EqualTo(kv.Value), $"{label}['{kv.Key}']");
            }
        }

        /// <summary>
        /// Copies a <see cref="SaveSchema.PersistedState"/> onto the live SO so a
        /// test can author a rich save through the service's own Save() path.
        /// </summary>
        private static void ApplyRichStateOntoSO(GameState s, SaveSchema.PersistedState p)
        {
            s.Pets = p.Pets;
            s.StarterPetId = p.StarterPetId;
            s.Onboarded = p.Onboarded.Value;
            s.BestWave = (int)p.BestWave.Value;
            s.Resources = p.Resources.Value;
            s.OwnedItemIds = p.OwnedItemIds;
            s.PetBonds = new List<int> { 4, 2, 1 };
            s.Voidshards = (int)p.Voidshards.Value;
            s.Towers = new List<int> { 2, 0, 1, 3, 0, 0, 0, 0, 0 };
            s.TowerAbilities = new List<int> { 1, 0, 2, 0, 0, 0, 0, 0, 0 };
            s.WallLevel = (int)p.WallLevel.Value;
            s.Stone = (int)p.Stone.Value;
            s.Iron = (int)p.Iron.Value;
            s.Wood = (int)p.Wood.Value;
            s.BuildingCooldowns = new SerializableDict<string, double>(p.BuildingCooldowns);
            s.PendingBuilds = p.PendingBuilds;
            s.TutorialStep = p.TutorialStep.Value;
            s.JoystickSensitivity = (float)p.JoystickSensitivity.Value;
            s.MovementStyle = p.MovementStyle.Value;
            s.Muted = p.Muted.Value;
            s.MusicVolume = (float)p.MusicVolume.Value;
            s.SfxVolume = (float)p.SfxVolume.Value;
            s.Difficulty = p.Difficulty.Value;
            s.VoiceOvers = p.VoiceOvers.Value;
            s.OwnedPets = p.OwnedPets;
            s.SeenTutorials = new SerializableDict<string, bool>(p.SeenTutorials);
            s.BoundWallet = p.BoundWallet;
            s.HeroClass = p.HeroClass.ToOpt();
            s.Inventory = p.Inventory.Value;
            s.AtbLossStreak = (int)p.AtbLossStreak.Value;
            s.BreachStyle = p.BreachStyle.Value;
            s.BuildingDamage = new SerializableDict<string, double>(p.BuildingDamage);
            s.Dungeons = p.Dungeons;
            s.ActiveDungeonRun = p.ActiveDungeonRun;
            s.Quests = p.Quests;
            s.Regions = p.Regions;
            s.MyInviteCode = p.MyInviteCode;
            s.Contacts = p.Contacts;
            s.BlockedCodes = p.BlockedCodes;
            s.Inbox = p.Inbox;
            s.LastInboxSyncAt = p.LastInboxSyncAt.Value;
        }
    }
}
