// =============================================================================
// DrawbridgeController — animates a drawbridge mesh rotating around its hinge.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// SETUP — 4 gotchas resolved (per CLI code review):
//
//   1. HINGE PIVOT: Attach this script to an empty GO at the hinge edge.
//      Make the bridge mesh a child. Assign it to _bridgeMesh (Inspector).
//      Rotation applies to _bridgeMesh — swings from the edge, not the centre.
//
//   2. COLLIDER FOLLOWS ROTATION: Blocking collider lives on _bridgeMesh so it
//      rotates in lockstep with the visual. Open = disabled. Raised = enabled.
//      No "hero bonks an open bridge" or "walks through a raised one" bugs.
//
//   3. PLAYER TAG DEPENDENCY: OnTriggerEnter checks for tag "Player".
//      BLOCKED on WO-105 (VillageSceneBuilder restore + hero tag reland).
//      Test only after WO-105 is RESULT'd — bridge will silently no-op until then.
//
//   4. NO DOOR DUPLICATION: Distinct from DoorController (hinged doors) and
//      CastleDoorController (trigger-open walls). All three coexist in Buildings/.
//
// Resolves DEF-99. Replaces the spell-VFX hack stand-in.
// =============================================================================

using System.Collections;
using UnityEngine;

namespace DeNelle.Village
{
    public class DrawbridgeController : MonoBehaviour
    {
        [Header("Bridge Mesh")]
        [Tooltip("Child transform holding the bridge mesh + its blocker collider. Rotated on animation.")]
        public Transform bridgeMesh;

        [Header("Rotation")]
        public float raisedAngle  = 90f;
        public float loweredAngle = 0f;
        public float animDuration = 1.2f;

        [Header("VFX")]
        public ParticleSystem landDustVfx;

        private bool _isLowered;
        private bool _animating;
        private Collider _blockingCollider;

        private void Awake()
        {
            if (bridgeMesh == null) bridgeMesh = transform;
            _blockingCollider = bridgeMesh.GetComponent<Collider>();
            ApplyAngle(raisedAngle);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isLowered || _animating) return;
            // "Player" is a built-in tag; "HeroTarget" may be undefined (CompareTag
            // throws on an undefined tag) — guard the second check.
            if (!other.CompareTag("Player") && !HasTag(other, "HeroTarget")) return;
            StartCoroutine(LowerRoutine());
        }

        /// <summary>Undefined-tag-safe CompareTag (Unity throws on an undefined tag).</summary>
        private static bool HasTag(Component c, string tag)
        {
            if (c == null) return false;
            try { return c.CompareTag(tag); }
            catch (UnityEngine.UnityException) { return false; }
        }

        public void LowerImmediate()
        {
            if (_animating) return;
            StopAllCoroutines();
            StartCoroutine(LowerRoutine());
        }

        public void Raise()
        {
            if (!_isLowered || _animating) return;
            StartCoroutine(RaiseRoutine());
        }

        private IEnumerator LowerRoutine()
        {
            _animating = true;
            yield return AnimateTo(raisedAngle, loweredAngle);
            _isLowered = true;
            _animating = false;
            if (_blockingCollider != null) _blockingCollider.enabled = false;
            landDustVfx?.Play();
        }

        private IEnumerator RaiseRoutine()
        {
            _animating = true;
            if (_blockingCollider != null) _blockingCollider.enabled = true;
            yield return AnimateTo(loweredAngle, raisedAngle);
            _isLowered = false;
            _animating = false;
        }

        private IEnumerator AnimateTo(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animDuration));
                ApplyAngle(Mathf.Lerp(from, to, t));
                yield return null;
            }
            ApplyAngle(to);
        }

        private void ApplyAngle(float xDeg)
        {
            var e = bridgeMesh.localEulerAngles;
            bridgeMesh.localEulerAngles = new Vector3(xDeg, e.y, e.z);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.6f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, 5f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.right * 2f);
        }
    }
}
