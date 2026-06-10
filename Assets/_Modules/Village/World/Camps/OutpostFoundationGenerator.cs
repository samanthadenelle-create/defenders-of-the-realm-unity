// =============================================================================
// OutpostFoundationGenerator — productionizes the sandbox outpost builder into
// the real claim -> raid -> loot loop (FIRST SLICE: claim -> WOOD outpost -> recipe).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps   (STATIC)
//
// THE VISION: a claimed camp grows a REAL, persistent fortification — generated
// from the SAME catalog pieces (wall_wood / tower_ground_archer / gate_stone) and
// the SAME ONE creation path (StructureFactory.Create) the village build mode uses.
// Not bespoke primitives, not the global PlacementGrid.
//
// TWO HALVES (deliberately split — pure data vs. scene side-effects):
//   1. GenerateFootprintRecipe(w, d, tier) -> List<PlacedStructureData> in LOCAL
//      cell coords. Deterministic, pure, allocation-only. cell (0,0) == local origin.
//      Perimeter = walls, corners = corner towers, one door gap mid-front (z==0).
//   2. Realize(recipe, root, surfaceMask) -> seats the root on the terrain, then
//      walks each record turning LOCAL cell math into a WORLD Pose and calls
//      StructureFactory.Create(entry, pose, root). Per-piece grounding is handled
//      INSIDE Create (ReseatCorrectedBottom). Each piece gets a carving
//      NavMeshObstacle footprint blocker (copied from BaseLayoutLoader) — no rebake.
//
// LANDMINES AVOIDED (per the architecture plan):
//   * Does NOT touch PlacementGrid.Instance or GameState.BaseLayout — those are
//     the VILLAGE-scoped global singletons; routing camp pieces through them would
//     corrupt the village layout. We do LOCAL cell math against the outpost root.
//   * Does NOT call BaseLayoutLoader.Rebuild() (the destroy-all/respawn-all
//     pop-on-at-once bug). We own a per-piece Create loop.
//   * Footprint colliders measure the UPRIGHT mesh via
//     StructureFactory.MeasureUprightFootprintMetres (no hand-rolled bounds).
//   * cellSize matches PlacementGrid (3 m) so a camp piece reads at village scale.
//
// PlacedStructureData (DeNelle.Core) is the ONLY Core type persisted; Village->Core
// is allowed. ASCII-only strings. LogWarning, never error (pack-missing-safe).
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;

namespace DeNelle.Village.World.Camps
{
    /// <summary>The fortification material tier a claimed camp generates at.</summary>
    public enum OutpostTier
    {
        Wood = 0,   // wall_wood perimeter (the first slice)
        Stone = 1,  // wall_stone perimeter (a later upgrade bite)
    }

    /// <summary>
    /// Generates and realizes a claimed-camp fortification from catalog pieces.
    /// Static + pure-data first half; scene-side-effect second half. Mirrors the
    /// village build pipeline (StructureFactory.Create) without touching the
    /// village-scoped PlacementGrid / GameState.BaseLayout singletons.
    /// </summary>
    public static class OutpostFoundationGenerator
    {
        /// <summary>Local cell size in metres — matches PlacementGrid.cellSize (3 m).</summary>
        public const float CellSize = 3f;

        // -- Catalog ids the recipe references (must exist in CatalogRegistry) ----
        private const string IdWallWood   = "wall_wood";
        private const string IdWallStone  = "wall_stone";
        private const string IdCornerTower = "tower_ground_archer";
        private const string IdGate       = "gate_stone";

        // =====================================================================
        // HALF 1 — PURE DATA. Deterministic footprint recipe in LOCAL cell coords.
        // =====================================================================

