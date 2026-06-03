// =============================================================================
// BaseLayoutLoader — the runtime twin of VillageSceneBuilder.BuildBuildings.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-108 P0: reads GameState.BaseLayout and instantiates one structure per
// record via the ONE creation path (StructureFactory.Create over CatalogEntry).
// Places at PlacementGrid.CellToWorld(cell) + yawSteps*90, adds a footprint
// collider + a NavMeshObstacle (carve, no per-place rebake) + a PlacedStructure
// marker, and occupies the grid cells.
//
//   • EMPTY BaseLayout  → no-op: the default VillageSceneBuilder village (the
//                         seed) stands untouched.
//   • NON-EMPTY layout  → REPLACES the builder's output at runtime (this is the
//                         player's edited base) by spawning under a dedicated
//                         root the loader owns.
//
// Does NOT touch VillageSceneBuilder or any .unity file — it reads the same
// catalog data the builder authors from and rebuilds at runtime, per the WO.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Instantiates the player's persisted <see cref="GameState.BaseLayout"/> at
    /// runtime. The data-driven counterpart to the editor village builder; an empty
    /// layout falls through to the default village seed.
    /// </summary>
    public sealed class BaseLayoutLoader : MonoBehaviour
    {
        public static BaseLayoutLoader Instance { get; private set; }

        [Tooltip("Auto-load GameState.BaseLayout on Start. Disable to drive load manually (tests).")]
        [SerializeField] private bool _loadOnStart = true;

        private Transform _root;            // parent for all loaded structures
        private readonly List<PlacedStructure> _loaded = new List<PlacedStructure>();

        /// <summary>The structures this loader currently has in the scene.</summary>
        public IReadOnlyList<PlacedStructure> Loaded => _loaded;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (_loadOnStart) LoadFromState();
        }

        /// <summary>Ensure a loader exists in the scene (called by BuildModeController).</summary>
        public static BaseLayoutLoader EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("BaseLayoutLoader");
            return go.AddComponent<BaseLayoutLoader>();
        }

        private Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("PlayerBaseLayout");
                    _root = go.transform;
                }
                return _root;
            }
        }

        /// <summary>
        /// (Re)build the scene from <see cref="GameState.BaseLayout"/>. An empty or
        /// absent layout is a no-op (the default village seed stands). A non-empty
        /// layout clears any previously-loaded structures and rebuilds.
        /// </summary>
        public void LoadFromState()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var layout = state != null ? state.BaseLayout : null;
            if (layout == null || layout.Count == 0)
            {
                // Empty → fall through to the default village (the seed). No-op.
                return;
            }

            Rebuild(layout);
        }

        /// <summary>
        /// Drop a structure from the live loaded-set WITHOUT freeing its cells or
        /// destroying it (the caller — BuildModeController.SellSelected — already
        /// frees the grid + destroys the object). Prevents a double-free on Exit.
        /// </summary>
        public void Forget(PlacedStructure ps)
        {
            if (ps == null) return;
            _loaded.Remove(ps);
        }

        /// <summary>Destroy currently-loaded structures and free their grid cells.</summary>
        public void ClearLoaded()
        {
            var grid = PlacementGrid.Instance;
            foreach (var ps in _loaded)
            {
                if (ps == null) continue;
                grid?.Free(ps.gridCell, ps.footprint);
                Destroy(ps.gameObject);
            }
            _loaded.Clear();
        }

        /// <summary>Build every record in <paramref name="layout"/> into the scene.</summary>
        public void Rebuild(IReadOnlyList<PlacedStructureData> layout)
        {
            ClearLoaded();

            var grid = PlacementGrid.Instance;
            if (grid == null)
            {
                // The grid is the source of truth for cell↔world; create one if the
                // build flow has not yet (loader can run before BuildModeController).
                grid = new GameObject("PlacementGrid").AddComponent<PlacementGrid>();
            }
            grid.ClearAll();

            int built = 0;
            for (int i = 0; i < layout.Count; i++)
            {
                if (Spawn(layout[i], grid) != null) built++;
            }
            Debug.Log($"[BaseLayoutLoader] Loaded {built}/{layout.Count} placed structure(s) from BaseLayout.");
        }

        /// <summary>
        /// Spawn one record: resolve its CatalogEntry, build it via StructureFactory
        /// at the cell centre + yaw, then add footprint collider + NavMeshObstacle +
        /// the PlacedStructure marker, and occupy the grid. Returns null on a missing
        /// entry (logged), so a stale id degrades gracefully.
        /// </summary>
        public PlacedStructure Spawn(PlacedStructureData data, PlacementGrid grid)
        {
            var entry = CatalogRegistry.Get(data.itemId);
            if (entry == null)
            {
                Debug.LogWarning($"[BaseLayoutLoader] BaseLayout id '{data.itemId}' not in registry — skipped.");
                return null;
            }

            var cell = new Vector2Int(data.cellX, data.cellZ);
            Vector3 pos = grid.CellToWorld(cell);
            var rot = Quaternion.Euler(0f, data.yawSteps * 90f, 0f);

            var go = StructureFactory.Create(entry, new Pose(pos, rot), Root);
            if (go == null) return null;

            Vector2Int footprint = grid.FootprintCells(
                entry.repo != null && entry.repo.placement != null ? entry.repo.placement.footprint : 3f);

            AddFootprintBlocker(go, footprint, grid.cellSize);

            var ps = go.AddComponent<PlacedStructure>();
            ps.itemId = data.itemId;
            ps.gridCell = cell;
            ps.footprint = footprint;
            ps.yawSteps = data.yawSteps;
            ps.level = Mathf.Max(1, data.level);
            ps.sellValue = (entry.repo != null ? entry.repo.buildCost : 0) / 2;

            // DEF-208 — 3-tier visual progression: a taller, tier-tinted read per level
            // (1 bronze · 2 silver · 3 gold). Visual-only; the gameplay upgrade owns stats.
            var tier = go.AddComponent<StructureTierVisual>();
            tier.Apply(ps.level);
            ps.TierVisual = tier;

            grid.Occupy(cell, footprint, data.itemId);
            _loaded.Add(ps);
            return ps;
        }

        /// <summary>
        /// Mirror of VillageSceneBuilder.AddBuildingFootprintCollider: a box that
        /// covers the footprint, plus a NavMeshObstacle that CARVES the baked
        /// navmesh at runtime (no per-place rebake — the WO's mobile-safe path).
        /// The gate-clearance rule (PlacementGrid / BuildModeController) keeps a
        /// spawn→Heart lane open, so carving never fully walls off the enemy path.
        /// </summary>
        private static void AddFootprintBlocker(GameObject go, Vector2Int footprintCells, float cellSize)
        {
            float w = Mathf.Max(1, footprintCells.x) * cellSize;
            float d = Mathf.Max(1, footprintCells.y) * cellSize;

            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(w, 4f, d);
            box.center = new Vector3(0f, 2f, 0f);

            var obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(w, 4f, d);
            obstacle.center = new Vector3(0f, 2f, 0f);
            obstacle.carving = true;   // carve the baked mesh, no full rebake
        }
    }
}
