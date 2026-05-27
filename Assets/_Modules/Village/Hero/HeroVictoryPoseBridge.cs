// =============================================================================
// HeroVictoryPoseBridge — DEF-70 (4): wave-clear victory pose on the hero.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village (the only hero assembly on this branch).
//
// SPEC INTENT: when a wave is CLEARED, fire a "VictoryPose" trigger on the hero's
// base Animator layer (play a 2-3s celebration, then transition back to Idle). If
// a new wave STARTS before the pose finishes, reset the trigger so it isn't left
// pending. Subscribe/unsubscribe only in OnEnable/OnDisable (no polling).
//
// RECONCILIATION TO THIS BRANCH (do not trust the spec's wiring verbatim):
//   • The spec subscribes from HeroLocomotion to "WaveManager.OnWaveCompleted".
//     On THIS branch there is NO OnWaveCompleted — WaveManager (not a singleton)
//     exposes UnityEvent<int> OnWaveCleared and OnWaveStarted. So this is a BRIDGE
//     that lives on the WaveManager GameObject, exactly like DailyQuestCombatBridge:
//     [RequireComponent(typeof(WaveManager))] + cache in Reset/OnEnable +
//     AddListener/RemoveListener in OnEnable/OnDisable. The orchestrator adds this
//     component to the WaveManager GameObject (same place as DailyQuestCombatBridge /
//     WaveHudBridge), keeping the hero scripts free of wave-loop coupling.
//   • The hero controller (Assets/Editor/HeroAnimatorSetup.cs) has ONLY Speed(float)
//     + Cast(trigger). It has NO "VictoryPose" trigger yet — SetTrigger / ResetTrigger
//     on an undefined parameter is a Unity warning, NOT an exception, so this runs
//     safely today and becomes a real celebration once the controller adds it.
//   • The hero's Animator rides on the swapped-in "HeroBody" child (HeroBodySwapper),
//     so we locate the hero via HeroLocomotion then resolve its Animator with the
//     same child-search + transform.Find("HeroBody") self-heal the hero scripts use.
//
// CONTROLLER ADDITION REQUIRED for a real victory pose (document only — author in
// HeroAnimatorSetup.cs; do NOT hand-edit the controller asset):
//   • Add a Trigger parameter named exactly "VictoryPose" (hash below is the contract).
//   • Add a 2-3s celebration state on the BASE layer (index 0 — NOT masked, the whole
//     body celebrates) reached by an AnyState transition on the VictoryPose trigger,
//     with an exit-time transition back to Idle.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Bridges WaveManager wave-clear / wave-start events to a hero "VictoryPose"
    /// animation trigger. Lives on the WaveManager GameObject (bridge pattern).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveManager))]
    public sealed class HeroVictoryPoseBridge : MonoBehaviour
    {
        /// <summary>
        /// Animator <c>VictoryPose</c> trigger hash — cached so there is never a
        /// per-event string lookup (acceptance criterion). The controller must define
        /// a Trigger parameter with this exact name (see file header).
        /// </summary>
        private static readonly int VictoryPoseHash = Animator.StringToHash("VictoryPose");

        [SerializeField] private WaveManager _wave;

        // Cached hero animator. Resolved lazily on the event because the hero body
        // is swapped in at runtime (HeroBodySwapper) and may not exist at OnEnable.
        private Animator _heroAnimator;

        private void Reset() => _wave = GetComponent<WaveManager>();

        private void OnEnable()
        {
            if (_wave == null) _wave = GetComponent<WaveManager>();
            if (_wave != null)
            {
                _wave.OnWaveCleared.AddListener(HandleWaveCleared);
                _wave.OnWaveStarted.AddListener(HandleWaveStarted);
            }
        }

        private void OnDisable()
        {
            if (_wave != null)
            {
                _wave.OnWaveCleared.RemoveListener(HandleWaveCleared);
                _wave.OnWaveStarted.RemoveListener(HandleWaveStarted);
            }
        }

        /// <summary>Wave cleared → celebrate.</summary>
        private void HandleWaveCleared(int waveNumber)
        {
            Animator anim = ResolveHeroAnimator();
            if (anim != null) anim.SetTrigger(VictoryPoseHash);
        }

        /// <summary>
        /// A new wave started → cancel any pending celebration so the pose isn't
        /// left queued to fire after combat has resumed.
        /// </summary>
        private void HandleWaveStarted(int waveNumber)
        {
            Animator anim = ResolveHeroAnimator();
            if (anim != null) anim.ResetTrigger(VictoryPoseHash);
        }

        /// <summary>
        /// Finds the hero's Animator. Caches it once resolved. Uses
        /// FindObjectsByType (NOT the deprecated FindObjectOfType) with a length
        /// guard, then drills to the Animator on the swapped-in "HeroBody" child the
        /// same way HeroLocomotion / HeroAbilities do.
        /// </summary>
        private Animator ResolveHeroAnimator()
        {
            if (_heroAnimator != null) return _heroAnimator;

            var heroes = FindObjectsByType<HeroLocomotion>(FindObjectsSortMode.None);
            if (heroes == null || heroes.Length == 0) return null;

            Transform heroRoot = heroes[0].transform;
            var bodyT = heroRoot.Find("HeroBody");
            if (bodyT != null) _heroAnimator = bodyT.GetComponentInChildren<Animator>();
            if (_heroAnimator == null) _heroAnimator = heroRoot.GetComponentInChildren<Animator>();
            return _heroAnimator;
        }
    }
}
