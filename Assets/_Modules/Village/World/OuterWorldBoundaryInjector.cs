// =============================================================================
// OuterWorldBoundaryInjector — runtime world-boundary wall for OuterWorld.
// -----------------------------------------------------------------------------
// SYMPTOM: The OuterWorld terrain is 1000x1000 m centred at origin (edge at ±500;
// WO-468 Phase 1 enlarged it from 300x300). There is NO world-boundary collider,
// so the hero walks off the edge into the void. A boundary builder was specced in
// WORK_ORDER_33 but never implemented.
//
// WHAT THIS DOES (asset-independent, always lands):
//   On every scene load (and once at app start) — when OuterWorld is LOADED
//   (it loads ADDITIVELY over MainCastle_Hall and is NEVER the active scene, so
//   we must gate on isLoaded, not GetActiveScene) — inject an invisible perimeter
//   of 4 BoxColliders just inside the ±500 edge (at ±485) to wall the play area.
//   The colliders are 20 m tall and 2 m thick, forming a closed ring the hero
//   cannot cross, parented into the OuterWorld scene so they unload with it.
//
// Mirrors the runtime-fixer pattern of GroundZFightFixer:
//   • [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + SceneManager.sceneLoaded
//     re-arm — the player boots elsewhere and reaches OuterWorld LATER, so a
//     one-shot check would miss it; we re-run on every scene load.
//   • OuterWorld-scene-gated — never touches Title / village / dungeons.
//   • WEBGL-SAFE: an uncaught exception in a sceneLoaded handler HALTS the WebGL
//     player, so every entry point is wrapped in try/catch.
//   • IDEMPOTENT: if a GameObject named "OuterWorldBoundary" already exists in
//     the scene, do nothing — repeated loads never stack duplicate walls.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.World
{
    public static class OuterWorldBoundaryInjector
    {
        // Name of the parent boundary object — used as the idempotency guard.
        private const string BoundaryName = "OuterWorldBoundary";

        // Active scene that gets the boundary ring.
        private const string TargetScene = "OuterWorld";

        /// <summary>
        /// Registrar. Runs once at app start, then re-runs on EVERY scene load —
        /// the player reaches OuterWorld LATER, so a one-shot check would miss it.
        /// Idempotent per load (guarded by the existing-object check).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Also build for the scene already active at app start.
            SafeBuild();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SafeBuild();
        }

        // Never let the boundary build throw out of a sceneLoaded handler (halts WebGL).
        private static void SafeBuild()
        {
            try { BuildBoundary(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OuterWorldBoundary] boundary build threw (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Inject the 4-collider perimeter ring just inside the OuterWorld edge.
        /// No-op outside the OuterWorld scene, or when the ring already exists.
        /// </summary>
        public static void BuildBoundary()
        {
            // OuterWorld loads ADDITIVELY over MainCastle_Hall — it is NEVER the
            // ACTIVE scene, so gate on whether it is LOADED, not GetActiveScene().
            Scene ow = SceneManager.GetSceneByName(TargetScene);
            if (!ow.IsValid() || !ow.isLoaded) return;

            // IDEMPOTENT: a ring already in any loaded scene → done.
            if (GameObject.Find(BoundaryName) != null) return;

            var parent = new GameObject(BoundaryName);

            // WO-468 WRAPPED SEAM (2026-06-27): the terrain is ORIGIN-CENTERED again (1000x1000,
            // edges at ±500) and WRAPS the castle on all 4 sides — superseding the WO-468-Phase-2
            // south-shift (the old CenterZ=-572 / z=-74..-1070 ring walled only the south half against
            // the now-centered terrain, leaving N/E/W open to the void). The castle sits at world
            // origin; ALL 4 gate crossings are INTERIOR warps at ±66 (HeroLinkCrossing/SceneTransition),
            // fully inside this perimeter — so the ±485 ring is the FAR map edge and is a CLOSED 4-wall
            // box (NO gate-lane gaps: a gap on a gate axis would only expose the void at ±485 for the
            // player walking straight out, since the gates aren't at the boundary). ~15 m inside the
            // ±500 edge, 20 m tall, 2 m thick.
            const float Edge = 485f;
            const float Len  = 970f;   // span across the other axis (±485)
            AddWall(parent.transform, "North", new Vector3(0f, 10f,  Edge), new Vector3(Len, 20f, 2f));
            AddWall(parent.transform, "South", new Vector3(0f, 10f, -Edge), new Vector3(Len, 20f, 2f));
            AddWall(parent.transform, "East",  new Vector3( Edge, 10f, 0f), new Vector3(2f, 20f, Len));
            AddWall(parent.transform, "West",  new Vector3(-Edge, 10f, 0f), new Vector3(2f, 20f, Len));

            // A new GameObject lands in the ACTIVE scene (MainCastle_Hall) by default;
            // move the ring into OuterWorld so it unloads/reloads with that scene.
            SceneManager.MoveGameObjectToScene(parent, ow);

            Debug.Log("[OuterWorldBoundary] closed 4-wall ring injected at ±485 (origin-centered OuterWorld 1000x1000, wraps the castle).");
        }

        // Create one invisible wall: a GameObject with ONLY a BoxCollider (no MeshRenderer).
        private static void AddWall(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = center;

            var box = wall.AddComponent<BoxCollider>();
            box.size = size;
        }
    }
}
