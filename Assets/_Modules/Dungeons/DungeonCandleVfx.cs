// =============================================================================
// DungeonCandleVfx — the authored CandleAnchor flame, and the loop slot it borrows.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// ## WHY THIS FILE CHANGED (WO-1229) — READ THE CAPTURE, NOT THE THEORY
//
// Owner's device, dg_starter_loop, 08-25 (tmp/felt2/logcat-auth.txt):
//
//   19:29:24.762 [Flow:DungeonVFX] bound 44 CandleAnchor marker(s) to proximity-pooled
//                                  Env_Candle flames in 'dg_starter_loop'.
//   19:30:29.174 [Flow:VFXManager] PlayLoop('Env_Candle')     SKIPPED — active loops 24/24
//        ... continuously, once a second, until 19:31:26.309 ...
//                [Flow:VFXManager] PlayLoop('Aura_NearDeath') SKIPPED — active loops 24/24
//                [Flow:HeroHpAura] 'NearDeath' aura was REFUSED by VFXManager (loop cap or
//                                  quality gate). This is the PRIMARY colourblind low-HP
//                                  read — if it is being dropped, the hero has no
//                                  non-colour danger signal. Retrying.
//
// FORTY-FOUR anchors, each an INDEPENDENT, FIRST-COME claimant on a GLOBAL pool of
// twenty-four. Nothing here leaked a handle — every exit path already called Stop().
// The defect is one level up: this class had no POPULATION BOUND and no notion that
// anything else in the game might need a slot more than a candle does. Enemy and pet
// auras have had exactly that bound since WO-889 (VfxAuraProximityCuller, nearest-N)
// precisely so they "can never monopolise the pool no matter how many bodies a wave
// spawns". Room dress — the most numerous loop class in the game — had none.
//
// So the candle now JOINS THAT RING (the ambient half of it) instead of racing for
// the pool. Two consequences worth stating because they are the fix:
//   • The nearest N candles are lit and the rest are dark BY BUDGET. A flame you
//     cannot resolve was paying a pool slot to be invisible.
//   • The ambient ring stops short of VfxLoopBudget.AccessibilityReserve, so the
//     colourblind low-HP tell cannot be outbid by decoration. Ever.
//
// ⚠ THE CEILING WAS NOT TOUCHED, IN EITHER DIRECTION. This repo has met this
// saturation at 20/20, then 40/40, then 24/24; a ceiling that keeps moving while the
// symptom returns is a leak being papered over. The consumer yields instead.
//
// ⚠ THE SECOND HALF OF THE CAUSE, NOW FIXED IN VfxLoopBudget: the whole capture above
// happened at the VILLAGE tier (24) inside a dungeon. VfxLoopBudget.DungeonLoops (48)
// exists for exactly this scene, and the only thing that had ever declared the dungeon
// tier was DungeonSceneBootstrap — a MonoBehaviour present in ZERO scenes and ZERO
// prefabs, so VFXManager.ApplyDungeonMode(true) never ran in a shipped build and there
// is not one [Flow:VfxBudget] line in any device log. The owner ruled on 2026-08-26 to
// turn the tier on and to bind it WITHOUT an authoring step: VfxLoopBudget now resolves
// it from the loaded scene set on its own runtime hook. The ring and the reserve below
// still apply — 48 is headroom, not a licence to unbind room dress.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Dungeons
{
    /// <summary>Proximity-streamed pooled flame for an authored CandleAnchor marker.
    /// Budgeted by the ambient half of <see cref="VfxAuraProximityCuller"/> (WO-1229).</summary>
    [DisallowMultipleComponent]
    public sealed class DungeonCandleVfx : MonoBehaviour, IProximityAura
    {
        private const float StartRange = 13f;
        private const float StopRange = 16f;
        private Transform _hero;
        private VFXHandle _handle;
        private float _nextCheck;

        // Granted by the ambient ring. Starts FALSE so a room's worth of anchors cannot
        // all light in the frame before the ring's first tick — the transient saturation
        // is the very bug. See Allowed for the no-culler degradation.
        private bool _allowedByRing;

        public void Configure(Transform hero) => _hero = hero;

        /// <summary>
        /// True while a grant is in force. Degrades PERMISSIVE when no culler exists at
        /// all (headless runs, a bootstrap that threw): a dungeon with no flames and no
        /// error is worse than an unbudgeted one, and the culler's own teardown makes the
        /// same choice for the same reason.
        /// </summary>
        private bool Allowed => _allowedByRing || VfxAuraProximityCuller.Instance == null;

        // ── IProximityAura ───────────────────────────────────────────────────

        public Transform AuraTransform => this == null ? null : transform;

        /// <summary>
        /// Range with hysteresis: light at 13 m, keep until 16 m. The RANGE question is
        /// the driver's; the BUDGET question is the ring's. Keeping them apart is what
        /// stops "beyond the ring" from being mistaken for "out of range" in a log.
        /// </summary>
        public bool WantsAura
        {
            get
            {
                if (_hero == null) return false;
                float sq = (_hero.position - transform.position).sqrMagnitude;
                float r  = IsLit ? StopRange : StartRange;
                return sq <= r * r;
            }
        }

        public void SetAuraAllowed(bool allowed)
        {
            if (_allowedByRing == allowed) return;
            _allowedByRing = allowed;
            if (!allowed) StopFlame("ring revoked the grant");
        }

        private bool IsLit => _handle != null && _handle.IsAlive;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void OnEnable()  => VfxAuraProximityCuller.RegisterAmbient(this);

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.25f;

            // WO-1229: a null hero used to `return` here, which meant a LIT candle whose
            // hero reference died (scene hand-off, hero despawn) held its loop slot with
            // no path left that could ever release it. Release instead — the flame is
            // relit the moment a hero exists again.
            if (_hero == null)
            {
                if (IsLit) StopFlame("hero reference gone");
                return;
            }

            bool want = WantsAura;
            if (want && Allowed && !IsLit) StartFlame();
            else if (!want && _handle != null) StopFlame("hero left the 16 m keep-range");
        }

        private void StartFlame()
        {
            var manager = VFXManager.Instance;
            if (manager == null) return;
            _handle = manager.PlayEnvironment(VFXType.Env_Candle, transform);
            if (_handle != null) _handle.SetPosition(transform.position);
        }

        private void StopFlame(string why)
        {
            if (_handle == null) return;

            // WO-1229: IMMEDIATE, not graceful. The graceful path (the old default) stops
            // emission and defers the pool return — and therefore the loop-registry
            // removal, which IS the decrement — by 2.5 s. Across 44 anchors that is a
            // rolling block of slots held by candles that are already dark, on top of the
            // ones legitimately lit. A flame sixteen metres behind the hero has no tail
            // anyone can see, so there is nothing to be graceful about.
            _handle.Stop(true);
            _handle = null;

            var mgr = VFXManager.Instance;
            FlowTrace.Throttle("DungeonVFX", "candle-release", 1f,
                "Env_Candle RELEASED ('" + why + "') — the slot is back in the pool " +
                (mgr != null ? "(loops now " + mgr.ActiveLoopCount + "/" + mgr.MaxActiveLoops + ")" : "") +
                ". This line is the WO-1229 acceptance: the count must be seen going DOWN.");
        }

        private void OnDisable()
        {
            VfxAuraProximityCuller.UnregisterAmbient(this);
            StopFlame("component disabled");
        }

        private void OnDestroy()
        {
            VfxAuraProximityCuller.UnregisterAmbient(this);
            StopFlame("component destroyed");
        }
    }

    /// <summary>Connects bake-authored markers without hand-editing dungeon scenes.</summary>
    internal static class DungeonCandleVfxInstaller
    {
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Bind(SceneManager.GetActiveScene(), null);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Bind(scene, null);

        internal static void Rebind(Scene scene, Transform hero) => Bind(scene, hero);

        private static void Bind(Scene scene, Transform resolvedHero)
        {
            // WO-1229: the dungeon-scene test is NOT repeated here. VfxLoopBudget owns the
            // one predicate, because the loop TIER and the candle BINDING must agree about
            // what a dungeon is - two copies of this test would be the same duplicated-state
            // drift that left the tier itself dead in zero scenes.
            if (!scene.IsValid() || !scene.isLoaded || !VfxLoopBudget.IsDungeonSceneName(scene.name)) return;

            Transform hero = resolvedHero;
            if (hero == null)
            {
                var heroGo = GameObject.FindGameObjectWithTag("Player");
                hero = heroGo != null ? heroGo.transform : null;
            }
            if (hero == null) return;
            int bound = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var marker in root.GetComponentsInChildren<Transform>(true))
            {
                if (marker == null || marker.name != "CandleAnchor") continue;
                var flame = marker.GetComponent<DungeonCandleVfx>();
                if (flame == null) flame = marker.gameObject.AddComponent<DungeonCandleVfx>();
                flame.Configure(hero);
                bound++;
            }
            if (bound > 0)
                FlowTrace.Step("DungeonVFX", $"bound {bound} CandleAnchor marker(s) to proximity-pooled " +
                    $"Env_Candle flames in '{scene.name}'. WO-1229: they now share the ambient half of " +
                    $"the nearest-N ring (max {VfxLoopBudget.AmbientEnvRing} lit at once, never touching " +
                    $"the {VfxLoopBudget.AccessibilityReserve}-slot accessibility reserve), so the count " +
                    "bound here is no longer the count that competes for the pool.");
        }
    }
}