        /// <summary>
        /// Emit a deterministic perimeter fortification recipe in LOCAL cell coords
        /// (cell (0,0) == the outpost root's local origin). Perimeter cells become
        /// walls (the tier's wall id), the four corners become corner towers, and a
        /// single one-cell DOOR gap is left at the middle of the front edge (z==0).
        /// Side columns (x==0 / x==max) get yawSteps=1 so their wall faces inward.
        /// Pure: allocates and returns a list, no scene side-effects. Clamps tiny
        /// dimensions to a sane minimum so a degenerate 1x1 never throws.
        /// </summary>
        public static List<PlacedStructureData> GenerateFootprintRecipe(int gridWidth, int gridDepth, OutpostTier tier)
        {
            var recipe = new List<PlacedStructureData>();

            // Clamp so a perimeter ring (corners + a centred door) is always well-formed.
            int w = Mathf.Max(3, gridWidth);
            int d = Mathf.Max(3, gridDepth);

            string wallId = tier == OutpostTier.Stone ? IdWallStone : IdWallWood;
            int doorX = w / 2;   // door sits mid front edge

            for (int x = 0; x < w; x++)
            {
                for (int z = 0; z < d; z++)
                {
                    bool onEdge = x == 0 || x == w - 1 || z == 0 || z == d - 1;
                    if (!onEdge) continue;   // interior stays open

                    bool isCorner = (x == 0 || x == w - 1) && (z == 0 || z == d - 1);
                    bool isDoor   = z == 0 && x == doorX && !isCorner;
                    if (isDoor)
                    {
                        // The one-door gap: place the gate instead of a wall.
                        recipe.Add(new PlacedStructureData(IdGate, x, z, 0, 1));
                        continue;
                    }

                    if (isCorner)
                    {
                        recipe.Add(new PlacedStructureData(IdCornerTower, x, z, 0, 1));
                        continue;
                    }

                    // Side (left/right) columns face inward — quarter-turn the wall so its
                    // long axis runs along the Z edge instead of the X edge.
                    int yawSteps = (x == 0 || x == w - 1) ? 1 : 0;
                    recipe.Add(new PlacedStructureData(wallId, x, z, yawSteps, 1));
                }
            }

            return recipe;
        }

        // =====================================================================
        // HALF 2 — REALIZE. LOCAL cell math -> WORLD Pose -> StructureFactory.Create.
        // =====================================================================

        /// <summary>
        /// Build every record in <paramref name="recipe"/> as a live structure under
        /// <paramref name="root"/>. First seats the root on the terrain surface
        /// (raycast down from +500 against <paramref name="surfaceMask"/>), then per
        /// record: resolve the CatalogEntry, compose the LOCAL cell offset into a WORLD
        /// Pose via root.TransformPoint, and call the ONE creation path. Each created
        /// piece gets a carving NavMeshObstacle footprint blocker (no rebake).
        /// Null/empty-safe: a null root or empty recipe is a logged no-op.
        /// </summary>
        public static void Realize(IReadOnlyList<PlacedStructureData> recipe, Transform root, LayerMask surfaceMask)
        {
            if (root == null)
            {
                Debug.LogWarning("[OutpostFoundationGenerator] Realize called with a null root — skipped.");
                return;
            }
            if (recipe == null || recipe.Count == 0)
            {
                Debug.LogWarning("[OutpostFoundationGenerator] Realize called with an empty recipe — skipped.");
                return;
            }

            // Ride the WHOLE outpost root on the terrain BEFORE composing piece poses,
            // so every piece is placed relative to the surface height (lifted from
            // the sandbox EnemyOutpostBuilder.SeatRootOnSurface).
            SeatRootOnSurface(root, surfaceMask);

            int built = 0;
            for (int i = 0; i < recipe.Count; i++)
            {
                if (RealizePiece(recipe[i], root)) built++;
            }

            Debug.Log($"[OutpostFoundationGenerator] Realized {built}/{recipe.Count} fortification piece(s) on '{root.name}'.");
        }

