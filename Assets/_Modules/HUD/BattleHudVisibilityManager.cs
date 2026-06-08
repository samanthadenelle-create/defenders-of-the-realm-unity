// =============================================================================
// BattleHudVisibilityManager — WO-337
// -----------------------------------------------------------------------------
// SINGLE RESPONSIBILITY: show the BATTLE HUD (abilities, hero vitals, wave /
// enemy-count, combat status) ONLY during ACTIVE COMBAT, and hide it when idle
// (village plaza, open-world exploration, title, hero-select, any non-combat).
//
//   SHOW when:
//     • A village wave is ACTIVE  — WaveManager.Phase == Countdown | Active
//       (the wave loop's "in combat" window: prepare countdown → fighting).
//     • An arena/dungeon battle is live — a DeNelle.BattleATB.BattleController
//       exists and is enabled in the scene.
//   HIDE otherwise (idle village, exploration, title, hero-select, …).
//
// The IDLE / village UI (Build button, Castle/Heart HP, resource strip) lives on
// VillageHudController's BASE canvas and is NOT touched here — only the separate
// BATTLE-HUD CanvasGroup (VillageHudController.BattleHudGroup, its own canvas at
// sortingOrder ~150) is faded. So: idle village = build / resources / castle-HP
// visible while abilities / vitals / wave-info are hidden; wave active = the
// battle HUD fades IN.
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
        private const float FadeSeconds = 0.3f;          // smooth show/hide fade
        private const float ReResolveInterval = 0.5f;    // re-find systems / re-eval (cheap)

        // ── Reflection handles (resolved lazily; HUD → Core asmdef preserved) ──
        private System.Type _waveManagerType;
        private PropertyInfo _wavePhaseProp;   // WaveManager.Phase => WavePhase enum
        private object _waveManager;           // the live WaveManager instance (boxed)

        private System.Type _battleControllerType;

        // Cached battle-active flags driven by reflected WaveManager UnityEvents.
        private bool _waveEventActive;         // set by OnWaveStarted/Countdown, cleared by Cleared/Defeat
        private bool _waveEventsBound;

        // ── Target group ──────────────────────────────────────────────────────
        private VillageHudController _hud;
        private CanvasGroup _battleGroup;

        // ── Fade state ────────────────────────────────────────────────────────
        private float _targetAlpha;            // 0 hidden, 1 shown
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
            if (_battleGroup != null) _battleGroup.alpha = _targetAlpha;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // New scene → the HUD / WaveManager / BattleController may have changed.
            _hud = null;
            _battleGroup = null;
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

            // Smooth 0.3s fade of the battle-HUD CanvasGroup (alpha + raycasts).
            if (_battleGroup == null) return;
            float step = FadeSeconds > 0f ? Time.unscaledDeltaTime / FadeSeconds : 1f;
            _battleGroup.alpha = Mathf.MoveTowards(_battleGroup.alpha, _targetAlpha, step);
            bool interactive = _targetAlpha > 0.5f;
            _battleGroup.blocksRaycasts = interactive;
            _battleGroup.interactable = interactive;
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
                    _battleGroup = _hud != null ? _hud.BattleHudGroup : null;
                }
                else if (_battleGroup == null)
                {
                    _battleGroup = _hud.BattleHudGroup;
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
                bool inCombat = IsWaveActive() || IsInBattle();
                _targetAlpha = inCombat ? 1f : 0f;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BattleHudVisibilityManager] RefreshVisibility failed: " + e.Message);
            }
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

        /// <summary>Arena/dungeon ATB battle live — a BattleController exists + enabled.</summary>
        private bool IsInBattle()
        {
            if (_battleControllerType == null) return false;
            var bc = FindObjectOfType(_battleControllerType) as Behaviour;
            return bc != null && bc.isActiveAndEnabled;
        }
    }
}
