// =============================================================================
// HeroPoseController — WO-365: context-aware hero pose (TOWN relaxed vs COMBAT ready).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// SPEC INTENT:
//   • TOWN / idle in the village (no active wave) → relaxed/breathing IDLE stance,
//     weapon SHEATHED (hidden).
//   • BATTLE / exploration (a wave is live, breached, or otherwise in combat) →
//     combat-ready stance, weapon DRAWN (visible).
//   • Smooth 0.30s transition between the two.
//
// HOW IT LISTENS (no WaveManager edits — listen only, per WO-365):
//   WaveManager (same DeNelle.Village assembly) exposes public UnityEvent fields:
//     OnWaveStarted / OnWaveCleared / OnBreach (WaveNumberEvent : UnityEvent<int>)
//     OnDefeat (UnityEvent). We FIND the live WaveManager (FindObjectOfType) and
//     AddListener / RemoveListener to those existing events — exactly how
//     HeroVictoryPoseBridge + the HUD bridges subscribe. We add NO hook inside
//     WaveManager. Mapping:
//        OnWaveStarted / OnBreach  → Combat (weapon drawn, combat stance)
//        OnWaveCleared / OnDefeat  → Idle   (weapon sheathed, relaxed stance)
//     SceneManager.sceneLoaded on a village scene → Idle (fresh town entry).
//   The 0.5s poll is the safety net: if events were missed (WaveManager spawned
//   after us, or a phase changed without an event) we re-evaluate from
//   WaveManager.Phase (Countdown/Active = Combat) and re-bind to a new instance.
//
// ANIMATOR (param-guarded — animator-param-guard memory):
//   Drives a single BOOL parameter "Combat" (true = combat-ready, false = relaxed).
//   GUARDED by a cached HasParameter scan (RefreshParamCache) so an unguarded
//   SetBool on a controller that lacks the param never spams a per-call error.
//   The hero's runtimeAnimatorController is swapped in at runtime (HeroBodySwapper),
//   so we re-scan whenever the resolved Animator changes. If the controller has no
//   "Combat" param / no combat+idle clips, the stance drive is a silent no-op and
//   the weapon show/hide still works — see "BEST-EFFORT" below.
//
// WEAPON SHEATH (the visible, always-working half):
//   Weapon visuals live as children under the swapped-in "HeroBody":
//     • "BowProp"      — the Ranger bow (HeroBowAttachment, under the LeftHand bone)
//     • "GearVisual_*" — sword/staff/mace/shield (GearVisualApplier, hand bones)
//   We collect those and SetActive(false) when sheathed / SetActive(true) when drawn.
//   The toggle happens at the MIDPOINT of the 0.30s transition so it reads as a
//   draw/sheath beat rather than a pop. Re-collected on each transition so gear
//   equipped mid-session (shop/level-up re-apply) is picked up.
//
// BEST-EFFORT / NEEDS AUTHORING:
//   • The current hero controller (HeroAnimatorSetup) defines only Speed(float) +
//     Victory(trigger) — there is NO "Combat" bool and NO dedicated combat-stance /
//     relaxed-breathing idle clips yet. So today this delivers the WEAPON sheath/draw
//     + sets the (currently absent) "Combat" bool best-effort. CONTROLLER ADDITION
//     REQUIRED for the visible stance change: add a Bool param "Combat" and two
//     idle states (relaxed-breathing when false, weapon-ready when true) with a 0.30s
//     transition between them. Author in Assets/Editor/HeroAnimatorSetup.cs — do NOT
//     hand-edit the controller asset.
//
// SELF-BOOTSTRAP + SAFETY:
//   RuntimeInitializeOnLoadMethod spawns a DDOL host (TownHudBridge pattern). Every
//   event read + animator/weapon touch is try/catch-guarded (an uncaught throw halts
//   the WebGL player). Village-scene-gated: only active work in a "Village*" scene.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Switches the hero between a relaxed TOWN pose (weapon sheathed) and a
    /// combat-ready BATTLE pose (weapon drawn) by listening to WaveManager's
    /// existing lifecycle events. Self-bootstrapping, village-scene-gated,
    /// param-guarded, WebGL-safe. See the file header for what is best-effort.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroPoseController : MonoBehaviour
    {
        // ── Tunables ──────────────────────────────────────────────────────────
        private const float TransitionSeconds = 0.30f;   // WO-365 smooth blend
        private const float ReResolveInterval = 0.5f;     // re-find systems / re-eval
        private const string CombatBoolName = "Combat";   // animator bool param (best-effort)

        private static readonly int CombatBoolHash = Animator.StringToHash(CombatBoolName);

        // ── Singleton DDOL host ───────────────────────────────────────────────
        private static HeroPoseController _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("HeroPoseController");
            DontDestroyOnLoad(go);
            go.AddComponent<HeroPoseController>();
        }

        // ── State ─────────────────────────────────────────────────────────────
        private bool _combatPose;          // current desired pose (true = combat-ready)
        private bool _poseApplied;          // has the current pose been pushed at least once
        private float _pollTimer;
        private Coroutine _transition;

        // WaveManager subscription (FOUND, not modified — listen only).
        private WaveManager _wave;

        // Hero animator (rides the swapped-in "HeroBody" child) + its param guard.
        private Animator _animator;
        private Animator _paramCheckedAnimator;
        private bool _hasCombatParam;

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeWave();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeWave();
            if (_instance == this) _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // Our own dedicated host — destroying the duplicate component is safe
                // (never Destroy(gameObject) on a shared host — singleton-dedup memory).
                Destroy(this);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            // Boot into the relaxed TOWN pose (snap, no transition on first frame).
            ResolveSystems();
            _combatPose = EvaluateCombat();
            ApplyPose(_combatPose, instant: true);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // New scene → animator / weapon / WaveManager may all have changed.
            _animator = null;
            _paramCheckedAnimator = null;
            UnsubscribeWave();
            _wave = null;
            _poseApplied = false;

            // Entering a village scene starts relaxed; non-village scenes are left
            // alone (the poll + events still drive combat-ready where relevant).
            if (IsVillageScene(scene.name))
            {
                ResolveSystems();
                _combatPose = EvaluateCombat();
                ApplyPose(_combatPose, instant: true);
            }
        }

        private void Update()
        {
            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = ReResolveInterval;

            try
            {
                ResolveSystems();        // lazy-find a WaveManager / animator that appeared later
                bool want = EvaluateCombat();
                if (want != _combatPose || !_poseApplied)
                {
                    _combatPose = want;
                    ApplyPose(_combatPose, instant: false);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HeroPoseController] poll failed: " + e.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Resolve the hero animator + subscribe to the live WaveManager events.
        // ─────────────────────────────────────────────────────────────────────
        private void ResolveSystems()
        {
            try
            {
                ResolveAnimator();
                TryResolveAndSubscribeWave();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HeroPoseController] ResolveSystems failed: " + e.Message);
            }
        }

        private void ResolveAnimator()
        {
            if (_animator == null)
            {
                var heroes = FindObjectsByType<HeroLocomotion>(FindObjectsSortMode.None);
                if (heroes != null && heroes.Length > 0)
                {
                    Transform heroRoot = heroes[0].transform;
                    var bodyT = heroRoot.Find("HeroBody");
                    if (bodyT != null) _animator = bodyT.GetComponentInChildren<Animator>();
                    if (_animator == null) _animator = heroRoot.GetComponentInChildren<Animator>();
                }
            }

            // Re-scan the controller's params whenever the resolved Animator changes
            // (HeroBodySwapper assigns a fresh controller at runtime).
            if (_animator != null && _animator != _paramCheckedAnimator)
                RefreshParamCache();
        }

        // animator-param-guard memory: scan declared params once per (re)resolve so a
        // SetBool never hits a missing param (which would log every call). A null /
        // param-less controller leaves _hasCombatParam false → the stance drive no-ops.
        private void RefreshParamCache()
        {
            _paramCheckedAnimator = _animator;
            _hasCombatParam = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (var p in _animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.nameHash == CombatBoolHash)
                {
                    _hasCombatParam = true;
                    break;
                }
            }
        }

        // FIND the WaveManager and AddListener to its EXISTING events (no WaveManager
        // edits). Re-binds if a new instance spawned (e.g. WaveManager created after us).
        private void TryResolveAndSubscribeWave()
        {
            if (_wave != null) return;
            var wave = FindFirstObjectByType<WaveManager>();
            if (wave == null) return;

            _wave = wave;
            _wave.OnWaveStarted.AddListener(OnEnterCombat);
            _wave.OnBreach.AddListener(OnEnterCombat);
            _wave.OnWaveCleared.AddListener(OnLeaveCombat);
            _wave.OnDefeat.AddListener(OnDefeat);
        }

        private void UnsubscribeWave()
        {
            if (_wave == null) return;
            // The events are non-null UnityEvent fields on WaveManager; guard anyway.
            if (_wave.OnWaveStarted != null) _wave.OnWaveStarted.RemoveListener(OnEnterCombat);
            if (_wave.OnBreach != null) _wave.OnBreach.RemoveListener(OnEnterCombat);
            if (_wave.OnWaveCleared != null) _wave.OnWaveCleared.RemoveListener(OnLeaveCombat);
            if (_wave.OnDefeat != null) _wave.OnDefeat.RemoveListener(OnDefeat);
            _wave = null;
        }

        // ── Event handlers (listen-only) ──────────────────────────────────────
        private void OnEnterCombat(int _) => RequestPose(true);
        private void OnLeaveCombat(int _) => RequestPose(false);
        private void OnDefeat() => RequestPose(false);

        private void RequestPose(bool combat)
        {
            if (combat == _combatPose && _poseApplied) return;
            _combatPose = combat;
            ApplyPose(combat, instant: false);
        }

        /// <summary>
        /// Combat-ready when a wave is live (Countdown/Active). The events flip the
        /// pose instantly; this is the poll-time authoritative re-evaluation
        /// (safety net for missed events / late WaveManager).
        /// </summary>
        private bool EvaluateCombat()
        {
            if (_wave == null) return false;
            var phase = _wave.Phase;
            return phase == WavePhase.Countdown || phase == WavePhase.Active;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Apply the pose: animator bool (guarded) + weapon show/hide over 0.30s.
        // ─────────────────────────────────────────────────────────────────────
        private void ApplyPose(bool combat, bool instant)
        {
            try
            {
                // Stance bool — guarded; the controller's own transition gives the
                // 0.30s anim blend once the "Combat" param + states are authored.
                if (_animator != null && _hasCombatParam)
                    _animator.SetBool(CombatBoolHash, combat);

                _poseApplied = true;

                if (instant || !isActiveAndEnabled || TransitionSeconds <= 0f)
                {
                    SetWeaponVisible(combat);
                    return;
                }

                if (_transition != null) StopCoroutine(_transition);
                _transition = StartCoroutine(TransitionRoutine(combat));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HeroPoseController] ApplyPose failed: " + e.Message);
            }
        }

        // Toggle the weapon at the MIDPOINT of the transition so it reads as a
        // draw/sheath beat that lines up with the animator's 0.30s stance blend.
        private IEnumerator TransitionRoutine(bool combat)
        {
            yield return new WaitForSecondsRealtime(TransitionSeconds * 0.5f);
            SetWeaponVisible(combat);
            _transition = null;
        }

        /// <summary>
        /// Show (drawn) / hide (sheathed) the hero's weapon visuals. Re-collected
        /// each call so gear equipped mid-session is honoured. Guarded throughout.
        /// </summary>
        private void SetWeaponVisible(bool visible)
        {
            try
            {
                foreach (var w in CollectWeaponVisuals())
                    if (w != null && w.activeSelf != visible) w.SetActive(visible);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HeroPoseController] SetWeaponVisible failed: " + e.Message);
            }
        }

        // Weapon visuals are children under the swapped-in HeroBody:
        //   • "BowProp"      — Ranger bow (HeroBowAttachment, LeftHand bone)
        //   • "GearVisual_*" — sword/staff/mace/shield (GearVisualApplier, hand bones)
        // We search the whole hero hierarchy (the props sit on bone children, not the
        // body root) and match by name. Inactive objects are included so a sheathed
        // weapon can be re-shown.
        private readonly List<GameObject> _weaponBuf = new List<GameObject>();

        private List<GameObject> CollectWeaponVisuals()
        {
            _weaponBuf.Clear();

            Transform heroRoot = null;
            if (_animator != null)
            {
                // Walk up to the hero root that owns HeroLocomotion (animator is on the body).
                var loco = _animator.GetComponentInParent<HeroLocomotion>();
                heroRoot = loco != null ? loco.transform : _animator.transform;
            }
            else
            {
                var heroes = FindObjectsByType<HeroLocomotion>(FindObjectsSortMode.None);
                if (heroes != null && heroes.Length > 0) heroRoot = heroes[0].transform;
            }
            if (heroRoot == null) return _weaponBuf;

            var all = heroRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t == null || t.name == null) continue;
                string n = t.name;
                if (n == "BowProp" || n.IndexOf("GearVisual", System.StringComparison.Ordinal) >= 0)
                    _weaponBuf.Add(t.gameObject);
            }
            return _weaponBuf;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static bool IsVillageScene(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) &&
            sceneName.IndexOf("Village", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