        /// <summary>
        /// Coroutine variant of <see cref="Realize"/> that spreads the per-piece
        /// StructureFactory.Create cost across frames (yield every <paramref name="piecesPerFrame"/>)
        /// instead of realizing the whole fort in ONE frame — fixing the multi-second
        /// single-frame stall when RaidOutpostSystem spawns an outpost on OuterWorld load.
        /// WHAT is built is identical to <see cref="Realize"/>; only the timing changes.
        /// Re-checks <paramref name="root"/> each batch so a mid-build Destroy bails cleanly.
        /// </summary>
        public static IEnumerator RealizeStaggered(IReadOnlyList<PlacedStructureData> recipe, Transform root,
                                                   LayerMask surfaceMask, int piecesPerFrame = 2)
        {
            if (root == null)
            {
                Debug.LogWarning("[OutpostFoundationGenerator] RealizeStaggered called with a null root — skipped.");
                yield break;
            }
            if (recipe == null || recipe.Count == 0)
            {
                Debug.LogWarning("[OutpostFoundationGenerator] RealizeStaggered called with an empty recipe — skipped.");
                yield break;
            }

            SeatRootOnSurface(root, surfaceMask);

            int per = Mathf.Max(1, piecesPerFrame);
            int built = 0;
            int inBatch = 0;
            for (int i = 0; i < recipe.Count; i++)
            {
                // The outpost can be Destroyed mid-build (scene swap / Arena recreate) —
                // bail the moment its root is gone so we never Create against a dead root.
                if (root == null) yield break;

                if (RealizePiece(recipe[i], root)) built++;

                if (++inBatch >= per)
                {
                    inBatch = 0;
                    yield return null;
                }
            }

            Debug.Log($"[OutpostFoundationGenerator] Realized (staggered) {built}/{recipe.Count} fortification piece(s) on '{root.name}'.");
        }

        // Realize ONE recipe record under root. Shared by Realize + RealizeStaggered so
        // both produce IDENTICAL pieces (resolve entry -> local cell -> world pose ->
        // StructureFactory.Create -> per-piece carving footprint blocker). Returns true
        // if a piece was created.
        private static bool RealizePiece(PlacedStructureData rec, Transform root)
        {
            var entry = CatalogRegistry.Get(rec.itemId);
            if (entry == null)
            {
                Debug.LogWarning($"[OutpostFoundationGenerator] recipe id '{rec.itemId}' not in registry — skipped.");
                return false;
            }

            // LOCAL cell coords -> WORLD via the outpost root's TRS (mirror of
            // StructureFactory.CreateGroup). cell (0,0) sits at the root origin.
            Vector3 localOffset = new Vector3(rec.cellX * CellSize, 0f, rec.cellZ * CellSize);
            Vector3 worldPos = root.TransformPoint(localOffset);
            Quaternion worldRot = root.rotation *
                                  Quaternion.Euler(0f, rec.yawSteps * 90f + rec.yawOffset, 0f);

            var go = StructureFactory.Create(entry, new Pose(worldPos, worldRot), root);
            if (go == null) return false;

            // Carve the navmesh per piece (no full rebake), measuring the UPRIGHT
            // footprint so the blocker matches the placed mesh.
            float footprintM = StructureFactory.MeasureUprightFootprintMetres(entry);
            AddFootprintBlocker(go, footprintM);
            return true;
        }

        // =====================================================================
        // Helpers (lifted from the sandbox builder + BaseLayoutLoader).
        // =====================================================================

        // Raycast straight DOWN from high above the root against surfaceMask; if it
        // hits ground/terrain, set the root's Y to the hit point so the whole outpost
        // rides on the surface at any elevation. No hit -> leave Y as-is. Ignores
        // triggers so we don't seat on a prompt/zone volume.
        private static void SeatRootOnSurface(Transform root, LayerMask surfaceMask)
        {
            Vector3 origin = root.position + Vector3.up * 500f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f,
                                surfaceMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 p = root.position;
                p.y = hit.point.y;
                root.position = p;
            }
        }

        // Mirror of BaseLayoutLoader.AddFootprintBlocker: a box covering the upright
        // footprint + a carving NavMeshObstacle (no per-place rebake). One piece each;
        // we never call a full rebuild.
        private static void AddFootprintBlocker(GameObject go, float footprintMetres)
        {
            float s = Mathf.Max(1f, footprintMetres);

            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(s, 4f, s);
            box.center = new Vector3(0f, 2f, 0f);

            var obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(s, 4f, s);
            obstacle.center = new Vector3(0f, 2f, 0f);
            obstacle.carving = true;   // carve the baked mesh, no full rebake
        }
    }
}
