// =============================================================================
// PetClipPlayer — WO-184 runtime fallback so a controllerless pet still animates.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Pets   Namespace: DeNelle.Pets
//
// THE PROBLEM (WO-184): the deployed pet is the ice-wolf — a Tripo/CC5 "Coyote"
// export imported as a GENERIC rig (animationType 2). It is NOT a KayKit
// Rig_Medium humanoid, so the shared Pet.controller's Rig_Medium clips can't
// bind to it; and no per-species ice-wolf.controller asset ships on this branch.
// With no valid AnimatorController, the Animator has nothing to play and the rig
// freezes in its bind/T-pose — exactly the bug the playtest hit.
//
// THE FIX: the ice-wolf FBX DOES carry its own embedded animation take(s)
// (verified: 3 AnimStacks / 186 AnimCurveNodes inside the file). This component
// plays one of those embedded clips DIRECTLY on the Animator via a PlayableGraph
// (UnityEngine.Playables / .Animations) — which needs NO AnimatorController
// asset and is fully build-safe (UnityEditor types are never touched). It is the
// last-resort fallback: PetDeployer.WirePetAnimator prefers a real .controller
// when one is authored, and only attaches this when none is found.
//
// It loops the clip and scales playback speed with the pet's movement so a
// stationary pet idles slowly and a moving pet animates at full rate — a cheap
// stand-in for a proper idle<->walk blend until a hand-authored quadruped
// controller ships (the GAP-PRIMARY flagged in AnimatorSetup.cs).
// =============================================================================

using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DeNelle.Pets
{
    /// <summary>
    /// Plays a single embedded <see cref="AnimationClip"/> on a pet's
    /// <see cref="Animator"/> via a <see cref="PlayableGraph"/>, with no
    /// AnimatorController asset required. Used as the WO-184 fallback for the
    /// ice-wolf (Generic rig, own embedded take, no shipped controller).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetClipPlayer : MonoBehaviour
    {
        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationClipPlayable _clipPlayable;
        private bool _ready;

        // Playback speed eases between a slow "idle" rate and full "move" rate
        // off the pet's per-frame displacement, so the single clip reads as
        // calmer at rest and livelier while hunting.
        private const float IdleRate = 0.45f;   // playback speed when stationary
        private const float MoveRate = 1.0f;    // playback speed when moving
        private const float MoveSpeedForFull = 2.0f; // world u/s that maps to MoveRate
        private const float RateLerp = 6f;      // how fast the rate eases

        private Vector3 _lastPosition;
        private float _currentRate = IdleRate;

        /// <summary>
        /// Initialises the player on <paramref name="animator"/> with
        /// <paramref name="clip"/>. Safe to call once after the pet mesh spawns;
        /// no-ops if either argument is null.
        /// </summary>
        public void Initialize(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null) return;
            _animator = animator;
            // The graph wraps the clip and routes it to the Animator output.
            _graph = PlayableGraph.Create("PetClipPlayer_" + animator.name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            // Loop the clip ourselves regardless of the clip's import wrap mode —
            // Tripo takes often import as ClampForever, which would freeze on the
            // last frame (a subtler T-pose). ApplyFootIK off: Generic rig has none.
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetDuration(double.MaxValue);

            var output = AnimationPlayableOutput.Create(_graph, "PetAnim", animator);
            output.SetSourcePlayable(_clipPlayable);

            // Keep animating off-screen — the follow camera frames just past pets,
            // and a culled Animator re-freezes the rig (re-T-pose on re-entry).
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _lastPosition = transform.position;
            _graph.Play();
            _ready = true;
        }

        private void Update()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Map per-frame displacement -> a target playback rate (idle..move).
            float moved = (transform.position - _lastPosition).magnitude / dt;
            _lastPosition = transform.position;
            float targetRate = Mathf.Lerp(
                IdleRate, MoveRate, Mathf.Clamp01(moved / MoveSpeedForFull));
            _currentRate = Mathf.Lerp(_currentRate, targetRate, RateLerp * dt);

            if (_clipPlayable.IsValid())
                _clipPlayable.SetSpeed(_currentRate);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
