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
using DeNelle.Core.Combat;       // IDamageableStructure - owner-liveness orphan guard (Village -> Core, section 5)
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

        // ORPHAN GUARD (F8 owner felt-test 2026-07-15 "i see a vfx but no tower, maybe
        // destroyed?"): the aura is a POOLED Hovl loop parented to the tower. OnDisable/
        // OnDestroy cover the DESTROY + DISABLE death paths, but a tower that BREAKS to an
        // inoperable shell keeps its root ACTIVE (no lifecycle event fires), and a body that
        // failed to spawn / rendered invisible leaves the ring seated over nothing. There is
        // NO Unity lifecycle callback for either, so a throttled owner-liveness + visible-body
        // self-check is the catch-all that guarantees the aura can never outlive the tower.
        private const float OwnerCheckInterval = 0.5f;   // throttle for the self-check
        private const float FirstCheckGrace    = 1.5f;   // let the body model spawn / re-skin first
        private IDamageableStructure _owner;             // null for a pure landmark (no HP model)
        private float _checkTimer;
        private bool  _bodyConfirmed;                    // a visible body was seen at least once

        private void Start()
        {
            _started = true;
            _owner = GetComponentInParent<IDamageableStructure>();
            _checkTimer = FirstCheckGrace;
            StartAura();
        }

        private void OnEnable()
        {
            // Re-acquire on re-enable (only after the first Start so we do not spawn
            // before the transform is seated). Idempotent via the handle guard.
            if (_started) StartAura();
        }

        // Immediate stop on every lifecycle teardown: the pooled loop returns to the pool
        // NOW (no 2.5s graceful strand that could linger as a detached, still-playing loop).
        private void OnDisable() => StopAura(immediate: true);
        private void OnDestroy() => StopAura(immediate: true);

        private void Update()
        {
            // Only a live handle can be orphaned; skip the walk entirely otherwise.
            if (_handle == null || !_handle.IsAlive) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = OwnerCheckInterval;

            // The tower is a broken/dead shell (root still active, so no OnDisable/OnDestroy)?
            bool ownerDead = _owner != null && !_owner.IsAlive;
            // The body mesh never spawned / is disabled (ring seated over nothing)? Cache the
            // first positive so a healthy tower stops paying for the renderer walk.
            if (!_bodyConfirmed) _bodyConfirmed = HasVisibleBody();
            bool noBody = !_bodyConfirmed;

            if (ownerDead || noBody)
            {
                // Section 12 smoking gun: Fail lands in the errors-only break-log. This single
                // line disambiguates the cause on the next capture: ownerDead => broken-shell
                // not torn down; noVisibleBody => body failed to spawn / invisible; owner absent
                // => pure landmark whose body vanished.
                FlowTrace.Fail("ArcaneAura",
                    $"'{name}' ORPHAN aura STOPPED: aura loop was playing with no live tower body " +
                    $"(ownerDead={ownerDead}, ownerPresent={_owner != null}, noVisibleBody={noBody}) - " +
                    "matches F8 'i see a vfx but no tower'.");
                StopAura(immediate: true);
            }
        }

        /// <summary>True when a non-particle body renderer (the tower mesh) is live under this
        /// structure. ParticleSystemRenderers are excluded so the aura's OWN VFX (and any other
        /// effect) never counts as the body.</summary>
        private bool HasVisibleBody()
        {
            var rends = GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                if (r.enabled && r.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

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

        private void StopAura(bool immediate = false)
        {
            if (_handle == null) return;
            _handle.Stop(immediate);
            _handle = null;
        }

        /// <summary>
        /// External teardown (structure-death cleanup, owner felt-test 2026-07-15:
        /// "tower was destroyed ... but the vfx ... still exist"). Because the tower
        /// goes to a broken SHELL on death (no Destroy/disable of the root), this
        /// component's OnDisable/OnDestroy never fire and the aura loop would keep
        /// running. The break observer calls this to Stop the loop and disable the
        /// component so OnEnable cannot re-acquire it over a dead shell. Re-enable the
        /// component (on repair) to bring the aura back.
        /// </summary>
        public void StopAndDisable()
        {
            StopAura(immediate: true);
            enabled = false;
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
