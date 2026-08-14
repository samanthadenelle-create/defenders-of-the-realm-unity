// =============================================================================
// PortalStructure — the ONE portal look, shared by every portal in the game.
// -----------------------------------------------------------------------------
// OWNER DIRECTIVE (2026-08-14, from a Seeker felt-test screenshot of the dungeon
// exit): "this portal and all portals like this should have a vfx that help tell
// its a portal smoke or vortex or anything" + "i want high end much better vfx
// prefabs that actually look good" + "all of the portals should be this".
//
// WHAT WAS WRONG — and it is not that any one portal was broken. It is that the
// word "portal" meant TWO UNRELATED THINGS in this tree:
//   * The DUNGEON EXIT wore the owner's Tripo art (Assets/Art/Dungeon/Exit/Portal.fbx,
//     Addressables key "dungeon/exit/portal") and carried NO threshold effect at all —
//     a handsome stone ruin with a hole in it. Nothing said "this leads somewhere".
//   * The OVERWORLD dungeon portals were ~18 code-built CUBES plus procedural additive
//     quads (DungeonWorldPortalSpawner.BuildArch + PortalVFXController), i.e. a
//     completely different silhouette AND a completely different (procedural) effect.
// Two looks, two effect systems, one word. This class is the single structure both
// now load, so a portal is recognisable as a portal wherever the player meets one.
//
// WHY A SHARED HELPER RATHER THAN A SECOND COPY: the swap is not trivial — it is an
// async Addressables load, a collider strip, a hero-derived height normalize AND a
// bounds re-seat (see NormalizeToHeight for why the re-seat is load-bearing on a Tripo
// pivot). A hand-copied second implementation in the Village assembly is precisely the
// duplicated-state drift CLAUDE.md §5/§2 keeps having to un-rot. Core is the only
// assembly both DeNelle.Village and DeNelle.Dungeons reference, and it already carries
// Unity.Addressables + UniTask, so the shared seat costs no new dependency.
//
// PRESENTATION-ONLY (ARCHITECTURE_PRINCIPLES: presentation never touches the objects):
// this loads art and seats it. It owns no gameplay, no trigger, no routing — the
// caller's own collider stays the only collider, which is why every collider on the
// loaded instance is destroyed here. An exit the hero can be blocked out of is a
// softlock, and that is a strictly worse bug than a plain-looking arch.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.World
{
    /// <summary>
    /// Loads and seats the owner's portal art — the single structure worn by EVERY
    /// portal (dungeon exits, overworld dungeon gates). Async, failure-tolerant, and
    /// purely presentational: callers keep their own trigger/routing untouched.
    /// </summary>
    public static class PortalStructure
    {
        private const string Sys = "Portal";

        /// <summary>Addressable key for the owner's Portal art. CONTENT-ADDRESSED, not a
        /// path, so moving the FBX cannot break it (registered by DungeonPortalAddressable).
        /// Declared here rather than per-caller so the two portal families cannot drift onto
        /// two different assets — which is the exact defect this class exists to end.</summary>
        public const string Address = "dungeon/exit/portal";

        /// <summary>Catalog key for the threshold VFX (the vortex/smoke that says "portal").
        /// Owner-tagged in Assets/Editor/VfxManualPicks.json; resolved through the shared
        /// VFXManager pool. Named here so the exit and the overworld gate ask for the SAME
        /// effect — the routing on the overworld side has been wired and waiting since
        /// WO-869, held only because nothing was tagged to this key.</summary>
        public const string AuraKey = "Portal_Threshold_Aura";

        /// <summary>Hero height, matching the NavMeshAgent the hero actually runs on
        /// (HeroLocomotion: `_agent.height = 1.8f`). Referenced, never re-guessed.</summary>
        public const float HeroHeightRef = 1.8f;

        /// <summary>Owner ruling 2026-08-14: an interior portal reads at 1.5x a person.
        /// As-authored the Tripo mesh imports at roughly a THIRD of hero height — a doorway
        /// you could not walk through. Derived from the hero rather than typed as a world
        /// scale, so a hero-rig change carries it.</summary>
        public const float PortalHeroMultiple = 1.5f;

        /// <summary>Interior (dungeon) portal height in metres — 1.5x the hero.</summary>
        public static float InteriorHeight => HeroHeightRef * PortalHeroMultiple;

        /// <summary>Outcome of a swap. <see cref="Instance"/> is null on every failure path;
        /// <see cref="Handle"/> must be handed to <see cref="Release"/> by the caller on
        /// teardown or the bundle stays resident for the whole session.</summary>
        public struct SwapResult
        {
            public GameObject Instance;
            public AsyncOperationHandle<GameObject> Handle;
            public bool Ok => Instance != null;
        }

        /// <summary>
        /// Load the portal art, parent it under <paramref name="host"/> at local origin,
        /// strip its colliders, and normalize it to <paramref name="targetHeight"/> seated on
        /// the host's position.
        ///
        /// ASYNC, and deliberately NOT WaitForCompletion: there is zero precedent for a
        /// blocking Addressables wait in this tree and it is a known hazard on WebGL, the
        /// primary platform. Callers are expected to have placeholder geometry ALREADY
        /// standing and to retire it only once this returns Ok — so a missing content build
        /// degrades to "the old arch" rather than to an invisible portal.
        /// </summary>
        public static async UniTask<SwapResult> SwapInAsync(Transform host, float targetHeight,
                                                            string instanceName = "Portal_Owner")
        {
            var result = new SwapResult();
            if (host == null)
            {
                FlowTrace.Warn(Sys, "SwapInAsync called with a NULL host - nothing to seat the portal on.");
                return result;
            }

            GameObject prefab = null;
            try
            {
                result.Handle = Addressables.LoadAssetAsync<GameObject>(Address);
                // Await the handle, then read Result off the handle itself: ToUniTask() on
                // AsyncOperationHandle<T> yields no value in this UniTask version.
                await result.Handle.ToUniTask();
                prefab = result.Handle.Status == AsyncOperationStatus.Succeeded ? result.Handle.Result : null;
            }
            catch (Exception e)
            {
                // Loud, never silent: a missing bundle is the WO-974/975 failure class and it
                // must be VISIBLE in the capture rather than inferred from bare geometry.
                FlowTrace.Warn(Sys, $"portal addressable '{Address}' did not load ({e.GetType().Name}: " +
                                    $"{e.Message}) - the caller's placeholder geometry stands. This is what a " +
                                    "missing content build looks like at runtime.");
                return result;
            }

            // The host can be torn down while the load is in flight (scene change mid-await).
            if (prefab == null || host == null || host.gameObject == null)
            {
                FlowTrace.Warn(Sys, $"portal addressable '{Address}' resolved NULL (or the host died mid-load) " +
                                    "- the caller's placeholder geometry stands.");
                return result;
            }

            var portal = UnityEngine.Object.Instantiate(prefab, host);
            portal.name = instanceName;
            portal.transform.localPosition = Vector3.zero;
            portal.transform.localRotation = Quaternion.identity;
            NormalizeToHeight(portal, targetHeight);

            // Decorative ONLY - the caller's trigger must stay the sole collider, or the hero
            // can be blocked out of their own portal.
            foreach (var col in portal.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.Destroy(col);

            result.Instance = portal;
            FlowTrace.Step(Sys, $"PORTAL structure swapped in from '{Address}' onto '{host.name}' " +
                                $"at {targetHeight:0.##}m - the one shared portal look.");
            return result;
        }

        /// <summary>Release the Addressables handle. Idempotent-safe; call from OnDestroy.</summary>
        public static void Release(ref SwapResult result)
        {
            if (result.Handle.IsValid()) Addressables.Release(result.Handle);
            result.Instance = null;
        }

        /// <summary>Uniformly scale so the RENDERED height matches <paramref name="target"/>,
        /// then RE-SEAT the instance so its base stands on the seat it was placed at.
        ///
        /// ⚠ The re-seat is NOT optional garnish: the scale is SCALE-ONLY about the pivot, and
        /// "Tripo pivots are far off centre, so scaling localScale flings the visible mesh away
        /// from the capsule" (verbatim, AtbCombatantSwapper.cs:184-187 — the 'hero in empty
        /// area' bug). This asset IS a Tripo extract and the scale multiplies any pivot-to-mesh
        /// offset by the same factor, so without the re-seat a doorway ends up sunk in the floor
        /// or hanging in the air while its height reads perfectly correct.
        ///
        /// Traces the RESOLVED WORLD SPAN (min.y / centre / max.y) BEFORE and AFTER, not just
        /// measured-vs-target: a height-only line cannot tell "correctly seated" apart from
        /// "2.7m tall and floating 3m up" (docs/INSTRUMENTATION_STANDARD.md §1.4b).</summary>
        public static void NormalizeToHeight(GameObject go, float target)
        {
            if (go == null) return;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0)
            {
                FlowTrace.Warn(Sys, $"'{go.name}' has NO renderers - cannot normalize height OR re-seat " +
                                    "(it will render at its authored scale and wherever its pivot lands, " +
                                    "or not at all).");
                return;
            }

            // The seat = the world position the instance was placed at. Scaling localScale is
            // about the pivot, so the pivot does not move; capture it once and drive the mesh
            // back onto it afterwards.
            Vector3 seat = go.transform.position;

            Bounds b = MeasureBounds(rends);
            float h = b.size.y;
            if (h <= 0.001f)
            {
                FlowTrace.Warn(Sys, $"'{go.name}' measured {h:0.###}m tall at span y[{b.min.y:0.##}..{b.max.y:0.##}] " +
                                    "- degenerate bounds, leaving scale AND seat untouched (it may render " +
                                    "flat or invisible).");
                return;
            }

            float k = target / h;
            FlowTrace.Step(Sys, $"'{go.name}' normalize BEFORE: seat {seat} | span y[{b.min.y:0.##}..{b.max.y:0.##}] " +
                                $"h={h:0.##}m | centre {b.center} | target {target:0.##}m (scale x{k:0.###})");
            go.transform.localScale *= k;

            // Re-measure AFTER the scale — the bounds moved, and by how much is exactly the
            // unknown the trace above cannot answer on its own.
            Bounds scaled = MeasureBounds(rends);
            Vector3 delta = new Vector3(seat.x - scaled.center.x,
                                        seat.y - scaled.min.y,      // base ON the floor, not centre
                                        seat.z - scaled.center.z);
            go.transform.position += delta;

            Bounds seated = MeasureBounds(rends);
            FlowTrace.Step(Sys, $"'{go.name}' normalize AFTER: scaled span y[{scaled.min.y:0.##}..{scaled.max.y:0.##}] " +
                                $"centre {scaled.center} -> re-seat delta {delta} -> final span " +
                                $"y[{seated.min.y:0.##}..{seated.max.y:0.##}] h={seated.size.y:0.##}m " +
                                $"centre {seated.center} (base sits on seat y {seat.y:0.##})");
            // The one thing that must be TRUE at the end, asserted rather than assumed.
            if (Mathf.Abs(seated.min.y - seat.y) > 0.05f)
                FlowTrace.Warn(Sys, $"'{go.name}' re-seat did NOT land: base y {seated.min.y:0.###} vs seat " +
                                    $"y {seat.y:0.###} (off by {seated.min.y - seat.y:0.###}m) - the portal is " +
                                    "sunk into or floating above the floor. Suspect a non-uniform parent scale " +
                                    "or a renderer that moves after this frame (animated/particle bounds).");
        }

        /// <summary>Measure the WORLD bounds of a loaded portal instance. Public so a caller can
        /// seat its threshold VFX in the actual OPENING rather than at a hardcoded height that
        /// silently goes wrong the moment the art is retuned.</summary>
        public static Bounds MeasureBounds(GameObject go)
            => go != null ? MeasureBounds(go.GetComponentsInChildren<Renderer>(true)) : default;

        /// <summary>Encapsulated WORLD bounds over an already-fetched renderer set. Split out so
        /// the before/after/final measurements above are provably the same measurement three
        /// times — a second inline copy is how they drift.</summary>
        private static Bounds MeasureBounds(Renderer[] rends)
        {
            Bounds b = default; bool has = false;
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }
}
