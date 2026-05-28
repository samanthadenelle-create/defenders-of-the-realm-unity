// =============================================================================
// GameStateService — load / save / mutators / events (spec §1.6)
// -----------------------------------------------------------------------------
// The behaviour layer — the Unity analog of Zustand's `subscribe` plus the
// `persist` middleware. A MonoBehaviour singleton holding a reference to the
// live GameState SO.
//
//   Load()   — PlayerPrefs['dotr-save'] → JSON → SaveMigrator → SaveSchema.Validate
//              → apply onto the SO. Absent save ⇒ the SO keeps fresh defaults.
//   Save()   — Newtonsoft-serialize the SO's 41 persisted fields into the
//              SaveFile envelope → PlayerPrefs.SetString → PlayerPrefs.Save().
//   mutators — typed methods mirroring the Week-1 Zustand slice actions; each
//              writes the SO, raises its per-domain event, and Save()s.
//   Reset()  — the React `reset()` carve-out: wipes progression but keeps
//              boundWallet, breachStyle and all social fields.
//
// IMPROVEMENT #1 (adopted): per-domain UnityEvents instead of one fat
// GameStateChanged — HUD widgets subscribe only to the domain they render,
// preserving the selective-subscription performance the Zustand selectors gave.
// IMPROVEMENT #4 NOT adopted: storage stays PlayerPrefs (port-spec mandate).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace DeNelle.Core.State
{
    /// <summary>
    /// The live-state behaviour layer — load, save, typed mutators and
    /// per-domain change events over the <see cref="GameState"/> SO.
    /// </summary>
    public sealed class GameStateService : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static GameStateService _instance;

        /// <summary>The live service instance (null before bootstrap).</summary>
        public static GameStateService Instance => _instance;

        [Tooltip("The live GameState ScriptableObject — the in-memory persisted state.")]
        [SerializeField] private GameState _state;

        [Tooltip("Auto-load the save in Awake. Disable for tests that load manually.")]
        [SerializeField] private bool _loadOnAwake = true;

        /// <summary>The live persisted state. Never null after <see cref="Awake"/>.</summary>
        public GameState State => _state;

        /// <summary>
        /// Backend-controlled remote config. Populated on <see cref="LoadFromBackend"/>;
        /// returns <see cref="ServerConfig.Default"/> until the first successful load.
        /// Never null.
        /// </summary>
        public ServerConfig ServerConfig { get; private set; } = ServerConfig.Default;

        // ── Per-domain change events (improvement #1) ────────────────────────
        /// <summary>Raised when the resource wallet / materials / voidshards change.</summary>
        public readonly UnityEvent ResourcesChanged = new UnityEvent();
        /// <summary>Raised when <see cref="GameState.BestWave"/> is recorded.</summary>
        public readonly UnityEvent WaveRecorded = new UnityEvent();
        /// <summary>Raised when the tutorial step advances or a tutorial is seen.</summary>
        public readonly UnityEvent TutorialAdvanced = new UnityEvent();
        /// <summary>Raised when onboarding / hero class / wallet change.</summary>
        public readonly UnityEvent PlayerChanged = new UnityEvent();
        /// <summary>Raised when an audio / movement / difficulty setting changes.</summary>
        public readonly UnityEvent SettingsChanged = new UnityEvent();
        /// <summary>Raised when the pet roster or bond ranks change.</summary>
        public readonly UnityEvent PetsChanged = new UnityEvent();
        /// <summary>Raised when village towers / walls / build state change.</summary>
        public readonly UnityEvent VillageChanged = new UnityEvent();
        /// <summary>Raised when dungeon / quest / region progress changes.</summary>
        public readonly UnityEvent DungeonProgressChanged = new UnityEvent();
        /// <summary>Raised when ATB inventory / loss streak / building damage change.</summary>
        public readonly UnityEvent CombatChanged = new UnityEvent();
        /// <summary>Raised when social contacts / inbox change.</summary>
        public readonly UnityEvent SocialChanged = new UnityEvent();
        /// <summary>Raised after a full <see cref="Reset"/> or <see cref="Load"/>.</summary>
        public readonly UnityEvent StateReplaced = new UnityEvent();

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            _instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            if (_state == null)
                _state = ScriptableObject.CreateInstance<GameState>();

            if (_loadOnAwake)
                Load();

            // WO1: ensure the PersistenceBridge is alive so wave-clear saves,
            // scene-enter loads, and quit saves are all wired automatically.
            if (Application.isPlaying)
                PersistenceBridge.EnsureExists();

            // WO2-4: analytics event tracker with batching, offline queue, circuit breaker.
            if (Application.isPlaying)
                DeNelle.Core.Analytics.EventTracker.EnsureExists();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Guarantees a live service exists in ANY scene — even one entered without
        /// the boot flow (a dev direct-boot, or the Village as the first built
        /// scene). No-op in normal play: the boot scene's GameStateService Awake
        /// sets <see cref="_instance"/> first, so this never runs; and if a later
        /// scene carries its own component it destroys itself against the existing
        /// singleton (see Awake). A code-created instance still loads the same
        /// PlayerPrefs save in its Awake, so progress is preserved, not reset.
        ///
        /// Without this, every scene where Instance is null silently breaks the
        /// economy: dev resource grants no-op, the build menu falls back to a local
        /// crystal balance, and wall repair can't spend — all of which read
        /// GameStateService.State.Resources. (WisdomCurrencyService already
        /// self-installs, which is why +Wisdom worked but +Crystals did not.)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("GameStateService (auto)");
            go.AddComponent<GameStateService>();   // Awake sets _instance + DontDestroyOnLoad + loads the save
        }

        // =====================================================================
        //  Load — PlayerPrefs → migrate → validate → apply to the SO
        // =====================================================================

        /// <summary>
        /// Loads the save from PlayerPrefs key <c>dotr-save</c>. Migrates an
        /// out-of-date save through <see cref="SaveMigrator"/> and validates it
        /// through <see cref="SaveSchema.Validate"/> before applying it onto the
        /// SO. If no save exists, the SO keeps its fresh defaults (a new game).
        /// Returns true when an existing save was loaded.
        /// </summary>
        public bool Load()
        {
            if (!PlayerPrefs.HasKey(SaveSchema.PlayerPrefsKey))
            {
                StateReplaced.Invoke();
                return false; // brand-new game — fresh SO defaults stand.
            }

            var json = PlayerPrefs.GetString(SaveSchema.PlayerPrefsKey);
            if (string.IsNullOrEmpty(json))
            {
                StateReplaced.Invoke();
                return false;
            }

            SaveSchema.SaveFile file;
            try
            {
                file = JsonConvert.DeserializeObject<SaveSchema.SaveFile>(json, SaveSchema.JsonSettings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameStateService] Save parse failed — keeping fresh state. {ex.Message}");
                StateReplaced.Invoke();
                return false;
            }
            if (file == null || file.State == null)
            {
                Debug.LogError("[GameStateService] Save envelope missing its state payload — keeping fresh state.");
                StateReplaced.Invoke();
                return false;
            }

            // Migrate-then-validate: the schema must see the up-to-date shape.
            var migration = SaveMigrator.MigrateForImport(file.State, file.StoreVersion);
            if (!migration.Ok)
            {
                Debug.LogError($"[GameStateService] Save migration rejected — keeping fresh state. {migration.Reason}");
                StateReplaced.Invoke();
                return false;
            }

            var validation = SaveSchema.Validate(migration.Data);
            if (!validation.Ok)
            {
                Debug.LogError($"[GameStateService] {validation.Message}");
                StateReplaced.Invoke();
                return false;
            }

            ApplyPersisted(validation.Data);
            _state.SchemaVersion = SaveSchema.CurrentVersion;
            StateReplaced.Invoke();
            return true;
        }

        // =====================================================================
        //  Save — serialize the SO's 41 fields into the SaveFile envelope
        // =====================================================================

        /// <summary>
        /// Serializes the SO's 41 persisted fields into the <see cref="SaveSchema.SaveFile"/>
        /// envelope and writes it to PlayerPrefs key <c>dotr-save</c>. The literal
        /// analog of Zustand <c>persist</c> writing to localStorage.
        /// </summary>
        public void Save()
        {
            if (_state == null) return;

            var file = new SaveSchema.SaveFile
            {
                Format = SaveSchema.FileFormat,
                StoreVersion = SaveSchema.CurrentVersion,
                ExportedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Wallet = _state.BoundWallet,
                State = Snapshot(),
            };

            try
            {
                var json = JsonConvert.SerializeObject(file, SaveSchema.JsonSettings);
                PlayerPrefs.SetString(SaveSchema.PlayerPrefsKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameStateService] Save failed. {ex.Message}");
            }
        }

        /// <summary>
        /// Adds <paramref name="amount"/> crystals to the live state (negative to
        /// spend; clamped &gt;= 0), persists, and raises <see cref="ResourcesChanged"/>.
        /// The convenience seam AdminOverlay's reflective "+crystals" actions look
        /// for ("if AddCrystals isn't defined, owner adds it"), so callers needn't
        /// reach into the Resources struct directly.
        /// </summary>
        public void AddCrystals(int amount)
        {
            if (_state == null) return;
            var r = _state.Resources;
            r.Crystals = Mathf.Max(0, r.Crystals + amount);
            _state.Resources = r;
            Save();
            ResourcesChanged.Invoke();
        }

        /// <summary>Snapshots the SO's 41 persisted fields into a <see cref="SaveSchema.PersistedState"/>.</summary>
        public SaveSchema.PersistedState Snapshot()
        {
            var s = _state;
            return new SaveSchema.PersistedState
            {
                Pets = s.Pets,
                StarterPetId = s.StarterPetId,
                Onboarded = s.Onboarded,
                BestWave = s.BestWave,
                Resources = s.Resources,
                OwnedItemIds = s.OwnedItemIds,
                PetBonds = ToDoubleList(s.PetBonds),
                Voidshards = s.Voidshards,
                AetherCrystals = s.AetherCrystals,
                Towers = ToDoubleList(s.Towers),
                TowerAbilities = ToDoubleList(s.TowerAbilities),
                WallLevel = s.WallLevel,
                Stone = s.Stone,
                Iron = s.Iron,
                Wood = s.Wood,
                BuildingCooldowns = new Dictionary<string, double>(s.BuildingCooldowns),
                PendingBuilds = s.PendingBuilds,
                TutorialStep = s.TutorialStep,
                JoystickSensitivity = s.JoystickSensitivity,
                MovementStyle = s.MovementStyle,
                Muted = s.Muted,
                MusicVolume = s.MusicVolume,
                SfxVolume = s.SfxVolume,
                Difficulty = s.Difficulty,
                VoiceOvers = s.VoiceOvers,
                OwnedPets = s.OwnedPets,
                SeenTutorials = new Dictionary<string, bool>(s.SeenTutorials),
                BoundWallet = s.BoundWallet,
                HeroClass = s.HeroClass.ToNullable(),
                Inventory = s.Inventory,
                AtbLossStreak = s.AtbLossStreak,
                BreachStyle = s.BreachStyle,
                BuildingDamage = new Dictionary<string, double>(s.BuildingDamage),
                Dungeons = s.Dungeons,
                ActiveDungeonRun = s.ActiveDungeonRun,
                Quests = s.Quests,
                Regions = s.Regions,
                MyInviteCode = s.MyInviteCode,
                Contacts = s.Contacts,
                BlockedCodes = s.BlockedCodes,
                Inbox = s.Inbox,
                LastInboxSyncAt = s.LastInboxSyncAt,
            };
        }

        /// <summary>
        /// Applies a validated <see cref="SaveSchema.PersistedState"/> onto the live SO.
        /// A field the partial save omits keeps the SO's existing (fresh-default)
        /// value — mirrors the React shallow-merge tolerance.
        /// </summary>
        private void ApplyPersisted(SaveSchema.PersistedState p)
        {
            var s = _state;
            if (p.Pets != null) s.Pets = p.Pets;
            if (p.StarterPetId != null) s.StarterPetId = p.StarterPetId;
            if (p.Onboarded.HasValue) s.Onboarded = p.Onboarded.Value;
            if (p.BestWave.HasValue) s.BestWave = (int)p.BestWave.Value;
            if (p.Resources.HasValue) s.Resources = p.Resources.Value;
            if (p.OwnedItemIds != null) s.OwnedItemIds = p.OwnedItemIds;
            if (p.PetBonds != null) s.PetBonds = ToIntList(p.PetBonds);
            if (p.Voidshards.HasValue) s.Voidshards = (int)p.Voidshards.Value;
            if (p.AetherCrystals.HasValue) s.AetherCrystals = (int)p.AetherCrystals.Value;
            if (p.Towers != null) s.Towers = ToIntList(p.Towers);
            if (p.TowerAbilities != null) s.TowerAbilities = ToIntList(p.TowerAbilities);
            if (p.WallLevel.HasValue) s.WallLevel = (int)p.WallLevel.Value;
            if (p.Stone.HasValue) s.Stone = (int)p.Stone.Value;
            if (p.Iron.HasValue) s.Iron = (int)p.Iron.Value;
            if (p.Wood.HasValue) s.Wood = (int)p.Wood.Value;
            if (p.BuildingCooldowns != null) s.BuildingCooldowns = new SerializableDict<string, double>(p.BuildingCooldowns);
            if (p.PendingBuilds != null) s.PendingBuilds = p.PendingBuilds;
            if (p.TutorialStep.HasValue) s.TutorialStep = p.TutorialStep.Value;
            if (p.JoystickSensitivity.HasValue) s.JoystickSensitivity = (float)p.JoystickSensitivity.Value;
            if (p.MovementStyle.HasValue) s.MovementStyle = p.MovementStyle.Value;
            if (p.Muted.HasValue) s.Muted = p.Muted.Value;
            if (p.MusicVolume.HasValue) s.MusicVolume = (float)p.MusicVolume.Value;
            if (p.SfxVolume.HasValue) s.SfxVolume = (float)p.SfxVolume.Value;
            if (p.Difficulty.HasValue) s.Difficulty = p.Difficulty.Value;
            if (p.VoiceOvers.HasValue) s.VoiceOvers = p.VoiceOvers.Value;
            if (p.OwnedPets != null) s.OwnedPets = p.OwnedPets;
            if (p.SeenTutorials != null) s.SeenTutorials = new SerializableDict<string, bool>(p.SeenTutorials);
            if (p.BoundWallet != null) s.BoundWallet = p.BoundWallet;
            s.HeroClass = p.HeroClass.ToOpt();
            if (p.Inventory.HasValue) s.Inventory = p.Inventory.Value;
            if (p.AtbLossStreak.HasValue) s.AtbLossStreak = (int)p.AtbLossStreak.Value;
            if (p.BreachStyle.HasValue) s.BreachStyle = p.BreachStyle.Value;
            if (p.BuildingDamage != null) s.BuildingDamage = new SerializableDict<string, double>(p.BuildingDamage);
            if (p.Dungeons != null) s.Dungeons = p.Dungeons;
            s.ActiveDungeonRun = p.ActiveDungeonRun;
            if (p.Quests != null) s.Quests = p.Quests;
            if (p.Regions != null) s.Regions = p.Regions;
            if (p.MyInviteCode != null) s.MyInviteCode = p.MyInviteCode;
            if (p.Contacts != null) s.Contacts = p.Contacts;
            if (p.BlockedCodes != null) s.BlockedCodes = p.BlockedCodes;
            if (p.Inbox != null) s.Inbox = p.Inbox;
            if (p.LastInboxSyncAt.HasValue) s.LastInboxSyncAt = p.LastInboxSyncAt.Value;
        }

        // =====================================================================
        //  Mutators — typed ports of the Week-1 Zustand slice actions
        // =====================================================================

        /// <summary>playerSlice <c>finishOnboarding</c> — mark onboarding complete.</summary>
        public void FinishOnboarding()
        {
            _state.Onboarded = true;
            PlayerChanged.Invoke();
            Save();
        }

        /// <summary>
        /// playerSlice <c>recordRun</c> — record the best wave reached
        /// (<c>bestWave = max(bestWave, waveReached)</c>).
        /// </summary>
        public void RecordRun(int waveReached)
        {
            _state.BestWave = Mathf.Max(_state.BestWave, waveReached);
            WaveRecorded.Invoke();
            Save();
        }

        /// <summary>playerSlice <c>bindWallet</c> — tag the save to a wallet. Idempotent.</summary>
        public void BindWallet(string address)
        {
            if (_state.BoundWallet == address) return;
            _state.BoundWallet = address;
            PlayerChanged.Invoke();
            Save();
        }

        /// <summary>playerSlice <c>chooseHero</c> — lock in the hero class. Idempotent.</summary>
        public void ChooseHero(HeroClass cls)
        {
            var opt = ((HeroClass?)cls).ToOpt();
            if (_state.HeroClass == opt) return;
            _state.HeroClass = opt;
            PlayerChanged.Invoke();
            Save();
        }

        /// <summary>settingsSlice <c>setMuted</c> — toggle global audio mute.</summary>
        public void SetMuted(bool muted)
        {
            _state.Muted = muted;
            SettingsChanged.Invoke();
            Save();
        }

        /// <summary>settingsSlice <c>setMusicVolume</c> — set music volume (0–100).</summary>
        public void SetMusicVolume(float volume)
        {
            _state.MusicVolume = volume;
            SettingsChanged.Invoke();
            Save();
        }

        /// <summary>settingsSlice <c>setSfxVolume</c> — set sound-effects volume (0–100).</summary>
        public void SetSfxVolume(float volume)
        {
            _state.SfxVolume = volume;
            SettingsChanged.Invoke();
            Save();
        }

        /// <summary>settingsSlice <c>setDifficulty</c> — change the difficulty preference.</summary>
        public void SetDifficulty(Difficulty difficulty)
        {
            _state.Difficulty = difficulty;
            SettingsChanged.Invoke();
            Save();
        }

        /// <summary>settingsSlice <c>setMovementStyle</c> — change the movement control style.</summary>
        public void SetMovementStyle(MovementStyle style)
        {
            _state.MovementStyle = style;
            SettingsChanged.Invoke();
            Save();
        }

        /// <summary>
        /// settingsSlice <c>setBreachStyle</c> — switch the breach battle system.
        /// Idempotent.
        /// </summary>
        public void SetBreachStyle(BreachStyle style)
        {
            if (_state.BreachStyle == style) return;
            _state.BreachStyle = style;
            SettingsChanged.Invoke();
            Save();
        }

        /// <summary>settingsSlice <c>setVoiceOvers</c> — toggle voice-overs.</summary>
        public void SetVoiceOvers(bool on)
        {
            _state.VoiceOvers = on;
            SettingsChanged.Invoke();
            Save();
        }

        /// <summary>
        /// settingsSlice <c>setJoystickSensitivity</c> — set the joystick
        /// sensitivity, clamped to 0.3–1.5 (the React setter's clamp).
        /// </summary>
        public void SetJoystickSensitivity(float value)
        {
            _state.JoystickSensitivity = Mathf.Clamp(value, 0.3f, 1.5f);
            SettingsChanged.Invoke();
            Save();
        }

        /// <summary>tutorialSlice <c>advanceTutorial</c> — advance one step, caps at Done.</summary>
        public void AdvanceTutorial()
        {
            if (_state.TutorialStep == TutorialStep.Done) return;
            var next = (int)_state.TutorialStep + 1;
            _state.TutorialStep = next > 7 ? TutorialStep.Done : (TutorialStep)next;
            TutorialAdvanced.Invoke();
            Save();
        }

        /// <summary>tutorialSlice <c>markTutorialSeen</c> — flag a one-shot tutorial. Idempotent.</summary>
        public void MarkTutorialSeen(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_state.SeenTutorials.TryGetValue(key, out var seen) && seen) return;
            _state.SeenTutorials[key] = true;
            TutorialAdvanced.Invoke();
            Save();
        }

        // =====================================================================
        //  Reset — the React reset() carve-out (§1.6)
        // =====================================================================

        /// <summary>
        /// New Game — wipes progression exactly like the React <c>reset()</c>.
        /// Does NOT clear <see cref="GameState.BoundWallet"/> or
        /// <see cref="GameState.BreachStyle"/> (preferences survive a New Game),
        /// and never touches the social fields (invite code / contacts / inbox).
        /// Transient runtime fields (prepTimerLocked, paused, dungeonEnteredAt,
        /// torchBurnEndsAt, activeRegionRun) live in runtime SOs, not here, so
        /// Reset simply omits them.
        /// </summary>
        public void Reset()
        {
            var s = _state;
            s.Pets = new List<PetData>();
            s.StarterPetId = null;
            s.Onboarded = false;
            s.BestWave = 0;
            s.Resources = ResourceBalance.Starter;
            s.OwnedItemIds = new List<string>();
            s.PetBonds = new List<int> { 0, 0, 0 };
            s.Voidshards = 5;
            s.AetherCrystals = 0;
            s.Towers = GameState.NewZeroed(Constants.TowerSlots);
            s.TowerAbilities = GameState.NewZeroed(Constants.TowerSlots);
            s.WallLevel = 0;
            s.Stone = 20;
            s.Iron = 5;
            s.Wood = 15;
            s.BuildingCooldowns = new SerializableDict<string, double>();
            s.PendingBuilds = new List<PendingTowerBuild>();
            s.TutorialStep = TutorialStep.Step1;
            s.JoystickSensitivity = 1f;
            s.SeenTutorials = new SerializableDict<string, bool>();
            // Wipe the hero so onboarding re-prompts. Do NOT unbind the wallet.
            s.HeroClass = HeroClassOpt.None;
            s.Inventory = AtbInventory.Empty;
            s.AtbLossStreak = 0;
            s.BuildingDamage = new SerializableDict<string, double>();
            s.Dungeons = DungeonProgress.Empty();
            s.ActiveDungeonRun = null;
            s.Quests = QuestProgress.Empty();
            s.Regions = RegionProgress.Empty();
            // NOTE: BoundWallet, BreachStyle and every social field are deliberately
            // left untouched — preferences and identity survive a New Game.
            s.SchemaVersion = SaveSchema.CurrentVersion;

            StateReplaced.Invoke();
            Save();
        }

        // =====================================================================
        //  Backend Delta Sync — JSON + Neon (Mobile-Optimised)
        // =====================================================================
        //
        //  Architecture:
        //    • Snapshot()    →  SaveSchema.PersistedState (already exists)
        //    • Delta builder →  compares current snapshot vs _lastSyncedSnapshot
        //    • SyncDeltaPayload — flat plain-C# class, JSON-serialised
        //    • Offline queue →  PlayerPrefs "dotr-sync-queue" (JSON list)
        //    • ResourceBalance fields: Crystals / Food / Coins  (NOT Gold/Gems/XP)
        //
        //  Required packages (already in project):
        //    • Newtonsoft.Json     (com.unity.nuget.newtonsoft-json)
        //    • UniTask             (com.cysharp.unitask)
        // =====================================================================

        // ── Config ───────────────────────────────────────────────────────────
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";
#else
        private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";
