// =============================================================================
// VfxPoolGuard - the ONE seam that decides what may sit in a VFX free list, and
// what must never be dereferenced when it comes back out.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ## WHY THIS EXISTS (WO-955, proven from two captured exceptions)
//
// Owner session 2026-08-10, during repeated arena deaths:
//
//   NullReferenceException
//   UnityEngine.GameObject.get_transform ()
//   DeNelle.Village.VFXManager.Acquire (VFXType, VFXCatalog+Entry&) VFXManager.cs:876
//   DeNelle.Village.VFXManager.PlayLoop (...) :537
//   DeNelle.Village.VFXManager.PlayAura (...) :359
//   DeNelle.Village.HeroHpStateAura.Apply (...) HeroHpStateAura.cs:285
//
// and 3 minutes later, a DIFFERENT caller in a DIFFERENT scene (dg_ember_deep):
//
//   EnemyAuraVFX.StartHeld :219 -> PlayAura :359 -> PlayLoop :537 -> Acquire :876
//
// Two facts follow from the pair: the poisoned free list PERSISTS ACROSS SCENE
// LOADS (the pool is session-long, hung off the DontDestroyOnLoad singleton), and
// EVERY Acquire caller is exposed. So the fix belongs here, at the pool seam, and
// never in a caller.
//
// ## THE TWO HALVES, AND WHY BOTH ARE NEEDED
//
// A pooled host is safe from a scene unload for exactly one reason: while dormant
// it is parented under _poolRoot, which is a child of the DontDestroyOnLoad
// [VFXManager]. Everything else in this system depends on that one invariant.
//
//   1. THE READ SIDE - DrainToLiveHost. Even with the write side perfect, a free
//      list read must never dereference a corpse: `reused.transform` on a
//      destroyed GameObject is the captured NRE above. Drain past dead slots,
//      say so once, and let the caller instantiate fresh. Capacity self-heals.
//
//   2. THE WRITE SIDE - IsPoolSafe. This is where the corpses come from. Both
//      return paths reparent to _poolRoot and then enqueue UNCONDITIONALLY, and
//      Transform.SetParent is a call that Unity REFUSES AND LOGS RATHER THAN
//      THROWS when the current parent is mid-(de)activation - that non-throwing
//      refusal is proven from data in ReturnHovlToPool's header (owner F8
//      2026-07-17: a Guard.Try wrapper caught nothing because nothing was thrown,
//      and the LogError kept firing for 55 minutes across 22 captures). A refused
//      reparent leaves the host under a SCENE object while it is nonetheless
//      sitting in the free list; the scene unloads; the list now holds a corpse.
//      The Hovl path does not even attempt the reparent in that window - it
//      deactivates in place and enqueues anyway, with a comment that the object
//      may be "destroyed with its tower" and that Acquire "drops null entries".
//      That is the invariant break, written down.
//
// A queue entry that is not under the pool root is a landmine with a timer. This
// class is the checkpoint both queues pass through so neither can plant one.
//
// ## WHAT THIS CLASS DELIBERATELY DOES NOT DO
//
// It does not reparent, does not deactivate, does not enqueue and does not own a
// pool. The (de)activation-window rules for WHEN a SetParent may legally be
// issued are hard-won and live with the pools that learned them (WO-929 in
// VFXManager.CompleteReturn, the 2026-07-17 fix in ReturnHovlToPool). Moving them
// here would relitigate two captured bugs for tidiness. This class answers two
// questions and emits the trace for one of them; the pools keep the behaviour.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Free-list safety for the VFX pools: drain past destroyed hosts on the way
    /// out, and judge whether a host is safe to put back in. Pure decisions plus
    /// one FlowTrace on the failure path - it owns no pool and starts no effect.
    /// </summary>
    public static class VfxPoolGuard
    {
        /// <summary>
        /// Dequeue until a LIVE host is found, evicting every Unity-destroyed slot on
        /// the way past. Returns null when the queue is exhausted (or is null/empty),
        /// which is the caller's signal to instantiate fresh - a drained pool must
        /// self-heal its capacity, never shrink silently and never throw out of a Play
        /// call.
        /// <para>
        /// <paramref name="evicted"/> is the whole point: it is non-zero ONLY when the
        /// invariant was broken, so a caller (and the regression) can tell a healthy
        /// reuse from a poisoned free list without parsing a log line. One Warn is
        /// emitted per drain that evicted anything - naming the pool, the count and
        /// what is left - and NOTHING is logged on the healthy path.
        /// </para>
        /// </summary>
        /// <param name="free">The pool's free list. May be null or empty.</param>
        /// <param name="poolLabel">Pool identity for the trace, e.g. the VFXType or Hovl key.</param>
        /// <param name="evicted">How many destroyed slots were dropped. 0 on the healthy path.</param>
        public static GameObject DrainToLiveHost(Queue<GameObject> free, string poolLabel, out int evicted)
        {
            evicted = 0;
            if (free == null) return null;

            GameObject live = null;
            while (free.Count > 0)
            {
                var candidate = free.Dequeue();
                // Unity's overloaded == null: true for a DESTROYED GameObject as well as a
                // real null reference. Both are dead slots and neither may be dereferenced.
                if (candidate == null) { evicted++; continue; }
                live = candidate;
                break;
            }

            if (evicted > 0)
            {
                FlowTrace.Warn("VFXManager",
                    $"pool '{poolLabel}': {evicted} pooled host(s) were DESTROYED while sitting in the free " +
                    $"list — dead slot(s) evicted (WO-955). Handing back " +
                    (live != null ? "the next live slot" : "nothing; a fresh instance will be built") +
                    $"; {free.Count} slot(s) remain. A dormant host can only die if it was enqueued while " +
                    "parented OUTSIDE the DontDestroyOnLoad pool root — find that return, it is the destroyer.");
            }

            return live;
        }

        /// <summary>
        /// True when <paramref name="go"/> is genuinely parked under <paramref name="poolRoot"/>
        /// and is therefore safe to enqueue. Silent by design: the two return paths call this
        /// on EVERY return, so a trace here would print on the healthy path and say nothing;
        /// each caller emits its own line describing the branch it actually took.
        /// <para>
        /// A null <paramref name="poolRoot"/> answers false - if the pool has no root there is
        /// nothing keeping a dormant host alive across a scene load, which is precisely the
        /// condition this guard exists to refuse.
        /// </para>
        /// </summary>
        public static bool IsPoolSafe(GameObject go, Transform poolRoot)
        {
            if (go == null || poolRoot == null) return false;
            return go.transform.parent == poolRoot;
        }

        /// <summary>
        /// Human-readable identity of whatever a host is parented to, for the trace on the
        /// refusal path. This is the destroyer hunt's payload: it names the object whose
        /// teardown is about to take a pooled host with it.
        /// </summary>
        public static string DescribeParent(GameObject go)
        {
            if (go == null) return "<destroyed>";
            var p = go.transform.parent;
            if (p == null) return "<scene root>";
            return p.name + " (scene='" + p.gameObject.scene.name + "')";
        }
    }
}
