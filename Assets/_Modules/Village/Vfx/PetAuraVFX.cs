// =============================================================================
// PetAuraVFX — auto-manages a pet's VFX aura via VFXManager. DEF-VFX-03.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Attach to any pet prefab root. On Start it calls VFXManager.PlayPetAura()
// which selects Aura_PetLevel1 / 2 / 3 based on petLevel. The handle is
// stored and Stop()'d on destroy so the pool is kept clean.
//
// Level up: call RefreshAura(newLevel) from the pet progression system.
//
// Usage:
//   // In inspector: set PetLevel (1-3). VFX starts automatically on spawn.
//   // From code: petAuraVFX.RefreshAura(2);
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Manages the persistent VFX aura on a pet. Attach to the pet prefab root.
    /// Scales VFX intensity with pet level (1 = dim, 2 = medium, 3 = bright).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetAuraVFX : MonoBehaviour
    {
        [Tooltip("Current pet level (1-3). Controls aura intensity. " +
                 "Call RefreshAura(level) when the level changes.")]
        [SerializeField, Range(1, 3)] private int _petLevel = 1;

        [Tooltip("Offset from the pet's pivot where the aura is spawned.")]
        [SerializeField] private Vector3 _auraOffset = Vector3.zero;

        private VFXHandle _handle;
        private int _activeLevel;

        private void Start()
        {
            SpawnAura(_petLevel);
        }

        private void OnDestroy()
        {
            _handle?.Stop(immediate: true);
        }

        // ── Public ────────────────────────────────────────────────────────────

        /// <summary>
        /// Change the pet's level and refresh its aura to match.
        /// Safe to call at any time; stops the old aura and starts the new one.
        /// </summary>
        public void RefreshAura(int newLevel)
        {
            newLevel = Mathf.Clamp(newLevel, 1, 3);
            if (newLevel == _activeLevel) return;

            _handle?.Stop(immediate: false);
            _handle = null;
            SpawnAura(newLevel);
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void SpawnAura(int level)
        {
            _activeLevel = level;
            _petLevel    = level;

            if (VFXManager.Instance == null) return;

            // Create a pivot child at the desired offset so the aura position
            // is adjustable without moving the pet itself.
            var pivot = new GameObject("[AuraPivot]");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = _auraOffset;

            _handle = VFXManager.Instance.PlayPetAura(this, level);

            // Re-parent the aura to the pivot so it follows offset correctly.
            _handle?.SetParent(pivot.transform, worldPositionStays: false);
        }
    }
}
