// =============================================================================
// BattleHudVisibilityManager — WO-337 + WO-339 (now the single HUD-MODE manager)
// -----------------------------------------------------------------------------
// SINGLE RESPONSIBILITY: drive the CONTEXT-AWARE HUD MODE — cross-fading three
// states across the TOWN-HUD and BATTLE-HUD CanvasGroups:
//
//   • BATTLE  — a wave is ACTIVE (WaveManager.Phase == Countdown|Active) OR an
//               arena/dungeon BattleController is live.  →  BATTLE HUD in,
//               TOWN HUD out.
//   • TOWN    — idle in the village (VillageHudController.InVillage, NO active
//               combat).                                  →  TOWN HUD in,
//               BATTLE HUD out.
//   • HIDDEN  — exploration (OuterWorld / outside the town ring, no combat) or
//               any non-combat non-village screen.        →  BOTH faded out
//               (minimal HUD — base chrome + compass only).
//
// The two HUD groups (VillageHudController.TownHudGroup @ sortingOrder 140,
// .BattleHudGroup @ 150) are CROSS-FADED here (0.6s). The always-on base chrome
// (build button, party frames) lives on VillageHudController's base canvas and is
// NOT touched — exactly as before. So nothing on the base canvas regresses.
//
// SHARED CONTEXT (WO-339): village-vs-world is read from
// VillageHudController.InVillage so we DON'T duplicate the radial/scene test the
// controller already runs — one source of truth for "in the village".
//
// ASMDEF DISCIPLINE (HUD → Core only): WaveManager lives in DeNelle.Village and
// BattleController in DeNelle.BattleATB — neither is referenced by DeNelle.HUD.
// We resolve + subscribe to both by REFLECTION (the same decoupling the rest of
// this module uses, e.g. CompassHudBootstrap → HeroLocomotion).
//
// WEBGL-SAFE: RefreshVisibility is wrapped in try/catch — an uncaught throw halts
// the WebGL player. The reflection lookups + event re-binds are all guarded.
// =============================================================================

