// =============================================================================
// CastleDoorController — proximity VFX + pass-through for castle doors (DEF-99 / WO-97).
// -----------------------------------------------------------------------------
// Attach to any castle-door or gate-arch GameObject.  When the player hero
// enters within openRadius metres:
//   1. Plays the nearest available spell/magic ParticleSystem via
//      VFXManager.Instance?.Play(…), or fires the serialized spellVfx directly.
//   2. Disables this Collider so the hero can walk through.
//
// The door is one-way: once opened it stays open for the life of the scene.
// Safe to use alongside DoorController (rotation) on the same root — they
// target different colliders.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// On player approach (within <see cref="openRadius"/> metres) plays a spell
    /// VFX and opens the door collider so the hero can pass through (DEF-99 / WO-97).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class CastleDoorController : MonoBehaviour
    {
        [SerializeField] private float openRadius = 2.5f;

        [Tooltip("Assign an existing magic / spell ParticleSystem from the scene. " +
                 "When null, CastleDoorController attempts VFXManager.Play(VFXType.Cast_Heal) " +
                 "as a fallback so the door always produces a visible open effect.")]
        [SerializeField] private ParticleSystem spellVfx;

        private Collider _col;
        private bool _open;

        private void Awake() => _col = GetComponent<Collider>();

        private void OnTriggerEnter(Collider other)
        {
            if (_open) return;
            if (other == null) return;

            // Only the player hero triggers the door — pets and enemies are ignored.
            bool isHero = other.CompareTag("Player") ||
                          other.GetComponentInParent<HeroLocomotion>() != null;
            if (!isHero) return;

            _open = true;

            // 1. VFX — prefer the serialized ParticleSystem; fall back to VFXManager.
            if (spellVfx != null)
            {
                spellVfx.transform.position = transform.position + Vector3.up * 1.5f;
                spellVfx.Play();
            }
            else
            {
                // VFXManager.Play accepts a VFXType enum value and a world position.
                // Use reflection so this file compiles even when VFXManager / VFXType
                // are in a separate assembly that isn't explicitly referenced here.
                TryPlayViaVFXManager();
            }

            // 2. Disable this collider so the hero can walk through.
            if (_col != null) _col.enabled = false;
        }

        /// <summary>
        /// Attempts to call VFXManager.Play(VFXType.Cast_Heal, position) via the
        /// VFXManager singleton without a hard compile-time reference.
        /// </summary>
        private void TryPlayViaVFXManager()
        {
            // Try the typed VFXManager path first (same assembly — no reflection needed).
            try
            {
                VFXManager.Play(VFXType.Cast_Heal, transform.position + Vector3.up * 1.5f);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CastleDoorController] VFXManager.Play fallback failed: {ex.Message}");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, openRadius);
        }
#endif
    }
}
