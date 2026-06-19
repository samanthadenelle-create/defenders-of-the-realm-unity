// =============================================================================
// EnvironmentVFX — attach to scene props (torches, lanterns, portals) to get
// a persistent looping VFX from VFXManager. DEF-VFX-04.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// USAGE:
//   1. Select a torch/brazier/lantern GameObject in the scene.
//   2. Add Component → EnvironmentVFX.
//   3. Set VFX Type to Env_TorchFlame (or Env_LanternGlow, Env_DungeonPortal…).
//   4. Adjust LocalOffset to position the flame at the torch tip.
//   5. Play the scene — the flame loop starts automatically.
//
// The handle is stopped on OnDisable / OnDestroy so pools stay clean. Toggling
// the GameObject off/on correctly stops and restarts the effect.
//
// For DESTROYABLE OBJECTS:
//   Call TriggerDestruction() from the object's damage/death handler. This plays
//   a oneshot destruction burst (dust + sparks) and then stops the idle loop.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;   // §12 TGVRU: trace the env-VFX flow

namespace DeNelle.Village
{
    /// <summary>
    /// Persistent environment VFX loop tied to a scene prop. Handles automatic
    /// start/stop with the GameObject lifecycle and pool return on cleanup.
    /// </summary>
    public sealed class EnvironmentVFX : MonoBehaviour
    {
        [Tooltip("Which environment VFX loop to play on this prop.\n" +
                 "Env_TorchFlame = torch / brazier\n" +
                 "Env_LanternGlow = lantern warm glow\n" +
                 "Env_DungeonPortal = dungeon entrance portal\n" +
                 "Env_GroundFog = cold dungeon ground fog")]
        [SerializeField] private VFXType _vfxType = VFXType.Env_TorchFlame;

        [Tooltip("World-space offset from this transform's position where the VFX spawns.\n" +
                 "Use this to align flames to the torch tip without moving the mesh.")]
        [SerializeField] private Vector3 _localOffset = new Vector3(0f, 1.2f, 0f);

        [Tooltip("If true, the VFX is parented to this transform and moves with it.\n" +
                 "Set false for large area effects (ground fog, etc.) that should stay in place.")]
        [SerializeField] private bool _followTransform = true;

        [Header("Destruction VFX (optional — for breakable props)")]
        [Tooltip("Oneshot dust/smoke to play when the prop is destroyed (e.g. a barrel breaking).")]
        [SerializeField] private VFXType _destructionDustType = VFXType.Env_DestructionDust;
        [Tooltip("Oneshot sparks to play on destruction.")]
        [SerializeField] private VFXType _destructionSparksType = VFXType.Env_DestructionSparks;

        private VFXHandle _handle;

        // The detached anchor created for a NON-following effect (ground fog etc.) so the loop
        // has a stable Transform to ride. Tracked so StopLoop destroys it instead of orphaning a
        // stray "[EnvVFXAnchor]" GameObject in the scene on every stop/restart.
        private GameObject _detachedAnchor;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()  => StartLoop();
        private void OnDisable() => StopLoop();
        private void OnDestroy() => StopLoop();

        // ── Public ────────────────────────────────────────────────────────────

        /// <summary>
        /// Call from a damage/death handler when this prop is destroyed.
        /// Plays a oneshot destruction burst and stops the idle loop.
        /// </summary>
        public void TriggerDestruction()
        {
            using var _ = FlowTrace.Enter("EnvVFX", $"TriggerDestruction '{name}'");

            // U §12: VFXManager.Play is null-safe on Instance, but a null Instance means the
            // destruction burst SILENTLY no-ops (no dust/sparks on a broken prop). Self-report
            // that case so a quiet destruction surfaces in the break-log instead of being invisible.
            if (VFXManager.Instance == null)
                FlowTrace.Warn("EnvVFX",
                    $"TriggerDestruction '{name}': VFXManager.Instance is null — destruction burst " +
                    $"({_destructionDustType}/{_destructionSparksType}) will not appear (no VFXManager in scene).");

            Vector3 worldPos = transform.TransformPoint(_localOffset);
            FlowTrace.Step("EnvVFX",
                $"TriggerDestruction '{name}': playing {_destructionDustType} + {_destructionSparksType} at {worldPos}.");
            VFXManager.Play(_destructionDustType,   worldPos);
            VFXManager.Play(_destructionSparksType, worldPos);
            HitStopManager.DoShake(0.06f, 0.2f);   // subtle environmental shake
            StopLoop();
        }

        /// <summary>Change the VFX type at runtime and restart the loop.</summary>
        public void SetVFXType(VFXType type)
        {
            StopLoop();
            _vfxType = type;
            StartLoop();
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void StartLoop()
        {
            // U §12: a null VFXManager means this prop's idle loop SILENTLY never appears (dark
            // torch / no portal glow). Once-report so a scene missing the VFXManager self-detects
            // instead of looking like the prop just has no effect.
            if (VFXManager.Instance == null)
            {
                FlowTrace.Once("EnvVFX", $"nomanager:{name}",
                    $"StartLoop '{name}': VFXManager.Instance is null — env loop '{_vfxType}' will not appear.");
                return;
            }
            if (_handle != null && _handle.IsAlive) return;   // already running

            Vector3 worldPos = transform.TransformPoint(_localOffset);

            // For a NON-following effect we need a stable detached anchor. Build it ONCE and track
            // it so StopLoop tears it down — the old code newed a fresh "[EnvVFXAnchor]" on every
            // start and never freed it, orphaning a stray GameObject per stop/restart cycle.
            Transform parent;
            if (_followTransform)
            {
                parent = transform;
            }
            else
            {
                if (_detachedAnchor == null)
                {
                    _detachedAnchor = new GameObject($"[EnvVFXAnchor:{name}]");
                    _detachedAnchor.transform.position = worldPos;
                }
                parent = _detachedAnchor.transform;
            }

            _handle = VFXManager.Instance.PlayEnvironment(_vfxType, parent);

            // R §12: a null handle here = the loop fell through (cap hit or procedural-loop build
            // failed inside VFXManager) and this prop is SILENTLY effect-less. Trace it so the
            // missing flame/glow self-reports rather than reading as "designer forgot the VFX".
            if (_handle != null)
            {
                _handle.SetPosition(worldPos);
                FlowTrace.Step("EnvVFX", $"StartLoop '{name}': env loop '{_vfxType}' started (follow={_followTransform}).");
            }
            else
            {
                FlowTrace.Warn("EnvVFX",
                    $"StartLoop '{name}': PlayEnvironment('{_vfxType}') returned a NULL handle — " +
                    "loop did not start (VFXManager loop-cap hit or procedural fallback failed); prop is effect-less.");
            }
        }

        private void StopLoop()
        {
            _handle?.Stop();
            _handle = null;
            // Tear down the detached anchor we created for a non-following effect so it never
            // orphans in the scene (and a later restart rebuilds a fresh one).
            if (_detachedAnchor != null)
            {
                Destroy(_detachedAnchor);
                _detachedAnchor = null;
            }
        }

        // ── Gizmo so designers can see the offset in the editor ───────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.7f);
            Vector3 worldPos = transform.TransformPoint(_localOffset);
            Gizmos.DrawWireSphere(worldPos, 0.12f);
            Gizmos.DrawLine(transform.position, worldPos);
        }
    }
}
