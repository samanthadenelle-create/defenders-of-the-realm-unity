// =============================================================================
// NestedTypes — the nested persisted data shapes (spec §1.4)
// -----------------------------------------------------------------------------
// Plain [Serializable] data containers — the C# port of the nested objects the
// persisted GameState carries: PetData, ResourceBalance, PendingTowerBuild,
// AtbInventory, ChatContact, ChatMessage, DungeonProgress, QuestProgress /
// QuestState, ActiveDungeonRun, LootStash, RegionProgress.
//
// They are serialized by Newtonsoft for the save file (handles Dictionary<,>
// natively) and are marked [Serializable] so the live GameState SO survives an
// editor domain reload. Newtonsoft maps C# PascalCase members to the React
// camelCase JSON keys via [JsonProperty]. Enum members serialize via
// StringEnumConverter (registered globally in SaveSchema.JsonSettings).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>A single owned pet — mirrors the <c>Pet</c> interface in gameDesign.ts.</summary>
    [Serializable]
    public sealed class PetData
    {
        [JsonProperty("id")] public string Id;
        /// <summary>Always "local-player" in the no-auth client.</summary>
        [JsonProperty("ownerId")] public string OwnerId = "local-player";
        [JsonProperty("species")] public PetSpecies Species;
        /// <summary>Optional player-given nickname.</summary>
        [JsonProperty("nickname", NullValueHandling = NullValueHandling.Ignore)]
        public string Nickname;
        [JsonProperty("level")] public int Level;
        [JsonProperty("xp")] public int Xp;
        [JsonProperty("unlockedSkillIds")] public List<string> UnlockedSkillIds = new List<string>();
        [JsonProperty("equippedActiveIds")] public List<string> EquippedActiveIds = new List<string>();
    }

    /// <summary>Player wallet — crystals / food / coins (resourcesSlice).</summary>
    [Serializable]
    public struct ResourceBalance
    {
        [JsonProperty("crystals")] public int Crystals;
        [JsonProperty("food")] public int Food;
        [JsonProperty("coins")] public int Coins;

        public ResourceBalance(int crystals, int food, int coins)
        {
            Crystals = crystals;
            Food = food;
            Coins = coins;
        }

        /// <summary>STARTER_RESOURCES — { crystals:250, food:80, coins:15 }.</summary>
        public static ResourceBalance Starter => new ResourceBalance(250, 80, 15);

        /// <summary>ZERO_RESOURCES — an empty wallet.</summary>
        public static ResourceBalance Zero => new ResourceBalance(0, 0, 0);
    }

    /// <summary>An in-flight pet-assisted tower build (villageSlice PendingTowerBuild).</summary>
    [Serializable]
    public struct PendingTowerBuild
    {
        [JsonProperty("slot")] public int Slot;
        [JsonProperty("ability")] public int Ability;
        /// <summary>ms timestamp the build finishes.</summary>
        [JsonProperty("finishAt")] public double FinishAt;
    }

    /// <summary>Shared consumable inventory carried into an ATB battle (combatSlice).</summary>
    [Serializable]
    public struct AtbInventory
    {
        [JsonProperty("potions")] public int Potions;
        [JsonProperty("manaCrystals")] public int ManaCrystals;
        [JsonProperty("cleanses")] public int Cleanses;
        /// <summary>
        /// Dungeon lantern-mechanic consumable. OPTIONAL — a save written before
        /// the lantern feature omits the key; readers default it to 0. Added
        /// without a save-schema bump (rides inside the existing inventory object).
        /// </summary>
        [JsonProperty("torches", NullValueHandling = NullValueHandling.Ignore)]
        public int Torches;

        /// <summary>Empty inventory — { potions:0, manaCrystals:0, cleanses:0 }.</summary>
        public static AtbInventory Empty => new AtbInventory();
    }

    /// <summary>A locally-saved chat contact (socialSlice / services/chat).</summary>
    [Serializable]
    public sealed class ChatContact
    {
        [JsonProperty("code")] public string Code;
        [JsonProperty("nickname", NullValueHandling = NullValueHandling.Ignore)]
        public string Nickname;
    }

    /// <summary>A cached inbox message (socialSlice / services/chat).</summary>
    [Serializable]
    public sealed class ChatMessage
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("senderCode")] public string SenderCode;
        [JsonProperty("recipientCode")] public string RecipientCode;
        [JsonProperty("phraseId")] public string PhraseId;
        [JsonProperty("sentAt")] public double SentAt;
        /// <summary>Unix ms read time, or null when unread.</summary>
        [JsonProperty("readAt")] public double? ReadAt;
    }

    /// <summary>A resolved dungeon loot stash (mirrors LootStash in types/loot.ts).</summary>
    [Serializable]
    public sealed class LootStash
    {
        [JsonProperty("crystals")] public int Crystals;
        [JsonProperty("food")] public int Food;
        [JsonProperty("coins")] public int Coins;
        [JsonProperty("stone")] public int Stone;
        [JsonProperty("iron")] public int Iron;
        [JsonProperty("wood")] public int Wood;
        [JsonProperty("petBondShards")] public Dictionary<string, double> PetBondShards = new Dictionary<string, double>();
        [JsonProperty("skillPoints")] public Dictionary<string, double> SkillPoints = new Dictionary<string, double>();
    }

    /// <summary>A mid-run dungeon save — resumes where the player left off (dungeonsSlice).</summary>
    [Serializable]
    public sealed class ActiveDungeonRun
    {
        [JsonProperty("dungeonId")] public string DungeonId;
        [JsonProperty("avatarNodeId")] public string AvatarNodeId;
        [JsonProperty("visitedNodes")] public List<string> VisitedNodes = new List<string>();
        [JsonProperty("clearedEncounters")] public List<string> ClearedEncounters = new List<string>();
        [JsonProperty("openedChests")] public List<string> OpenedChests = new List<string>();
        [JsonProperty("readLore")] public List<string> ReadLore = new List<string>();
        [JsonProperty("loot")] public LootStash Loot = new LootStash();
        /// <summary>ms timestamp the run started.</summary>
        [JsonProperty("startedAt")] public double StartedAt;
    }

    /// <summary>
    /// The seed-and-stone clay jar planted at the village Farm — Alduin's boss
    /// reward (COTT-6). OPTIONAL: absent on a save written before COTT-6.
    /// </summary>
    [Serializable]
    public sealed class SeedTree
    {
        [JsonProperty("plantedAtWave")] public int PlantedAtWave;
    }

    /// <summary>The persisted dungeon-progress ledger (dungeonsSlice DungeonProgress).</summary>
    [Serializable]
    public sealed class DungeonProgress
    {
        /// <summary>dungeonId → true once discovered.</summary>
        [JsonProperty("discovered")] public Dictionary<string, bool> Discovered = new Dictionary<string, bool>();
        /// <summary>dungeonId → number of times cleared.</summary>
        [JsonProperty("cleared")] public Dictionary<string, int> Cleared = new Dictionary<string, int>();
        /// <summary>dungeonId → fastest clear in seconds.</summary>
        [JsonProperty("bestTime")] public Dictionary<string, double> BestTime = new Dictionary<string, double>();
        /// <summary>dungeonId → true once cleared without taking a hit.</summary>
        [JsonProperty("noHitClear")] public Dictionary<string, bool> NoHitClear = new Dictionary<string, bool>();
        /// <summary>
        /// dungeonId → Keeper-defeat count. OPTIONAL — added without a schema bump;
        /// a save without it null-coalesces to {}.
        /// </summary>
        [JsonProperty("deathsByDungeon", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, int> DeathsByDungeon;
        /// <summary>
        /// dungeonId → { loreId → true } 3D lore-stone read ledger. OPTIONAL —
        /// added without a schema bump.
        /// </summary>
        [JsonProperty("loreReadByDungeon", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, Dictionary<string, bool>> LoreReadByDungeon;
        /// <summary>Boss-reward seed jar (COTT-6). OPTIONAL — absent before COTT-6.</summary>
        [JsonProperty("seedTree", NullValueHandling = NullValueHandling.Ignore)]
        public SeedTree SeedTree;

        /// <summary>
        /// Fresh dungeon progress — the v6 migration seed. Seeds the starter
        /// dungeon (The Healer's Cottage) as discovered so the Dungeon Select
        /// screen always shows ≥1 playable dungeon.
        /// </summary>
        public static DungeonProgress Empty()
        {
            return new DungeonProgress
            {
                Discovered = new Dictionary<string, bool> { { SaveSchema.StarterDungeonId, true } },
                Cleared = new Dictionary<string, int>(),
                BestTime = new Dictionary<string, double>(),
                NoHitClear = new Dictionary<string, bool>(),
            };
        }
    }

    /// <summary>One live quest's state (saveSchema questStateSchema).</summary>
    [Serializable]
    public sealed class QuestState
    {
        [JsonProperty("beatIndex")] public int BeatIndex;
        [JsonProperty("flags")] public Dictionary<string, bool> Flags = new Dictionary<string, bool>();
    }

    /// <summary>The persisted quest ledger (dungeonsSlice QuestProgress).</summary>
    [Serializable]
    public sealed class QuestProgress
    {
        [JsonProperty("active")] public Dictionary<string, QuestState> Active = new Dictionary<string, QuestState>();
        [JsonProperty("completed")] public Dictionary<string, bool> Completed = new Dictionary<string, bool>();
        [JsonProperty("available")] public Dictionary<string, bool> Available = new Dictionary<string, bool>();

        /// <summary>Fresh empty quest progress — the v6 migration seed.</summary>
        public static QuestProgress Empty()
        {
            return new QuestProgress
            {
                Active = new Dictionary<string, QuestState>(),
                Completed = new Dictionary<string, bool>(),
                Available = new Dictionary<string, bool>(),
            };
        }
    }

    /// <summary>The persisted Realm Map region-progress ledger (regionsSlice).</summary>
    [Serializable]
    public sealed class RegionProgress
    {
        /// <summary>regionId → true once discovered (gate met).</summary>
        [JsonProperty("discovered")] public Dictionary<string, bool> Discovered = new Dictionary<string, bool>();
        /// <summary>regionId → true once the region's main objective is cleared.</summary>
        [JsonProperty("cleared")] public Dictionary<string, bool> Cleared = new Dictionary<string, bool>();

        /// <summary>Fresh empty region progress — the v10 migration seed.</summary>
        public static RegionProgress Empty()
        {
            return new RegionProgress
            {
                Discovered = new Dictionary<string, bool>(),
                Cleared = new Dictionary<string, bool>(),
            };
        }
    }
}
