// =============================================================================
// Core State — Reset() carve-out tests (EditMode)
// -----------------------------------------------------------------------------
// core-state-port.md §1.6: GameStateService.Reset() (the "New Game" action) wipes
// progression back to starter values but DELIBERATELY PRESERVES:
//   - boundWallet   (the save stays tagged to its wallet)
//   - breachStyle   (a player preference — survives New Game)
//   - myInviteCode / contacts / blockedCodes / inbox / lastInboxSyncAt
//                   (social identity — reset() never touches it)
//
// These tests mutate state, populate the preserved + wiped fields, call Reset(),
// and assert the carve-out: the preserved fields are untouched and every
// progression field is back at its starter value.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.State;

namespace DeNelle.Core.Tests
{
    [TestFixture]
    public class ResetCarveOutTest
    {
        private GameStateService _service;
        private GameState _state;

        [SetUp]
        public void SetUp()
        {
            TestSupport.ClearSave();
            _service = TestSupport.SpawnService(out _state);
        }

        [TearDown]
        public void TearDown()
        {
            TestSupport.DestroyService(_service);
            _service = null;
            _state = null;
            TestSupport.ClearSave();
        }

        // =====================================================================
        //  Preserved fields — the carve-out
        // =====================================================================

        [Test]
        public void reset_preserves_the_bound_wallet()
        {
            _state.BoundWallet = "BwBB9LUS3Nmxqgc41xNbGUygsUVQniv9PdngiycicjJV";
            _service.ResetToNewGame();
            Assert.That(_state.BoundWallet,
                Is.EqualTo("BwBB9LUS3Nmxqgc41xNbGUygsUVQniv9PdngiycicjJV"),
                "Reset() must NOT unbind the wallet.");
        }

        [Test]
        public void reset_preserves_the_breach_style_preference()
        {
            _state.BreachStyle = BreachStyle.TowerSim;
            _service.ResetToNewGame();
            Assert.That(_state.BreachStyle, Is.EqualTo(BreachStyle.TowerSim),
                "Reset() must NOT clear the breachStyle preference.");
        }

        [Test]
        public void reset_preserves_every_social_field()
        {
            _state.MyInviteCode = "ABC123";
            _state.Contacts = new List<ChatContact>
            {
                new ChatContact { Code = "XYZ789", Nickname = "Ally" },
            };
            _state.BlockedCodes = new List<string> { "BAD111" };
            _state.Inbox = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Id = "msg-1",
                    SenderCode = "XYZ789",
                    RecipientCode = "ABC123",
                    PhraseId = "phrase-1",
                    SentAt = 1716000000000.0,
                },
            };
            _state.LastInboxSyncAt = 1716000200000.0;

            _service.ResetToNewGame();