#endif
        private const string SaveUrl       = BackendBase + "/api/game/save";
        private const string LoadUrl       = BackendBase + "/api/game/load";
        private const string SyncQueueKey  = "dotr-sync-queue";
        private const float  MinSyncDelay  = 8f;   // seconds between background syncs

        // ── State ─────────────────────────────────────────────────────────
        // The last snapshot the server acknowledged — null means never synced.
        // Uses PersistedState (plain class) NOT the GameState ScriptableObject.
        private SaveSchema.PersistedState _lastSyncedSnapshot;
        private bool  _isSyncing;
        private float _lastSyncTime = -999f;

        // ── Lifecycle hooks ───────────────────────────────────────────────
        private void OnApplicationPause(bool paused)
        {
            if (paused) SyncToBackend(highPriority: true).Forget();
        }

        private void OnApplicationQuit()
        {
            SyncToBackend(highPriority: true).Forget();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Call after every wave is completed. Records the wave locally (via
        /// <see cref="RecordRun"/>) then immediately pushes a high-priority
        /// delta to the backend.
        /// </summary>
        public async UniTask SyncAfterWave(int completedWave)
        {
            RecordRun(completedWave);                     // local save inside RecordRun
            await SyncToBackend(highPriority: true);
        }

        /// <summary>
        /// Call before any scene transition. Ensures local + backend are both
        /// flushed before Unity tears down the scene.
        /// </summary>
        public async UniTask SaveBeforeSceneChange()
        {
            Save();
            await SyncToBackend(highPriority: true);
        }

        /// <summary>
        /// Fetches this player's authoritative server record and merges it onto
        /// the live SO. Merge policy: server wins on BestWave (anti-rollback);
        /// local wins on Towers and Pets (last in-session layout stands).
        /// </summary>
        public async UniTask LoadFromBackend()
        {
            if (string.IsNullOrEmpty(_state?.BoundWallet)) return;

            var url = $"{LoadUrl}?playerId={Uri.EscapeDataString(_state.BoundWallet)}";
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Accept", "application/json");

            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Sync] Load failed: {req.error}");
                return;
            }

            BackendLoadResponse resp;
            try
            {
                resp = JsonConvert.DeserializeObject<BackendLoadResponse>(
                    req.downloadHandler.text, SaveSchema.JsonSettings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Sync] Load parse error: {ex.Message}");
                return;
            }

            if (resp?.Success != true || resp.Data == null) return;

            // Absorb remote config if present; keep existing config on null (older backend).
            if (resp.Config != null)
            {
                ServerConfig = resp.Config;
                Debug.Log("[Sync] ServerConfig refreshed from backend.");
            }

            var server = resp.Data;

            // Server wins on BestWave only — never roll the player back.
            if (server.BestWave.HasValue && server.BestWave > (_state.BestWave))
                _state.BestWave = (int)server.BestWave.Value;

            // Resources: take server value (authoritative for economy integrity).
            if (server.Resources.HasValue)
                _state.Resources = server.Resources.Value;
            if (server.Voidshards.HasValue) _state.Voidshards = (int)server.Voidshards.Value;
            if (server.AetherCrystals.HasValue) _state.AetherCrystals = (int)server.AetherCrystals.Value;
            if (server.Stone.HasValue)      _state.Stone       = (int)server.Stone.Value;
            if (server.Iron.HasValue)       _state.Iron        = (int)server.Iron.Value;
            if (server.Wood.HasValue)       _state.Wood        = (int)server.Wood.Value;

            // Advance ack marker and re-persist locally.
            _lastSyncedSnapshot = Snapshot();
            Save();
            StateReplaced.Invoke();
            Debug.Log("[Sync] Server state merged onto local SO.");
        }

        // ── Core sync pipeline ────────────────────────────────────────────

        private async UniTask SyncToBackend(bool highPriority = false)
        {
            if (_isSyncing) return;
            if (!highPriority && Time.time - _lastSyncTime < MinSyncDelay) return;
            if (string.IsNullOrEmpty(_state?.BoundWallet)) return;   // wallet required

            _isSyncing     = true;
            _lastSyncTime  = Time.time;

            try
            {
                // Drain queued failures first so the server sees events in order.
                await FlushOfflineQueue();

                var delta = BuildDeltaPayload();
                if (delta == null)
                {
                    Debug.Log("[Sync] No changes since last sync — skipped.");
                    return;
                }

                bool ok = await SendDelta(delta);
                if (ok)
                    _lastSyncedSnapshot = Snapshot();
                else
                    EnqueueOffline(delta);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private async UniTask<bool> SendDelta(SyncDeltaPayload delta)
        {
            // `delta` is the "something changed" signal (BuildDeltaPayload gates it);
            // we send the FULL snapshot so save/load round-trip through one shape.
            byte[] body;
            try
            {
                // Serialize the full PersistedState under the SAME camelCase keys
                // LoadFromBackend reads (nested "resources", arrays, …), then add the
                // backend's required lowercase "playerId". The deployed store is a
                // merge-upsert, so strip null fields — never null-out a server value
                // on a partial sync.
                var snapshot = Snapshot();
                var jo = JObject.FromObject(snapshot, JsonSerializer.Create(SaveSchema.JsonSettings));
                foreach (var p in jo.Properties().Where(p => p.Value.Type == JTokenType.Null).ToList())
                    jo.Remove(p.Name);
                jo["playerId"] = _state.BoundWallet;
                body = Encoding.UTF8.GetBytes(jo.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Sync] JSON serialize error: {ex.Message}");
                return false;
            }

            using var req = new UnityWebRequest(SaveUrl, "POST")
            {
                uploadHandler   = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");

            await req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Sync] Saved {body.Length} bytes (full snapshot).");
                return true;
            }

            Debug.LogWarning($"[Sync] Save failed ({req.responseCode}): {req.error}");
            return false;
        }

        // ── Delta builder — compares snapshots via PersistedState ─────────

        private SyncDeltaPayload BuildDeltaPayload()
        {
            var cur  = Snapshot();
            var prev = _lastSyncedSnapshot;   // null on first sync — sends everything
            bool any = false;

            var d = new SyncDeltaPayload
            {
                PlayerId      = _state.BoundWallet,
                SchemaVersion = SaveSchema.CurrentVersion,
            };

            // Resources
            if (ResourcesDiffer(cur, prev))
            {
                var r = cur.Resources ?? default;
                d.Crystals  = r.Crystals;
                d.Food      = r.Food;
                d.Coins     = r.Coins;
                d.Voidshards = (int?)cur.Voidshards;
                d.Stone      = (int?)cur.Stone;
                d.Iron       = (int?)cur.Iron;
                d.Wood       = (int?)cur.Wood;
                any = true;
            }

            // Towers
            if (TowersDiffer(cur, prev))
            {
                d.Towers         = cur.Towers?.Select(v => (int)v).ToArray();
                d.TowerAbilities = cur.TowerAbilities?.Select(v => (int)v).ToArray();
                any = true;
            }

            // Wave
            if (prev == null || cur.BestWave != prev.BestWave)
            {
                d.BestWave = (int?)cur.BestWave;
                any = true;
            }

            // Pets — JSON-encoded; PetData/PetSpecies carry complex Unity types
            // that would require full MessagePack attribute chains to serialize
            // directly, so we embed them as a JSON string inside the MsgPack body.
            if (PetsDiffer(cur, prev))
            {
                d.PetsJson     = JsonConvert.SerializeObject(cur.Pets,       SaveSchema.JsonSettings);
                d.OwnedPetsJson = JsonConvert.SerializeObject(cur.OwnedPets, SaveSchema.JsonSettings);
                d.StarterPetId  = cur.StarterPetId;
                any = true;
            }

            return any ? d : null;
        }

        private static bool ResourcesDiffer(SaveSchema.PersistedState a, SaveSchema.PersistedState b)
        {
            if (b == null) return true;
            var ar = a.Resources; var br = b.Resources;
            return ar?.Crystals != br?.Crystals
                || ar?.Food     != br?.Food
                || ar?.Coins    != br?.Coins
                || a.Voidshards != b.Voidshards
                || a.Stone      != b.Stone
                || a.Iron       != b.Iron
                || a.Wood       != b.Wood;
        }

        private static bool TowersDiffer(SaveSchema.PersistedState a, SaveSchema.PersistedState b)
        {
            if (b == null) return true;
            bool towersMatch   = a.Towers?.SequenceEqual(b.Towers ?? Enumerable.Empty<double>()) ?? b.Towers == null;
            bool abilitiesMatch = a.TowerAbilities?.SequenceEqual(b.TowerAbilities ?? Enumerable.Empty<double>()) ?? b.TowerAbilities == null;
            return !towersMatch || !abilitiesMatch;
        }

        private static bool PetsDiffer(SaveSchema.PersistedState a, SaveSchema.PersistedState b) =>
            b == null
            || (a.Pets?.Count ?? 0)      != (b.Pets?.Count ?? 0)
            || (a.OwnedPets?.Count ?? 0) != (b.OwnedPets?.Count ?? 0)
            || a.StarterPetId != b.StarterPetId;

        // ── Offline queue ─────────────────────────────────────────────────

        private void EnqueueOffline(SyncDeltaPayload delta)
        {
            var queue = LoadOfflineQueue();
            queue.Add(delta);
            PlayerPrefs.SetString(SyncQueueKey,
                JsonConvert.SerializeObject(queue, SaveSchema.JsonSettings));
            PlayerPrefs.Save();
            Debug.LogWarning($"[Sync] Queued offline payload (queue depth: {queue.Count}).");
        }

        private async UniTask FlushOfflineQueue()
        {
            if (!PlayerPrefs.HasKey(SyncQueueKey)) return;
            var queue = LoadOfflineQueue();
            if (queue.Count == 0) return;

            var remaining = new List<SyncDeltaPayload>();
            foreach (var queued in queue)
            {
                if (!await SendDelta(queued)) remaining.Add(queued);
            }

            if (remaining.Count == 0) PlayerPrefs.DeleteKey(SyncQueueKey);
            else
            {
                PlayerPrefs.SetString(SyncQueueKey,
                    JsonConvert.SerializeObject(remaining, SaveSchema.JsonSettings));
            }
            PlayerPrefs.Save();
        }

        private List<SyncDeltaPayload> LoadOfflineQueue()
        {
            var raw = PlayerPrefs.GetString(SyncQueueKey, "[]");
            try
            {
                return JsonConvert.DeserializeObject<List<SyncDeltaPayload>>(
                    raw, SaveSchema.JsonSettings) ?? new List<SyncDeltaPayload>();
            }
            catch
            {
                return new List<SyncDeltaPayload>();
            }
        }

        // ── Data types ────────────────────────────────────────────────────

        /// <summary>
        /// Wire payload sent to /api/game/save. All fields nullable — null means
        /// "no change in this domain, skip the DB column". Flat primitive-only
        /// so MessagePack needs no custom resolvers.
        /// Pets and OwnedPets are pre-serialized JSON strings to avoid nesting
        /// complex Unity types in the delta object.
        /// </summary>
        public sealed class SyncDeltaPayload
        {
            public string PlayerId      { get; set; }
            public int    SchemaVersion { get; set; }

            // Resources — null = domain unchanged
            public int? Crystals  { get; set; }
            public int? Food      { get; set; }
            public int? Coins     { get; set; }
            public int? Voidshards { get; set; }
            public int? Stone     { get; set; }
            public int? Iron      { get; set; }
            public int? Wood      { get; set; }

            // Towers — null = no layout change
            public int[] Towers         { get; set; }
            public int[] TowerAbilities { get; set; }

            // Wave
            public int? BestWave { get; set; }

            // Pets — JSON string (complex Unity types inside)
            public string PetsJson      { get; set; }
            public string OwnedPetsJson { get; set; }
            public string StarterPetId  { get; set; }
        }

        private sealed class BackendLoadResponse
        {
            [JsonProperty("success")] public bool                       Success { get; set; }
            [JsonProperty("data")]    public SaveSchema.PersistedState  Data    { get; set; }
            [JsonProperty("config")]  public ServerConfig               Config  { get; set; }
        }

        // ── Conversions ──────────────────────────────────────────────────────
        private static List<double> ToDoubleList(List<int> src)
        {
            var list = new List<double>(src.Count);
            foreach (var v in src) list.Add(v);
            return list;
        }

        private static List<int> ToIntList(List<double> src)
        {
            var list = new List<int>(src.Count);
            foreach (var v in src) list.Add((int)v);
            return list;
        }
    }
}
