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
using UnityEngine.SceneManagement;   // active-scene name — gate the village base-layout to its own scene
using DeNelle.Core.Catalog;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;   // TGVRU: instrument the base-layout spawn seam (§12)

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

        // HUB-SCOPE GUARD (build-defense regression, owner playtest 2026-06-19):
        // GameState.BaseLayout is the PLAYER'S VILLAGE base — a single GLOBAL list (NOT
        // scene-scoped). The pure HUB scenes (MainCastle_Hall / CastleHub) are NOT the
        // village; they must never replay the village base. Build Mode was wired into the
        // castle hub by commit ff2d64b7 ("drop Village-only scene guard"), which also let a
        // place-in-hub spin up a BaseLayoutLoader whose Start() then re-instantiated the
        // WHOLE prior-session village base INTO THE HUB ("it remembers the previous play's
        // towers and re-adds them on load"). This is the regressed guard, restored: the
        // loader spawns the base layout ONLY in a buildable VILLAGE scene, never in a hub.
        private static readonly HashSet<string> _hubScenesNoBaseLayout = new HashSet<string>
        {
            "MainCastle_Hall",
            "CastleHub",
        };

        private Transform _root;            // parent for all loaded structures
        private readonly List<PlacedStructure> _loaded = new List<PlacedStructure>();

        // Guard: the persisted set is instantiated exactly ONCE per session. After the
        // initial load, incremental adds go through Spawn() (one piece). LoadFromState()
        // must NEVER re-run Rebuild() (destroy-all/respawn-all) — that mass-rebuild is the
        // cause of every prior piece (+ a stale prior-session building) popping on at once.
        private bool _loadedOnce;

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
            // Idempotent: only the FIRST call instantiates the persisted set. Any later
            // call early-returns so we never re-run Rebuild()'s destroy-all/respawn-all
            // (which made every prior piece + a stale prior-session building pop on at
            // once). Subsequent placements add ONE piece each via Spawn() (the add path).
            if (_loadedOnce) return;
            _loadedOnce = true;

            // HUB-SCOPE GUARD: never replay the player's VILLAGE base into a pure HUB scene
            // (MainCastle_Hall / CastleHub). BaseLayout is a single GLOBAL list, so without
            // this a build-in-hub (which spins up this loader via EnsureExists) would dump the
            // whole prior-session village base into the hub — the reported "remembers previous
            // play's towers and re-adds them" regression. Self-reports so a future regression
            // (a new hub name, or the guard removed again) is visible in the capture, §12.
            string scene = SceneManager.GetActiveScene().name;
            if (_hubScenesNoBaseLayout.Contains(scene))
            {
                int n = GameStateService.Instance != null && GameStateService.Instance.State != null
                    && GameStateService.Instance.State.BaseLayout != null
                        ? GameStateService.Instance.State.BaseLayout.Count : 0;
                FlowTrace.Warn("BaseLayout",
                    $"LoadFromState: SKIPPED in hub scene '{scene}' — the village base ({n} placed " +
                    "structure(s)) does NOT replay in a hub (would re-add prior-session towers).");
                return;
            }

            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            var layout = state != null ? state.BaseLayout : null;
            if (layout == null || layout.Count == 0)
            {
                // Empty → fall through to the default village (the seed). No-op.
                FlowTrace.Step("BaseLayout",
                    $"LoadFromState: scene '{scene}' has an empty BaseLayout — default seed stands (no replay).");
                return;
            }

            FlowTrace.Step("BaseLayout",
                $"LoadFromState: scene '{scene}' — replaying {layout.Count} persisted village structure(s).");
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
            // U + R: a PARTIAL base (built < count) means some of the player's persisted buildings
            // silently vanished on load — the worst kind of "my base is wrong" bug. Warn (not a
            // happy Log) when any record failed so the shortfall self-reports; each failing record
            // already Fail'd in Spawn with its id.
            if (built < layout.Count)
                FlowTrace.Warn("BaseLayout",
                    $"Rebuild: loaded only {built}/{layout.Count} placed structure(s) — " +
                    $"{layout.Count - built} record(s) FAILED to spawn (see prior FAILED lines for ids).");
            else
                FlowTrace.Step("BaseLayout",
                    $"Rebuild: loaded {built}/{layout.Count} placed structure(s) from BaseLayout.");
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
                // U + R: a stale/renamed id drops one of the player's buildings on load. Fail-loud
                // naming the id (skip-not-abort: the rest of the layout still builds).
                FlowTrace.Fail("BaseLayout",
                    $"Spawn: BaseLayout id '{data.itemId}' not in registry — structure skipped (one building lost).");
                return null;
            }

            var cell = new Vector2Int(data.cellX, data.cellZ);
            Vector3 pos = grid.CellToWorld(cell);
            // SEAT HEIGHT — CellToWorld returns the flat grid plane (Y = origin.y, normally 0).
            // A persisted worldY != 0 seats the structure ELEVATED (e.g. a defense on a wall-walk
            // top — the defensive posture). Old records carry worldY = 0 → ground placement,
            // unchanged. Approximately() so a 0 from an old save is treated as "use the grid plane".
            if (!Mathf.Approximately(data.worldY, 0f)) pos.y = data.worldY;
            var rot = Quaternion.Euler(0f, data.yawSteps * 90f + data.yawOffset, 0f);

            GameObject go = null;
            // G(uard the Build): StructureFactory.Create can throw on a corrupt/missing prefab;
            // an unguarded throw aborts the whole Rebuild loop (every LATER building is lost).
            Guard.Try("BaseLayout", $"StructureFactory.Create '{data.itemId}'",
                () => go = StructureFactory.Create(entry, new Pose(pos, rot), Root));
            if (go == null)
            {
                // THE WORST SEAM (was a fully-silent `if (go == null) return null;`): the factory
                // produced no body for a VALID catalog entry — the player's building vanishes with
                // zero diagnostics. Fail-loud naming the entry id so the dead build self-reports.
                FlowTrace.Fail("BaseLayout",
                    $"Spawn: StructureFactory.Create returned null for entry '{data.itemId}' at cell ({cell.x},{cell.y}) — " +
                    "structure NOT built (one building lost; check the entry's prefabPath).");
                return null;
            }

            // FIX (footprint from CORRECTED bounds) — measure the UPRIGHT, OrientationFix-
            // applied mesh so the footprint matches what the ghost showed (a lying-down
            // prefab would report a long, wrong footprint and refuse to sit near a wall).
            Vector2Int footprint = grid.FootprintCells(StructureFactory.MeasureUprightFootprintMetres(entry));

            AddFootprintBlocker(go, footprint, grid.cellSize);

            var ps = go.AddComponent<PlacedStructure>();
            ps.itemId = data.itemId;
            ps.gridCell = cell;
            ps.footprint = footprint;
            ps.yawSteps = data.yawSteps;
            ps.level = Mathf.Max(1, data.level);
            ps.worldY = data.worldY;
            ps.wallMounted = data.wallMounted;
            ps.sellValue = (entry.repo != null ? entry.repo.buildCost : 0) / 2;

            // ELEVATION PERK — a defense seated on a wall-walk top gets the high-ground range/LOS
            // bonus. Applied HERE (not at place-time) so it covers both fresh placement (Place →
            // Spawn) AND reload from save, and because it is a MULTIPLIER it survives tier upgrades
            // (ApplyTierStats recomputes the base Range from the catalog without touching it). Gated
            // on the explicit wallMounted flag — NOT worldY != 0 — so a structure on merely raised
            // terrain never wrongly gets the bonus. Bounded to +25%.
            if (data.wallMounted)
            {
                const float kWallWalkRangeMult = 1.25f;
                var dt = go.GetComponent<DefenseTower>();
                if (dt != null) dt.ElevationRangeMult = kWallWalkRangeMult;
                var at = go.GetComponent<ArcaneTower>();
                if (at != null) at.ElevationRangeMult = kWallWalkRangeMult;
            }

            // DEF-208 — 3-tier visual progression. When the catalog authors a per-tier MODEL
            // (upgradeVisualPath — owner F8 2026-07-06), the reload swaps to it and the legacy
            // scale/tint step is skipped (the model IS the progression); otherwise the classic
            // taller/tinted read applies (1 bronze · 2 silver · 3 gold). Reskin BEFORE Apply
            // so Apply collects the new model's renderers.
            bool tierModel = ps.level >= 2 && StructureFactory.ReskinForLevel(go, entry, ps.level);
            var tier = go.AddComponent<StructureTierVisual>();
            tier.Apply(tierModel ? 1 : ps.level);
            ps.TierVisual = tier;

            // S5 — re-assert the per-tier GAMEPLAY stats so a structure saved above level 1
            // reloads at its tier (tower range/damage, wall toughness), not the base stats
            // StructureFactory attached. No-op for a level-1 structure.
            if (ps.level > 1)
                BuildModeController.ApplyTierStats(ps, ps.level);

            grid.Occupy(cell, footprint, data.itemId);
            _loaded.Add(ps);

            // WO-612: a structure saved mid-construction re-arms its scaffold on load.
            // The service's offline-fair sweep runs before this (it completes overdue
            // jobs on state load), so IsBuilding == true only for genuinely unfinished
            // jobs. Fresh placements are keyed AFTER Spawn (Place calls StartBuild
            // post-charge), so this is load-path only — no double-attach.
            if (DeNelle.Core.FeatureFlags.BuildTimers && BuildTimerService.Instance != null
                && BuildTimerService.Instance.IsBuilding(UnderConstructionVisual.KeyFor(data)))
                UnderConstructionVisual.Attach(ps, UnderConstructionVisual.KeyFor(data));

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
