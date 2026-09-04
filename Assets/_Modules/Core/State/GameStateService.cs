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
//   ResetToNewGame() — the React `reset()` carve-out: wipes progression but keeps
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
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Jobs;
using DeNelle.Core.Web3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace DeNelle.Core.State
{
    /// <summary>
    /// WO-860 Part A - the PlayerPrefs key contract for the per-class equipped-gear
    /// slots. These live OUTSIDE the save envelope (they are PlayerPrefs, not
    /// GameState fields), which is exactly why New Game used to inherit them: the
    /// owner's "I keep getting this axe I tried one time" is a once-equipped
    /// <c>dotr-equip-weapon-knight</c> that no reset ever deleted.
    ///
    /// The prefixes live HERE in Core (not in DeNelle.Village.GearLoadout, which
    /// WRITES them) so the writer and the New-Game eraser share ONE definition -
    /// Village references Core, so GearLoadout consumes these consts directly.
    /// Duplicating the literals is how they would silently drift apart again.
    ///
    /// Key shape: <c>&lt;prefix&gt;&lt;lowercase class key&gt;</c>, e.g.
    /// "dotr-equip-weapon-knight". The class key is the HeroClass enum name
    /// lowercased (StoryCompanionInjector binds companion loadouts the same way),
    /// so <see cref="PlayableHeroes.AllKnownJobKeys"/> enumerates every key a
    /// reset must clear.
    /// </summary>
    public static class EquipPrefKeys
    {
        /// <summary>MAIN-hand weapon, per class.</summary>
        public const string Weapon = "dotr-equip-weapon-";
        /// <summary>Body armor, per class.</summary>
        public const string Armor = "dotr-equip-armor-";
        /// <summary>OFF-hand item / shield, per class.</summary>
        public const string OffHand = "dotr-equip-offhand-";
        /// <summary>Ring accessory, per class (WO-543).</summary>
        public const string Ring = "dotr-equip-ring-";
        /// <summary>Amulet accessory, per class (WO-543).</summary>
        public const string Amulet = "dotr-equip-amulet-";

        /// <summary>
        /// The per-class W/E/R ability-bar loadout key (WO-861 Phase 0 made it per-class).
        /// Shape: "dotr-loadout-&lt;class&gt;-v1". The knight's key is byte-identical to the
        /// pre-861 GLOBAL key "dotr-loadout-knight-v1", so no migration is needed.
        /// </summary>
        public const string LoadoutPrefix = "dotr-loadout-";
        /// <summary>Suffix half of <see cref="LoadoutPrefix"/> (the schema version tag).</summary>
        public const string LoadoutSuffix = "-v1";

        /// <summary>
        /// WO-1019 Part A - the per-class HOT-SWAP (assignable extras) bar key.
        /// Shape: "dotr-skillbar-&lt;class&gt;-extra-v1".
        ///
        /// THE DEFECT THIS RETIRES (owner felt-test 2026-08-10, verbatim: "he inherits the
        /// hotswap from previous character"): AssignableSkillBar persisted under ONE GLOBAL
        /// key, <see cref="SkillBarLegacyGlobalKey"/>. WO-861 Phase 0 made the W/E/R rail
        /// (<see cref="LoadoutPrefix"/>) per-class for exactly this reason and the hot-swap
        /// rail was never given the same treatment, so switching Grom -> Thrain re-rendered
        /// the KNIGHT's assigned skills on the Mage's bar - and the first assign saved them
        /// back over the shared key. Same defect shape as F8 seq-642 (GearLoadout) and
        /// WO-967 (the HUD class literal): one store, many heroes.
        ///
        /// Unlike <see cref="LoadoutKeyFor"/> this key is NOT byte-identical to the legacy
        /// one for any class, so AssignableSkillBar performs a FILTERED one-shot read of the
        /// legacy value: each class inherits only the entries it actually owns
        /// (AbilityCatalog.IsUsableByClass). The contaminating ids are dropped by
        /// construction rather than by a migration step that could get it wrong.
        /// </summary>
        public const string SkillBarPrefix = "dotr-skillbar-";
        /// <summary>Suffix half of <see cref="SkillBarPrefix"/> (bar name + schema version tag).</summary>
        public const string SkillBarSuffix = "-extra-v1";
        /// <summary>The pre-WO-1019 GLOBAL hot-swap bar key, kept ONLY so the filtered
        /// migration read and the New-Game reset can both name the same literal.</summary>
        public const string SkillBarLegacyGlobalKey = "dotr-skillbar-extra-v1";

        /// <summary>Every equip-slot prefix, so a reset can loop instead of listing them.</summary>
        public static readonly string[] AllSlotPrefixes = { Weapon, Armor, OffHand, Ring, Amulet };

        /// <summary>The W/E/R loadout key for a lowercase class key.</summary>
        public static string LoadoutKeyFor(string classKey) =>
            LoadoutPrefix + (string.IsNullOrEmpty(classKey) ? "knight" : classKey.Trim().ToLowerInvariant()) +
            LoadoutSuffix;

        /// <summary>The HOT-SWAP (assignable extras) bar key for a lowercase class key.</summary>
        public static string SkillBarKeyFor(string classKey) =>
            SkillBarPrefix + (string.IsNullOrEmpty(classKey) ? "knight" : classKey.Trim().ToLowerInvariant()) +
            SkillBarSuffix;
    }

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

        // ── Pluggable save IO seam (Tier-2, WO-547) ──────────────────────────
        /// <summary>
        /// The swappable save-IO backend. Serialization (SaveSchema &lt;-&gt; JSON)
        /// stays here in the service; only the raw read/write/exists/delete is
        /// delegated. Defaults to <see cref="LocalSaveProvider"/> (PlayerPrefs) so
        /// behaviour is identical to before the seam. Assign a cloud DB / Solana
        /// implementation to retarget where saves live — a one-line swap, e.g.
        /// <c>GameStateService.Provider = new CloudSaveProvider();</c>.
        /// </summary>
        public static ISaveProvider Provider { get; set; } = new LocalSaveProvider();

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

        /// <summary>
        /// WO-1128 §3.3 — the server's OWN last_seen for this player (unix-ms, same unit
        /// as <c>serverNowMs</c>), as emitted by <c>api/game/load.js</c>. Null until a
        /// cloud load has succeeded this process, and null forever on an older backend
        /// that does not send the field.
        /// <para>
        /// ⛔ THIS IS A READOUT, NOT A CLOCK. It is the anchor the SERVER measures the
        /// client's declared accrual window against; it must never be written into
        /// <see cref="GameState.LastHarvestClaimMs"/>. That field has exactly THREE legal
        /// writers, all inside <c>OfflineClaimCoordinator</c> (AdvanceAndSave / StampClock),
        /// and a fourth writer here would break the single-owner rule the coordinator's
        /// <c>IOfflineClaimConsumer</c> contract states out loud. Rolling the claim clock
        /// back to the server's window would also hand the device a RE-CLAIMABLE stretch on
        /// its next launch — the exact double-grant <c>api/game/save.js</c> refuses to do
        /// server-side for the same reason.
        /// </para>
        /// </summary>
        public double? ServerLastSeenMs { get; private set; }

        /// <summary>
        /// WO-1128 §3.3 — the most recent accrual clamp the server reported on a save, or
        /// null when the last save was accepted whole. Kept for display/diagnostics ("your
        /// offline haul was provisional") so a future screen needs no extra round trip.
        /// </summary>
        public AccrualReconcileReport LastAccrualReconcile { get; private set; }

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
        /// <summary>Raised after a full <see cref="ResetToNewGame"/> or <see cref="Load"/>.</summary>
        public readonly UnityEvent StateReplaced = new UnityEvent();

        /// <summary>
        /// WO-1220 — raised by <see cref="ResetToNewGame"/> ONLY (never by <see cref="Load"/>),
        /// after every persisted field has been re-seeded and before the notify/persist tail.
        ///
        /// THE DEFECT IT EXISTS FOR (owner felt-test 2026-08-26, Seeker 2026.08.26.341419):
        /// a New Game re-seeded ~60 GameState fields and the town came up blank and correct —
        /// and the hero still read <c>Lv 4</c> with a previous class's talent applied. The save
        /// is only HALF of the progression: the other half lives in **DontDestroyOnLoad runtime
        /// singletons that a scene load never touches** —
        ///   • <c>HeroProgression</c> (Village) — the LIVE per-run level/XP authority, which
        ///     writes itself back over GameState on the next XP grant;
        ///   • <c>WisdomCurrencyService</c> (Village) — Wisdom + the unlocked talent-node ids,
        ///     persisted in its OWN PlayerPrefs blob, hero-prefixed but pooled, so a Mage node
        ///     stays applied on a brand-new Ranger;
        ///   • <c>SkillSystem</c> (Core) — craft-skill levels + banked skill points, which are
        ///     not persisted AT ALL, so their survival is proof the process never restarted.
        /// Zeroing the save fields cannot reach any of them. This event does.
        ///
        /// STATIC by necessity: the subscribers outlive any one service instance and two of the
        /// three live in DeNelle.Village, which DeNelle.Core must never reference (CLAUDE.md §5).
        /// Village -> Core subscription is the sanctioned direction. Subscribers MUST unsubscribe
        /// (OnDisable / OnDestroy) — a static event holds its listeners forever otherwise.
        /// </summary>
        public static event Action NewGameStarted;

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

            // START-FLOW GUARANTEE (2026-06-23): the body builder (HeroBodySwapper.ResolveHeroClass)
            // TRUSTS that HeroClass is persisted before it builds, and screams (FlowTrace.Fail) if it
            // is not. Most paths persist it at SELECTION (HeroSelectController) or at the Continue load
            // (TitleController.OnContinue) — but the BYPASS paths reach MainCastle_Hall WITHOUT either:
            //   • the AutoPilot/headless boot (AutoPilotDriver.BootToGameplay loads the castle directly)
            //   • a fresh launch / cleared-or-stale save that was never picked in a prior session
            // This Awake is the ONE chokepoint EVERY entry path crosses (boot scene's service, the
            // RuntimeInitialize auto-instance, and any direct-boot scene). Seal the source HERE: if the
            // load left HeroClass unset, persist V1's default now — ChooseHero applies the KnightOnly
            // force + Save()s — so NO path can reach body-build with HeroClass None. The HeroBodySwapper
            // canary then only ever fires on a true regression (this guarantee removed/broken).
            EnsureHeroClassPersisted();

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
            // §12 TGVRU: ride the load thread top-to-bottom so a player-device save
            // problem (corrupt JSON, missing payload, migration reject, schema fail)
            // self-reports to the break-log instead of silently snapping back to a
            // fresh game. R (safe-default) is unchanged — every reject still keeps
            // fresh state and re-raises StateReplaced; the rejects are now LOUD.
            using var _t = FlowTrace.Enter("Save", "Load (PlayerPrefs -> migrate -> validate -> apply)");

            if (!Provider.Exists(SaveSchema.PlayerPrefsKey))
            {
                SeedBlankFoundingOnMissingSave("no save key present");
                StateReplaced.Invoke();
                return false; // brand-new game — blank founding, not a legacy pre-v30 town.
            }

            // §12: delegate the raw read to the swappable provider, Guarded so an IO
            // failure self-reports (via FlowTrace.Fail) instead of silently blanking.
            string stored = null;
            Guard.Try("Save", "Provider.Read (load save IO)",
                () => stored = Provider.Read(SaveSchema.PlayerPrefsKey));
            FlowTrace.Step("Save", $"read save via {Provider.GetType().Name} (len={(stored?.Length ?? 0)}).");
            if (string.IsNullOrEmpty(stored))
            {
                FlowTrace.Warn("Save", "save key present but value is EMPTY — seeding blank founding (WO-1250).");
                SeedBlankFoundingOnMissingSave("save key present but value is EMPTY");
                StateReplaced.Invoke();
                return false;
            }

            // ── LB-3 save-integrity gate (atomic single-key envelope) ─────────
            // The keyed HMAC is embedded in the FRONT of the stored value and split
            // back out here. A present-but-invalid signature = tamper/corruption →
            // reject and keep fresh defaults (never load the attacker-chosen blob).
            // BACKWARD-COMPAT: a legacy save (raw JSON, no embedded sig) is loaded
            // ONCE, then re-written WITH a sig on the next Save() (do not wipe
            // existing players). The key is client-embedded (best-effort
            // obfuscation; real authority is server-side LB-1).
            string json = SaveSchema.TryExtractSigned(stored, out bool sigPresent, out bool sigValid);
            bool legacyUnsignedSave = !sigPresent;
            if (legacyUnsignedSave)
            {
                FlowTrace.Step("Save", "no integrity signature present — legacy unsigned save; loading once + re-signing on apply.");
            }
            else if (!sigValid)
            {
                FlowTrace.Fail("Save", "[Flow:Save] HMAC mismatch — tamper/corruption; rejecting save, keeping fresh state.");
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
                FlowTrace.Fail("Save", $"Save parse FAILED (corrupt JSON) — keeping fresh state. {ex.GetType().Name}: {ex.Message}");
                StateReplaced.Invoke();
                return false;
            }
            if (file == null || file.State == null)
            {
                FlowTrace.Fail("Save", "Save envelope missing its state payload (file or file.State null) — keeping fresh state.");
                StateReplaced.Invoke();
                return false;
            }

            // Migrate-then-validate: the schema must see the up-to-date shape.
            var migration = SaveMigrator.MigrateForImport(file.State, file.StoreVersion);
            if (!migration.Ok)
            {
                FlowTrace.Fail("Save", $"Save migration REJECTED (storeVersion={file.StoreVersion}) — keeping fresh state. {migration.Reason}");
                StateReplaced.Invoke();
                return false;
            }

            var validation = SaveSchema.Validate(migration.Data);
            if (!validation.Ok)
            {
                FlowTrace.Fail("Save", $"Save schema validation FAILED — keeping fresh state. {validation.Message}");
                StateReplaced.Invoke();
                return false;
            }

            // V (verify): the validated payload applied — the load produced live state.
            ApplyPersisted(validation.Data);
            _state.SchemaVersion = SaveSchema.CurrentVersion;
            FlowTrace.Step("Save", $"Load OK — applied save (storeVersion={file.StoreVersion} -> schema v{SaveSchema.CurrentVersion}).");
            // WO-1220 §12 — name the hero progression this load just installed. A Load that
            // runs AFTER a New Game is one of only two ways GameState.HeroLevel can climb back
            // above 1 (the other is HeroProgression.WriteBackToState, which warns on the same
            // shape), so this line and that one between them account for every re-introduction.
            FlowTrace.Step("Save",
                $"Load OK — hero progression installed from the save: level={_state.HeroLevel} " +
                $"xp={_state.HeroXp:0.#} lifetime={_state.HeroLifetimeXp:0.#}.");
            // LB-3 one-time migration: a legacy save had no integrity signature.
            // Re-write it now so the NEXT load is gated by a valid HMAC. Idempotent
            // (Save writes payload + sig); existing players keep their progress.
            if (legacyUnsignedSave)
            {
                FlowTrace.Step("Save", "migrating legacy unsigned save — re-writing WITH integrity signature.");
                Save();
            }
            StateReplaced.Invoke();
            return true;
        }

        /// <summary>
        /// WO-1250 — a missing/empty save is a brand-new game, not a legacy pre-v30
        /// town. The ScriptableObject default <c>StrategicPlacementMigrated = false</c>
        /// is the UNMIGRATED shape (bake owns the town; the WO-673 writer then grants
        /// every BakedRows id, which after the 2026-08-19 upright bake is the
        /// Weaponsmith and Armorer visuals). ResetToNewGame already sets the marker
        /// true; this path is the one a first APK launch takes when Title "Start New"
        /// has not yet run (or the hub is reached from a boot that skipped it).
        /// Does NOT fire <see cref="NewGameStarted"/> — that event is ResetToNewGame's.
        /// </summary>
        private void SeedBlankFoundingOnMissingSave(string why)
        {
            if (_state == null) _state = ScriptableObject.CreateInstance<GameState>();
            _state.StrategicPlacementMigrated = true;
            _state.EverBuiltStructureIds = new List<string>();
            _state.BaseLayout = new List<PlacedStructureData>();
            FlowTrace.Step("Save",
                $"brand-new game ({why}) — WO-1250 blank founding seeded: " +
                "StrategicPlacementMigrated=true everBuilt=[] BaseLayout=[] " +
                "(Weaponsmith/Armorer baked twins stay down).");
        }

        // =====================================================================
        //  Save — serialize the SO's persisted fields into the SaveFile envelope
        // =====================================================================

        /// <summary>
        /// Serializes the SO's persisted fields (~60 today, was 41 at v10) into the
        /// <see cref="SaveSchema.SaveFile"/>
        /// envelope and writes it to PlayerPrefs key <c>dotr-save</c>. The literal
        /// analog of Zustand <c>persist</c> writing to localStorage.
        /// </summary>
        public void Save()
        {
            if (_state == null) return;
            EnsureAccount("local save");   // mint a guest identity if not logged in (offline-first)

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
                // Serialization stays here; only the raw write is delegated to the
                // swappable provider (LocalSaveProvider by default — PlayerPrefs).
                var json = JsonConvert.SerializeObject(file, SaveSchema.JsonSettings);
                // LB-3: embed the keyed HMAC in the FRONT of the value and write it
                // in a SINGLE atomic Provider.Write, so the payload and signature can
                // never tear apart (a torn pair would reject a valid save → save loss).
                Provider.Write(SaveSchema.PlayerPrefsKey, SaveSchema.EmbedSignature(json));
                FlowTrace.Step("Save", $"wrote signed save via {Provider.GetType().Name} (len={json.Length}).");
            }
            catch (Exception ex)
            {
                // §12 TGVRU: a local save write failure is a player-device save
                // problem — route it to the break-log, not just the console.
                FlowTrace.Fail("Save", $"local Save FAILED (provider write) — progress not persisted this frame. {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds <paramref name="amount"/> crystals to the live state (negative to
        /// spend; clamped &gt;= 0), persists, and raises <see cref="ResourcesChanged"/>.
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

        /// <summary>
        /// Adds <paramref name="amount"/> food to the live state (negative to spend;
        /// clamped &gt;= 0), persists, and raises <see cref="ResourcesChanged"/>.
        /// DEF-121 — Food is one of the four harvestables (Wood/Food/Iron/Crystals);
        /// it lives on the wallet struct (Resources.Food). Mirrors AddCrystals so
        /// harvest/upgrade callers needn't reach into the Resources struct directly.
        /// </summary>
        public void AddFood(int amount)
        {
            if (_state == null) return;
            var r = _state.Resources;
            r.Food = Mathf.Max(0, r.Food + amount);
            _state.Resources = r;
            Save();
            ResourcesChanged.Invoke();
        }

        /// <summary>Snapshots the SO's persisted fields (~60 today, was 41 at v10) into a <see cref="SaveSchema.PersistedState"/>.</summary>
        public SaveSchema.PersistedState Snapshot()
        {
            var s = _state;
            return new SaveSchema.PersistedState
            {
                Pets = s.Pets,
                StarterPetId = s.StarterPetId,
                PetName = s.PetName,   // Audit P2 (save-load) — persist the player-named starter

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
                // WO-1212: `Stone = s.Stone,` removed. The retired balance is no longer
                // written to the wire, so a save this client produces carries no `stone`
                // key at all - which is what makes the field GONE on the next load rather
                // than merely ignored. The nullable wire field stays on PersistedState so
                // an OLDER save (or an older client's row) can still be read and discarded.
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
                GearInventory = s.GearInventory,
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
                LastHarvestClaimMs = s.LastHarvestClaimMs,
                BuildJobs = s.BuildJobs != null ? new List<BuildJobData>(s.BuildJobs) : null,
                AdSkipsUsedToday = s.AdSkipsUsedToday,
                AdSkipDayKey = s.AdSkipDayKey,
                DailyChestDayKey = s.DailyChestDayKey,
                BaseLayout = s.BaseLayout != null ? new List<PlacedStructureData>(s.BaseLayout) : null,
                Magic = s.Magic,
                PartyMemberIds = s.PartyMemberIds != null ? new List<string>(s.PartyMemberIds) : null,
                Zones = s.Zones != null ? new List<DeNelle.Core.World.ZoneState>(s.Zones) : null,   // WO-164 — zone graph (v17)
                ArenaDefense = s.ArenaDefense != null ? new List<PlacedDefenderData>(s.ArenaDefense) : null,   // WO-389 — pre-placed Arena defenders (v19)
                Settlements = s.Settlements != null ? new List<DeNelle.Core.World.SettlementState>(s.Settlements) : null,   // WO-159 — node-settlement claim/HP/lockout (v21)
                Army = s.Army,   // WO-453 — persisted army roster (v22); serialized straight to JSON by the save layer
                BuildingTiers = s.BuildingTiers,   // WO-430 — per-building upgrade tiers (v23); serialized straight to JSON
                VillageTier = s.VillageTier,   // WO-432 — global tech-gate tier (v24)
                OwnedBuildingPerks = s.OwnedBuildingPerks != null ? new List<string>(s.OwnedBuildingPerks) : null,   // WO-432 — owned research perks (v24)
                EchoCount = s.EchoCount,             // ECHO_WORKFORCE_SPEC — owned Echo workers (v25)
                SiloResources = s.SiloResources,     // ECHO_WORKFORCE_SPEC — pooled silo buffer (v25)
                WavesCompleted = s.WavesCompleted,   // ECHO_WORKFORCE_SPEC — Echo-unlock wave counter (v25)
                PopulationXP = s.PopulationXP,             // WO-587 — accumulated Population XP (v28)
                PopulationQuests = s.PopulationQuests,     // WO-587 — Population quest counter (v28)
                PopulationOutposts = s.PopulationOutposts, // WO-587 — Population outpost counter (v28)
                PopulationEchoSlots = s.PopulationEchoSlots, // WO-587 — highest unlocked echo slot (v28)
                HeroLevel = s.HeroLevel,             // F8-47 — hero level (v29)
                HeroXp = s.HeroXp,                   // F8-47 — banked XP toward next level (v29)
                HeroLifetimeXp = s.HeroLifetimeXp,   // F8-47 — lifetime XP counter (v29)
                StrategicPlacementMigrated = s.StrategicPlacementMigrated,   // WO-673 — one-shot bake→BaseLayout migration marker (v30)
                EchoLanes = s.EchoLanes,             // WO-681/658 — per-Echo gather-lane CSV (additive default-on-read)
                FreeBuildsUsed = s.FreeBuildsUsed != null ? new List<string>(s.FreeBuildsUsed) : null,   // v32 — consumed first-build freebies (one-shot, never resets)
                Tribes = s.Tribes != null ? new List<DeNelle.Core.World.TribeState>(s.Tribes) : null,   // WO-160 — roaming-raider progress (v34, REDS #4)
                Wards = s.Wards != null ? new List<DeNelle.Core.World.WardStoneState>(s.Wards) : null,   // WO-112 — relit wards + earned reach (v34, REDS #4)
                Arena = s.Arena,             // ARENA MVP — W/L ledger wins/losses/streak/totalPurse (v34, REDS #4); struct -> nullable wire field
                PetActiveSlots = s.PetActiveSlots != null ? new List<string>(s.PetActiveSlots) : null,   // flag_17 — pet slot->species deploy map (v34, REDS #3)
                ObsidianQueue = s.ObsidianQueue,   // WO-773 — common multi-channel work queue (v35); serialized straight to JSON by the save layer
                BarracksLevel = s.BarracksLevel,   // WO-771.9 — barracks level (additive default-on-read; NO schema bump)
                TroopLevels = s.TroopLevels != null ? new Dictionary<string, int>(s.TroopLevels) : null,   // WO-771.9 — per-troop upgrade levels (additive default-on-read)
                GearLevels = s.GearLevels != null ? new Dictionary<string, int>(s.GearLevels) : null,   // WO-808 — per-instance gear power levels (additive default-on-read)
                EverBuiltStructureIds = s.EverBuiltStructureIds != null ? new List<string>(s.EverBuiltStructureIds) : null,   // WO-834 — ever-player-built ledger (v36; monotonic — the blank-town baked-twin gate input)
                EverAcquiredItemIds = s.EverAcquiredItemIds != null ? new List<string>(s.EverAcquiredItemIds) : null,
                DefenseReports = s.DefenseReports != null ? new List<DeNelle.Core.Defense.DefenseOutcomeRecord>(s.DefenseReports) : null,   // WO-1026 — defence-report ring buffer (additive default-on-read; NO schema bump)
                LastSiegeUnixMs = s.LastSiegeUnixMs,   // WO-1026 — siege cadence clock (SEPARATE from LastHarvestClaimMs by design — WO-1147)
                EverCompletedRaid = s.EverCompletedRaid,   // WO-823 Phase E (v41) - has this save ever finished a raid; the ONE input to the first-raid soft gate
                RaidCooldowns = s.RaidCooldowns != null ? new List<RaidCooldownRecord>(s.RaidCooldowns) : null,   // WO-728 — per-camp raid cooldown windows (additive default-on-read; NO schema bump)
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
            if (p.PetName != null) s.PetName = p.PetName;   // Audit P2 (save-load) — restore named starter
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
            // WO-1212 - the `stone` WIRE key is an INBOUND ALIAS onto the ONE live Stone
            // balance (Resources.Food), never a second field. It lands only when the payload
            // carries no `resources` block at all - a sender that speaks `stone` and nothing
            // else - so nothing is silently dropped on the floor. When the live slot IS
            // present (every save this client has ever written), the stored number is
            // DISCARDED and said out loud: its only writers were the new-game seed and a dev
            // top-up, so folding it in would credit value nobody earned. See GameState.cs.
            if (p.Stone.HasValue && p.Stone.Value != 0d)
            {
                if (!p.Resources.HasValue)
                {
                    var aliased = s.Resources;
                    aliased.Food = (int)p.Stone.Value;
                    s.Resources = aliased;
                    FlowTrace.Warn("Save",
                        $"WO-1212: legacy `stone` wire key ALIASED onto the live Stone slot " +
                        $"(Resources.Food={aliased.Food}); the payload carried no `resources` block.");
                }
                else
                {
                    FlowTrace.Step("Save",
                        $"WO-1212: DISCARDED retired balance stone={p.Stone.Value:0}. Nothing read or " +
                        $"spent it; the live Stone the player sees is Resources.Food={s.Resources.Food}, " +
                        "left untouched. Discard by design - the field only ever held a seed or a dev top-up.");
                }
            }
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
            if (p.GearInventory != null) s.GearInventory = p.GearInventory;   // v20; null on old saves → keep default empty
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
            if (p.LastHarvestClaimMs.HasValue) s.LastHarvestClaimMs = p.LastHarvestClaimMs.Value;
            if (p.BuildJobs != null) s.BuildJobs = p.BuildJobs;
            if (p.AdSkipsUsedToday.HasValue) s.AdSkipsUsedToday = (int)p.AdSkipsUsedToday.Value;
            if (p.AdSkipDayKey != null) s.AdSkipDayKey = p.AdSkipDayKey;
            if (p.DailyChestDayKey != null) s.DailyChestDayKey = p.DailyChestDayKey;
            if (p.BaseLayout != null) s.BaseLayout = p.BaseLayout;
            if (p.Magic.HasValue) s.Magic = (int)p.Magic.Value;   // DEF-121 — tech-axis currency
            if (p.PartyMemberIds != null) s.PartyMemberIds = p.PartyMemberIds;   // WO-301 — party roster
            if (p.Zones != null) s.Zones = p.Zones;   // WO-164 — zone graph (v17)
            if (p.ArenaDefense != null) s.ArenaDefense = p.ArenaDefense;   // WO-389 — pre-placed Arena defenders (v19)
            if (p.Settlements != null) s.Settlements = p.Settlements;   // WO-159 — node-settlement claim/HP/3-day razed lockout (v21)
            s.Army = p.Army ?? new ArmyStorage();      // WO-453 — army roster (v22); never null (older saves load an empty cap-10 army)
            s.BuildingTiers = p.BuildingTiers ?? new System.Collections.Generic.Dictionary<string, int>();   // WO-430 — building tiers (v23); never null
            s.VillageTier = p.VillageTier;   // WO-432 — tech-gate tier (v24); 0 on older saves
            s.OwnedBuildingPerks = p.OwnedBuildingPerks ?? new System.Collections.Generic.List<string>();   // WO-432 — owned research perks (v24); never null
            if (p.EchoCount.HasValue) s.EchoCount = (int)p.EchoCount.Value;             // ECHO_WORKFORCE_SPEC — owned Echoes (v25); absent → migrator seeds 1
            if (p.SiloResources.HasValue) s.SiloResources = p.SiloResources.Value;     // ECHO_WORKFORCE_SPEC — silo buffer (v25); absent → keep 0
            if (p.WavesCompleted.HasValue) s.WavesCompleted = (int)p.WavesCompleted.Value;  // ECHO_WORKFORCE_SPEC — wave counter (v25); absent → keep 0
            if (p.PopulationXP.HasValue) s.PopulationXP = (int)p.PopulationXP.Value;                 // WO-587 — Population XP (v28); absent → keep 0
            if (p.PopulationQuests.HasValue) s.PopulationQuests = (int)p.PopulationQuests.Value;     // WO-587 — quest counter (v28); absent → keep 0
            if (p.PopulationOutposts.HasValue) s.PopulationOutposts = (int)p.PopulationOutposts.Value; // WO-587 — outpost counter (v28); absent → keep 0
            if (p.PopulationEchoSlots.HasValue) s.PopulationEchoSlots = (int)p.PopulationEchoSlots.Value; // WO-587 — unlocked echo slots (v28); absent → migrator seeds 1
            if (p.HeroLevel.HasValue) s.HeroLevel = (int)p.HeroLevel.Value;             // F8-47 — hero level (v29); absent → migrator seeds 1
            if (p.HeroXp.HasValue) s.HeroXp = (float)p.HeroXp.Value;                    // F8-47 — banked XP (v29); absent → keep 0
            if (p.HeroLifetimeXp.HasValue) s.HeroLifetimeXp = (float)p.HeroLifetimeXp.Value; // F8-47 — lifetime XP (v29); absent → keep 0
            if (p.StrategicPlacementMigrated.HasValue) s.StrategicPlacementMigrated = p.StrategicPlacementMigrated.Value; // WO-673 — migration marker (v30); absent → migrator seeds false
            if (p.EchoLanes != null) s.EchoLanes = p.EchoLanes;   // WO-681/658 — echo lane CSV; absent → keep the "wood" starter default
            if (p.FreeBuildsUsed != null) s.FreeBuildsUsed = p.FreeBuildsUsed;   // v32 — consumed freebies; absent → keep the fresh empty list (old save gains full freebies, correct)
            if (p.Tribes != null) s.Tribes = p.Tribes;   // WO-160 — roaming-raider progress (v34); absent → keep the fresh empty list (migrator seeds [])
            if (p.Wards != null) s.Wards = p.Wards;       // WO-112 — relit wards + earned reach (v34); absent → keep the fresh empty list
            if (p.Arena.HasValue) s.Arena = p.Arena.Value;   // ARENA MVP — W/L ledger (v34); absent → keep the SO's zeroed default (migrator seeds Empty)
            if (p.PetActiveSlots != null) s.PetActiveSlots = p.PetActiveSlots;   // flag_17 — pet slot->species deploy map (v34); absent → PetAcquisitionService falls back to the legacy starter-in-slot-0 rebuild
            s.ObsidianQueue = p.ObsidianQueue ?? ObsidianQueueState.Empty();   // WO-773 — common work queue (v35); never null (older saves are built by the v34→v35 migrator, a truly-absent queue = a fresh empty three-channel queue)
            if (p.BarracksLevel.HasValue) s.BarracksLevel = p.BarracksLevel.Value < 1 ? 1 : p.BarracksLevel.Value;   // WO-771.9 — barracks level; absent → keep the SO's default (1)
            if (p.TroopLevels != null) s.TroopLevels = p.TroopLevels;   // WO-771.9 — per-troop upgrade levels; absent → keep the fresh empty dict (all baseline)
            if (p.GearLevels != null) s.GearLevels = p.GearLevels;   // WO-808 — per-instance gear levels; absent → keep the fresh empty dict (all baseline)
            if (p.EverBuiltStructureIds != null) s.EverBuiltStructureIds = p.EverBuiltStructureIds;   // WO-834 — ever-built ledger (v36); absent → keep the fresh empty list (MigrateToV36 seeds real pre-v36 saves before this runs)
            if (p.EverAcquiredItemIds != null) s.EverAcquiredItemIds = p.EverAcquiredItemIds;
            if (p.DefenseReports != null)
            {
                // WO-1026 — defence reports; absent → keep the fresh empty list (a pre-WO-1026
                // save simply has no attack history, which is literally true). NORMALISED on read
                // so no panel/oracle ever meets a null sub-object from a partial or older wire.
                for (int i = 0; i < p.DefenseReports.Count; i++)
                    p.DefenseReports[i] = DeNelle.Core.Defense.DefenseOutcomeRecord.Normalize(p.DefenseReports[i]);
                s.DefenseReports = p.DefenseReports;
            }
            if (p.LastSiegeUnixMs.HasValue) s.LastSiegeUnixMs = p.LastSiegeUnixMs.Value;   // WO-1026 — siege cadence clock; absent → 0 = seed forward, no retroactive siege
            if (p.RaidCooldowns != null)
            {
                // WO-728 — per-camp raid cooldowns; absent → keep the fresh empty list (a
                // pre-WO-728 save simply has no camp recovering, which is literally true).
                // NORMALISED on read so no card/oracle ever meets a null id or a NaN stamp
                // from a partial, older, or hand-edited wire.
                for (int i = 0; i < p.RaidCooldowns.Count; i++)
                    p.RaidCooldowns[i] = RaidCooldownRecord.Normalize(p.RaidCooldowns[i]);
                s.RaidCooldowns = p.RaidCooldowns;
            }
            // WO-823 Phase E (v41) - the first-raid soft gate signal. Absent on a pre-v41
            // wire -> keep GameState's false, which is FAIL-OPEN (an easier first raid),
            // never a lockout; MigrateToV41 derives a better answer for real old saves
            // before this runs.
            if (p.EverCompletedRaid.HasValue) s.EverCompletedRaid = p.EverCompletedRaid.Value;
            EnsureZoneGraph(s);                       // backfill a pre-v17 / empty save's zone graph
        }

        /// <summary>
        /// WO-164 — idempotently seeds the zone graph onto <paramref name="s"/> ONLY when
        /// it is null or empty (so the 5 default zones can never duplicate across the
        /// fresh-save, post-load and migrator call sites). Shared by ResetToNewGame and
        /// the post-load backfill in <see cref="ApplyPersisted"/>.
        /// </summary>
        private static void EnsureZoneGraph(GameState s)
        {
            if (s == null) return;
            if (s.Zones != null && s.Zones.Count > 0) return;
            s.Zones = new List<DeNelle.Core.World.ZoneState>(DeNelle.Core.World.ZoneManager.DefaultZoneGraph());
        }

        // =====================================================================
        //  Mutators — typed ports of the Week-1 Zustand slice actions
        // =====================================================================

        /// <summary>playerSlice <c>finishOnboarding</c> — mark onboarding complete.</summary>
        public void FinishOnboarding()
        {
            _state.Onboarded = true;

            // WO-301 — the canonical JOIN MOMENT: completing the tutorial enrols the
            // first companion into the persisted party (Sylas at beat-1). This is the
            // single chokepoint both FTUE completion paths funnel through (the Yarn
            // <<enable_full_controls>> command and the dialogue-less inline fallback),
            // so the companion "knows it's in the party" from here on — every spawn
            // reads the roster, and a party UI frame shows. The companion is always a
            // DIFFERENT class from the player's hero (canon hero→companion mapping),
            // computed Core-side so Core never references DeNelle.Village (circular ref).
            AddToParty(FirstCompanionId());   // AddToParty fires PlayerChanged + Save (idempotent)

            PlayerChanged.Invoke();
            Save();
        }

        /// <summary>
        /// WO-301 — the id of the first companion who joins on tutorial complete. The
        /// companion is always a DIFFERENT class from the player's chosen hero (the
        /// canon hero→companion mapping — Mage→Knight·Knight→Ranger·Ranger→Cleric·
        /// Cleric→Mage; mirrors DeNelle.Village.CompanionSpawner.CompanionClassFor),
        /// so it never spawns as a hero clone. The id is the companion's HeroClass name.
        /// </summary>
        private string FirstCompanionId()
        {
            HeroClass player = _state != null ? (_state.HeroClass.ToNullable() ?? HeroClass.Knight) : HeroClass.Knight;
            HeroClass companion;
            switch (player)
            {
                case HeroClass.Mage:   companion = HeroClass.Knight; break;  // → Grom
                case HeroClass.Knight: companion = HeroClass.Ranger; break;  // → Sylas
                case HeroClass.Ranger: companion = HeroClass.Cleric; break;  // → Elara
                case HeroClass.Cleric: companion = HeroClass.Mage;   break;  // → Thrain
                default:               companion = HeroClass.Ranger; break;  // → Sylas (beat-1 default)
            }
            return companion.ToString();
        }

        // ── Party roster mutators (WO-301) ────────────────────────────────────

        /// <summary>
        /// WO-301 — add a companion <paramref name="id"/> to the persisted party
        /// roster (in join order), persist, and raise <see cref="PlayerChanged"/> so
        /// spawn + party-UI refresh. Idempotent: a member already in the party is a
        /// no-op (no duplicate frame, no extra save). The id is the companion's
        /// <see cref="HeroClass"/> name (e.g. "Ranger").
        /// </summary>
        public void AddToParty(string id)
        {
            if (_state == null || string.IsNullOrEmpty(id))
            {
                FlowTrace.Warn("Roster", $"AddToParty('{id}') no-op (state null or empty id)");
                return;
            }
            if (_state.PartyMemberIds == null) _state.PartyMemberIds = new List<string>();
            bool already = _state.PartyMemberIds.Contains(id);
            FlowTrace.Step("Roster", $"guard for {id}: alreadyPresent={already} " +
                $"(checked PartyMemberIds=[{string.Join(",", _state.PartyMemberIds)}])");
            if (already) return;   // already in the party
            _state.PartyMemberIds.Add(id);
            FlowTrace.Step("Roster", $"AddToParty: enrolled '{id}' -> roster now [{string.Join(",", _state.PartyMemberIds)}] (fires PlayerChanged -> spawn)");
            PlayerChanged.Invoke();
            Save();
        }

        /// <summary>
        /// WO-301 — remove a companion <paramref name="id"/> from the party roster,
        /// persist, and raise <see cref="PlayerChanged"/>. No-op when not present.
        /// </summary>
        public void RemoveFromParty(string id)
        {
            if (_state == null || string.IsNullOrEmpty(id)) return;
            if (_state.PartyMemberIds == null) return;
            if (!_state.PartyMemberIds.Remove(id)) return;   // wasn't in the party
            PlayerChanged.Invoke();
            Save();
        }

        /// <summary>WO-301 — true when companion <paramref name="id"/> is in the party roster.</summary>
        public bool IsInParty(string id)
        {
            if (_state == null || string.IsNullOrEmpty(id) || _state.PartyMemberIds == null) return false;
            return _state.PartyMemberIds.Contains(id);
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

        /// <summary>playerSlice <c>bindWallet</c> — tag the save to a wallet. Idempotent.
        /// <para>UNATTESTED by design: this overload keys the LOCAL save only. It cannot
        /// grant a cloud identity, because callers that reach it (email sign-in, skin
        /// paths, debug tools) have no way to prove the string came from a real signing
        /// wallet. Use the attested overload for that.</para></summary>
        public void BindWallet(string address) => BindWallet(address, attestedRealWallet: false);

        /// <summary>
        /// Tag the save to a wallet, optionally ATTESTING that <paramref name="address"/>
        /// came from a real, key-holding, signing wallet provider
        /// (WalletService.IsRealSigningWallet) — the ONLY way an address becomes a cloud
        /// save key (see IsRealWalletConnected).
        /// <para>
        /// The attestation is stored per-device in PlayerPrefs, NOT in the save envelope:
        /// it is a statement about what this device proved, so it must not travel with a
        /// copied/restored save, and keeping it out of the envelope avoids a schema bump.
        /// An attested address is never DOWNGRADED by a later unattested bind of the same
        /// string (the early-out below), and binding a different address simply stops
        /// matching, which fails the allowlist automatically.
        /// </para>
        /// </summary>
        public void BindWallet(string address, bool attestedRealWallet)
        {
            if (attestedRealWallet)
            {
                if (IsCloudIdentityShaped(address))
                {
                    PlayerPrefs.SetString(AttestedIdentityKey, address);
                    PlayerPrefs.Save();
                    _warnedUnattested = false;
                    FlowTrace.Step("Save", "cloud identity ATTESTED by a real signing wallet - cloud sync enabled.");
                }
                else
                {
                    // Refuse loudly: a "real" provider handing back something that is not
                    // wallet-shaped is a provider bug, and silently trusting it is exactly
                    // the hole the allowlist exists to close.
                    FlowTrace.Fail("Save",
                        "a wallet provider claimed a REAL connection but the address is not base58/32-44 " +
                        "(len=" + (address?.Length ?? 0) + ") - attestation REFUSED, staying local-only.");
                }
            }

            if (_state.BoundWallet == address) return;
            _state.BoundWallet = address;
            PlayerChanged.Invoke();
            Save();
        }

        /// <summary>
        /// Binds a non-wallet player id only after the backend has verified its provider credential
        /// and minted a session. Kept separate from BindWallet so a Play identity can never acquire
        /// wallet attestation and wallet-only artifacts retain their existing path unchanged.
        /// </summary>
        public bool BindVerifiedExternalIdentity(string playerId)
        {
            if (!IsGooglePlayIdentity(playerId))
            {
                FlowTrace.Fail("Auth", "external identity bind refused: unsupported player-id shape.");
                return false;
            }
            if (_state == null) return false;
            if (_state.BoundWallet != playerId)
            {
                _state.BoundWallet = playerId;
                PlayerChanged.Invoke();
                Save();
            }
            return true;
        }

        public static bool IsGooglePlayIdentity(string id)
        {
            const string prefix = "play-";
            if (string.IsNullOrEmpty(id) || id.Length != prefix.Length + 64 ||
                !id.StartsWith(prefix, StringComparison.Ordinal)) return false;
            for (int i = prefix.Length; i < id.Length; i++)
            {
                char c = id[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            }
            return true;
        }

        /// <summary>playerSlice <c>chooseHero</c> — lock in the hero class. Idempotent.</summary>
        public void ChooseHero(HeroClass cls)
        {
            // WO-861 Phase 0: the Knight force is no longer HARDCODED here. The single
            // roster truth is PlayableHeroes, which since the 2026-08-05 unlock resolves to
            // { Knight, Ranger, Mage } (ff.knightonly defaults OFF) - so a Ranger or Mage
            // pick is now KEPT, and only the Cleric still coerces. A selection is
            // coerced ONLY when it is not in the playable set, and the whole game widens
            // together the moment that set does. Never silently swallow the coercion:
            // "I picked Sylas and got Grom" must be readable in the trace.
            if (!PlayableHeroes.IsPlayable(cls))
            {
                FlowTrace.Warn("Save",
                    $"ChooseHero({cls}) is NOT in the playable set [{PlayableHeroes.Describe()}] - " +
                    $"coerced to {PlayableHeroes.Default}.");
                cls = PlayableHeroes.Default;
            }
            var opt = ((HeroClass?)cls).ToOpt();
            if (_state.HeroClass == opt) return;
            _state.HeroClass = opt;
            PlayerChanged.Invoke();
            Save();
        }

        /// <summary>
        /// START-FLOW GUARANTEE — persists a default HeroClass at session/load init if (and only if)
        /// the loaded state left it unset, so a bypass entry path (headless/AutoPilot boot, fresh
        /// launch, or a save never picked in a prior session) can NEVER reach body-build with
        /// HeroClass = None. Idempotent: a no-op when a real pick is already persisted.
        /// The default it persists is <see cref="PlayableHeroes.Default"/> (Knight), and that stayed
        /// correct through the 2026-08-05 multi-hero unlock: only a player who never reached the
        /// select screen lands here, and Knight is still the roster's opening slot. It is a
        /// FALLBACK, not a force - a real Ranger/Mage pick is persisted by ChooseHero and never
        /// touched by this method.
        /// </summary>
        private void EnsureHeroClassPersisted()
        {
            if (_state != null && _state.HeroClass.ToNullable().HasValue) return; // already picked — leave it
            FlowTrace.Warn("Save",
                $"EnsureHeroClassPersisted: HeroClass was UNSET after load - persisting the default " +
                $"({PlayableHeroes.Default}) at the session/load source so no bypass entry path reaches " +
                "body-build with HeroClass None.");
            ChooseHero(PlayableHeroes.Default); // playable-set checked + Save()
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
        /// ResetToNewGame simply omits them.
        /// <para>
        /// THE STANDING RULE (audit 2026-08-02): every persisted GameState field is either
        /// assigned in this body or is a documented carve-out above. Two were neither -
        /// <see cref="GameState.Settlements"/> (never assigned) and
        /// <see cref="GameState.Zones"/> (only backfilled) - so a "new" game inherited the
        /// old realm's discovery flags and node claims. ResetToNewGameFullClearRegression
        /// now enumerates GameState by reflection and fails on the NEXT unassigned field,
        /// because the defect here is always a field someone forgot, never a wrong value.
        /// </para>
        /// </summary>
        public void ResetToNewGame()
        {
            // Lazy-init mirrors Awake: ResetToNewGame() is "New Game" and must work even when called
            // before Awake has run — EditMode tests AddComponent without the MonoBehaviour
            // lifecycle, and a reset-before-load path would otherwise null-deref here (every
            // other accessor guards _state; Reset() must too). Fixes 16 save/reset tests.
            if (_state == null) _state = ScriptableObject.CreateInstance<GameState>();
            var s = _state;
            // WO-1220 §12 — the ORDER is the evidence. The owner's device showed the hero
            // restoring at level 4 AFTER this method ran, so the one thing the trace has to
            // make legible is what the hero fields held ON ENTRY vs what this reset leaves,
            // vs what the LIVE HeroProgression stamps back afterwards (see
            // HeroProgression.WriteBackToState, which now warns when it clobbers a fresh
            // save). Captured before a single assignment lands.
            int priorHeroLevel = s.HeroLevel;
            float priorHeroXp = s.HeroXp;
            FlowTrace.Step("Save",
                $"ResetToNewGame: ENTER — hero on entry level={priorHeroLevel} xp={priorHeroXp:0.#} " +
                $"lifetime={s.HeroLifetimeXp:0.#} (about to re-seed every persisted field).");
            // PET-ACQUISITION REWORK (owner 2026-06-13): a New Game starts with NO pet —
            // the pet is acquired ONLY from the Echo Hollow pet-shop (PetHouse Yarn node →
            // <<spawn_named_pet>> → PetAcquisitionService.Acquire), never pre-granted. So a
            // New Game must clear EVERY pet-ownership field, not just the roster: OwnedPets
            // (the PetSpecies enum mirror) and PetName previously survived the reset, which
            // left a "ghost" owned pet on a repeat New Game (the "pet not reset on start new"
            // flag). Reset them all here so the loop is repeatable from a true blank slate.
            s.Pets = new List<PetData>();
            s.StarterPetId = null;
            s.OwnedPets = new List<PetSpecies>();   // pet-acquisition rework — clear the owned-species mirror.
            s.PetName = null;                        // pet-acquisition rework — clear the player-named starter.
            s.PetBonds = new List<int> { 0, 0, 0 };  // (re)zero guardian bond ranks for the fresh pet.
            s.Onboarded = false;
            s.BestWave = 0;
            s.Resources = ResourceBalance.Starter;
            s.OwnedItemIds = new List<string>();
            // (PetBonds reset moved up into the pet-acquisition-rework block above.)
            s.Voidshards = 5;
            s.AetherCrystals = 0;
            s.Towers = GameState.NewZeroed(Constants.TowerSlots);
            s.TowerAbilities = GameState.NewZeroed(Constants.TowerSlots);
            s.WallLevel = 0;
            // WO-1212: the invisible Stone seed is REMOVED. It seeded a balance no HUD read
            // and no cost spent, so nothing player-visible changes. It is DROPPED, not folded
            // into Resources.Food: the ticket's own later correction rules `discard, do not
            // sum`, and folding would quietly raise a fresh town's VISIBLE Stone from 80 to
            // 100 - a founding-economy change on the day WO-1217 ruled that ladder. One line
            // (`s.Resources.Food += 20;`) restores it if the lead rules the other way.
            // Owner ruling 2026-07-13 evening — the founding seed is ZERO: the per-id
            // free-first-build flags (FreeBuildsUsed, below) REPLACE the resource seed.
            // Players earn everything beyond the one-free-each kit from production
            // (prevents all-defense-no-town). StartingBudget stays the one authoritative
            // pair (NestedTypes.cs), now 0/0.
            s.Iron = StartingBudget.StrategicIron;
            s.Wood = StartingBudget.StrategicWood;
            // WO-1217 Slice B (owner ruling 2026-08-26, verbatim: "so start gold at 200").
            // Gold IS Resources.Coins — the shop/sell/research wallet — so this must land
            // AFTER `s.Resources = ResourceBalance.Starter` above, whose coins:15 it replaces.
            // Seeded through StartingBudget for the same reason wood/iron are: ONE authoritative
            // home for the founding budget, no literal scattered here.
            // ⛔ The two lines ABOVE stay ZERO (owner ruling 2026-07-13). The invisible
            // Stone seed further up was WO-1212's to retire and IS now retired; neither is
            // touched by this ruling.
            s.Resources.Coins = StartingBudget.StrategicGold;
            s.BuildingCooldowns = new SerializableDict<string, double>();
            s.PendingBuilds = new List<PendingTowerBuild>();
            s.TutorialStep = TutorialStep.Step1;
            s.JoystickSensitivity = 1f;
            s.SeenTutorials = new SerializableDict<string, bool>();
            // Wipe the hero so onboarding re-prompts. Do NOT unbind the wallet.
            s.HeroClass = HeroClassOpt.None;
            s.Inventory = AtbInventory.Empty;
            // WO-949 (owner F8 2026-08-10 "Can we start the user with some potions"): the founding
            // kit now includes StartingBudget.FoundingHealPotions Minor Healing Draughts. This dict
            // IS the persisted VillageInventory larder (VillageInventory.EnsureLoaded pulls it), and
            // the key is the canonical belt-potion id (HudCommands.HpPotionId = "minor-heal-potion")
            // the HUD potion slot + ConsumableUseService.TryUse consume — so the granted stack shows
            // on the belt badge from turn one. Every New Game routes through here
            // (TitleController:359), which is the ONE founding grant seam; existing saves are
            // untouched (no read-migration grant). No schema bump: the dict shape is unchanged.
            // WO-1235 adds the Mana Draughts to the SAME dictionary — deliberately not a second
            // grant seam. The WO is explicit: "Do not invent a second grant path". The key is
            // HudCommands.ManaPotionId ("cons_mana_draught"), which is the id the HUD's mana slot
            // and ConsumableUseService.TryUse already consume — NOT HpPotionId, which is health.
            s.GearInventory = new System.Collections.Generic.Dictionary<string, int>
            {
                { DeNelle.Core.HUD.HudCommands.HpPotionId, StartingBudget.FoundingHealPotions },
                { DeNelle.Core.HUD.HudCommands.ManaPotionId, StartingBudget.FoundingManaPotions },
            };
            FlowTrace.Step("Founding",
                "founding grant: " + StartingBudget.FoundingHealPotions + "x '" +
                DeNelle.Core.HUD.HudCommands.HpPotionId + "' + " +
                StartingBudget.FoundingManaPotions + "x '" +
                DeNelle.Core.HUD.HudCommands.ManaPotionId +
                "' seeded into the larder (WO-949 / WO-1235).");
            s.AtbLossStreak = 0;
            s.BuildingDamage = new SerializableDict<string, double>();
            s.Dungeons = DungeonProgress.Empty();
            s.ActiveDungeonRun = null;
            s.Quests = QuestProgress.Empty();
            s.Regions = RegionProgress.Empty();
            s.LastHarvestClaimMs = 0;   // New Game → reseed the accrual clock on next load (no haul).
            s.BuildJobs = new List<BuildJobData>();   // WO-172 — clear in-flight construction timers.
            s.ObsidianQueue = ObsidianQueueState.Empty();   // WO-773 — New Game: an empty three-channel work queue (no jobs on Builder/Train/Research).
            s.BarracksLevel = 1;                             // WO-771.9 — New Game: day-one barracks (unlocks Footman + Archer).
            s.TroopLevels = new System.Collections.Generic.Dictionary<string, int>();   // WO-771.9 — New Game: every troop at baseline (level 1).
            s.GearLevels = new System.Collections.Generic.Dictionary<string, int>();   // WO-808 — New Game: all gear at authored baseline (level 1).
            s.AdSkipsUsedToday = 0;
            s.AdSkipDayKey = null;
            s.DailyChestDayKey = null;
            // WO-108/WO-682/WO-707 — New Game starts on the BLANK template (owner ruling
            // 2026-07-12 "I want to see the blank template and add buildings"): authored
            // shell only, ZERO pre-placed functional buildings. The WO-682 FTUE "grace
            // default" Forge that used to be pre-placed here was KILLED by owner ruling
            // 2026-07-13 ("I don't want the forge to start with — should be placed by
            // player"): every building, Forge included, is the player's to place from the
            // WO-707 founding seed (650w/385i affords one of each + the 3 containers).
            // Vendor talk-routes now come online only as their buildings are placed; the
            // guided first-placement FTUE is WO-702 (Sylas the Steward).
            s.BaseLayout = new List<PlacedStructureData>();
            s.ArenaDefense = new List<PlacedDefenderData>();  // WO-389 — New Game starts with no pre-placed Arena defense.
            s.Army = new ArmyStorage();                       // WO-453 — New Game starts with an empty cap-10 army.
            s.BuildingTiers = new System.Collections.Generic.Dictionary<string, int>();   // WO-430 — New Game: all buildings at tier 0 (locked).
            s.VillageTier = 0;   // WO-432 — New Game: village tier 0 (no research gated open yet).
            s.OwnedBuildingPerks = new System.Collections.Generic.List<string>();   // WO-432 — New Game: no research perks owned.
            s.Magic = 0;                                      // DEF-121 — tech-axis currency resets on New Game.
            s.EchoCount = 1;                                  // ECHO_WORKFORCE_SPEC — New Game starts with the 1 starter Echo.
            s.SiloResources = 0;                              // ECHO_WORKFORCE_SPEC — empty silo on New Game.
            s.WavesCompleted = 0;                             // ECHO_WORKFORCE_SPEC — no waves cleared yet.
            s.PopulationXP = 0;                               // WO-587 — New Game starts with no Population XP.
            s.PopulationQuests = 0;                           // WO-587 — no quests counted yet.
            s.PopulationOutposts = 0;                         // WO-587 — no outposts counted yet.
            s.PopulationEchoSlots = 1;                        // WO-587 — start with the 1 starter echo slot (Wood).
            s.HeroLevel = 1;                                  // F8-47 — New Game starts a fresh level-1 hero.
            s.HeroXp = 0f;                                    // F8-47 — no banked XP yet.
            s.HeroLifetimeXp = 0f;                            // F8-47 — no lifetime XP yet.
            s.StrategicPlacementMigrated = true;              // WO-682 — New Game: nothing to migrate (no auto-placed town was ever granted); marker SET so the one-shot writer never runs and the bake standdown activates = the blank template. Existing saves still migrate via the marker-false path (SaveMigrator v30 seeds false).
            s.EchoLanes = "harvest:1";                        // WO-738 — New Game: the starter Echo (Frosthowl, index 0) is assigned to the Harvest lane at level 1, so it visibly gathers from turn one (its PREFERRED lane is the stubbed Exploration, but the owner ruling is the first echo must gather). Later Echoes idle until assigned. Richer "lane:level" token grammar.
            s.PartyMemberIds = new List<string>();            // WO-301 — start alone; the first companion joins on tutorial complete.
            s.FreeBuildsUsed = new List<string>();            // v32 — New Game: every catalog id's one-time FREE first build is live again (per-save flags; they replace the retired wood/iron founding seed).
            s.DefenseReports = new List<DeNelle.Core.Defense.DefenseOutcomeRecord>();   // WO-1026 — New Game: no attack history.
            s.LastSiegeUnixMs = 0;                            // WO-1026 — New Game: reseed the siege cadence clock on first evaluation (no retroactive assault).
            s.RaidCooldowns = new List<RaidCooldownRecord>();  // WO-728 — New Game: no camp is recovering, every raid is available. AUDIT NOTE: this line is what stops "Start New" inheriting the previous save's lockouts — exactly the Settlements defect found 2026-08-02 directly below.
            s.EverCompletedRaid = false;                      // WO-823 Phase E (v41) - New Game: no raid has ever been finished, so the FIRST raid is softened to 3 deployable slots. RaidDeployController.ReconcileRaidEnd stamps it true at the first raid exit (victory, retreat OR hero death) and the full army cap applies from then on, permanently.
            s.EverBuiltStructureIds = new List<string>();     // WO-834 (v36) — New Game: nothing ever built. With StrategicPlacementMigrated=true (above) this makes every baked twin's surface gate CLOSED = the truly blank Build-Your-Own town; choosing Default Town clears the marker and the migration writer then grants the template ids.
            s.EverAcquiredItemIds = new List<string>();       // New Game: no item-discovery progression earned.
            s.Tribes = new List<DeNelle.Core.World.TribeState>();          // WO-160 (v34) — New Game: no claimed tribe progress (managers re-seed from defs).
            s.Wards = new List<DeNelle.Core.World.WardStoneState>();       // WO-112 (v34) — New Game: no relit wards (base reach only).
            s.Arena = ArenaProgress.Empty;                    // ARENA MVP (v34) — New Game: zeroed W/L ledger.
            s.PetActiveSlots = new List<string>();            // flag_17 (v34) — New Game: no pet slotted (no pet owned on a fresh save).
            s.Settlements = new List<DeNelle.Core.World.SettlementState>();   // WO-159 (v21) — New Game: no claimed or razed node settlements. AUDIT 2026-08-02: this line was MISSING, so "Start New" inherited every claimed/razed node from the previous save INCLUDING its 3-day razed lockout. Added following the Tribes/Wards precedent directly above.
            // WO-164 — RESEED the zone graph. AUDIT 2026-08-02: this used to be a bare
            // EnsureZoneGraph(s) call, which is a BACKFILL helper - it early-returns when
            // Zones already has entries (by design: the 5 defaults must never duplicate
            // across the fresh-save / post-load / migrator call sites). On a reset that
            // early-return meant the previous save's map discovery + clear flags survived
            // intact, so a brand-new game opened on a pre-explored realm. Nulling first is
            // what turns the backfill into a reseed; the helper stays the single seeder so
            // the default graph is still authored in exactly one place.
            s.Zones = null;
            EnsureZoneGraph(s);
            ClearEquipPrefs();                                // WO-860 Part A1 — see below.
            ClearProgressionPrefs();                          // WO-1220 — see below.
            ClearHarvestPrefs();                              // WO-1371 — see below.
            // NOTE: BoundWallet, BreachStyle and every social field are deliberately
            // left untouched — preferences and identity survive a New Game.
            s.SchemaVersion = SaveSchema.CurrentVersion;

            // WO-1220 — the save is only half the progression. Tell the LIVE runtime
            // singletons (HeroProgression / WisdomCurrencyService / SkillSystem) that this
            // is a New Game so they drop the previous run's in-memory state, BEFORE the
            // notify tail and BEFORE Save() — so anything a subscriber writes back lands in
            // the snapshot this call persists rather than in the NEXT one.
            // Guard.TryEach is not usable on a raw multicast delegate, so the invoke is
            // wrapped: one throwing subscriber must never abort the rest of the reset, and
            // it must never fail silently either (§12).
            var newGame = NewGameStarted;
            if (newGame != null)
            {
                foreach (var handler in newGame.GetInvocationList())
                {
                    try { ((Action)handler).Invoke(); }
                    catch (Exception ex)
                    {
                        FlowTrace.Fail("Save",
                            $"ResetToNewGame: a NewGameStarted subscriber THREW " +
                            $"({handler.Method?.DeclaringType?.Name}.{handler.Method?.Name}) — that " +
                            $"system KEPT the previous run's in-memory progression. " +
                            $"{ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            FlowTrace.Step("Save",
                $"ResetToNewGame: EXIT — hero level {priorHeroLevel}->{s.HeroLevel} " +
                $"xp {priorHeroXp:0.#}->{s.HeroXp:0.#}; notified " +
                $"{(newGame == null ? 0 : newGame.GetInvocationList().Length)} live progression " +
                "subscriber(s). Anything that reads level>1 after this line re-introduced it.");

            StateReplaced.Invoke();
            Save();
        }

        /// <summary>
        /// WO-860 Part A1 — New Game must not inherit an old EQUIP.
        ///
        /// THE BUG THIS FIXES (owner felt-test 2026-08-02, "on new I keep getting this axe
        /// I tried one time"): the equipped weapon / off-hand / armor / ring / amulet are
        /// persisted OUTSIDE the save envelope, in per-class PlayerPrefs
        /// (<see cref="EquipPrefKeys"/>) that GearLoadout.EquipWeaponById &amp;c. write.
        /// ResetToNewGame wiped ~40 GameState fields but never touched those keys, so
        /// GearLoadout.ApplyPersistedEquip restored the axe over the auto/starter pick on
        /// every single New Game — deterministically, forever.
        ///
        /// PlayerPrefs has no key enumeration, so we delete an explicit key set: every
        /// slot prefix x every known class key (the HeroClass enum lowercased — the same
        /// key a COMPANION loadout binds via GearLoadout.BindOwnerClass, so companion
        /// gear is cleared too). Also clears the per-class W/E/R ability-bar loadout
        /// (dotr-loadout-&lt;class&gt;-v1): it is the identical "stale PlayerPrefs survives
        /// New Game" defect, and a fresh hero must start on his class's stock Q/W/E/R.
        ///
        /// DeleteKey on an absent key is a documented no-op, so this is safe + idempotent.
        /// </summary>
        private static void ClearEquipPrefs()
        {
            int deleted = 0;
            foreach (var classKey in PlayableHeroes.AllKnownJobKeys())
            {
                foreach (var prefix in EquipPrefKeys.AllSlotPrefixes)
                {
                    string key = prefix + classKey;
                    if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); deleted++; }
                }
                string loadoutKey = EquipPrefKeys.LoadoutKeyFor(classKey);
                if (PlayerPrefs.HasKey(loadoutKey)) { PlayerPrefs.DeleteKey(loadoutKey); deleted++; }
                // WO-1019: the HOT-SWAP bar is the THIRD store with this exact shape and was the
                // only one this reset never touched, so a New Game inherited the old hero's
                // assigned extras verbatim. Clear it with the rest.
                string skillBarKey = EquipPrefKeys.SkillBarKeyFor(classKey);
                if (PlayerPrefs.HasKey(skillBarKey)) { PlayerPrefs.DeleteKey(skillBarKey); deleted++; }
            }
            // WO-1019: and the pre-per-class GLOBAL hot-swap key, or a New Game would re-migrate
            // the old shared bar back in on first read.
            if (PlayerPrefs.HasKey(EquipPrefKeys.SkillBarLegacyGlobalKey))
            {
                PlayerPrefs.DeleteKey(EquipPrefKeys.SkillBarLegacyGlobalKey);
                deleted++;
            }
            PlayerPrefs.Save();
            FlowTrace.Step("Save",
                $"ResetToNewGame: cleared {deleted} stale equip/loadout PlayerPrefs key(s) " +
                "(dotr-equip-* + dotr-loadout-* + dotr-skillbar-*) - a new game starts on the class " +
                "STARTER loadout, never an old equip and never another hero's hot-swap bar.");
        }

        /// <summary>
        /// WO-1220 — New Game must not inherit the old hero's TALENTS.
        ///
        /// THE BUG THIS FIXES (owner felt-test 2026-08-26): a brand-new Ranger came up with a
        /// level-4 Mage's talent applied — <c>[Flow:HeroTalents] Aether Bond applied: +20 % mana
        /// regen (shared.n5)</c> — and a talent HP bonus of +35 on a hero that had never spent a
        /// point. Wisdom and the unlocked talent-node ids are persisted OUTSIDE the save
        /// envelope, in WisdomCurrencyService's own PlayerPrefs blob
        /// (<c>dotr-talents-v1</c>: <c>{ Wisdom, Unlocked[] }</c>), for the reason its header
        /// states — "we deliberately keep state local to this service rather than extending
        /// GameState". ResetToNewGame re-seeded ~60 GameState fields and never touched that key,
        /// so every New Game re-read the previous run's talents. Node ids are hero-prefixed but
        /// the POOL is shared, and <c>shared.*</c> nodes are not hero-prefixed at all — which is
        /// why the carryover crossed classes rather than staying with the Mage.
        ///
        /// This is the SAME defect shape as <see cref="ClearEquipPrefs"/> (WO-860 A1) and the
        /// hot-swap bar (WO-1019): a second persistence store that the one reset never learned
        /// about. Deleting the key is the half that survives an app restart; the
        /// <see cref="NewGameStarted"/> event is the half that reaches the LIVE
        /// DontDestroyOnLoad service, which is holding the same values in memory and would
        /// otherwise write them straight back out.
        ///
        /// DeleteKey on an absent key is a documented no-op, so this is safe + idempotent.
        /// </summary>
        private static void ClearProgressionPrefs()
        {
            int deleted = 0;
            foreach (var key in ProgressionPrefKeys)
            {
                if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); deleted++; }
            }
            PlayerPrefs.Save();
            FlowTrace.Step("Save",
                $"ResetToNewGame: cleared {deleted} of {ProgressionPrefKeys.Length} stale talent/Wisdom " +
                "PlayerPrefs key(s) (dotr-talents-v1) - a new game starts with ZERO Wisdom and ZERO " +
                "unlocked talent nodes, never the previous hero's tree.");
        }

        /// <summary>
        /// WO-1220 — the progression stores that live OUTSIDE the save envelope and must be
        /// erased by a New Game. Named here so the reset and its regression read the SAME list
        /// (a key restated in a second place is this repo's most repeated defect).
        /// <c>dotr-talents-v1</c> is WisdomCurrencyService's blob (Wisdom + unlocked node ids).
        /// </summary>
        public static readonly string[] ProgressionPrefKeys = { TalentPrefKey };

        /// <summary>
        /// WO-1220 — the ONE authority for WisdomCurrencyService's PlayerPrefs key. The service
        /// itself (DeNelle.Village.Talents) now reads it from here rather than declaring its own
        /// copy: the reset and the store must never be able to drift onto two different keys,
        /// which is exactly how this store stayed invisible to the reset for so long.
        /// </summary>
        public const string TalentPrefKey = "dotr-talents-v1";

        // =====================================================================
        //  WO-1371 — the HARVEST stores that live outside the save envelope
        // =====================================================================

        /// <summary>
        /// WO-1371 — the ONE authority for ResourceCollector's three PlayerPrefs key prefixes.
        /// <c>ResourceCollector</c> (DeNelle.Village) now aliases these rather than declaring its
        /// own copies: the collector and the reset must never be able to drift onto two different
        /// key spellings, which is exactly how this store stayed invisible to the reset.
        /// </summary>
        public const string CollectorPendingPrefPrefix    = "dotr.collector.pending.";
        public const string CollectorHpPrefPrefix         = "dotr.collector.hp.";
        public const string CollectorLastAccrualPrefPrefix = "dotr.collector.lastaccrual.";

        /// <summary>
        /// WO-1371 — <c>ResourceBuildingState</c>'s level prefix (DeNelle.Village), named here for
        /// the same single-authority reason as the collector prefixes above.
        /// </summary>
        public const string ResourceBuildingLevelPrefPrefix = "dotr.resbuilding.level.";

        /// <summary>
        /// WO-1371 — the INDEX of every building id a collector has ever persisted state under.
        ///
        /// <para>The collector keys are <c>prefix + arbitrary building id</c>, and PlayerPrefs has
        /// no key enumeration, so the <see cref="ProgressionPrefKeys"/> fixed-array pattern does
        /// not extend to them. <see cref="ResourceCollector"/> appends its id here on every save,
        /// which makes the key space ENUMERABLE without inventing a second source of truth about
        /// which buildings exist. Comma-separated; ids contain no commas.</para>
        /// </summary>
        public const string CollectorKnownIdsPrefKey = "dotr.collector.ids";

        /// <summary>The three per-building collector prefixes, in one place for the reset and its
        /// oracle to share.</summary>
        public static readonly string[] CollectorPrefPrefixes =
        {
            CollectorPendingPrefPrefix,
            CollectorHpPrefPrefix,
            CollectorLastAccrualPrefPrefix,
        };

        /// <summary>WO-1371 — record <paramref name="buildingId"/> in the enumerable index so a
        /// New Game can find and delete its keys. Idempotent; called from the collector's save.</summary>
        public static void RegisterCollectorId(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return;
            string raw = PlayerPrefs.GetString(CollectorKnownIdsPrefKey, string.Empty);
            foreach (var existing in raw.Split(','))
                if (string.Equals(existing, buildingId, StringComparison.Ordinal)) return;
            PlayerPrefs.SetString(CollectorKnownIdsPrefKey,
                string.IsNullOrEmpty(raw) ? buildingId : raw + "," + buildingId);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// WO-1371 — every building id whose collector keys may exist: the persisted index UNION
        /// the authored Collector catalog. The index alone is complete for anything this build
        /// wrote; the catalog covers a device whose keys predate the index.
        /// </summary>
        public static List<string> KnownCollectorIds()
        {
            var ids = new List<string>();
            void Add(string id)
            {
                if (string.IsNullOrEmpty(id)) return;
                for (int i = 0; i < ids.Count; i++)
                    if (string.Equals(ids[i], id, StringComparison.Ordinal)) return;
                ids.Add(id);
            }

            foreach (var id in PlayerPrefs.GetString(CollectorKnownIdsPrefKey, string.Empty).Split(','))
                Add(id);

            // Never let a catalog hiccup abort the reset — a missing id costs one uncleared key,
            // a throw here would cost the whole New Game (§12: log it, never swallow it).
            Guard.Try("Save", "enumerate collector catalog ids", () =>
            {
                foreach (var e in DeNelle.Core.Catalog.CatalogRegistry.OfType(
                             DeNelle.Core.Catalog.CatalogType.Collector))
                {
                    if (e == null) continue;
                    string bid = e.repo != null && !string.IsNullOrEmpty(e.repo.collectorBuildingId)
                        ? e.repo.collectorBuildingId
                        : e.id;
                    Add(bid);
                }
            });
            return ids;
        }

        /// <summary>
        /// WO-1371 — New Game must not inherit the previous save's COLLECTOR FILL.
        ///
        /// THE BUG THIS FIXES (owner felt-test 2026-09-04, "how did i manage to acquire 3000 stone
        /// when this game is about 25 minutes old"): a game started at 09:44:43 banked 14,089
        /// resources eleven seconds later — <c>collect building=farm +7500 Food</c>,
        /// <c>lumbermill +5760 Wood</c>, <c>forge +829 Iron</c>. The measured honest rate that
        /// morning was ~0.58/s, so 14,089 is ~6.7 HOURS of accrual on an 11-second-old game. The
        /// fill was INHERITED: <c>register id=farm pending=7500/7500</c> was logged a full minute
        /// BEFORE the new game began.
        ///
        /// Collector pending/HP/last-accrual persist in PlayerPrefs OUTSIDE the save envelope
        /// (ResourceCollector's own three prefixes), so <see cref="ResetToNewGame"/> wiped ~60
        /// GameState fields — including <c>LastHarvestClaimMs = 0</c>, whose own comment says
        /// "reseed the accrual clock on next load (no haul)" — while the haul arrived anyway
        /// through a door the reset had never heard of. This is the FOURTH instance of that exact
        /// shape (WO-860 equip, WO-1019 hot-swap bar, WO-1220 talents, now this).
        ///
        /// <para>WHAT THE NEW-GAME STATE IS, and why: pending 0, HP full, and the accrual stamp
        /// DELETED rather than written. Deleting is what "reseed to now" MEANS for this store —
        /// <c>ResourceCollector.LoadState</c> reads an absent stamp as 0, and <c>CatchUpAway</c>'s
        /// documented fresh-collector arm then seeds it to now and back-fills NOTHING. Writing a
        /// timestamp here would instead hand the collector a window it must reason about.</para>
        ///
        /// <para>Also clears the <c>dotr.resbuilding.level.*</c> keys, which is why the owner's
        /// fresh town had a farm CAPACITY of 7500 instead of the base figure: the inherited LEVEL
        /// raised the cap that the inherited fill then filled.</para>
        ///
        /// <para>THE LIVE HALF is not here. Instances already in memory hold pending/HP/level and
        /// would write them straight back out on the next save — that half rides
        /// <see cref="NewGameStarted"/>, to which <c>ResourceCollector</c> subscribes
        /// (it zeroes live collectors and calls <c>ResourceBuildingState.ResetAll</c>, which also
        /// reaches <c>TechTree.ResetAll</c>). Same two-half shape as WO-1220.</para>
        ///
        /// DeleteKey on an absent key is a documented no-op, so this is safe + idempotent.
        /// </summary>
        private static void ClearHarvestPrefs()
        {
            var ids = KnownCollectorIds();
            int deleted = 0;
            foreach (var id in ids)
            {
                foreach (var prefix in CollectorPrefPrefixes)
                {
                    string key = prefix + id;
                    if (PlayerPrefs.HasKey(key)) { PlayerPrefs.DeleteKey(key); deleted++; }
                }
                string levelKey = ResourceBuildingLevelPrefPrefix + id;
                if (PlayerPrefs.HasKey(levelKey)) { PlayerPrefs.DeleteKey(levelKey); deleted++; }
            }
            if (PlayerPrefs.HasKey(CollectorKnownIdsPrefKey))
            {
                PlayerPrefs.DeleteKey(CollectorKnownIdsPrefKey);
                deleted++;
            }
            PlayerPrefs.Save();
            FlowTrace.Step("Save",
                $"ResetToNewGame: cleared {deleted} stale harvest PlayerPrefs key(s) across {ids.Count} " +
                "collector id(s) (dotr.collector.pending/hp/lastaccrual.* + dotr.resbuilding.level.*) - " +
                "a new game starts with an EMPTY collector and a base-level cap, never the previous " +
                "save's 14,089-resource fill (WO-1371).");
        }

        // =====================================================================
        //  WO-1371 — THE NEW-GAME PREF-STORE LEDGER (the class, not the instance)
        // =====================================================================

        /// <summary>How <see cref="ResetToNewGame"/> treats one out-of-envelope store.</summary>
        public enum NewGamePrefDisposition
        {
            /// <summary>The reset deletes it. Oracle-enforced: poison it, reset, assert gone.</summary>
            ClearedByReset = 0,
            /// <summary>Deliberately survives a New Game (identity / preference). Asserted PRESENT.</summary>
            DeliberatelyCarried = 1,
            /// <summary>KNOWN GAP - found by the WO-1371 audit, not yet addressed. Reported, not
            /// asserted, so the list can carry the truth instead of only the finished work.</summary>
            NotYetCleared = 2,
        }

        /// <summary>One PlayerPrefs-backed store that a New Game has an opinion about.</summary>
        public sealed class NewGamePrefStore
        {
            /// <summary>Exact key, or the key PREFIX when <see cref="PerId"/> is true.</summary>
            public string Key;
            /// <summary>True when the real key is <see cref="Key"/> + an arbitrary id.</summary>
            public bool PerId;
            public NewGamePrefDisposition Disposition;
            /// <summary>Why it has that disposition. A store is not exempt because nobody noticed
            /// it; it is exempt because a reason is written here.</summary>
            public string Why;
        }

        /// <summary>
        /// WO-1371 — EVERY persistence store that lives OUTSIDE the save envelope, with what a New
        /// Game does about it.
        ///
        /// <para>⭐ THIS LIST IS THE POINT OF THE TICKET. Four separate "a new game inherited X"
        /// defects have now shipped (WO-860 equip, WO-1019 hot-swap bar, WO-1220 talents, WO-1371
        /// collector fill) and each was fixed as an INSTANCE, so the fifth was always going to
        /// ship too. <c>ResetToNewGameFullClearRegression</c> Case 1 sweeps GameState FIELDS by
        /// reflection and says so in its own words - "this store is not one" - which is precisely
        /// the blind spot. <c>NewGamePrefStoreSweepRegression</c> sweeps THIS list instead, so the
        /// class becomes checkable: adding a row is all it takes to bring a store under the
        /// oracle.</para>
        ///
        /// <para>⚠ The <see cref="NewGamePrefDisposition.NotYetCleared"/> rows are the WO-1371
        /// audit's OTHER findings, recorded rather than fixed - fixing 13 stores in the collector's
        /// pass would have been scope creep, and losing the list would have been worse. Each is a
        /// candidate ticket; move a row to <see cref="NewGamePrefDisposition.ClearedByReset"/> in
        /// the same change that clears it and the oracle starts enforcing it.</para>
        /// </summary>
        public static readonly NewGamePrefStore[] NewGamePrefStores =
        {
            new NewGamePrefStore { Key = TalentPrefKey, PerId = false,
                Disposition = NewGamePrefDisposition.ClearedByReset,
                Why = "WO-1220 - Wisdom + unlocked talent nodes; a new Ranger inherited a Mage's tree" },
            new NewGamePrefStore { Key = CollectorPendingPrefPrefix, PerId = true,
                Disposition = NewGamePrefDisposition.ClearedByReset,
                Why = "WO-1371 - the 14,089-resource inherited collector fill" },
            new NewGamePrefStore { Key = CollectorHpPrefPrefix, PerId = true,
                Disposition = NewGamePrefDisposition.ClearedByReset,
                Why = "WO-1371 - a new town's collectors must not start pre-damaged" },
            new NewGamePrefStore { Key = CollectorLastAccrualPrefPrefix, PerId = true,
                Disposition = NewGamePrefDisposition.ClearedByReset,
                Why = "WO-1371 - deleted, not stamped: an absent stamp is what makes CatchUpAway seed to now and back-fill nothing" },
            new NewGamePrefStore { Key = ResourceBuildingLevelPrefPrefix, PerId = true,
                Disposition = NewGamePrefDisposition.ClearedByReset,
                Why = "WO-1371 - the inherited LEVEL is why a fresh farm had a 7500 cap instead of base" },
            new NewGamePrefStore { Key = CollectorKnownIdsPrefKey, PerId = false,
                Disposition = NewGamePrefDisposition.ClearedByReset,
                Why = "WO-1371 - the index of the above; it is rebuilt on the first save of the new game" },

            new NewGamePrefStore { Key = "dotr-settings-master-volume", PerId = false,
                Disposition = NewGamePrefDisposition.DeliberatelyCarried,
                Why = "setting - audio preference survives a New Game (same rule as the GameState settings carve-outs)" },
            new NewGamePrefStore { Key = "dotr-settings-quality-tier", PerId = false,
                Disposition = NewGamePrefDisposition.DeliberatelyCarried,
                Why = "setting - device performance preference" },
            new NewGamePrefStore { Key = "dotr-settings-screen-shake", PerId = false,
                Disposition = NewGamePrefDisposition.DeliberatelyCarried,
                Why = "setting - accessibility preference" },
            new NewGamePrefStore { Key = "dotr-account-id-v1", PerId = false,
                Disposition = NewGamePrefDisposition.DeliberatelyCarried,
                Why = "identity - the account outlives any one save, like BoundWallet" },
            new NewGamePrefStore { Key = "dotr-referral-code", PerId = false,
                Disposition = NewGamePrefDisposition.DeliberatelyCarried,
                Why = "social identity - reset() never touches the social fields" },

            // ── KNOWN GAPS (WO-1371 audit, 2026-09-04). NOT fixed in this pass. ──────────
            new NewGamePrefStore { Key = "dotr-arena-wins", PerId = false,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "arena W/L ledger persists outside GameState.Arena, which IS reset - the two disagree on a New Game" },
            new NewGamePrefStore { Key = "dotr-arena-losses", PerId = false,
                Disposition = NewGamePrefDisposition.NotYetCleared, Why = "as dotr-arena-wins" },
            new NewGamePrefStore { Key = "dotr-arena-streak", PerId = false,
                Disposition = NewGamePrefDisposition.NotYetCleared, Why = "as dotr-arena-wins" },
            new NewGamePrefStore { Key = "dotr-arena-purse", PerId = false,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "carried arena winnings - a currency a new game did not earn" },
            new NewGamePrefStore { Key = "dotr-camp-cleared-", PerId = true,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "per-camp cleared flag - a new game opens on a pre-cleared world map" },
            new NewGamePrefStore { Key = "dotr-camp-claimed-", PerId = true,
                Disposition = NewGamePrefDisposition.NotYetCleared, Why = "as dotr-camp-cleared-" },
            new NewGamePrefStore { Key = "dotr-camp-secured-", PerId = true,
                Disposition = NewGamePrefDisposition.NotYetCleared, Why = "as dotr-camp-cleared-" },
            new NewGamePrefStore { Key = "dotr-camp-recipe-", PerId = true,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "recipes learned from camps - inherited unlocks" },
            new NewGamePrefStore { Key = "dotr-raid-cleared-", PerId = true,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "per-raid cleared flag; GameState.RaidCooldowns IS reset, so these disagree" },
            new NewGamePrefStore { Key = "dotr-raid-owner-", PerId = true,
                Disposition = NewGamePrefDisposition.NotYetCleared, Why = "as dotr-raid-cleared-" },
            new NewGamePrefStore { Key = "dotr-raid-crystalday-", PerId = true,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "per-raid daily crystal gate - a new game may start already spent" },
            new NewGamePrefStore { Key = "dotr-node-discovered-", PerId = true,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "map discovery outside GameState.Zones, which IS force-reseeded (WO-164)" },
            new NewGamePrefStore { Key = "dotr-dungeon-portal-discovered-", PerId = true,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "dungeon portal discovery - inherited exploration" },
            new NewGamePrefStore { Key = "dotr-harvest-last-active", PerId = false,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "a second harvest clock beside GameState.LastHarvestClaimMs, which IS zeroed" },
            new NewGamePrefStore { Key = "dotr-daily-quests-v1", PerId = false,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "daily-quest progress; with -gates-visited-v1 and -day1-done-v1 beside it" },
            new NewGamePrefStore { Key = "dotr-cosmetics-v1", PerId = false,
                Disposition = NewGamePrefDisposition.NotYetCleared,
                Why = "owned cosmetics - MAY be a deliberate carry (entitlements); needs an owner ruling, which is why it is not silently exempted" },
        };

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

        /// <summary>
        /// Hard ceiling on EVERY backend request (seconds), matching BugReportVM's 15.
        /// <para>
        /// Without it a stalled socket (captive-WiFi portal, dead cell hand-off) never
        /// completes: SyncToBackend's `_isSyncing` guard - which IS reset in a finally -
        /// simply never reaches that finally, so every later sync early-returns for the
        /// REST OF THE SESSION with no error and no retry, and the player's progress
        /// stops leaving the device silently. UnityWebRequest.timeout aborts the request
        /// and completes it as a failure, which is what makes the finally reachable.
        /// </para>
        /// </summary>
        private const int RequestTimeoutSeconds = 15;

        // ── State ─────────────────────────────────────────────────────────
        // The last snapshot the server acknowledged — null means never synced.
        // Uses PersistedState (plain class) NOT the GameState ScriptableObject.
        private SaveSchema.PersistedState _lastSyncedSnapshot;
        private bool  _isSyncing;
        private float _lastSyncTime = -999f;
        private static bool _warnedGuestAccount;   // log the guest-wallet assignment once, not every sync
        private const string GuestWalletPrefix = "guest-local-";

        // ── Cloud identity (security audit 2026-08-02) ────────────────────────
        //
        // WHAT KEYS A CLOUD SAVE. Owner ruling: Firebase = ACCESS (distribution +
        // login); the Solana WALLET = DATA identity (the save key). The old test was a
        // DENYLIST - "BoundWallet does not start with guest-local-" - which said yes to
        // absolutely anything else: a devnet stub address, a Firebase UID, a debug
        // string. Two live consequences that this block closes:
        //   * the stub minted a constant-seeded 44-char base58 address, so a build
        //     missing SOLANA_SDK would have pointed every tester at ONE player_data row;
        //   * email sign-in bound the 28-char Firebase UID, which the server's wallet rail
        //     rejects (^[1-9A-HJ-NP-Za-km-z]{32,44}$) - those players would be 401'd out
        //     of their own saves the moment auth flips to Enforced.
        //
        // The test is now an ALLOWLIST with three independent gates, ALL required:
        //   1. shape        - base58 charset, 32..44 chars, not the guest prefix
        //                     (IsCloudIdentityShaped - the same rule the backend applies);
        //   2. attestation  - a REAL signing wallet provider vouched for this exact
        //                     address on this device (BindWallet's attested overload);
        //   3. current      - the attested address still equals the bound one.
        // A string can no longer talk its way into a cloud identity by looking right.
        private const string AttestedIdentityKey = "dotr-cloud-identity-attested";
        private const string LegacyIdentityKey   = "dotr-legacy-identity-orphaned";
        private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        private static bool _warnedUnattested;

        /// <summary>
        /// Shape gate for a cloud-save key: base58 charset (no 0/O/I/l), 32-44 chars,
        /// and never the local guest prefix. Deliberately the SAME rule as the backend's
        /// wallet regex - a value that fails here would be 401'd by /api/auth/nonce, so
        /// letting it key a save only manufactures an unreachable row.
        /// <para>Public + static so the regression oracle can assert it against real
        /// strings (a stub address, a Firebase UID, a genuine pubkey) with no scene.</para>
        /// </summary>
        public static bool IsCloudIdentityShaped(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id.StartsWith(GuestWalletPrefix, StringComparison.Ordinal)) return false;
            if (id.Length < 32 || id.Length > 44) return false;
            foreach (char c in id)
                if (Base58Alphabet.IndexOf(c) < 0) return false;
            return true;
        }

        /// <summary>The address a real signing wallet has vouched for ON THIS DEVICE, or empty.</summary>
        private static string AttestedCloudIdentity =>
            PlayerPrefs.GetString(AttestedIdentityKey, string.Empty);

        /// <summary>Robustness (owner 2026-06-07): if no real wallet/account is connected, assign a
        /// deterministic LOCAL guest wallet so the save/load-state flow always has an identity and
        /// runs end-to-end instead of silently skipping. Logged ONCE so we can confirm the path is
        /// exercised without digging. A real login later overwrites it via BindWallet.</summary>
        private void EnsureAccount(string op)
        {
            if (_state == null) return;
            RetireLegacyIdentity();
            if (!string.IsNullOrEmpty(_state.BoundWallet)) return;
            // LB-4: never embed the RAW device fingerprint in the player id — hash
            // it with a static salt (SHA256(deviceId + salt)) so the persisted /
            // synced id is a stable opaque token, not a re-identifiable hardware id.
            _state.BoundWallet = GuestWalletPrefix + HashDeviceId(SystemInfo.deviceUniqueIdentifier);
            if (!_warnedGuestAccount)
            {
                _warnedGuestAccount = true;
                // REDACTED (security audit 2026-08-15): the guest id is that save's SOLE
                // credential — whoever presents it gets the row (see wallet-auth.verifyGuest).
                // WebTrace subscribes to Application.logMessageReceived and POSTs captured log
                // lines to /api/trace, so printing it in full wrote a live credential into the
                // analytics pipe. A short prefix still identifies the id in a trace without
                // being usable as one.
                Debug.LogWarning($"[Persistence] No account connected — assigned LOCAL guest wallet " +
                                 $"'{RedactIdentity(_state.BoundWallet)}' so {op} can run (offline-first). Connect a real wallet to sync to cloud.");
            }
        }

        /// <summary>
        /// MIGRATION (security audit 2026-08-02). Before this change, email/Google sign-in
        /// bound the FIREBASE UID as BoundWallet, and the old denylist happily cloud-synced
        /// it - so real player_data rows keyed by a 28-char UID may exist on the server.
        /// Those rows are already unreachable under enforced auth (nonce.js rejects a UID),
        /// and continuing to write under that key only deepens the mess.
        /// <para>
        /// So: an id that is neither a guest key nor a valid cloud identity is RETIRED -
        /// stashed verbatim in PlayerPrefs (<c>dotr-legacy-identity-orphaned</c>) and
        /// reported loudly, then cleared so EnsureAccount re-mints the stable device-hash
        /// guest key. Nothing is orphaned SILENTLY, and NO local progress is lost: the local
        /// save is a single PlayerPrefs envelope, not a per-identity store - BoundWallet is
        /// a field inside it, so re-keying does not move or drop a byte of the player's game.
        /// The only thing left behind is the SERVER row, whose id is preserved here so a
        /// backend-side re-key can be run later against a known list.
        /// </para>
        /// </summary>
        private void RetireLegacyIdentity()
        {
            string id = _state?.BoundWallet;
            if (string.IsNullOrEmpty(id)) return;
            if (id.StartsWith(GuestWalletPrefix, StringComparison.Ordinal)) return;
            if (IsGooglePlayIdentity(id)) return;
            if (IsCloudIdentityShaped(id)) return;   // a real wallet-shaped key - leave it alone

            // Keep a small de-duplicated list rather than a single slot: a debug/dev bind
            // (DebugCanvasUI mints "0xTEST...", which is not base58 and lands here too)
            // must never overwrite and lose a genuine retired UID.
            string stash = PlayerPrefs.GetString(LegacyIdentityKey, string.Empty);
            var seen = new List<string>(stash.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            if (!seen.Contains(id))
            {
                seen.Insert(0, id);
                while (seen.Count > 4) seen.RemoveAt(seen.Count - 1);
                PlayerPrefs.SetString(LegacyIdentityKey, string.Join(";", seen));
                PlayerPrefs.Save();
            }
            _state.BoundWallet = null;
            FlowTrace.Warn("Save",
                "RETIRED a non-wallet save key (len=" + id.Length + ") that could never authenticate " +
                "against the backend - almost certainly a Firebase UID bound by the old email sign-in " +
                "path. Local progress is untouched; this device now uses its guest key. The old id is " +
                "preserved in PlayerPrefs '" + LegacyIdentityKey + "' so any server row under it can be " +
                "re-keyed deliberately instead of vanishing.");
        }

        /// <summary>True only when a REAL, provider-ATTESTED wallet keys this save — the gate for actual
        /// cloud load/save NETWORK calls. A guest/local wallet, an unattested address, or none returns
        /// false (local-save-only), which is expected and must not be treated as an error.
        /// <para>ALLOWLIST, not a denylist (see the block comment above): shape AND attestation AND
        /// currency. Anything that has not been vouched for by a real signing wallet on THIS device
        /// stays local, so a stub/UID/debug string can never reach a shared cloud row.</para></summary>
        /// <summary>Read-only public face of <see cref="IsRealWalletConnected"/> for callers that must
        /// answer "is this player ALREADY identified by a real wallet?" without touching sync internals —
        /// notably the boot login gate (LoginPanelController.ShouldContinueWithoutLogin, 2026-08-18), which
        /// must not re-prompt a returning wallet player. Synchronous and race-proof: it reads the persisted
        /// save key plus this device's attestation, so it is already true before any boot-time silent
        /// reconnect completes. A guest / unattested / unbound save returns false.</summary>
        public bool HasAttestedWalletIdentity => IsRealWalletConnected();

        private bool IsRealWalletConnected()
        {
            string id = _state?.BoundWallet;
            if (!IsCloudIdentityShaped(id)) return false;

            if (!string.Equals(AttestedCloudIdentity, id, StringComparison.Ordinal))
            {
                if (!_warnedUnattested)
                {
                    _warnedUnattested = true;
                    FlowTrace.Warn("Sync",
#if GOOGLE_PLAY
                        "the bound save key is identity-shaped but no verified Google Play session is " +
                        "available on this device - staying LOCAL-ONLY. Sign in again to resume cloud sync.");
#else
                        "the bound save key is wallet-SHAPED but no real wallet has attested it on this " +
                        "device - staying LOCAL-ONLY. Tap Connect Wallet once to re-attest and resume " +
                        "cloud sync. (This is also what stops a devnet-stub address from keying a shared row.)");
#endif
                }
                return false;
            }
            return true;
        }

        /// <summary>The local GUEST save key shape: "guest-local-" + 64 lowercase hex (see EnsureAccount).
        /// Deliberately the SAME rule as the backend's GUEST_RE in api/_lib/wallet-auth.js — a mismatch
        /// here means a guest whose saves 401 forever, silently.</summary>
        private static bool IsGuestIdentity(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!id.StartsWith(GuestWalletPrefix, StringComparison.Ordinal)) return false;
            if (id.Length != GuestWalletPrefix.Length + 64) return false;
            for (int i = GuestWalletPrefix.Length; i < id.Length; i++)
            {
                char c = id[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) return false;
            }
            return true;
        }

        /// <summary>True when this identity may talk to the cloud AT ALL — an attested wallet (full value,
        /// signed rail) OR a local guest (degraded, unverified rail, rate-limited and marked trust='guest'
        /// server-side). The guest rail exists because the front door offers "Play as Guest" and a tester
        /// who cannot save is a tester we lose. The two rails are chosen by the SHAPE of the bound id, never
        /// by which headers arrive, so a caller can never downgrade a wallet row onto the weak rail.</summary>
        private bool CanCloudSync() => IsRealWalletConnected() || IsGuestIdentity(_state?.BoundWallet) ||
                                       IsGooglePlayIdentity(_state?.BoundWallet);

        // ── Lifecycle hooks ───────────────────────────────────────────────
        private void OnApplicationPause(bool paused)
        {
            if (paused) SyncToBackend(highPriority: true).Forget();
        }

        private void OnApplicationQuit()
        {
            // Persist the local save SYNCHRONOUSLY first — this is the durable write that
            // must survive the quit (PlayerPrefs to disk). It always completes here.
            Save();

            // The backend network flush is BEST-EFFORT only: OnApplicationQuit cannot await,
            // so this fire-and-forget call is not guaranteed to finish before the process
            // exits. That is acceptable — SyncToBackend enqueues into the persisted offline
            // queue (PlayerPrefs key "dotr-sync-queue"), which is flushed on the NEXT launch.
            // Do NOT convert this to a blocking await (would hang / be killed on quit).
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
            // Attested wallet OR local guest — both rails may load. See CanCloudSync.
            if (!CanCloudSync())
            {
                Debug.Log("[Persistence] No wallet connected — skipping cloud load (local save only; expected).");
                return;
            }

            var url = $"{LoadUrl}?playerId={Uri.EscapeDataString(_state.BoundWallet)}";
            using var req = UnityWebRequest.Get(url);
            req.timeout = RequestTimeoutSeconds;   // never hang the boot-time cloud load
            req.SetRequestHeader("Accept", "application/json");

            // WO-1211: boot reads may use existing proof but may never mint or sign.
            // With no cached wallet session, keep the durable local save and defer proof
            // until the first authenticated action. Guest reads retain their guest header.
            bool guestLoad = DeNelle.Core.Web3.BackendRequestSigner.IsGuestIdentity(_state.BoundWallet);
            if (!DeNelle.Core.Web3.BackendRequestSigner.TryAttachCachedSession(req, _state.BoundWallet))
            {
                Debug.Log(guestLoad
                    ? "[Sync] Guest cloud LOAD had no usable proof - keeping local save."
                    : "[Sync] Wallet cloud LOAD has no cached session - keeping local save; boot will not ask for authorization.");
                return;
            }

            // WO-769: guard the throwing awaiter (401/non-2xx) so a cloud-load failure
            // never propagates (it runs on scene-enter via PersistenceBridge). Skip + keep local.
            try
            {
                await req.SendWebRequest();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Sync] Load request threw ({req.responseCode}): {e.Message} — keeping local save.");
                return;
            }

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

            // WO-912 §7.2: anchor the clock BEFORE any early-out below. The handshake is
            // valuable even when the payload turns out to be empty or rejected — the
            // rewarded-ad window is hardened by having ANY server time this process, and
            // throwing that away because there was no save data would be a silent loss.
            if (resp?.ServerNowMs != null) ServerClock.Sync(resp.ServerNowMs.Value);

            // WO-1128 §3.3 — RECORD the server's last_seen anchor; never write it to a clock.
            // This is the number the server measures our declared accrual window against, so
            // having it locally means a capture can show BOTH sides of a clamp. The one thing
            // it must NOT do is touch GameState.LastHarvestClaimMs: OfflineClaimCoordinator is
            // its single owner, and dragging that stamp backwards to the server's window would
            // make the stretch in between re-claimable on the next launch.
            if (resp?.ServerLastSeenMs != null && resp.ServerLastSeenMs.Value > 0d)
            {
                ServerLastSeenMs = resp.ServerLastSeenMs.Value;
                double localClaimMs = _state != null ? _state.LastHarvestClaimMs : 0d;
                FlowTrace.Step("Offline",
                    $"server last_seen anchor = {ServerLastSeenMs.Value:0} (local claim clock {localClaimMs:0}, " +
                    $"divergence {(localClaimMs - ServerLastSeenMs.Value) / 1000.0:F0}s) - recorded for display only; " +
                    "the claim clock is NOT touched here (OfflineClaimCoordinator owns it).");
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
            // Crystals are unified onto Resources.Crystals (save v18). A legacy/older
            // backend record may still carry an aetherCrystals balance the server
            // Resources blob doesn't include — fold it in so cloud load can't reopen the
            // split-brain. (Field kept for back-compat; left at 0 locally.)
            if (server.AetherCrystals.HasValue && server.AetherCrystals.Value > 0)
            {
                var cr = _state.Resources;
                cr.Crystals += (int)server.AetherCrystals.Value;
                _state.Resources = cr;
            }
            // WO-1212: the server's `stone` column is the retired balance. Same rule as the
            // local save path - alias it onto the live Stone slot ONLY when the row carried no
            // resources block, otherwise discard it aloud. Never a second balance, and never a
            // free top-up: the stored number is a seed/dev echo, not earned value.
            if (server.Stone.HasValue && server.Stone.Value != 0d)
            {
                if (!server.Resources.HasValue)
                {
                    var aliasedCloud = _state.Resources;
                    aliasedCloud.Food = (int)server.Stone.Value;
                    _state.Resources = aliasedCloud;
                    FlowTrace.Warn("Sync",
                        $"WO-1212: cloud `stone` ALIASED onto the live Stone slot " +
                        $"(Resources.Food={aliasedCloud.Food}); the row carried no resources block.");
                }
                else
                {
                    FlowTrace.Step("Sync",
                        $"WO-1212: DISCARDED retired cloud balance stone={server.Stone.Value:0}; live " +
                        $"Stone = Resources.Food={_state.Resources.Food}, untouched.");
                }
            }
            if (server.Iron.HasValue)       _state.Iron        = (int)server.Iron.Value;
            if (server.Wood.HasValue)       _state.Wood        = (int)server.Wood.Value;

            // Advance ack marker and re-persist locally.
            _lastSyncedSnapshot = Snapshot();
            Save();
            StateReplaced.Invoke();
            Debug.Log("[Sync] Server state merged onto local SO.");
        }

        // ── Backend save-auth (WO-121) — wallet-signed nonce headers ──────────
        //
        //  Counterpart to the WO-120 backend gate (api/_lib/wallet-auth.js). On a
        //  save/load the backend wants X-Wallet / X-Nonce / X-Signature, where the
        //  client GETs a nonce, builds the canonical message and ed25519-signs it.
        //
        //  TWO INDEPENDENT GATES, both required to sign (offline-safe by design):
        //    1. BackendAuthConfig.Enforced — the feature flag (off by default).
        //    2. CoreServices.WalletSigner.CanSign — a REAL signer is connected
        //       (the devnet stub registers but cannot sign).
        //  If either gate is open, we SKIP the headers, log that signing is
        //  stubbed, and the request goes out unauthed exactly as before. This is
        //  what keeps current offline/unauthed play working until the flag flips
        //  AND a real MWA signer (SolanaWalletProvider) lands.

        /// <summary>
        /// Attaches X-Wallet / X-Nonce / X-Signature to <paramref name="req"/> when
        /// backend auth is enforced AND a real signer is connected.
        /// <para>
        /// LB-4 — FAIL CLOSED. Returns <c>true</c> when it is safe to send the
        /// request, <c>false</c> when the caller MUST abort it:
        /// </para>
        /// <list type="bullet">
        ///   <item>flag OFF → returns true (offline/unauthed path unchanged — no headers).</item>
        ///   <item>flag ON + signer/nonce/signature obtained → headers set, returns true.</item>
        ///   <item>flag ON but signing fails at ANY step → returns FALSE so the caller
        ///         ABORTS rather than sending an UNAUTHED request on a real rail.</item>
        /// </list>
        /// <paramref name="payloadHashOrLoadTag"/> is the sha256-hex of the raw POST
        /// body, or the literal "load" for a GET.
        /// </summary>
        /* WO-1211 retired duplicate auth authority. Kept temporarily as block-commented
         * history for review; BackendRequestSigner is now the only live authority.
        private async UniTask<bool> TryAttachAuthHeaders(UnityWebRequest req, string payloadHashOrLoadTag)
        {
            // GUEST RAIL — no signature, no nonce. The id IS the credential (an unguessable 256-bit
            // device hash); the server rate-limits it and marks the row trust='guest'. This MUST come
            // FIRST: a guest has no signer at all and would otherwise fail closed below and never sync.
            if (IsGuestIdentity(_state?.BoundWallet))
            {
                req.SetRequestHeader("X-Guest-Id", _state.BoundWallet);
                return true;
            }

            if (!BackendAuthConfig.Enforced)
                return true; // flag off — current behaviour, no auth headers, send as before.

            var signer = CoreServices.WalletSigner;
            if (signer == null || !signer.CanSign)
            {
                Debug.LogError(
                    "[Sync] Backend auth ENFORCED but no real wallet signer is available " +
                    "(signing is stubbed) — ABORTING sync (fail-closed; refusing to send unauthed). " +
                    // WO-1363: the provider's name is an audit token in a literal that ships in
                    // every artifact. The Play build names no provider; nothing else changes.
#if GOOGLE_PLAY
                    "Connect a real signing account to sign.");
#else
                    "Connect a real wallet (SolanaWalletProvider) to sign.");
#endif
                return false;
            }

            var wallet = signer.WalletAddress;
            if (string.IsNullOrEmpty(wallet))
            {
                Debug.LogError("[Sync] Wallet signer reports CanSign but has no address — ABORTING sync (fail-closed).");
                return false;
            }

            // 1. GET a fresh single-use nonce bound to this wallet.
            var nonce = await FetchNonce(wallet);
            if (string.IsNullOrEmpty(nonce))
            {
                Debug.LogError("[Sync] Could not obtain an auth nonce — ABORTING sync (fail-closed; not sending unauthed).");
                return false;
            }

            // 2. Build the EXACT canonical message the backend reconstructs, then sign.
            //    dotr-save:v1:<wallet>:<nonce>:<sha256-hex-of-body | "load">
            var message = $"dotr-save:v1:{wallet}:{nonce}:{payloadHashOrLoadTag}";

            string signature;
            try
            {
                signature = await signer.SignMessageBase58(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Sync] Wallet signing failed ({ex.Message}) — ABORTING sync (fail-closed).");
                return false;
            }

            if (string.IsNullOrEmpty(signature))
            {
                Debug.LogError("[Sync] Wallet returned an empty signature — ABORTING sync (fail-closed).");
                return false;
            }

            // 3. Present the challenge response. Header names match the backend exactly.
            req.SetRequestHeader("X-Wallet", wallet);
            req.SetRequestHeader("X-Nonce", nonce);
            req.SetRequestHeader("X-Signature", signature);
            return true;
        }

        /// <summary>
        /// GET /api/auth/nonce?wallet=&lt;base58&gt; → the issued one-time nonce, or
        /// null on any failure (caller then sends unauthed rather than throwing).
        /// </summary>
        private async UniTask<string> FetchNonce(string wallet)
        {
            var url = $"{NonceUrl}?wallet={Uri.EscapeDataString(wallet)}";
            using var req = UnityWebRequest.Get(url);
            req.timeout = RequestTimeoutSeconds;   // a stalled nonce fetch stalls the whole sync
            req.SetRequestHeader("Accept", "application/json");

            // WO-769: the awaiter throws on non-2xx; honor the documented "null on any
            // failure rather than throwing" contract by catching it.
            try
            {
                await req.SendWebRequest();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Sync] Nonce fetch threw ({req.responseCode}): {e.Message}");
                return null;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Sync] Nonce fetch failed ({req.responseCode}): {req.error}");
                return null;
            }

            try
            {
                var resp = JsonConvert.DeserializeObject<NonceResponse>(req.downloadHandler.text);
                return resp != null && resp.Success ? resp.Nonce : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Sync] Nonce parse error: {ex.Message}");
                return null;
            }
        }
        */

        /// <summary>
        /// LB-4: salted SHA-256 of the device id → a stable opaque guest token.
        /// The salt is a constant build-time value: it only prevents the hash from
        /// being a plain unsalted deviceId digest (rainbow-table / cross-app
        /// correlation), it is not a secret. Returns lowercase hex.
        /// </summary>
        private const string GuestIdSalt = "dotr-guest-id:v1:9f3c7a";
        private static string HashDeviceId(string deviceId)
            => Sha256Hex(Encoding.UTF8.GetBytes((deviceId ?? string.Empty) + GuestIdSalt));

        /// <summary>
        /// Log-safe form of a player identity: keep the prefix (which names the RAIL —
        /// "guest-local-" vs a base58 wallet) plus 8 characters, then elide. Enough to
        /// correlate two log lines; not enough to present as the credential.
        /// </summary>
        private static string RedactIdentity(string id)
        {
            if (string.IsNullOrEmpty(id)) return "(none)";
            const int Keep = 8;
            if (id.StartsWith(GuestWalletPrefix, StringComparison.Ordinal))
            {
                int avail = id.Length - GuestWalletPrefix.Length;
                if (avail <= Keep) return id;
                return GuestWalletPrefix + id.Substring(GuestWalletPrefix.Length, Keep) + "...";
            }
            return id.Length <= Keep ? id : id.Substring(0, Keep) + "...";
        }

        /// <summary>Lowercase hex SHA-256 of the raw bytes — matches Node's crypto sha256 hex digest.</summary>
        private static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes ?? Array.Empty<byte>());
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private sealed class NonceResponse
        {
            [JsonProperty("success")]    public bool   Success    { get; set; }
            [JsonProperty("nonce")]      public string Nonce      { get; set; }
            [JsonProperty("expiresAt")]  public string ExpiresAt  { get; set; }
            [JsonProperty("ttlSeconds")] public int    TtlSeconds { get; set; }
        }

        // ── Core sync pipeline ────────────────────────────────────────────

        private async UniTask SyncToBackend(bool highPriority = false)
        {
            if (_isSyncing) return;
            if (!highPriority && Time.time - _lastSyncTime < MinSyncDelay) return;
            // Attested wallet OR local guest — both rails may sync. See CanCloudSync.
            if (!CanCloudSync())
            {
                Debug.Log("[Persistence] No wallet connected — skipping cloud sync (local save only; expected).");
                return;
            }

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

                bool ok = await SendCurrentSnapshot();
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

        /// <summary>
        /// Uploads the CURRENT full snapshot to /api/game/save under the CURRENT identity.
        /// <para>
        /// HONESTY NOTE (security audit 2026-08-02) — this was called
        /// <c>SendDelta(SyncDeltaPayload delta)</c> and never read its argument: it always
        /// posted <see cref="Snapshot"/>. The name made two real bugs invisible:
        /// FlushOfflineQueue "replayed" N queued payloads by posting the same current
        /// snapshot N times, and a payload queued under identity A would upload under
        /// whatever BoundWallet happened to be current at flush time (a wrong-key write).
        /// </para>
        /// <para>
        /// It is NOT converted into a real per-payload sender here: SyncDeltaPayload is a
        /// flat wire shape (Crystals/Towers/PetsJson...) while the deployed endpoint - and
        /// <see cref="LoadFromBackend"/> - round-trip the nested PersistedState shape.
        /// Sending the flat object would be a backend contract change, which is not this
        /// lane's call. So the parameter is GONE (a lie removed rather than a lie kept),
        /// the redundant N posts are collapsed at the caller, and the queue is honestly
        /// documented as a retry MARKER, not a body.
        /// </para>
        /// </summary>
        private async UniTask<bool> SendCurrentSnapshot()
        {
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
            req.timeout = RequestTimeoutSeconds;   // a stalled upload used to park _isSyncing for the session
            req.SetRequestHeader("Content-Type", "application/json");

            // WO-1211: writes use the one shared auth authority and always fail closed.
            // Guests retain their shaped device proof; wallets may reuse or mint a session.
            bool guestSave = DeNelle.Core.Web3.BackendRequestSigner.IsGuestIdentity(_state.BoundWallet);
            if (!await DeNelle.Core.Web3.BackendRequestSigner.TryAttachAsync(req, _state.BoundWallet, body))
            {
                Debug.LogError(guestSave
                    ? "[Sync] Guest cloud SAVE aborted - guest proof unavailable. Delta re-queued offline."
                    : "[Sync] Wallet cloud SAVE aborted - shared authentication unavailable (fail-closed). Delta re-queued offline.");
                return false;
            }

            // WO-769: a non-2xx (e.g. 401 while Neon isn't verifying the Firebase token yet)
            // makes the UniTask awaiter THROW UnityWebRequestException — which previously
            // propagated out and aborted scene navigation (see SceneRouter guard). Catch it
            // so this always fulfills its bool contract: log + re-queue offline (false).
            try
            {
                await req.SendWebRequest();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Sync] Save request threw ({req.responseCode}): {e.Message} — re-queued offline.");
                return false;
            }

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Sync] Saved {body.Length} bytes (full snapshot).");
                // WO-1128 §3.3 — the save response is no longer discarded. It carries the
                // server's clock handshake AND, when the server refused part of a claimed
                // time-derived gain, the clamp report. Reading it is what makes the offline
                // number PROVISIONAL rather than final on the device that produced it.
                ReadSaveResponse(req.downloadHandler != null ? req.downloadHandler.text : null);
                return true;
            }

            Debug.LogWarning($"[Sync] Save failed ({req.responseCode}): {req.error}");
            return false;
        }

        // ── WO-1128 §3.3 — the server's reconciled figure lands on the client ──────
        //
        //  THE SHAPE, stated once so nobody re-derives it wrong:
        //  the offline number the device computed is a DISPLAY ESTIMATE. It stays on
        //  screen immediately (offline play must never wait on a round trip, §4), and
        //  the save round trip is where it becomes final. api/game/save.js compares the
        //  window the client DECLARED against the server's own elapsed time and, if the
        //  device claimed hours it could not have been away for, stores a scaled-down
        //  balance and reports exactly what it refused. This applies that refusal locally
        //  so the two copies agree — otherwise the server row and the device disagree
        //  forever, and the very next save re-posts the fabricated number.
        //
        //  ⛔ BALANCES ONLY. NEVER THE CLOCK. GameState.LastHarvestClaimMs has three legal
        //  writers, all inside OfflineClaimCoordinator (AdvanceAndSave / StampClock). A
        //  fourth writer here would break the single-owner contract that file states at
        //  its IOfflineClaimConsumer declaration, and — worse — rolling the stamp back to
        //  the server's window would leave the difference RE-CLAIMABLE on the next launch:
        //  the double-grant the coordinator exists to prevent, and the reason
        //  api/game/save.js explicitly refuses to lower the incoming claim clock too.
        //
        //  ⛔ AND IT ONLY EVER SUBTRACTS. Every arm below is bounded above by the CURRENT
        //  local value, so a stale or malicious response can never be a grant path: the
        //  worst a forged `accrual` block can do is cost the sender their own resources.

        /// <summary>
        /// Parses the /api/game/save response: anchors <see cref="ServerClock"/> and applies
        /// any accrual clamp the server reported. Never throws — a save that succeeded must
        /// stay succeeded even if the body is unreadable.
        /// </summary>
        private void ReadSaveResponse(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            BackendSaveResponse resp = null;
            try
            {
                resp = JsonConvert.DeserializeObject<BackendSaveResponse>(json, SaveSchema.JsonSettings);
            }
            catch (Exception ex)
            {
                // §12: no silent catch. The save landed; we just could not read the receipt.
                FlowTrace.Warn("Sync",
                    $"save response parse FAILED ({ex.GetType().Name}: {ex.Message}) - the write succeeded, but any " +
                    "accrual clamp the server reported is unread this round trip. It will be re-reported on the next save.");
                return;
            }
            if (resp == null) return;

            // The save round trip is the most frequent handshake the client makes, so this
            // is the main way ServerClock stays anchored during a session (WO-912 §7.2).
            if (resp.ServerNowMs != null) ServerClock.Sync(resp.ServerNowMs.Value);

            LastAccrualReconcile = resp.Accrual;
            if (resp.Accrual != null) ApplyAccrualClamps(resp.Accrual);
        }

        /// <summary>
        /// Lowers the local time-derived balances the server refused, by the amount it
        /// refused — never below what the server already held, never below zero, and never
        /// upwards. Does NOT touch <see cref="GameState.LastHarvestClaimMs"/>.
        /// </summary>
        private void ApplyAccrualClamps(AccrualReconcileReport report)
        {
            if (_state == null || report?.Clamps == null || report.Clamps.Count == 0) return;

            int applied = 0;
            foreach (var clamp in report.Clamps)
            {
                if (clamp == null || string.IsNullOrEmpty(clamp.Field)) continue;

                double refused = clamp.Claimed - clamp.Allowed;
                if (!(refused > 0d)) continue;      // the server accepted this field whole

                Guard.Try("Sync", $"apply accrual clamp for '{clamp.Field}'", () =>
                {
                    int current = ReadTimeDerivedBalance(clamp.Field);
                    if (current < 0) return;        // unknown field — reported below, never guessed at

                    // FLOOR = whichever is lower of "what the server already banked" and
                    // "what the player has right now". The first mirrors the server's own
                    // refuse-don't-punish rule (a clamp never digs into a prior balance);
                    // the second is what stops a player who has SPENT since the snapshot
                    // from being handed resources back by a subtraction.
                    int floor  = (int)Math.Max(0d, Math.Min(current, clamp.Prior));
                    int target = (int)Math.Max(floor, current - refused);
                    if (target >= current) return;  // nothing to take — never raise a balance

                    WriteTimeDerivedBalance(clamp.Field, target);
                    applied++;
                    FlowTrace.Warn("Offline",
                        $"server REFUSED part of the claimed {clamp.Field}: claimed={clamp.Claimed:0} " +
                        $"allowed={clamp.Allowed:0} prior={clamp.Prior:0} -> local {current} to {target} " +
                        $"(honest fraction {report.HonestFraction ?? 1d:F4}; client window " +
                        $"{report.ClientWindowSec ?? 0d:F0}s vs server elapsed {report.ServerElapsedSec ?? 0d:F0}s). " +
                        "Balance only - the claim clock is untouched.");
                });
            }

            if (applied <= 0)
            {
                FlowTrace.Step("Offline",
                    $"server reported {report.Clamps.Count} accrual clamp(s) ({report.Reason}) but nothing " +
                    "needed lowering locally (already spent, or already at/below the allowed figure).");
                return;
            }

            // Persist + tell the HUD, or the player keeps seeing the number the server
            // just refused until something else happens to redraw.
            Save();
            ResourcesChanged.Invoke();
        }

        /// <summary>Reads one clamp-able balance by its lowercase WIRE key. -1 = unknown key.</summary>
        private int ReadTimeDerivedBalance(string field)
        {
            switch (field)
            {
                case "wood":  return _state.Wood;
                case "iron":  return _state.Iron;
                // WO-1212: the `stone` arm is GONE, and so is its mirror in
                // api/game/save.js TIME_DERIVED_BALANCES. It pointed at a balance nothing
                // displayed or spent, so a server clamp on it moved a number no player could
                // ever have seen. The default arm below FAILS loudly if the two lists
                // disagree again.
                case "food":  return _state.Resources.Food;
                default:
                    // Not silently skipped: a new TIME_DERIVED_BALANCES entry on the server
                    // with no arm here would clamp on the server and NOT on the device, and
                    // the two copies would drift apart with nothing said.
                    FlowTrace.Fail("Offline",
                        $"server clamped a balance this client cannot map: '{field}'. The server row and this " +
                        "device now disagree on it. Add the arm in GameStateService.ReadTimeDerivedBalance/" +
                        "WriteTimeDerivedBalance (mirrors TIME_DERIVED_BALANCES in api/game/save.js).");
                    return -1;
            }
        }

        /// <summary>Writes one clamp-able balance by its lowercase WIRE key. Subtractions only.</summary>
        private void WriteTimeDerivedBalance(string field, int value)
        {
            switch (field)
            {
                case "wood":  _state.Wood  = value; break;
                case "iron":  _state.Iron  = value; break;
                // WO-1212: `stone` retired - see ReadTimeDerivedBalance.
                case "food":
                {
                    // ResourceBalance is a STRUCT — mutate a copy and assign it back, or the
                    // write lands on a temporary and vanishes.
                    var wallet = _state.Resources;
                    wallet.Food = value;
                    _state.Resources = wallet;
                    break;
                }
            }
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
                // WO-1212: `stone` is no longer sent - one balance, one wire key (`food`).
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

        /// <summary>
        /// Records that this identity still has UNSENT changes. The stored payload is a
        /// retry MARKER (and an audit trail of what changed), NOT a body that is later
        /// uploaded verbatim — see <see cref="SendCurrentSnapshot"/> for why.
        /// </summary>
        private void EnqueueOffline(SyncDeltaPayload delta)
        {
            var queue = LoadOfflineQueue();
            queue.Add(delta);
            PlayerPrefs.SetString(SyncQueueKey,
                JsonConvert.SerializeObject(queue, SaveSchema.JsonSettings));
            PlayerPrefs.Save();
            Debug.LogWarning($"[Sync] Queued offline payload (queue depth: {queue.Count}).");
        }

        /// <summary>
        /// Drains the retry queue. Because the upload is always the CURRENT snapshot under
        /// the CURRENT identity (see <see cref="SendCurrentSnapshot"/>), this does exactly
        /// two honest things instead of pretending to replay bodies:
        /// <list type="number">
        ///   <item>DROPS entries queued under a DIFFERENT playerId, loudly. Posting them now
        ///         would write one player's key with another player's snapshot — the
        ///         wrong-key write the old per-entry loop performed silently.</item>
        ///   <item>Collapses every remaining entry into ONE upload. N identical posts were
        ///         pure redundant traffic (and N chances to half-fail).</item>
        /// </list>
        /// </summary>
        private async UniTask FlushOfflineQueue()
        {
            if (!PlayerPrefs.HasKey(SyncQueueKey)) return;
            var queue = LoadOfflineQueue();
            if (queue.Count == 0) return;

            string current = _state?.BoundWallet;
            var mine = new List<SyncDeltaPayload>();
            int foreign = 0;
            foreach (var queued in queue)
            {
                if (queued == null) continue;
                if (!string.IsNullOrEmpty(queued.PlayerId) &&
                    !string.Equals(queued.PlayerId, current, StringComparison.Ordinal)) { foreign++; continue; }
                mine.Add(queued);
            }

            if (foreign > 0)
                FlowTrace.Warn("Sync",
                    "dropped " + foreign + " queued sync marker(s) belonging to a DIFFERENT identity - the " +
                    "upload is this device's current snapshot, so sending them under the current key would " +
                    "overwrite the wrong player's row. Their state was never on this device to send.");

            if (mine.Count == 0)
            {
                PlayerPrefs.DeleteKey(SyncQueueKey);
                PlayerPrefs.Save();
                return;
            }

            // ONE upload covers every queued marker for this identity: the snapshot already
            // contains all of their effects.
            bool ok = await SendCurrentSnapshot();
            if (ok)
            {
                PlayerPrefs.DeleteKey(SyncQueueKey);
                // The queued work is now on the server; record it so the caller's own
                // delta build sees "no changes" instead of posting the same bytes again.
                _lastSyncedSnapshot = Snapshot();
            }
            else
                PlayerPrefs.SetString(SyncQueueKey,
                    JsonConvert.SerializeObject(mine, SaveSchema.JsonSettings));
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
            catch (Exception ex)
            {
                // §12 TGVRU: was a SILENT catch — a corrupt offline-sync queue would
                // drop every queued delta with no trace. R (empty queue) is kept, but
                // now it reports so a lost-sync class of bug reaches the break-log.
                FlowTrace.Fail("Save", $"offline sync queue parse FAILED — dropping corrupt queue. {ex.GetType().Name}: {ex.Message}");
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
            // WO-1212: `public int? Stone` removed - the retired balance is never sent.
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

            /// <summary>
            /// WO-912 §7.2 — the server's own unix-ms, used to anchor <see cref="ServerClock"/>.
            /// Nullable on purpose: an older backend that does not send it must keep loading
            /// normally rather than failing to parse, and the clock simply stays unanchored.
            /// </summary>
            [JsonProperty("serverNowMs")] public double?                ServerNowMs { get; set; }

            /// <summary>
            /// WO-1128 §3.3 — the server's own last_seen for this player (unix-ms).
            /// <c>api/game/load.js</c> has been sending this since WO-1128 and NOTHING
            /// parsed it, so the anchor the server reconciles against was invisible to the
            /// client. Nullable for the same reason as <see cref="ServerNowMs"/>: an older
            /// backend must still load.
            /// </summary>
            [JsonProperty("serverLastSeenMs")] public double?           ServerLastSeenMs { get; set; }
        }

        /// <summary>
        /// WO-1128 §3.3 — the <c>accrual</c> block <c>api/game/save.js</c> returns when it
        /// refused part of a claimed time-derived gain. Present ONLY when something was
        /// clamped (the endpoint omits it otherwise), so a non-null value always means
        /// "the server did not accept everything this device claimed".
        /// </summary>
        public sealed class AccrualReconcileReport
        {
            [JsonProperty("reconciled")]       public bool    Reconciled       { get; set; }
            [JsonProperty("reason")]           public string  Reason           { get; set; }
            /// <summary>Seconds the CLIENT declared between its stored and posted claim clocks.</summary>
            [JsonProperty("clientWindowSec")]  public double? ClientWindowSec  { get; set; }
            /// <summary>Seconds that actually elapsed on the SERVER's clock since last_seen.</summary>
            [JsonProperty("serverElapsedSec")] public double? ServerElapsedSec { get; set; }
            /// <summary>Fraction of the declared window the server judged honest (0..1).</summary>
            [JsonProperty("honestFraction")]   public double? HonestFraction   { get; set; }
            [JsonProperty("clamps")]           public List<AccrualClamp> Clamps { get; set; }
        }

        /// <summary>One clamped balance from <see cref="AccrualReconcileReport"/>.</summary>
        public sealed class AccrualClamp
        {
            /// <summary>Lowercase wire key: "wood" | "iron" | "stone" | "food".</summary>
            [JsonProperty("field")]       public string Field       { get; set; }
            /// <summary>The balance the client posted.</summary>
            [JsonProperty("claimed")]     public double Claimed     { get; set; }
            /// <summary>The balance the server stored instead (always &gt;= <see cref="Prior"/>).</summary>
            [JsonProperty("allowed")]     public double Allowed     { get; set; }
            /// <summary>The balance the server already held before this save.</summary>
            [JsonProperty("prior")]       public double Prior       { get; set; }
            [JsonProperty("claimedGain")] public double ClaimedGain { get; set; }
            [JsonProperty("allowedGain")] public double AllowedGain { get; set; }
        }

        /// <summary>
        /// WO-1128 §3.3 — what <c>api/game/save.js</c> answers with. Before this type existed
        /// the save response was thrown away entirely: the <c>accrual</c> clamp block AND the
        /// <c>serverNowMs</c> handshake (the most frequent one the client makes) both landed
        /// in a <c>DownloadHandlerBuffer</c> nobody read.
        /// </summary>
        private sealed class BackendSaveResponse
        {
            [JsonProperty("ok")]          public bool                     Ok          { get; set; }
            [JsonProperty("success")]     public bool                     Success     { get; set; }
            [JsonProperty("serverNowMs")] public double?                  ServerNowMs { get; set; }
            [JsonProperty("accrual")]     public AccrualReconcileReport   Accrual     { get; set; }
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
