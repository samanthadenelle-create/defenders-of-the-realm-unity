// =============================================================================
// ArcaneAura - a persistent magical aura loop for arcane-type towers.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner 2026-07-15: "arcane towers should have an aura." This tiny self-managing
// MonoBehaviour holds ONE looping Hovl aura at the structure it is attached to -
// the same reuse pattern HealingFountain uses for its Fountain_Heal_Aura (spawn a
// loop via VFXManager.PlayKey, keep the VFXHandle, Stop() it on teardown). No new
// art is authored: it reuses the "Arcane_Aura" catalog key (a looping magic circle,
// HovlVfxCatalogGenerator Map).
//
// COLORBLIND-SAFE (owner is red/green colorblind): the aura reads by MOTION +
// LUMINANCE (a slow rotating rune ring at the tower base), NOT by hue - the violet
// tint is only a hint. So it never encodes meaning in colour alone.
//
// ATTACH: added in code by the arcane-tower spawn paths -
//   - ArcaneTower.cs (the combat spire behaviour)
//   - StructureFactory GameplayBuilding case "arcane-tower" (catalog/BaseLayout landmark)
//   - HubStructureVisualInjector arcane swap (the baked hub landmark)
// Each call site guards with GetComponentInChildren<ArcaneAura>() so a tower never
// gets two auras.
//
// Null-safe throughout: VFXManager.PlayKey no-ops (returns null) when the manager or
// the "Arcane_Aura" catalog row is not ready yet, so this compiles/runs regardless -
// the aura simply appears once the catalog row is authored (regen the Hovl catalog:
// Defenders/VFX/Generate Hovl VFX Catalog -> HOVL_VFX_CATALOG_OK).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Holds one looping arcane aura VFX at the tower it is attached to.
    /// Spawns on enable, stops on disable/destroy (loop-handle lifecycle).</summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneAura : MonoBehaviour
    {
        [Tooltip("Hovl catalog loop key for the aura. Reused looping magic circle; " +
                 "PlayKey no-ops if the row is not authored yet.")]
        [SerializeField] private string _auraKey = "Arcane_Aura";

        [Tooltip("Metres above the tower origin to seat the aura ring.")]
        [SerializeField] private float _height = 0.4f;

        [Tooltip("Uniform scale for the aura loop (0 = catalog DefaultScale).")]
        [SerializeField] private float _scale = 2.2f;

        // HDR arcane violet - a HINT only. The aura's meaning is carried by MOTION +
        // LUMINANCE (rotating ring), never by this hue (owner colorblind).
        [SerializeField] private Color _tint = new Color(0.6f, 0.4f, 1f, 1f);

        private VFXHandle _handle;
        private bool _started;

        private void Start()
        {
            _started = true;
            StartAura();
        }

        private void OnEnable()
        {
            // Re-acquire on re-enable (only after the first Start so we do not spawn
            // before the transform is seated). Idempotent via the handle guard.
            if (_started) StartAura();
        }

        private void OnDisable() => StopAura();
        private void OnDestroy() => StopAura();

        private void StartAura()
        {
            if (_handle != null) return;   // already holding the loop
            _handle = VFXManager.PlayKey(
                _auraKey,
                transform.position + Vector3.up * _height,
                Quaternion.identity,
                transform,     // parent so the aura tracks the tower
                _tint,         // HDR violet tint (a hint; motion carries the read)
                _scale);
            FlowTrace.Step("ArcaneTower",
                $"'{name}' arcane aura '{_auraKey}' " +
                (_handle != null ? "spawned (loop held)."
                                 : "no-op (VFXManager/catalog not ready or key unauthored) - aura will appear once the row exists."));
        }

        private void StopAura()
        {
            if (_handle == null) return;
            _handle.Stop();
            _handle = null;
        }

        /// <summary>Attach an <see cref="ArcaneAura"/> to <paramref name="root"/> once
        /// (idempotent - skips if one already lives in the hierarchy). The single seam the
        /// arcane-tower spawn paths call so the aura wiring stays in one place.</summary>
        public static void Ensure(GameObject root)
        {
            if (root == null) return;
            if (root.GetComponentInChildren<ArcaneAura>(true) != null) return;
            root.AddComponent<ArcaneAura>();
        }
    }
}