            Assert.That(_state.MyInviteCode, Is.EqualTo("ABC123"), "reset must keep myInviteCode");
            Assert.That(_state.Contacts.Count, Is.EqualTo(1), "reset must keep contacts");
            Assert.That(_state.Contacts[0].Code, Is.EqualTo("XYZ789"));
            Assert.That(_state.BlockedCodes, Is.EqualTo(new List<string> { "BAD111" }),
                "reset must keep blockedCodes");
            Assert.That(_state.Inbox.Count, Is.EqualTo(1), "reset must keep inbox");
            Assert.That(_state.Inbox[0].Id, Is.EqualTo("msg-1"));
            Assert.That(_state.LastInboxSyncAt, Is.EqualTo(1716000200000.0),
                "reset must keep lastInboxSyncAt");
        }

        // =====================================================================
        //  Wiped fields — progression returns to starter values
        // =====================================================================

        [Test]
        public void reset_wipes_progression_back_to_starter_values()
        {
            // Mutate every progression field away from its starter value.
            _state.Pets = new List<PetData> { new PetData { Id = "p1" } };
            _state.StarterPetId = "p1";
            _state.Onboarded = true;
            _state.BestWave = 42;
            _state.Resources = new ResourceBalance(9999, 9999, 9999);
            _state.OwnedItemIds = new List<string> { "item-1" };
            _state.PetBonds = new List<int> { 4, 4, 4 };
            _state.Voidshards = 99;
            _state.Towers = new List<int> { 3, 3, 3, 3, 3, 3, 3, 3, 3 };
            _state.TowerAbilities = new List<int> { 2, 2, 2, 2, 2, 2, 2, 2, 2 };
            _state.WallLevel = 3;
            _state.Stone = 500;
            _state.Iron = 500;
            _state.Wood = 500;
            _state.BuildingCooldowns["crystal-mine"] = 123;
            _state.PendingBuilds = new List<PendingTowerBuild>
            {
                new PendingTowerBuild { Slot = 1, Ability = 1, FinishAt = 1 },
            };
            _state.TutorialStep = TutorialStep.Done;
            _state.JoystickSensitivity = 1.5f;
            _state.SeenTutorials["firstVillageEntry"] = true;
            _state.HeroClass = HeroClassOpt.Ranger;
            _state.Inventory = new AtbInventory { Potions = 9, ManaCrystals = 9, Cleanses = 9 };
            _state.AtbLossStreak = 5;
            _state.BuildingDamage["gate-2"] = 80;
            _state.Dungeons.Cleared["healers_cottage"] = 9;
            _state.ActiveDungeonRun = new ActiveDungeonRun { DungeonId = "healers_cottage" };
            _state.Quests.Completed["quest-0"] = true;
            _state.Regions.Discovered["region-1"] = true;

            _service.ResetToNewGame();

            Assert.That(_state.Pets, Is.Empty, "pets wiped");
            Assert.That(_state.StarterPetId, Is.Null, "starterPetId wiped");
            Assert.That(_state.Onboarded, Is.False, "onboarded wiped");
            Assert.That(_state.BestWave, Is.EqualTo(0), "bestWave wiped");
            Assert.That(_state.Resources.Crystals, Is.EqualTo(250), "resources -> STARTER");
            Assert.That(_state.Resources.Food, Is.EqualTo(80));
            Assert.That(_state.Resources.Coins, Is.EqualTo(15));
            Assert.That(_state.OwnedItemIds, Is.Empty, "ownedItemIds wiped");
            Assert.That(_state.PetBonds, Is.EqualTo(new List<int> { 0, 0, 0 }), "petBonds -> [0,0,0]");
            Assert.That(_state.Voidshards, Is.EqualTo(5), "voidshards -> 5");
            Assert.That(_state.Towers, Is.EqualTo(new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
                "towers -> [0]x9");
            Assert.That(_state.TowerAbilities,
                Is.EqualTo(new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0 }), "towerAbilities -> [0]x9");
            Assert.That(_state.WallLevel, Is.EqualTo(0), "wallLevel -> 0");
            Assert.That(_state.Stone, Is.EqualTo(20), "stone -> 20");
            // WO-682: strategic placement is always on — New Game seeds the core-kit
            // budget (StartingBudget constants), not the legacy 5 iron / 15 wood.
            Assert.That(_state.Iron, Is.EqualTo(StartingBudget.StrategicIron), "iron -> core-kit seed");
            Assert.That(_state.Wood, Is.EqualTo(StartingBudget.StrategicWood), "wood -> core-kit seed");
            Assert.That(_state.BuildingCooldowns, Is.Empty, "buildingCooldowns wiped");
            Assert.That(_state.PendingBuilds, Is.Empty, "pendingBuilds wiped");
            Assert.That(_state.TutorialStep, Is.EqualTo(TutorialStep.Step1), "tutorialStep -> Step1");
            Assert.That(_state.JoystickSensitivity, Is.EqualTo(1f).Within(1e-4),
                "joystickSensitivity -> 1");
            Assert.That(_state.SeenTutorials, Is.Empty, "seenTutorials wiped");
            Assert.That(_state.HeroClass, Is.EqualTo(HeroClassOpt.None), "heroClass wiped");
            Assert.That(_state.Inventory.Potions, Is.EqualTo(0), "inventory -> empty");
            Assert.That(_state.Inventory.ManaCrystals, Is.EqualTo(0));
            Assert.That(_state.Inventory.Cleanses, Is.EqualTo(0));
            Assert.That(_state.AtbLossStreak, Is.EqualTo(0), "atbLossStreak -> 0");
            Assert.That(_state.BuildingDamage, Is.Empty, "buildingDamage wiped");
            Assert.That(_state.ActiveDungeonRun, Is.Null, "activeDungeonRun -> null");
            Assert.That(_state.Quests.Completed, Is.Empty, "quests wiped");
            Assert.That(_state.Regions.Discovered, Is.Empty, "regions wiped");
        }

        [Test]
        public void reset_seeds_the_starter_dungeon_discovered()
        {
            _service.ResetToNewGame();
            // DungeonProgress.Empty() seeds healers_cottage so Dungeon Select has >=1 entry.
            Assert.That(_state.Dungeons.Discovered.ContainsKey(SaveSchema.StarterDungeonId), Is.True,
                "Reset() must leave the starter dungeon discovered.");
            Assert.That(_state.Dungeons.Discovered[SaveSchema.StarterDungeonId], Is.True);
        }

        [Test]
        public void reset_stamps_the_current_schema_version()
        {
            _state.SchemaVersion = 3;
            _service.ResetToNewGame();
            Assert.That(_state.SchemaVersion, Is.EqualTo(SaveSchema.CurrentVersion),
                "Reset() must stamp SchemaVersion to CurrentVersion.");
        }

        [Test]
        public void reset_then_relaunch_keeps_the_preserved_fields_persisted()
        {
            // Reset writes the save — the preserved fields must survive a reload.
            _state.BoundWallet = "WALLET-XYZ";
            _state.BreachStyle = BreachStyle.Atb;
            _state.MyInviteCode = "KEEP01";
            _state.Contacts = new List<ChatContact> { new ChatContact { Code = "FRIEND" } };
            _state.LastInboxSyncAt = 555;

            _service.ResetToNewGame(); // ResetToNewGame() also Save()s.

            TestSupport.DestroyService(_service);
            _service = TestSupport.SpawnService(out _state);
            Assert.That(_service.Load(), Is.True);

            Assert.That(_state.BoundWallet, Is.EqualTo("WALLET-XYZ"));
            Assert.That(_state.BreachStyle, Is.EqualTo(BreachStyle.Atb));
            Assert.That(_state.MyInviteCode, Is.EqualTo("KEEP01"));
            Assert.That(_state.Contacts.Count, Is.EqualTo(1));
            Assert.That(_state.Contacts[0].Code, Is.EqualTo("FRIEND"));
            Assert.That(_state.LastInboxSyncAt, Is.EqualTo(555));
        }
    }
}
