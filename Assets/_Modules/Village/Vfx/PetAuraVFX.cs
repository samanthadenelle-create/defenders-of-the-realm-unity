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
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;   // §12 TGVRU: trace the pet-aura flow

namespace DeNelle.Village
{
    /// <summary>
    /// Manages the persistent VFX aura on a pet. Attach to the pet prefab root.
    /// Scales VFX intensity with pet level (1 = dim, 2 = medium, 3 = bright).
    /// </summary>
    // WO-889 - TWO THINGS CHANGED HERE, both about loop lifetime:
    //
    // 1. IProximityAura. Pet auras join the nearest-N ring with enemy auras, so a field
    //    full of pets can never monopolise the loop pool. Pets and enemies are the ONLY
    //    two classes that opt in; towers, the Heart and boss phases deliberately do not
    //    (see VfxAuraProximityCuller's header for why culling those would delete
    //    information rather than save budget).
    //
    // 2. THE MISSING STOP PATHS. This component previously stopped its loop on OnDestroy
    //    ALONE. A pet that was merely DISABLED - pooled, stabled, or carried through a
    //    scene load - kept its handle, and the pooled VFX instance stayed checked out
    //    holding one of the global loop slots with nothing on screen to show for it. That
    //    is the same leak shape as a fire-and-forget play, arriving by a different door.
    //    OnDisable and scene-unload now close it, matching HeroHpStateAura's rule that
    //    EVERY exit path stops the loop.
    [DisallowMultipleComponent]
    public sealed class PetAuraVFX : MonoBehaviour, IProximityAura
    {
        [Tooltip("Current pet level (1-3). Controls aura intensity. " +
                 "Call RefreshAura(level) when the level changes.")]
        [SerializeField, Range(1, 3)] private int _petLevel = 1;

        [Tooltip("Offset from the pet's pivot where the aura is spawned.")]
        [SerializeField] private Vector3 _auraOffset = Vector3.zero;

        private VFXHandle _handle;
        private int _activeLevel;
        private bool _allowed = true;     // the nearest-N grant; permissive until revoked
        private bool _registered;
        private bool _started;            // Start has run at least once

        // ── IProximityAura (WO-889 nearest-N ring) ────────────────────────────

        Transform IProximityAura.AuraTransform => this == null ? null : transform;

        /// <summary>A live, enabled pet always wants its aura - level is the only variable.</summary>
        bool IProximityAura.WantsAura => isActiveAndEnabled;

        void IProximityAura.SetAuraAllowed(bool allowed)
        {
            if (_allowed == allowed) return;
            _allowed = allowed;

            if (!allowed)
            {
                // Graceful: a budget revoke is not a despawn, so let the tail die out.
                StopHeld(immediate: false, "nearest-N ring revoked the slot");
            }
            else if (_started && _handle == null)
            {
                SpawnAura(_petLevel);   // back inside the ring - re-acquire
            }
        }

        private void Start()
        {
            _started = true;
            Register();
            SpawnAura(_petLevel);
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _allowed = true;
            if (_started)
            {
                Register();
                if (_handle == null) SpawnAura(_petLevel);
            }
        }

        // WO-889: a pet that is DISABLED rather than destroyed (pooled / stabled / carried
        // across a scene load) used to keep its handle and hold a loop slot forever with
        // nothing visible. Immediate, because a disabled body may be reused elsewhere.
        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Unregister();
            StopHeld(immediate: true, "OnDisable");
        }

        private void OnDestroy()
        {
            Unregister();
            StopHeld(immediate: true, "OnDestroy");
        }

        // A scene unload can tear down the VFXManager and its pool while this pet survives
        // (pets are carried between scenes), stranding the checked-out instance.
        private void OnSceneUnloaded(Scene _) => StopHeld(immediate: true, "sceneUnloaded");

        /// <summary>Stop and release the held loop. Idempotent; safe with nothing held.</summary>
        private void StopHeld(bool immediate, string reason)
        {
            if (_handle == null) return;
            _handle.Stop(immediate);
            _handle = null;
            FlowTrace.Throttle("PetAura", "released", 2f,
                $"'{name}': released pet aura loop (reason={reason}, immediate={immediate}) - loop slot returned.");
        }

        private void Register()
        {
            if (_registered) return;
            VfxAuraProximityCuller.Register(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered) return;
            VfxAuraProximityCuller.Unregister(this);
            _registered = false;
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

            StopHeld(immediate: false, "level change -> " + newLevel);
            SpawnAura(newLevel);
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private void SpawnAura(int level)
        {
            using var _ = FlowTrace.Enter("PetAura", $"SpawnAura level={level} on '{name}'");
            _activeLevel = level;
            _petLevel    = level;

            // WO-889: outside the nearest-N ring this pet holds no loop. Recorded as the
            // level it WOULD show (above) so a later re-grant starts the right one; the
            // culler re-invokes this the moment the pet is back inside the ring.
            if (!_allowed)
            {
                FlowTrace.Throttle("PetAura", "ring-deferred", 2f,
                    $"SpawnAura '{name}': level {level} aura DEFERRED - the pet is outside the " +
                    $"nearest-N aura ring ({VfxLoopBudget.NearestAuraRing}). Not a missing effect; " +
                    "it returns automatically as the pet closes on the view.");
                return;
            }

            // U §12: a null VFXManager means the pet aura SILENTLY never appears. Once-report so a
            // scene with no VFXManager self-detects instead of the pet just looking auraless.
            if (VFXManager.Instance == null)
            {
                FlowTrace.Once("PetAura", $"nomanager:{name}",
                    $"SpawnAura '{name}': VFXManager.Instance is null — pet aura (level {level}) will not appear.");
                return;
            }

            // Create a pivot child at the desired offset so the aura position
            // is adjustable without moving the pet itself.
            var pivot = new GameObject("[AuraPivot]");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = _auraOffset;

            _handle = VFXManager.Instance.PlayPetAura(this, level);

            // R §12: a null handle = PlayPetAura fell through (loop-cap hit, or the catalog prefab
            // for this aura level is missing AND the procedural loop failed) — the pet is SILENTLY
            // auraless. Trace it AND tear down the now-orphaned pivot so we don't leak an empty
            // "[AuraPivot]" per failed spawn.
            if (_handle != null)
            {
                // Re-parent the aura to the pivot so it follows offset correctly.
                _handle.SetParent(pivot.transform, worldPositionStays: false);
                FlowTrace.Step("PetAura", $"SpawnAura '{name}': aura level {level} started + re-parented to pivot.");
            }
            else
            {
                FlowTrace.Warn("PetAura",
                    $"SpawnAura '{name}': PlayPetAura(level {level}) returned a NULL handle — " +
                    "aura did not start (loop-cap hit or missing catalog prefab + failed procedural fallback); pet is auraless.");
                Destroy(pivot);
            }
        }
    }
}