using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class BattleHudVisibilityManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static BattleHudVisibilityManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("BattleHudVisibilityManager");
            DontDestroyOnLoad(go);
            go.AddComponent<BattleHudVisibilityManager>();
        }

        // ── Tunables ──────────────────────────────────────────────────────────
        private const float FadeSeconds = 0.6f;          // WO-339: TOWN↔BATTLE cross-fade
        private const float ReResolveInterval = 0.5f;    // re-find systems / re-eval (cheap)

        // ── HUD modes (WO-339) ──────────────────────────────────────────────────
        private enum HudMode { Hidden, Town, Battle }

        // ── Reflection handles (resolved lazily; HUD → Core asmdef preserved) ──
        private System.Type _waveManagerType;
        private PropertyInfo _wavePhaseProp;   // WaveManager.Phase => WavePhase enum
        private object _waveManager;           // the live WaveManager instance (boxed)

        private System.Type _battleControllerType;

        // ── 9-zone battle HUD spawn (enemy-owned scenes, e.g. Village2) ─────────
        // The NEW battle HUD (DeNelle.Village.Arena.BattleHud9Zone) is normally only
        // created by BattleArenaHud inside an isolated arena fight. An enemy-owned
        // outpost like Village2 resolves to Battle mode here but spawns no arena, so
        // it would show the LEGACY _battleHudGroup instead. We spawn BattleHud9Zone
        // by REFLECTION (HUD → Core only; BattleHud9Zone lives in DeNelle.Village —
        // same decoupling as the WaveManager/BattleController handles above) and tear
        // it down when we leave the enemy scene / Battle mode. Idempotent: we track
        // the instance we own and never double-spawn if one already exists (arena).
        private System.Type _hud9Type;
        private object _hud9Instance;          // the BattleHud9Zone WE spawned (boxed)

        // Cached battle-active flags driven by reflected WaveManager UnityEvents.
        private bool _waveEventActive;         // set by OnWaveStarted/Countdown, cleared by Cleared/Defeat
        private bool _waveEventsBound;

        // ── Target groups ─────────────────────────────────────────────────────
        // WO-563: the OLD battle group is gone (VillageHudController.BattleHudGroup removed). This
        // manager now drives ONLY the TOWN fade + spawns/tears down the NEW 9-zone battle HUD for
        // the Battle scenes that have no other spawner (enemy-owned outposts + RaidBase_* raids;
        // an arena fight spawns its own via BattleArenaHud — the idempotent guard below avoids a
        // double-spawn there).
        private VillageHudController _hud;
        private CanvasGroup _townGroup;        // WO-339

        // ── Fade state ────────────────────────────────────────────────────────
        private float _townTargetAlpha;        // WO-339
        private float _pollTimer;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // Never Destroy(gameObject) on a shared host — but THIS host is our
                // own dedicated object, so destroying the duplicate component is safe.
                Destroy(this);
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            // Snap to the correct state on boot (no fade on first frame).
            ResolveTargets();
            RefreshVisibility();
            if (_townGroup != null) _townGroup.alpha = _townTargetAlpha;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // New scene → the HUD / WaveManager / BattleController may have changed.
            _hud = null;
            _townGroup = null;
            _waveManager = null;
            _waveEventsBound = false;
            _waveEventActive = false;
            ResolveTargets();
            RefreshVisibility();
        }

        private void Update()
        {
            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer <= 0f)
            {
                _pollTimer = ReResolveInterval;
                ResolveTargets();         // lazy-register systems that appear later
                RefreshVisibility();      // re-evaluate (covers polled WaveManager.Phase)
            }

            // WO-563: fade ONLY the town HUD group (0.6s) — the old battle group is gone.
            float step = FadeSeconds > 0f ? Time.unscaledDeltaTime / FadeSeconds : 1f;
            FadeGroup(_townGroup, _townTargetAlpha, step);
        }

        private static void FadeGroup(CanvasGroup g, float target, float step)
        {
            if (g == null) return;
            g.alpha = Mathf.MoveTowards(g.alpha, target, step);
            bool interactive = target > 0.5f;
            g.blocksRaycasts = interactive;
            g.interactable = interactive;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Resolve the HUD group + the battle-state systems (all reflection-safe)
        // ─────────────────────────────────────────────────────────────────────
        private void ResolveTargets()
        {
            try
            {
                if (_hud == null)
                {
                    _hud = FindObjectOfType<VillageHudController>();
                    _townGroup = _hud != null ? _hud.TownHudGroup : null;
                }
                else if (_townGroup == null)
                {
                    _townGroup = _hud.TownHudGroup;
                }

                ResolveWaveManager();
                ResolveBattleControllerType();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleHudVisibilityManager] ResolveTargets failed: " + e.Message);
            }
        }

        private void ResolveWaveManager()
        {
            if (_waveManagerType == null)
                _waveManagerType = System.Type.GetType("DeNelle.Village.WaveManager, DeNelle.Village");
            if (_waveManagerType == null) return;

            if (_waveManager == null || (_waveManager is Object o && o == null))
            {
                _waveManager = FindObjectOfType(_waveManagerType);
                _waveEventsBound = false; // re-bind events to the (new) instance
            }
            if (_waveManager == null) return;

            if (_wavePhaseProp == null)
                _wavePhaseProp = _waveManagerType.GetProperty("Phase",
                    BindingFlags.Public | BindingFlags.Instance);

            // Lazy-register the wave lifecycle UnityEvents (start/clear/defeat).
            if (!_waveEventsBound)
                BindWaveEvents();
        }

        // WaveManager exposes public UnityEvent fields: OnWaveStarted / OnWaveCleared
        // (WaveNumberEvent : UnityEvent<int>), OnCountdownTick (UnityEvent<float>) and
        // OnDefeat (UnityEvent). We add no-arg-compatible listeners via reflection so
        // we react instantly (the 0.5s poll on Phase is the safety net).
        private void BindWaveEvents()
        {
            try
            {
                AddIntListener("OnWaveStarted", () => _waveEventActive = true);
                AddIntListener("OnWaveCleared", () => _waveEventActive = false);
                AddIntListener("OnBreach", () => _waveEventActive = true);
                AddPlainListener("OnDefeat", () => _waveEventActive = false);
                _waveEventsBound = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleHudVisibilityManager] BindWaveEvents failed: " + e.Message);
                _waveEventsBound = true; // don't spin re-trying every poll
            }
        }

        private void AddIntListener(string fieldName, UnityAction onFire)
        {
            var ev = GetEventField<UnityEvent<int>>(fieldName);
            if (ev != null) ev.AddListener(_ => { onFire(); RefreshVisibility(); });
        }

        private void AddPlainListener(string fieldName, UnityAction onFire)
        {
            var ev = GetEventField<UnityEvent>(fieldName);
            if (ev != null) ev.AddListener(() => { onFire(); RefreshVisibility(); });
        }

        private T GetEventField<T>(string fieldName) where T : class
        {
            if (_waveManager == null || _waveManagerType == null) return null;
            var f = _waveManagerType.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            return f != null ? f.GetValue(_waveManager) as T : null;
        }

        private void ResolveBattleControllerType()
        {
            if (_battleControllerType == null)
                _battleControllerType = System.Type.GetType(
                    "DeNelle.BattleATB.BattleController, DeNelle.BattleATB");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Decide show/hide. WEBGL-SAFE — wrapped in try/catch (an uncaught throw
        //  halts the WebGL player).
        // ─────────────────────────────────────────────────────────────────────
        private void RefreshVisibility()
        {
            try
            {
                HudMode mode = EvaluateMode();
                // WO-563: only the TOWN group is faded now. TOWN → town in; BATTLE/HIDDEN → town out
                // (the 9-zone owns the battle screen; exploration shows only base chrome).
                _townTargetAlpha = mode == HudMode.Town ? 1f : 0f;
                // Spawn / tear down the NEW 9-zone battle HUD for Battle scenes that need it
                // (enemy-owned outposts + raids; arena spawns its own — guarded against double).
                ApplyBattleHud9(mode);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleHudVisibilityManager] RefreshVisibility failed: " + e.Message);
            }
        }

        // WO-339: combat wins; else village idle = TOWN; else (exploration) HIDDEN.
        // Village context is SHARED from VillageHudController.InVillage (one test).
        private HudMode EvaluateMode()
        {
            // WO-457: a RaidBase_* scene is a live combat scene (hero-led warband raid),
            // but it has no WaveManager and no ATB BattleController — so neither
            // IsWaveActive nor IsInBattle fires there and the battle HUD (ability bar +
            // wave/vitals chrome) would stay faded out. Treat the raid scene as BATTLE so
            // the combat HUD comes up. Cheap active-scene string test (Core-only).
            // BattleArena (real-time encounter) has no ATB BattleController, but it REGISTERS a
            // BattleLock probe (BattleArena.cs:102), so BattleLock.IsInBattle() is the Core-clean
            // signal that the arena fight is live -> Battle HUD in the arena (owner 2026-06-23).
            // WO-540 (observability only, no behavior change): EvaluateMode was the HUD
            // diagnostic blind spot. Log the decision inputs (throttled ~1/s) so an OuterWorld
            // rep-engagement / arena "old Town HUD leak" is PROVABLE from a graphics capture —
            // the suspect is BattleLock not engaging for a roaming-rep fight -> Town fallthrough.
            DeNelle.Core.Diagnostics.FlowTrace.Throttle("HUD", "evalmode", 1f,
                $"EvaluateMode inputs: wave={IsWaveActive()} atb={IsInBattle()} raid={IsRaidScene()} " +
                $"battleLock={DeNelle.Core.Combat.BattleLock.IsInBattle()} enemyScene={IsEnemyOwnedScene()} scene='{SceneManager.GetActiveScene().name}'");
            // WO-579 (#1 — owner felt-test 2026-06-28 "when i click START WAVE should change to battle
            // HUD"): a LIVE wave (Active / fighting) flips the hub to the Battle HUD (9-zone). This
            // REVERSES the earlier #7 call (which kept the Town HUD during a wave) per the owner's new
            // direction. The calm prepare-phase COUNTDOWN stays on the Town HUD (whose top-left clock
            // shows the next-wave timer); only the fighting phase pulls the Battle HUD up. Auto-start
            // and the manual "Start Wave" override both reach the Active phase → Battle here.
            if (IsWaveFighting() || IsInBattle() || IsRaidScene()
                || DeNelle.Core.Combat.BattleLock.IsInBattle()) return HudMode.Battle;

            // WO-470 / HUD-RCA: an ENEMY-OWNED scene (e.g. Village2, the enemy
            // outpost) is a combat scene too, but it isn't a RaidBase_* name, has no
            // WaveManager and no ATB BattleController — so none of the above fire and
            // it would mis-classify to Hidden (Village2 is NOT inVillage), hiding the
            // whole HUD. Ownership is read Core-clean (HubScenes.IsEnemyOwnedScene
            // mirrors DeNelle.Village.SceneOwnership via the same scene-configs.json;
            // HUD never references DeNelle.Village, CLAUDE.md §5). PRECEDENCE: this
            // sits above the town/inVillage fallthrough so the battle HUD wins.
            if (IsEnemyOwnedScene()) return HudMode.Battle;

            // Owner 2026-06-23 (felt-test: "we lose the HUD as I slide to OuterWorld; it returns once
            // I step back in the castle"): OuterWorld is now ACTIVE gameplay (encounters + harvesting),
            // so the HUD must persist past the town ring instead of vanishing the instant the hero
            // crosses the seam. Non-combat hub -> Town HUD whether the hero is in-ring OR out in
            // OuterWorld. (Was: inVillage ? Town : Hidden -- a radial hide from when OuterWorld was
            // empty transit. The radial InVillage check no longer gates HUD visibility.)
            return HudMode.Town;
        }

        /// <summary>True when the active scene is enemy-owned (WO-470). Core-clean —
        /// reads DeNelle.Core.HubScenes (HUD → Core only).</summary>
        private bool IsEnemyOwnedScene()
        {
            var s = SceneManager.GetActiveScene();
            if (!s.IsValid()) return false;
            bool enemy = DeNelle.Core.HubScenes.IsEnemyOwnedScene(s.name);
            if (enemy)
                DeNelle.Core.Diagnostics.FlowTrace.Step(
                    "HUD", $"[HUD] enemy-owned scene '{s.name}' -> Battle mode");
            return enemy;
        }

        /// <summary>True while a <c>RaidBase_*</c> raid scene is active (WO-457).</summary>
        private bool IsRaidScene()
        {
            var s = SceneManager.GetActiveScene();
            return s.IsValid() && DeNelle.Core.HubScenes.IsRaid(s.name);
        }

        /// <summary>Village wave defense in progress (prepare countdown or fighting).</summary>
        private bool IsWaveActive()
        {
            if (_waveEventActive) return true;

            // Authoritative poll of WaveManager.Phase (the real API; there is no
            // "IsWaveActive" — the spec's name is illustrative). Active states:
            // Countdown (1) and Active (2). Idle/Breached/Complete/Defeated = not.
            if (_waveManager == null || _wavePhaseProp == null) return false;
            object phase = _wavePhaseProp.GetValue(_waveManager);
            if (phase == null) return false;
            int p = System.Convert.ToInt32(phase);
            return p == 1 /* Countdown */ || p == 2 /* Active */;
        }

        /// <summary>
        /// WO-579: the wave is in its ACTIVE (fighting) phase — enemies are spawned and the hub is
        /// under attack. Distinct from <see cref="IsWaveActive"/> (which also includes the calm Countdown
        /// phase): the prepare-phase countdown stays on the TOWN HUD (top-left next-wave clock), and only
        /// the live wave flips to the Battle HUD (owner felt-test 2026-06-28 "click START WAVE → battle
        /// HUD"). Reads the same reflected <c>Phase</c> + the wave-started/cleared event flag.
        /// </summary>
        private bool IsWaveFighting()
        {
            if (_waveEventActive) return true;
            if (_waveManager == null || _wavePhaseProp == null) return false;
            object phase = _wavePhaseProp.GetValue(_waveManager);
            if (phase == null) return false;
            return System.Convert.ToInt32(phase) == 2; // Active
        }

        /// <summary>Arena/dungeon ATB battle live — a BattleController exists + enabled.</summary>
        private bool IsInBattle()
        {
            if (_battleControllerType == null) return false;
            var bc = FindObjectOfType(_battleControllerType) as Behaviour;
            return bc != null && bc.isActiveAndEnabled;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  9-zone HUD ownership for Battle scenes. WO-563: spawn for ANY Battle mode
        //  (enemy-owned outpost, RaidBase_* raid, or arena) so NO battle context is ever
        //  HUD-less. An arena fight already spawns one via BattleArenaHud — the idempotent
        //  guard in EnsureEnemySceneHud9 (FindObjectOfType != null → bail) prevents a double,
        //  and we only ever tear down the instance WE spawned. Spawns/tears down by reflection
        //  so DeNelle.HUD stays Core-only (BattleHud9Zone lives in DeNelle.Village).
        // ─────────────────────────────────────────────────────────────────────
        private void ApplyBattleHud9(HudMode mode)
        {
            bool wantHud9 = mode == HudMode.Battle && DeNelle.Core.FeatureFlags.BattleHud9Zone;
            if (wantHud9) EnsureEnemySceneHud9();
            else TearDownEnemySceneHud9();
        }

        private void EnsureEnemySceneHud9()
        {
            try
            {
                if (_hud9Type == null)
                    _hud9Type = System.Type.GetType(
                        "DeNelle.Village.Arena.BattleHud9Zone, DeNelle.Village");
                if (_hud9Type == null) return;

                // Our tracked instance still alive (Unity-null aware)? → reuse it.
                if (_hud9Instance is Object alive && alive != null) return;
                _hud9Instance = null;

                // Idempotent: if a BattleHud9Zone already exists (e.g. spawned by an
                // arena's BattleArenaHud), don't create a second one and don't claim it.
                if (FindObjectOfType(_hud9Type) != null) return;

                var create = _hud9Type.GetMethod("Create",
                    BindingFlags.Public | BindingFlags.Static);
                if (create == null) return;
                // Create() returns null when ff.battlehud9zone is OFF (already gated above).
                _hud9Instance = create.Invoke(null, null);
                if (_hud9Instance != null)
                    DeNelle.Core.Diagnostics.FlowTrace.Step("HUD",
                        "[HUD] enemy-owned scene -> spawned BattleHud9Zone (NEW battle HUD).");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleHudVisibilityManager] EnsureEnemySceneHud9 failed: " + e.Message);
            }
        }

        private void TearDownEnemySceneHud9()
        {
            try
            {
                if (_hud9Instance == null) return;
                if (_hud9Type != null && _hud9Instance is Object o && o != null)
                {
                    var close = _hud9Type.GetMethod("Close",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (close != null) close.Invoke(_hud9Instance, null);
                    else if (o is Component c && c != null) Destroy(c.gameObject);
                }
                _hud9Instance = null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleHudVisibilityManager] TearDownEnemySceneHud9 failed: " + e.Message);
            }
        }
    }
}
