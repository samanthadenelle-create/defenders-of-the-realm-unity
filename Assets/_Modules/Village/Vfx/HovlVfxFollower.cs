// =============================================================================
// HovlVfxFollower — keeps a pooled Hovl VFX on a moving target transform. WO-VFX-002.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Added on demand by VFXManager.PlayKey(..., follow:target) when the effect must
// track a moving transform WITHOUT being parented to it (e.g. a homing projectile
// trail toward an enemy, or a buff that trails a running unit). Parenting is the
// other option (PlayKey(..., parent:t)) and is preferred when the effect should
// inherit the parent's rotation/scale; FOLLOW just copies world position each frame.
//
// Reused across pool cycles: the component stays on the pooled GameObject; Begin()
// re-arms it and EndFollow() (called from ReturnHovlToPool) disarms it so a dormant
// pooled instance never chases a stale target.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Copies a target transform's world position onto this GameObject every frame
    /// while active. Null-safe: if the target is destroyed it disarms itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HovlVfxFollower : MonoBehaviour
    {
        private Transform _target;
        private bool      _active;
        private Vector3   _worldOffset;

        /// <summary>Begin following <paramref name="target"/>, preserving the current offset from it.</summary>
        public void Begin(Transform target, bool keepOffset = false)
        {
            _target = target;
            _active = target != null;
            _worldOffset = (keepOffset && target != null)
                ? transform.position - target.position
                : Vector3.zero;
        }

        /// <summary>Stop following (called when the effect returns to the pool).</summary>
        public void EndFollow()
        {
            _active = false;
            _target = null;
            _worldOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (!_active) return;
            if (_target == null) { _active = false; return; }
            transform.position = _target.position + _worldOffset;
        }
    }
}
