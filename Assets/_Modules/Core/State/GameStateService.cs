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
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;

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
