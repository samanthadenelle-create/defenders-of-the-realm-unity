// =============================================================================
// VfxPoolShapeRegression [vfx-pool-shape] -- the oracle for WO-955: a VFX free
// list may never hand back a DESTROYED host, and may never accept one.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// WHY THIS EXISTS (proven from captured data, owner session 2026-08-10):
//
//   NullReferenceException
//   UnityEngine.GameObject.get_transform ()
//   DeNelle.Village.VFXManager.Acquire (VFXType, VFXCatalog+Entry&) VFXManager.cs:876
//   DeNelle.Village.VFXManager.PlayLoop :537 -> PlayAura :359
//   DeNelle.Village.HeroHpStateAura.Apply HeroHpStateAura.cs:285
//
// and 3 minutes later, in a scene the first caller never touched:
//   EnemyAuraVFX.StartHeld :219 -> PlayAura :359 -> PlayLoop :537 -> Acquire :876
//   (dg_ember_deep)
//
// The pool hangs off the DontDestroyOnLoad singleton, so a poisoned free list
// outlives the scene that poisoned it and every Acquire caller is exposed. That
// is why the guarded seam is VfxPoolGuard and why this oracle tests IT rather
// than one caller.
//
// WHAT IT ASSERTS (real Unity destruction, not a stand-in for it):
//   1. DRAIN PAST CORPSES -- a queue of [destroyed, destroyed, live] hands back
//      the live host, reports evicted==2, and does not throw. This is the exact
//      captured shape: the NRE was `reused.transform` on a destroyed host.
//   2. FULLY POISONED POOL -- a queue of nothing but corpses returns null (the
//      caller's signal to instantiate fresh) with evicted==count. It must not
//      throw and must not hand back a corpse; capacity self-heals.
//   3. THE COUNTER IS THE SIGNAL -- a healthy queue reports evicted==0. Without
//      this case the suite could not tell a working drain from one that reports
//      breakage constantly (INSTRUMENTATION_STANDARD 1.4b: a field that cannot
//      report failure -- or that reports it always -- is a bug, not a nicety).
//   4. EMPTY / NULL queue -- null out, evicted 0, no throw.
//   5. THE WRITE SIDE -- IsPoolSafe accepts a host parented under the pool root
//      and REFUSES one parented under a scene object, a root-level one, a
//      destroyed one, and any host when the pool root itself is null. This is the
//      half that stops corpses being created: an entry not under the DDOL pool
//      root dies with its scene.
//   6. WIRING LINT -- both free lists (VFXManager.Acquire and
//      VFXManager.Hovl.AcquireHovl) actually route through DrainToLiveHost, both
//      return paths gate their enqueue on IsPoolSafe, and the drain still carries
//      a FlowTrace.Warn. A guard nothing calls is a guard that is not there, and
//      the Hovl drain's original silent `// Drop destroyed entries` is exactly how
//      this class of corruption stayed invisible until it surfaced as an NRE.
//
// NOTE ON THE WARN: this suite asserts the eviction COUNT, and lints that the
// Warn call-site exists in the drain. It does not capture the emitted log line --
// FlowTrace has no listener hook to subscribe to. Stated plainly rather than
// claimed.
//
// POSITIVE CONTROL (prove it can go red): in VfxPoolGuard.DrainToLiveHost, change
// `if (candidate == null)` to `if (false)` -- case 1 must fail with a throw or a
// returned corpse. Or make IsPoolSafe `return true;` -- case 5 must fail. Revert.
//
// Edit-mode object graph only: plain GameObjects + DestroyImmediate. No scene, no
// play mode, no VFXManager singleton (its Awake takes DontDestroyOnLoad and loads
// catalogs -- the seam under test is deliberately static and free of both).
//
// Registered in DataRegression.RunAll as [vfx-pool-shape].
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class VfxPoolShapeRegression
    {
        private const string ManagerPath = "Assets/_Modules/Village/Vfx/VFXManager.cs";
        private const string HovlPath    = "Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs";
        private const string GuardPath   = "Assets/_Modules/Village/Vfx/VfxPoolGuard.cs";

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var created  = new List<GameObject>();
            try
            {
                Case(failures, "drain-past-corpses",  () => Case1_DrainPastCorpses(failures, created));
                Case(failures, "fully-poisoned",      () => Case2_FullyPoisoned(failures, created));
                Case(failures, "healthy-is-silent",   () => Case3_HealthyReportsZero(failures, created));
                Case(failures, "empty-and-null",      () => Case4_EmptyAndNull(failures));
                Case(failures, "write-side",          () => Case5_WriteSide(failures, created));
                Case(failures, "wiring",              () => Case6_WiringLint(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                foreach (var go in created)
                {
                    if (go == null) continue;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                    else                       UnityEngine.Object.DestroyImmediate(go);
                }
            }

            if (failures.Count == 0)
            {
                reason = "VFX POOL SHAPE OK - a free list holding destroyed hosts is drained past them " +
                         "(the captured WO-955 NRE cannot recur), a fully poisoned pool returns null so " +
                         "capacity self-heals, the eviction count is zero on the healthy path, and the " +
                         "write side refuses to enqueue any host that is not parked under the DDOL pool " +
                         "root; both pools and both return paths are wired to the guard.";
                return true;
            }

            reason = failures.Count + " failure(s): " + string.Join(" | ", failures);
            return false;
        }

        /// <summary>Run one case, converting a throw into a failure rather than killing the suite.</summary>
        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add(name + ": THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static GameObject Make(string name, List<GameObject> created)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        // Destroy for real, so the queue entry is a genuine Unity-null (a managed reference
        // whose native object is gone) rather than a plain null the guard might handle by
        // accident. This is the state the captured stack dereferenced.
        private static void Kill(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else                       UnityEngine.Object.DestroyImmediate(go);
        }

        // =====================================================================
        //  Cases
        // =====================================================================

        private static void Case1_DrainPastCorpses(List<string> failures, List<GameObject> created)
        {
            var dead1 = Make("vfx_pool_dead_1", created);
            var dead2 = Make("vfx_pool_dead_2", created);
            var live  = Make("vfx_pool_live",   created);

            var q = new Queue<GameObject>();
            q.Enqueue(dead1);
            q.Enqueue(dead2);
            q.Enqueue(live);

            Kill(dead1);
            Kill(dead2);

            var got = VfxPoolGuard.DrainToLiveHost(q, "case1", out int evicted);

            if (got == null)
                failures.Add("drain-past-corpses: returned null although a LIVE host sat behind the two " +
                             "destroyed slots - the pool would rebuild instead of reusing.");
            else if (got != live)
                failures.Add("drain-past-corpses: returned '" + got.name + "' instead of the live host.");

            if (evicted != 2)
                failures.Add("drain-past-corpses: evicted==" + evicted + ", expected 2. The count is the " +
                             "only machine-readable signal that the free list was poisoned.");

            // And the dereference the captured stack died on must now be safe.
            if (got != null)
            {
                var _ = got.transform;   // would NRE on a corpse (the WO-955 crash site)
            }
        }

        private static void Case2_FullyPoisoned(List<string> failures, List<GameObject> created)
        {
            var a = Make("vfx_pool_all_dead_a", created);
            var b = Make("vfx_pool_all_dead_b", created);

            var q = new Queue<GameObject>();
            q.Enqueue(a);
            q.Enqueue(b);
            Kill(a);
            Kill(b);

            var got = VfxPoolGuard.DrainToLiveHost(q, "case2", out int evicted);

            if (got != null)
                failures.Add("fully-poisoned: handed back a host from a pool containing nothing but " +
                             "destroyed slots - that object is a corpse and the caller will dereference it.");
            if (evicted != 2)
                failures.Add("fully-poisoned: evicted==" + evicted + ", expected 2.");
            if (q.Count != 0)
                failures.Add("fully-poisoned: " + q.Count + " dead slot(s) left in the queue - a corpse " +
                             "that survives one drain simply crashes the next Acquire instead.");
        }

        private static void Case3_HealthyReportsZero(List<string> failures, List<GameObject> created)
        {
            var live  = Make("vfx_pool_healthy_1", created);
            var live2 = Make("vfx_pool_healthy_2", created);

            var q = new Queue<GameObject>();
            q.Enqueue(live);
            q.Enqueue(live2);

            var got = VfxPoolGuard.DrainToLiveHost(q, "case3", out int evicted);

            if (got != live)
                failures.Add("healthy-is-silent: a healthy pool must hand back its FIRST slot in order.");
            if (evicted != 0)
                failures.Add("healthy-is-silent: evicted==" + evicted + " on a pool with no dead slots. " +
                             "A signal that fires on the healthy path cannot report failure.");
            if (q.Count != 1)
                failures.Add("healthy-is-silent: the drain consumed " + (2 - q.Count) + " slot(s); a reuse " +
                             "must take exactly one.");
        }

        private static void Case4_EmptyAndNull(List<string> failures)
        {
            var empty = new Queue<GameObject>();
            if (VfxPoolGuard.DrainToLiveHost(empty, "case4-empty", out int e1) != null)
                failures.Add("empty-and-null: an empty pool must return null so the caller instantiates fresh.");
            if (e1 != 0)
                failures.Add("empty-and-null: an empty pool evicted " + e1 + " slot(s).");

            if (VfxPoolGuard.DrainToLiveHost(null, "case4-null", out int e2) != null)
                failures.Add("empty-and-null: a null queue must answer null, not a host.");
            if (e2 != 0)
                failures.Add("empty-and-null: a null queue evicted " + e2 + " slot(s).");
        }

        private static void Case5_WriteSide(List<string> failures, List<GameObject> created)
        {
            var poolRoot = Make("vfx_pool_root", created).transform;
            var scenery  = Make("vfx_scene_owner", created).transform;

            var pooled = Make("vfx_host_pooled", created);
            pooled.transform.SetParent(poolRoot, false);

            var stranded = Make("vfx_host_stranded", created);
            stranded.transform.SetParent(scenery, false);

            var rootLevel = Make("vfx_host_rootlevel", created);

            if (!VfxPoolGuard.IsPoolSafe(pooled, poolRoot))
                failures.Add("write-side: a host parked UNDER the pool root was refused - every normal " +
                             "return would stop pooling and the pools would churn.");

            if (VfxPoolGuard.IsPoolSafe(stranded, poolRoot))
                failures.Add("write-side: a host still parented under a SCENE object was accepted. That " +
                             "entry is not covered by the DontDestroyOnLoad pool root: the scene unload " +
                             "destroys it and the free list keeps the corpse (the WO-955 defect exactly).");

            if (VfxPoolGuard.IsPoolSafe(rootLevel, poolRoot))
                failures.Add("write-side: a host at the SCENE ROOT (no parent) was accepted - it dies with " +
                             "the scene just as surely as a parented one.");

            if (VfxPoolGuard.IsPoolSafe(pooled, null))
                failures.Add("write-side: a null pool root was accepted. With no root there is nothing " +
                             "keeping a dormant host alive across a load, which is the exact condition " +
                             "the guard exists to refuse.");

            Kill(rootLevel);
            if (VfxPoolGuard.IsPoolSafe(rootLevel, poolRoot))
                failures.Add("write-side: a DESTROYED host was judged safe to enqueue.");
        }

        private static void Case6_WiringLint(List<string> failures)
        {
            string guard = ReadOrFail(GuardPath, failures);
            string mgr   = ReadOrFail(ManagerPath, failures);
            string hovl  = ReadOrFail(HovlPath, failures);
            if (guard == null || mgr == null || hovl == null) return;

            // The drain must still be able to SPEAK. Section 12: instrumentation is permanent;
            // a drain that evicts corpses in silence is how this bug reached the owner as an NRE.
            if (!guard.Contains("FlowTrace.Warn"))
                failures.Add("wiring: VfxPoolGuard no longer emits a FlowTrace.Warn. Evicting a destroyed " +
                             "pooled host in silence discards the only evidence of the bad return that " +
                             "created it (CLAUDE.md 12 - never strip FlowTrace).");

            // Both free lists must READ through the guard.
            if (!mgr.Contains("VfxPoolGuard.DrainToLiveHost"))
                failures.Add("wiring: VFXManager.Acquire does not route through VfxPoolGuard.DrainToLiveHost " +
                             "- this is the exact call site of the captured NRE (VFXManager.cs:876).");
            if (!hovl.Contains("VfxPoolGuard.DrainToLiveHost"))
                failures.Add("wiring: AcquireHovl does not route through VfxPoolGuard.DrainToLiveHost. The " +
                             "string-keyed pool is the same seam with the same failure mode.");

            // Both return paths must WRITE through the guard.
            if (!mgr.Contains("VfxPoolGuard.IsPoolSafe"))
                failures.Add("wiring: VFXManager.CompleteReturn does not gate its enqueue on " +
                             "VfxPoolGuard.IsPoolSafe - an unprotected host can re-enter the free list.");
            if (!hovl.Contains("VfxPoolGuard.IsPoolSafe"))
                failures.Add("wiring: ReturnHovlToPool does not gate its enqueue on VfxPoolGuard.IsPoolSafe. " +
                             "This path is the one that USED to enqueue a host parented under a tower and " +
                             "call the resulting corpse tolerable.");
        }

        private static string ReadOrFail(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("wiring: missing source file " + path + " - the wiring lint cannot judge a " +
                             "file that is not there, so it reports rather than passing by default.");
                return null;
            }
            return File.ReadAllText(path);
        }
    }
}
