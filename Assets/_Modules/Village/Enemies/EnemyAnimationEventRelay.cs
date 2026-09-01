using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Receives reviewed FBX/clip Animation Events on the visual rig.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationEventRelay : MonoBehaviour
    {
        private Enemy _owner;

        public void Configure(Enemy owner) => _owner = owner;

        // Name is the animation-pipeline contract. Do not rename without migrating clip events.
        public void HitFrame()
        {
            if (_owner == null) _owner = GetComponentInParent<Enemy>();
            _owner?.OnAnimationHitFrame();
        }
    }
}
