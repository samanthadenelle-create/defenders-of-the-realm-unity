// =============================================================================
// ArenaNavMeshBaker - RUNTIME NavMesh bake for a player-castle Arena defender base.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// WO-388 Phase-2 (toggle-gated): when the Arena raids the PLAYER'S OWN castle
// (GameState.BaseLayout) instead of a seeded fort, that imported layout has NO
// pre-baked NavMesh at the raid anchor and NO ground (a recipe is structures, no
// floor) - so the garrison NavMeshAgents would fail to spawn and EnemyOutpost
// would silently auto-Clear() (a "silent win" with nothing drawn). This component
// bakes a LOCAL walkable surface at runtime so the garrison spawns + the hero can
// reach the walls.
//
// SHAPE (owner-relayed design, corrected per WO-388 sec.3):
//   1. WAIT for the fort geometry to be realized. EnemyOutpost builds the fort in
//      its Start() (this same frame the Arena AddComponents it) - so the fort
//      children appear a frame or two later. We wait until root.childCount > 0
//      (the Realize'd pieces + the [Garrison] root), not a blind fixed delay.
//   2. Add an INVISIBLE WALKABLE FLOOR child under the footprint (a Unity Plane,
//      renderer DISABLED, MeshCollider kept) at the root's y. A Plane is 10m/unit,
//      so scale ~5 = ~50m square - generous cover for the fort footprint. The
//      CARVING wall NavMeshObstacles the fort pieces add then cut the walls out of
//      this floor (floor walkable, walls block) - the CastleHubBuilder pattern.
//   3. Add + configure a NavMeshSurface (collectObjects = Children, useGeometry =
//      PhysicsColliders, agentTypeID = 0 = the default hero/enemy agent) and call
//      BuildNavMesh() (SYNC) - BuildNavMeshAsync() is NOT a NavMeshSurface method.
//
// The agent radius/height/climb/slope/step come from the project's DEFAULT agent
// settings (agentTypeID 0), which already match the enemies + hero + stairs - so
// they need not (and cannot) be set on the surface here; the surface only exposes
// the voxel/region overrides, which we tune for a tighter local bake.
//
// ISOLATION: added at runtime by ArenaMode ONLY when "Use My Castle" is ON; the
// default-OFF seeded path never touches this. LogWarning, never error.
// ASCII-only runtime strings.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// Bakes a local walkable NavMesh under a player-castle Arena defender base at
    /// runtime, so the garrison NavMeshAgents can spawn and the fight can resolve.
    /// Added by <see cref="ArenaMode"/> only when the "Use My Castle" toggle is ON.
    /// </summary>
    public sealed class ArenaNavMeshBaker : MonoBehaviour
    {
        // -- Walkable floor sizing (a Unity Plane is 10 m per unit of scale) -----
        // ~5 => ~50 m square: generous cover for the realized fort footprint. This is
        // the DEFAULT for the WO-388 castle path; a caller (BattleArena) may pass a larger
        // scale via BakeForCastle so the bigger open kite floor is fully covered without
        // mutating the verified castle default.
        private const float DefaultFloorPlaneScale = 5f;

        // Set per-bake from the BakeForCastle argument; defaults to the castle scale.
        private float _floorPlaneScale = DefaultFloorPlaneScale;

        // -- Surface overrides (the agent radius/height/climb/slope/step come from
        //    the default agent type 0; only the voxel/region knobs are tunable here) --
        private const float VoxelSize     = 0.18f;   // finer than default for a tight local bake
        private const float MinRegionArea = 0.5f;    // prune stray islands smaller than this

        // -- Fort-realization wait budget ------------------------------------
        private const int   MaxWaitFrames = 120;     // ~2 s @ 60 fps safety cap

        private NavMeshSurface _surface;

        /// <summary>
        /// Begin the runtime bake for the player-castle defender base under
        /// <paramref name="root"/> (the Arena outpost host). Safe to call right after
        /// EnemyOutpost.ConfigureArena - it waits for the fort to realize first.
        /// </summary>
        public void BakeForCastle(Transform root, float floorPlaneScale = DefaultFloorPlaneScale)
        {
            if (root == null)
            {
                Debug.LogWarning("[ArenaNavMeshBaker] null root - bake skipped.");
                return;
            }
            // The WO-388 castle path calls this with no arg -> keeps the verified 5f scale.
            // BattleArena passes ~8f so the larger open kite floor is fully baked.
            _floorPlaneScale = floorPlaneScale > 0f ? floorPlaneScale : DefaultFloorPlaneScale;
            StartCoroutine(BakeRoutine(root));
        }

        // Wait for the fort to realize -> add the walkable floor -> configure + bake.
        private IEnumerator BakeRoutine(Transform root)
        {
            // 1. WAIT for the fort geometry. EnemyOutpost.Start() (same/next frame)
            // realizes the pieces under this root; childCount > 0 once they exist.
            int waited = 0;
            while (root != null && root.childCount == 0 && waited < MaxWaitFrames)
            {
                waited++;
                yield return null;
            }
            if (root == null) yield break;
            // One more frame so the last-realized colliders are registered before baking.
            yield return null;

            // 2. INVISIBLE WALKABLE FLOOR under the footprint (renderer off, collider
            // kept). The carving wall-obstacles cut the walls out of this floor.
            AddWalkableFloor(root, _floorPlaneScale);

            // 3. CONFIGURE + BAKE the local surface (SYNC).
            _surface = root.gameObject.GetComponent<NavMeshSurface>();
            if (_surface == null) _surface = root.gameObject.AddComponent<NavMeshSurface>();

            _surface.collectObjects   = CollectObjects.Children;            // this host + its fort/floor children
            _surface.useGeometry      = NavMeshCollectGeometry.PhysicsColliders; // bake from colliders (renderer-off floor)
            _surface.agentTypeID      = 0;                                  // the default hero/enemy agent
            _surface.layerMask        = ~0;                                 // all layers (Default | Structure | Environment)
            _surface.overrideVoxelSize = true;
            _surface.voxelSize        = VoxelSize;
            _surface.minRegionArea    = MinRegionArea;

            // SYNC bake. BuildNavMeshAsync() is NOT a NavMeshSurface method (async
            // needs NavMeshBuilder.UpdateNavMeshDataAsync); the sync path is v1.
            _surface.BuildNavMesh();

            Debug.Log("[ArenaNavMeshBaker] Baked local NavMesh for player-castle defender base " +
                      $"(floor scale {_floorPlaneScale}, voxel {VoxelSize}).");
        }

        // An invisible, walkable floor plane sized to the fort footprint, sitting at
        // the root's y. Renderer OFF (invisible) but the MeshCollider is kept so the
        // PhysicsColliders bake picks it up - the CastleHubBuilder nav-floor pattern.
        private static void AddWalkableFloor(Transform root, float floorPlaneScale)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane); // MeshFilter + MeshRenderer + MeshCollider
            plane.name = "ArenaNavFloor_Invisible_Walkable";
            plane.transform.SetParent(root, false);
            plane.transform.localPosition = Vector3.zero;               // sit at the root y (the fort base)
            plane.transform.localScale = new Vector3(floorPlaneScale, 1f, floorPlaneScale);

            // Invisible but bakeable: hide the renderer; the NavMesh bakes from the
            // MeshCollider (useGeometry = PhysicsColliders), so visibility is moot.
            var r = plane.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
            if (plane.GetComponent<MeshCollider>() == null) plane.AddComponent<MeshCollider>();
        }
    }
}
