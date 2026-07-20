// =============================================================================
// Destructible - the ONE owner of a destructible entity's VFX lifecycle (WO-753).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner ruling 2026-07-19 (memory `destroyed-items-no-rebuild-full-cost-and-vfx-cleanup`):
//   1. When destroyed, an item's VFX are torn down WITH it - no orphans. (Root of the
//      live "two random VFX in the castle" / "i see a vfx but no tower" bug: leftover
//      effects from destroyed/removed structures whose VFX were never cleaned up.)
//   3. Architecture: ONE owner per concern (ARCHITECTURE_PRINCIPLES 2b) - a single
//      component owns the death lifecycle: on death it tears ALL of the entity's VFX
//      down (pool-return via the VFXManager/VfxPool handle Stop, plus stop+disable any
//      arcane aura, plus destroy standalone effect roots). Composed onto towers/buildings
//      so cleanup lives in ONE place instead of being scattered across reactive polls.
//
// WHY THIS EXISTS (the gap it closes): before WO-753 the arcane aura was kept from
// orphaning only by a throttled liveness POLL inside ArcaneAura.Update and a second
// poll inside StructureDamageVisuals.Evaluate. Those are reactive (a 0.5s / 0.3s window)
// and miss the genuine-removal paths that have NO steady poll on them - build-mode
// delete, StructureFactory reskin-replace, additive scene teardown. This component is
// the structural catch-all: its OnDestroy tears the VFX down on EVERY removal path, and
// NotifyBroken tears them down synchronously at the break moment, in one owner.
//
// COMPOSE: the destructible's own Awake calls Destructible.Ensure(gameObject) (Building,
// DefenseTower, ArcaneTower). The death path calls Destructible.For(go)?.NotifyBroken(...).
//
// NOT a damage model: it never reads/writes HP and never decides DESTROYED-vs-DAMAGED -
// the structure owns that (IsDestroyed / IsBroken). This owns only the VFX teardown.
// Null-safe + idempotent throughout; instrumented [Flow:Destroy] per CLAUDE.md section 12.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The single owner of a destructible entity's VFX teardown. Composed onto a
    /// tower/building; on death (<see cref="NotifyBroken"/>) or on any removal
    /// (<see cref="OnDestroy"/>) it returns every held loop handle to its pool, stops
    /// and disables any <see cref="ArcaneAura"/> in the hierarchy, and destroys any
    /// registered standalone effect root - so no VFX can outlive the structure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Destructible : MonoBehaviour
    {
        // Pooled loop/aura handles this entity owns (Stop -> pool-return on death).
        private readonly List<VFXHandle> _handles = new List<VFXHandle>();
        // Standalone (non-pooled) effect roots to Destroy on death.
        private readonly List<GameObject> _vfxRoots = new List<GameObject>();
        // OnDestroy latch - never re-run teardown after the object is already gone.
        private bool _removed;

        /// <summary>Idempotently attach a <see cref="Destructible"/> to <paramref name="root"/>
        /// (the single seam the destructible spawn/Awake paths call). Returns the component.</summary>
        public static Destructible Ensure(GameObject root)
        {
            if (root == null) return null;
            var d = root.GetComponent<Destructible>();
            if (d == null) d = root.AddComponent<Destructible>();
            return d;
        }

        /// <summary>The <see cref="Destructible"/> on <paramref name="root"/>, or null. Use at the
        /// death site: <c>Destructible.For(gameObject)?.NotifyBroken("...")</c>.</summary>
        public static Destructible For(GameObject root)
            => root != null ? root.GetComponent<Destructible>() : null;

        /// <summary>Register a pooled loop/aura handle so it is Stop()'d (pool-returned) on death.
        /// Null-safe. (ArcaneAura is auto-discovered - this is for any other held loop a caller owns.)</summary>
        public void RegisterHandle(VFXHandle handle)
        {
            if (handle == null) return;
            _handles.Add(handle);
        }

        /// <summary>Register a standalone (non-pooled) effect GameObject to Destroy on death. Null-safe.</summary>
        public void RegisterVfxRoot(GameObject go)
        {
            if (go == null) return;
            _vfxRoots.Add(go);
        }

        /// <summary>
        /// Tear down EVERY VFX this entity owns, in one place: pool-return each held loop handle,
        /// stop+disable any <see cref="ArcaneAura"/> under this root, and destroy each registered
        /// standalone effect root. Idempotent - safe to call at the break moment AND again from
        /// <see cref="OnDestroy"/>. Returns how many effects were torn down (for the trace).
        /// </summary>
        public int TeardownVfx(string reason)
        {
            int torn = 0;

            // 1. Pooled loop/aura handles owned directly by a caller -> immediate pool-return.
            for (int i = 0; i < _handles.Count; i++)
            {
                var h = _handles[i];
                if (h != null && h.IsAlive) { h.Stop(immediate: true); torn++; }
            }
            _handles.Clear();

            // 2. Any arcane aura in the hierarchy - stop the loop (pool-return) + disable the
            //    component so its OnEnable can't re-acquire over a dead shell. Symmetric with
            //    repair (StructureDamageVisuals re-enables it when the structure stands again).
            var auras = GetComponentsInChildren<ArcaneAura>(true);
            for (int i = 0; i < auras.Length; i++)
            {
                if (auras[i] == null) continue;
                auras[i].StopAndDisable();
                torn++;
            }

            // 3. Standalone (non-pooled) effect roots -> destroy so nothing lingers in the world.
            for (int i = 0; i < _vfxRoots.Count; i++)
            {
                var go = _vfxRoots[i];
                if (go != null) { Destroy(go); torn++; }
            }
            _vfxRoots.Clear();

            FlowTrace.Step("Destroy",
                $"[Flow:Destroy] Destructible.TeardownVfx '{name}' ({reason}): tore down {torn} VFX " +
                "(handles/auras/roots) - no orphaned effect left behind.");
            return torn;
        }

        /// <summary>
        /// Death hook - the entity broke/died. Tear its VFX down WITH it, synchronously, in ONE
        /// owner (ruling 1: no orphans, no reactive-poll window). The physical shell/removal is the
        /// caller's concern; this owns only the VFX. Null-safe via the <c>?.</c> call at the site.
        /// </summary>
        public void NotifyBroken(string reason)
        {
            using var _ = FlowTrace.Enter("Destroy", $"[Flow:Destroy] NotifyBroken '{name}' ({reason})");
            TeardownVfx(reason);
        }

        /// <summary>
        /// Catch-all: ANY path that removes this object (build-mode delete, StructureFactory
        /// reskin-replace, additive scene teardown) tears the VFX down here - a pooled loop is
        /// returned to its pool rather than destroyed-with-the-parent (pool shrink) or left
        /// orphaned, and no aura outlives the structure. This is the structural guarantee the
        /// reactive polls could not give.
        /// </summary>
        private void OnDestroy()
        {
            if (_removed) return;
            _removed = true;
            TeardownVfx("OnDestroy");
        }
    }
}
