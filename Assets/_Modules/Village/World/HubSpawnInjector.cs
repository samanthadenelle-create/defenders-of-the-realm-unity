// =============================================================================
// HubSpawnInjector — stop spawning INSIDE the Heart of Elarion.
// -----------------------------------------------------------------------------
// OWNER RULING (2026-08-05, verbatim): "when we spawned at the tree, can we change
// the positions to the tree? Go one or two steps in front of it so we don't spawn
// inside the tree like we have since day one."
//
// MEASURED (owner Player.log capture + scene read, NOT inferred):
//   • the hub spawn resolves to world (0, 0.08, 0) — proven on-mesh;
//   • the Heart/tree anchor 'HeartOfElarion' sits at world (0, 0, 12);
//   • the tree's XZ footprint reaches at least z = -4, i.e. an effective radius of
//     >= 16 m about (0,0,12). So (0, ·, 0) is 12 m from the trunk CENTRE and well
//     INSIDE the canopy/root mass — the player has been spawning in the tree since
//     day one. "One or two steps" therefore has to be measured from the tree's
//     EDGE, never from today's spawn.
//   • the tree carries NO colliders (they are stripped at bake — CastleHubBuilder
//     :2471); the only collider near the anchor is the Heart's 1 m capsule. The
//     footprint MUST therefore be sized from RENDERER bounds, not colliders.
//   • "in front" = the courtyard/gate side = -Z (MainGate_South, the world gate
//     marker at (0,0,-68), the wave-spawn ring at z ~ -60).
//
// FIX: at runtime, on each fresh load of the merged hub, union the world-space
// Renderer.bounds of the whole HeartOfElarion subtree, step
//   candidate = heart.position + back * (footprintRadiusXZ + 2f)
// (the +2f IS the owner's "one or two steps" clear of the root flare),
// NavMesh.SamplePosition it onto the baked mesh (z = -6 is NOT a proven on-mesh
// literal, so it is sampled, never shipped raw), then
//   1. repoint the baked 'HeroStartPoint_PlayerSpawn' marker at the result, and
//   2. warp the live hero there in the SAME handler.
//
// WHY both in one handler: HeroControlEnsurer.Ensure also runs on sceneLoaded and
// does NOT consult the marker for an already-in-scene hero, so moving the marker
// alone would lose an ordering race. Doing both here removes the race instead of
// depending on handler priority.
//
// WHY runtime-authored: Main_Castle_Overworld is builder-baked + owner-hand-dialed
// and is NEVER hand-edited (CLAUDE.md §3 — resave-corruption history). This mirrors
// the proven HomeReturnPortalInjector / CavePortalRepointInjector pattern:
// [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + a sceneLoaded re-arm, a WebGL-safe
// try/catch on every entry, idempotent per scene load.
//
// FACING is untouched — the hero keeps his current rotation (yaw 0, looking +Z at
// the tree), which preserves the FTUE beat where Sylas says "this tree is what we
// defend". This is a POSITIONAL fix only; the owner has not ruled on facing.
//
// FAIL-SAFE: no renderers, or a navmesh MISS, => log the line and MOVE NOTHING.
// A bad guess would strand the player somewhere worse than the canopy, and §12 law
// is that a failure is a LOGGED line, never a silent blank.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    public static class HubSpawnInjector
    {
        private const string HeartAnchorName = "HeartOfElarion";
        private const string MarkerName      = "HeroStartPoint_PlayerSpawn";

        /// <summary>The owner's "one or two steps" clear of the root flare, measured from the
        /// tree's renderer EDGE (not from the trunk centre and not from the old spawn).</summary>
        private const float StepClearM = 2f;

        /// <summary>NavMesh seat radius for the candidate. Generous enough to catch the courtyard
        /// plinth lift (castle.liftY) without being able to snap across the moat.</summary>
        private const float NavSampleM = 8f;

        /// <summary>Sanity clamp on a single renderer's XZ reach. The tree is big, but nothing in
        /// its subtree legitimately spans >120 m; a stray sky/particle renderer would otherwise
        /// shove the spawn into the moat. Clamped renderers are logged, never silently dropped.</summary>
        private const float MaxRendererSpanM = 120f;

        // Last resolved spawn + the scene it was resolved on (call sites must not reuse a hub
        // point in a scene that is not the hub).
        private static bool    _haveSpawn;
        private static Vector3 _spawn;
        private static string  _spawnScene = string.Empty;

        // Idempotency: keyed on the loaded scene's handle, which is unique per scene INSTANCE.
        // A re-entry on the same instance (an additive UI scene load, a double sceneLoaded) is a
        // no-op, so the player is never teleported twice.
        private static int _resolvedHandle;
        private static int _warpedHandle;

        /// <summary>The hub spawn this injector resolved (tree-edge + 2 m, navmesh-seated).
        /// <paramref name="requireCurrentScene"/> (default) only hands it back while the resolving
        /// scene is still the active one — every call site keeps its own fallback for false.</summary>
        public static bool TryGetHubSpawn(out Vector3 spawn, bool requireCurrentScene = true)
        {
            spawn = _spawn;
            if (!_haveSpawn) return false;
            if (requireCurrentScene &&
                !string.Equals(SceneManager.GetActiveScene().name, _spawnScene, System.StringComparison.Ordinal))
                return false;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SafeApply();   // also cover the scene already active at app start
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SafeApply();

        // Never throw out of a sceneLoaded handler (an uncaught throw halts the WebGL player).
        private static void SafeApply()
        {
            try { Apply(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HubSpawn] spawn repoint threw (non-fatal): " + e);
            }
        }

        /// <summary>Repoint the hub spawn marker + the live hero to the tree-edge step-off point.
        /// Public so a test/probe can drive it directly. Idempotent per scene instance.</summary>
        public static void Apply()
        {
            var active = SceneManager.GetActiveScene();
            if (!DeNelle.Core.HubScenes.IsOverworld(active.name)) return;   // merged hub only

            if (_resolvedHandle == active.handle && _warpedHandle == active.handle)
                return;                                                     // already done on this instance

            Transform heart = ResolveHeartAnchor();
            if (heart == null)
            {
                FlowTrace.Warn("HubSpawn",
                    $"'{HeartAnchorName}' not found on '{active.name}' — spawn left where the scene baked it " +
                    "(fail-safe: never move the player on a guess).");
                return;
            }

            if (_resolvedHandle != active.handle)
            {
                if (!ResolveSpawn(heart, active.name)) return;
                _resolvedHandle = active.handle;

                var marker = GameObject.Find(MarkerName);
                if (marker != null)
                {
                    Vector3 was = marker.transform.position;
                    marker.transform.position = _spawn;   // runtime repoint — NO scene edit
                    FlowTrace.Step("HubSpawn",
                        $"marker '{MarkerName}' repointed {was} -> {_spawn} (runtime only; the baked scene is untouched).");
                }
                else
                {
                    FlowTrace.Warn("HubSpawn",
                        $"marker '{MarkerName}' not found on '{active.name}' — the hero warp below still lands the " +
                        "step-off point, but marker-driven spawners keep their own fallback.");
                }
            }

            // Same handler as the marker move: HeroControlEnsurer.Ensure also runs on sceneLoaded and
            // does not consult the marker for an already-in-scene hero, so this removes the ordering
            // race rather than depending on handler priority.
            var loco = ResolveHero();
            if (loco == null)
            {
                FlowTrace.Warn("HubSpawn",
                    "no live HeroLocomotion at sceneLoaded — marker is repointed; the warp will be applied " +
                    "on the next handler pass once the hero exists.");
                return;
            }

            Vector3 from = loco.transform.position;
            loco.WarpTo(_spawn, loco.transform.rotation);   // keep facing (+Z, at the tree) — positional fix only
            _warpedHandle = active.handle;
            FlowTrace.Step("HubSpawn",
                $"hero warped {from} -> {_spawn} (rotation kept: yaw {loco.transform.eulerAngles.y:F0}) — " +
                "out of the canopy, two steps in front of the tree (owner ruling 2026-08-05).");
        }

        // ---------------------------------------------------------------------
        private static bool ResolveSpawn(Transform heart, string sceneName)
        {
            if (!TryFootprintRadiusXZ(heart, out float radius))
            {
                FlowTrace.Warn("HubSpawn",
                    $"no Renderer found under '{heart.name}' — cannot size the tree footprint (the tree has NO " +
                    "colliders, they are stripped at bake, so renderers are the only measure). Spawn NOT moved.");
                return false;
            }

            Vector3 candidate = heart.position + Vector3.back * (radius + StepClearM);   // -Z = courtyard/gate side

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, NavSampleM, NavMesh.AllAreas))
            {
                FlowTrace.Warn("HubSpawn",
                    $"NavMesh.SamplePosition MISS for candidate {candidate} (heart {heart.position}, " +
                    $"footprintRadiusXZ {radius:F1}m + {StepClearM}m step, search {NavSampleM}m) — spawn NOT moved " +
                    "(the old point is at least proven on-mesh).");
                return false;
            }

            _spawn      = hit.position;
            _haveSpawn  = true;
            _spawnScene = sceneName;
            FlowTrace.Step("HubSpawn",
                $"NavMesh HIT: candidate {candidate} -> spawn {_spawn} (dist {Vector3.Distance(candidate, _spawn):F2}m) " +
                $"| heart {heart.position}, footprintRadiusXZ {radius:F1}m, step {StepClearM}m, scene '{sceneName}'.");
            return true;
        }

        /// <summary>Union the world Renderer.bounds of the heart's whole subtree and return the XZ
        /// reach from the ANCHOR (max of the union extents and the actual -Z reach, so an off-centre
        /// canopy can never under-report the side we step off toward).</summary>
        private static bool TryFootprintRadiusXZ(Transform heart, out float radius)
        {
            radius = 0f;
            var renderers = heart.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return false;

            bool any = false;
            Bounds union = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                Bounds b = r.bounds;
                if (b.size.x > MaxRendererSpanM || b.size.z > MaxRendererSpanM)
                {
                    FlowTrace.Warn("HubSpawn",
                        $"renderer '{r.name}' spans {b.size.x:F0}x{b.size.z:F0}m (> {MaxRendererSpanM}m) — excluded " +
                        "from the tree footprint (a sky/particle renderer would shove the spawn into the moat).");
                    continue;
                }
                if (!any) { union = b; any = true; }
                else union.Encapsulate(b);
            }
            if (!any) return false;

            radius = Mathf.Max(union.extents.x, union.extents.z);
            radius = Mathf.Max(radius, heart.position.z - union.min.z);   // true -Z reach from the anchor
            return radius > 0.01f;
        }

        private static Transform ResolveHeartAnchor()
        {
            var anchor = GameObject.Find(HeartAnchorName);
            if (anchor != null) return anchor.transform;

            var heart = Object.FindAnyObjectByType<HeartController>();
            if (heart != null) return heart.transform;

            var visual = GameObject.Find("TreeOfLife_Visual");
            return visual != null ? visual.transform : null;
        }

        private static HeroLocomotion ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");   // WO-450: the canonical hero tag
            if (tagged != null)
            {
                var loco = tagged.GetComponent<HeroLocomotion>();
                if (loco != null) return loco;
            }
            return Object.FindFirstObjectByType<HeroLocomotion>();
        }
    }
}
