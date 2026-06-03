// =============================================================================
// GameState — the persisted-state ScriptableObject (spec §1.6)
// -----------------------------------------------------------------------------
// The Unity analog of the Zustand store's DATA. A pure data container — NO
// logic, NO events — holding exactly the 41 persisted fields the React
// `partialize` selects, plus SchemaVersion. One asset instance lives at
// Assets/_Modules/Core/State/GameState.asset and is the in-memory live state.
//
// Mutators / subscribers / load+save live in GameStateService. Fresh defaults
// here = a brand-new game; the service overwrites them on Load() when a save
// exists. The save layer (SaveSchema / SaveMigrator) round-trips all 41 fields.
//
// Slices are a React code-organisation device only — at runtime the state is
// one FLAT object, so this SO is flat too (the `── Region ──` comments only
// echo the slice grouping for readers).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.State
{
    /// <summary>
    /// The persisted slice of game state — the 41 fields selected by the React
    /// store's <c>partialize</c>. A pure data container.
    /// </summary>
    [CreateAssetMenu(menuName = "Defenders/Core/Game State", fileName = "GameState")]
    public sealed class GameState : ScriptableObject
    {
        /// <summary>Persisted-save schema version. = SaveSchema.CurrentVersion (10).</summary>
        public int SchemaVersion = SaveSchema.CurrentVersion;

        // ── Player (playerSlice) ─────────────────────────────────────────────
        /// <summary>#3 — true once pet creation + tutorial are complete.</summary>
        public bool Onboarded;
        /// <summary>#4 — highest wave cleared. Clamped ≥0.</summary>
        public int BestWave;
        /// <summary>
        /// #28 — chosen hero class. Inspector-friendly nullable wrapper
        /// (HeroClassOpt.None = the React null). Improvement #5 NOT adopted.
        /// </summary>
        public HeroClassOpt HeroClass = HeroClassOpt.None;
        /// <summary>#27 — wallet the save is tagged to. null when unbound.</summary>
        public string BoundWallet;

        // ── Resources (resourcesSlice) ───────────────────────────────────────
        /// <summary>#5 — main wallet. Fresh = STARTER_RESOURCES {250,80,15}.</summary>
        public ResourceBalance Resources = ResourceBalance.Starter;
        /// <summary>#8 — rare Voidshard currency. Fresh = 5. Clamped ≥0.</summary>
        public int Voidshards = 5;
        /// <summary>Aether Crystals — tower-empowerment currency (v11). Fresh = 0. Clamped >= 0.</summary>
        public int AetherCrystals = 0;
        /// <summary>#12 — gathered Stone. Fresh = 20. Clamped ≥0.</summary>
        public int Stone = 20;
        /// <summary>#13 — gathered Iron. Fresh = 5. Clamped ≥0.</summary>
        public int Iron = 5;
        /// <summary>#14 — gathered Wood. Fresh = 15. Clamped ≥0.</summary>
        public int Wood = 15;
        /// <summary>#6 — ids of purchased store items.</summary>
        public List<string> OwnedItemIds = new List<string>();

        // ── Pets (petsSlice) ─────────────────────────────────────────────────
        /// <summary>#1 — owned pet roster.</summary>
        public List<PetData> Pets = new List<PetData>();
        /// <summary>#2 — id of the onboarding starter pet. null until chosen.</summary>
        public string StarterPetId;
        /// <summary>#7 — guardian Bond Ranks [Aether, Flame, Ice]. Fresh = [0,0,0].</summary>
        public List<int> PetBonds = new List<int> { 0, 0, 0 };
        /// <summary>#25 — unlocked Warden species.</summary>
        public List<PetSpecies> OwnedPets = new List<PetSpecies>();

        // ── Village (villageSlice) ───────────────────────────────────────────
        /// <summary>#9 — tower tier per slot. Length = TowerSlots (9). Fresh = [0]×9.</summary>
        public List<int> Towers = NewZeroed(Constants.TowerSlots);
        /// <summary>#10 — tower ability per slot. Length = 9. Fresh = [0]×9.</summary>
        public List<int> TowerAbilities = NewZeroed(Constants.TowerSlots);
        /// <summary>#11 — wall level 0..WALL_MAX_LEVEL (3). Fresh = 0.</summary>
        public int WallLevel;
        /// <summary>#15 — buildingId → ready-at ms timestamp.</summary>
        public SerializableDict<string, double> BuildingCooldowns = new SerializableDict<string, double>();
        /// <summary>#16 — in-flight pet-assisted tower builds.</summary>
        public List<PendingTowerBuild> PendingBuilds = new List<PendingTowerBuild>();
        /// <summary>#32 — buildingId → 0..100 damage. Keys incl. gate-0..gate-3, "heart".</summary>
        public SerializableDict<string, double> BuildingDamage = new SerializableDict<string, double>();

        // ── Settings (settingsSlice) ─────────────────────────────────────────
        /// <summary>#18 — movement joystick sensitivity. Fresh = 1. Setter clamps 0.3..1.5.</summary>
        public float JoystickSensitivity = 1f;
        /// <summary>#19 — hero movement control style. Fresh = Auto.</summary>
        public MovementStyle MovementStyle = MovementStyle.Auto;
        /// <summary>#31 — breach battle system. Fresh = Ask. Survives New Game.</summary>
        public BreachStyle BreachStyle = BreachStyle.Ask;
        /// <summary>#20 — global audio mute. Fresh = false (owner directive 2026-05-20:
        /// the a11y-muted default hid the new music wiring; players see-it-and-mute-it
        /// if they need silence, but the default expectation now is "music plays").</summary>
        public bool Muted;
        /// <summary>#21 — music volume 0..100. Fresh = 70.</summary>
        public float MusicVolume = 70f;
        /// <summary>#22 — sfx volume 0..100. Fresh = 80.</summary>
        public float SfxVolume = 80f;
        /// <summary>#23 — gameplay difficulty. Fresh = Normal.</summary>
        public Difficulty Difficulty = Difficulty.Normal;
        /// <summary>#24 — voice-over toggle. Stored, not yet wired.</summary>
        public bool VoiceOvers;

        // ── Tutorial (tutorialSlice) ─────────────────────────────────────────
        /// <summary>#17 — first-time tutorial step. Fresh = Step1; migrated saves = Done.</summary>
        public TutorialStep TutorialStep = TutorialStep.Step1;
        /// <summary>#26 — one-shot tutorial-seen flags.</summary>
        public SerializableDict<string, bool> SeenTutorials = new SerializableDict<string, bool>();

        // ── ATB / combat (combatSlice) ───────────────────────────────────────
        /// <summary>#29 — shared ATB consumable inventory. Fresh = {0,0,0}.</summary>
        public AtbInventory Inventory = AtbInventory.Empty;
        /// <summary>#30 — consecutive ATB defeats. Fresh = 0. Clamped ≥0.</summary>
        public int AtbLossStreak;

        // ── Dungeons / quests / regions ──────────────────────────────────────
        /// <summary>#33 — discovered/cleared/best-time/no-hit dungeon ledger.</summary>
        public DungeonProgress Dungeons = DungeonProgress.Empty();
        /// <summary>#34 — mid-run dungeon save. null when no run is in progress.</summary>
        public ActiveDungeonRun ActiveDungeonRun;
        /// <summary>#35 — active/completed/available quest ledger.</summary>
        public QuestProgress Quests = QuestProgress.Empty();
        /// <summary>#36 — Realm Map discovered/cleared region ledger.</summary>
        public RegionProgress Regions = RegionProgress.Empty();

        // ── Social (socialSlice) ─────────────────────────────────────────────
        /// <summary>#37 — this player's 6-char invite code. Stable once generated.</summary>
        public string MyInviteCode;
        /// <summary>#38 — locally-added chat contacts.</summary>
        public List<ChatContact> Contacts = new List<ChatContact>();
        /// <summary>#38a — codes whose incoming messages are dropped.</summary>
        public List<string> BlockedCodes = new List<string>();
        /// <summary>#38b — cached inbox, newest-first.</summary>
        public List<ChatMessage> Inbox = new List<ChatMessage>();
        /// <summary>#38c — unix ms of the last inbox sync. Fresh = 0.</summary>
        public double LastInboxSyncAt;

        // ── Offline harvest accrual (WO-115) ─────────────────────────────────
        /// <summary>
        /// Unix-ms of the last offline-harvest accrual claim (WO-115). Fresh = 0
        /// (never claimed → seeded to now on first load, no retroactive haul). The
        /// accrual clock: OfflineHarvestService integrates each active harvest source's
        /// rate over (now − this) on resume/load, banks the capped haul, then advances
        /// this to now. Mirrors <see cref="LastInboxSyncAt"/> exactly (a double unix-ms
        /// field that round-trips through the save layer). Append-only at the END so
        /// older saves stay loadable.
        /// </summary>
        public double LastHarvestClaimMs;

        // ── Build/upgrade timers + ad-skip (WO-172) ──────────────────────────
        /// <summary>
        /// In-flight construction/upgrade jobs (WO-172) — the CoC time-sink. Each
        /// counts down in REAL wall-clock time (offline too), keyed by structure id;
        /// the structure completes at job.FinishMs. Managed by BuildTimerService.
        /// Mirrors <see cref="PendingBuilds"/>'s "small serializable struct in a list"
        /// persistence shape. Append-only field at the END so older saves stay loadable.
        /// </summary>
        public List<BuildJobData> BuildJobs = new List<BuildJobData>();

        /// <summary>
        /// Rewarded-ad build-skips used in the current local day (WO-172 daily cap).
        /// Reset to 0 when <see cref="AdSkipDayKey"/> rolls to a new day. Clamped ≥0.
        /// </summary>
        public int AdSkipsUsedToday;

        /// <summary>
        /// Local-day key ("yyyy-MM-dd", device-local) the <see cref="AdSkipsUsedToday"/>
        /// counter belongs to. When today's key differs, the counter resets (daily cap).
        /// </summary>
        public string AdSkipDayKey;

        // ── World / zones (WO-164) ────────────────────────────────────────────
        /// <summary>Per-region zone records — discovery/clear flags, neighbor graph,
        /// and City/Horde destination tag (WO-164). Seeded from
        /// <see cref="DeNelle.Core.World.ZoneManager.DefaultZoneGraph"/> on a fresh save.
        /// Append-only field — added at the END so older saves stay loadable. NOT yet
        /// wired into SaveSchema/SaveMigrator; the save owner (Agent 3) must add its
        /// (de)serialization + bump the schema version for it to round-trip to disk.</summary>
        public List<DeNelle.Core.World.ZoneState> Zones = new List<DeNelle.Core.World.ZoneState>();

        // ── World content (WO-159 settlements / WO-160 tribes) ───────────────
        /// <summary>Per-tribe roaming-raider records (WO-160) — members remaining, cleared
        /// flag, clear-count for reduced respawn, last-seen. Append-only field at the END so
        /// older saves stay loadable. Like <see cref="Zones"/>, NOT yet wired into
        /// SaveSchema/SaveMigrator — the save owner adds its (de)serialisation + bumps the
        /// schema version for it to round-trip to disk. Lives correctly in-memory meanwhile.</summary>
        public List<DeNelle.Core.World.TribeState> Tribes = new List<DeNelle.Core.World.TribeState>();

        /// <summary>Per-site node-settlement records (WO-159) — claim phase, defence HP, and
        /// the razed-site game-day lockout. Append-only field at the END. Same save-wiring note
        /// as <see cref="Tribes"/>/<see cref="Zones"/>: in-memory now, schema round-trip is the
        /// save owner's follow-up.</summary>
        public List<DeNelle.Core.World.SettlementState> Settlements = new List<DeNelle.Core.World.SettlementState>();

        /// <summary>Per-ward relight records (WO-112) — the earned exploration reach. Each
        /// carries its ward id/region/granted-reach and whether the Keeper has relit it; the
        /// set of lit wards drives per-march reach (WardReach) and the forgetting effect.
        /// Append-only field at the END so older saves stay loadable. Same save-wiring note as
        /// <see cref="Tribes"/>/<see cref="Settlements"/>/<see cref="Zones"/>: in-memory now,
        /// the schema round-trip (SaveSchema/SaveMigrator + version bump) is the save owner's
        /// follow-up.</summary>
        public List<DeNelle.Core.World.WardStoneState> Wards = new List<DeNelle.Core.World.WardStoneState>();

        // ── Build Mode — the player's base layout (WO-108 P0 data spine) ──────
        /// <summary>
        /// The player's placed-structure layout — the CREATE-verb spine (WO-108).
        /// One <see cref="PlacedStructureData"/> per player-placed structure (grid
        /// cell + discrete yaw + level). Empty on a fresh save → the runtime
        /// <c>BaseLayoutLoader</c> falls through to the default VillageSceneBuilder
        /// village (the seed), and the first build-mode entry seeds this from that
        /// default so the player edits their familiar town, not a blank plot.
        /// Round-trips through SaveSchema (v14) — additive at the END so older
        /// saves load empty. This is exactly the payload that becomes
        /// server-authoritative when async raids land (principle #2).
        /// </summary>
        public List<PlacedStructureData> BaseLayout = new List<PlacedStructureData>();

        // ── Pets — onboarding-named starter pet (WO-277) ─────────────────────
        /// <summary>
        /// The player-chosen name for their starter pet, set in the tutorial's pet
        /// introduction (WO-277). Empty/null until the player names it. Persisted via
        /// <see cref="GameStateService.Save"/> directly (not yet in SaveSchema's
        /// round-trip — like Zones/Tribes it lives in-memory + the PlayerPrefs SO
        /// snapshot until the save owner threads it through SaveSchema). Append-only
        /// field at the END of the class so older saves stay loadable.
        /// </summary>
        public string PetName;

        // ── Magic — building-upgrade TECH axis (DEF-121 / WO-230) ─────────────
        /// <summary>
        /// Magic — the tech-tree gating currency (DEF-121). NOT a harvestable: there
        /// is no Magic MineNode / pickup. It is earned through progression (boss/dungeon
        /// tech rewards) and spent on Magic-gated building-upgrade tiers that unlock
        /// tech-tree nodes (see <c>ResourceBuildingProgression</c>'s Magic tier). Fresh = 0.
        /// Clamped &gt;= 0. Round-trips through SaveSchema (v15) — append-only field at the
        /// END so older saves load with Magic = 0.
        /// </summary>
        public int Magic = 0;

        /// <summary>A fresh List&lt;int&gt; of <paramref name="count"/> zeros.</summary>
        public static List<int> NewZeroed(int count)
        {
            var list = new List<int>(count);
            for (var i = 0; i < count; i++) list.Add(0);
            return list;
        }
    }
}
