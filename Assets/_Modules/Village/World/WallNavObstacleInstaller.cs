// =============================================================================
// WallNavObstacleInstaller (DEF-224) — runtime carving backstop for the village
// perimeter walls, so enemies cannot path THROUGH a wall even when the baked
// NavMesh did not carve it.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS:
//   Enemies move ONLY as NavMeshAgents (Enemy.cs → NavMeshAgent.SetDestination).
//   A NavMeshAgent is constrained to the NavMesh and IGNORES physics colliders
//   entirely — so a wall's BoxCollider does NOTHING to stop an agent. The only
//   thing that stops an agent is the NavMesh being CARVED (no walkable surface)
//   where the wall stands.
//
//   The perimeter walls are blocked purely by the BAKE: VillageSceneBuilder marks
//   the wall renderers + the "WallBarrier-*" boxes NavigationStatic, and the bake
//   voxelizes them into obstacles. That works ONLY if the scene was baked with
//   those pieces present and correct. When the bake is stale / the lane wasn't
//   carved (the recurring bake-order fragility — see PIPELINE_STATE / memory),
//   the NavMesh stays continuous across the wall and agents walk straight through.
//
//   This installer is the bake-INDEPENDENT safety net: at runtime it finds the
//   builder's "WallBarrier-*" blocker boxes (full-height Y0→wall-top, already
//   broken at every gate opening) and gives each a carving NavMeshObstacle. A
//   carving obstacle cuts a hole in the LIVE NavMesh at runtime, so the wall
//   blocks agents regardless of what the bake did. Same proven pattern the
//   player-built structures already use (BaseLayoutLoader: carving=true).
//
//   GATES STAY WALKABLE: we carve ONLY the "WallBarrier-*" boxes, which the
//   builder deliberately does NOT place across the gate gaps — so the gate lanes
//   are never carved and enemies still pour through an open gate (and a destroyed
//   gate). We never touch the gate objects or the moat bridges.
//
// SHIPS IN A CODE BUILD: self-bootstraps via RuntimeInitializeOnLoadMethod (like
//   TargetManager / NavPathCoordinator) and re-scans on every sceneLoaded — so it
//   needs NO scene-file edit and NO rebake to take effect.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime backstop that adds carving <see cref="NavMeshObstacle"/>s to the
    /// village perimeter wall barriers so NavMeshAgent enemies cannot walk through
    /// a wall even if the baked NavMesh failed to carve it. Gate openings are left
    /// untouched (the barriers are already gate-gapped), so gate-walkthrough — and
    /// destroyed-gate pour-through — is preserved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallNavObstacleInstaller : MonoBehaviour
    {
        /// <summary>Object-name prefix of the builder's full-height wall blocker boxes
        /// (VillageSceneBuilder.Fortify.BuildRamparts → "WallBarrier-North-W", etc.).
        /// These are deliberately broken at every gate gap, so carving exactly these
        /// blocks the walls while leaving the gate lanes open.</summary>
        private const string BarrierPrefix = "WallBarrier";

        private static WallNavObstacleInstaller _instance;

        // Track which objects we've already fitted so a re-scan never double-adds.
        private readonly HashSet<int> _carved = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[WallNavObstacleInstaller]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<WallNavObstacleInstaller>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            // The Village scene is already active when AfterSceneLoad fires — scan now.
            ScanAndCarve();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ScanAndCarve();

        /// <summary>
        /// Finds every active "WallBarrier-*" object in the loaded scenes and gives
        /// it a carving NavMeshObstacle (idempotent). Public so a dev tool / test
        /// can force a re-scan after a scene is built at runtime.
        /// </summary>
        public void ScanAndCarve()
        {
            int added = 0;

            // GetComponentsInChildren can't scan the whole scene; walk every root.
            // This is a one-shot scan on scene load — never a per-frame cost.
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root == null) continue;
                    added += CarveBarriersUnder(root.transform);
                }
            }

            if (added > 0)
                Debug.Log($"[WallNavObstacleInstaller] Added {added} carving NavMeshObstacle(s) to " +
                          "perimeter wall barriers — walls now block NavMeshAgent enemies regardless " +
                          "of the baked NavMesh (gate openings left walkable).");
        }

        /// <summary>
        /// Recursively fits a carving obstacle to every "WallBarrier-*" transform
        /// under <paramref name="t"/>. Returns the count newly added.
        /// </summary>
        private int CarveBarriersUnder(Transform t)
        {
            int added = 0;
            if (t == null) return added;

            if (t.name.StartsWith(BarrierPrefix, System.StringComparison.Ordinal))
            {
                if (TryFitObstacle(t.gameObject)) added++;
                // A barrier has no barrier-children — still recurse for safety, cheap.
            }

            for (int i = 0; i < t.childCount; i++)
                added += CarveBarriersUnder(t.GetChild(i));

            return added;
        }

        /// <summary>
        /// Adds a box-shaped carving <see cref="NavMeshObstacle"/> sized to the
        /// object's collider/renderer bounds. No-op if one is already present
        /// (idempotent across re-scans). Returns true when a NEW obstacle was added.
        /// </summary>
        private bool TryFitObstacle(GameObject go)
        {
            if (go == null) return false;
            int id = go.GetInstanceID();
            if (!_carved.Add(id)) return false;          // already handled this object

            if (go.GetComponent<NavMeshObstacle>() != null) return false;  // baked/authored already

            // Size the obstacle to the local bounds of the box. The barrier boxes are
            // CreatePrimitive(Cube)s, so localScale IS the world size (unit-cube mesh)
            // and a unit-size box obstacle scaled by the transform matches exactly.
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape  = NavMeshObstacleShape.Box;
            obstacle.size   = Vector3.one;               // unit cube × transform.lossyScale = world size
            obstacle.center = Vector3.zero;
            // Carve the LIVE navmesh so the wall blocks agents without a rebake. The
            // wall never moves, so carveOnlyStationary keeps the carve cheap/stable.
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            return true;
        }
    }
}
