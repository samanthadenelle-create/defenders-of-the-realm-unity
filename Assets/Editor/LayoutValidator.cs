// =============================================================================
// LayoutValidator — the pipeline's "verify" step for a base-layout recipe.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor
//
// Owner: "since all is just offsets you can run safeguard math checks." This is
// PURE MATH over a BaseLayout / placement recipe (List<PlacedStructureData>) +
// the Core catalog — it catches the bug classes we keep hitting (float / out-of-
// bounds / overlap / missing orientation / blocked gate lane / round-trip drift)
// BEFORE they surface in-game, so a bad recipe never reaches a bake or a build.
//
// WHY EDITOR + REPLICATED MATH (not a call into Village):
//   DeNelle.Editor references DeNelle.Core but NOT DeNelle.Village (asmdef). The
//   recipe types (PlacedStructureData, CatalogEntry, RepoProps, PlacementRules,
//   GameState, CatalogRegistry) all live in Core, so the validator reads them
//   directly. The grid / footprint / gate-lane MATH lives in Village
//   (PlacementGrid, StructureFactory, BuildModeController) which Editor can't
//   reference — so the constants + formulae are REPLICATED here, deliberately
//   kept in lock-step with those sources (see the per-check comments citing the
//   Village line each formula mirrors). Keeping the math pure also means this
//   exact checker can later run POST-BAKE / server-side headless.
//
// SCOPE NOTE (intentional limits, called out for follow-up):
//   • ON-GROUND: the recipe is a flat XZ planner — Y comes from the placement
//     raycast at load, NOT from the recipe (PlacementGrid header §11-15). The
//     recipe has no per-placement Y, so "float" can only be checked against the
//     grid plane (origin.y). We assert the cell→world Y == origin.y (no recipe
//     can encode a float), and surface the limit rather than fake a check.
//   • GATE-LANE: gates are Village scene objects (Gate MonoBehaviour) the recipe
//     does NOT store and the Editor asmdef can't reference by type. We resolve
//     gates from the ACTIVE SCENE by GameObject name ("Gate") and replicate
//     BuildModeController.IsTooCloseToGate's lane math. When validating a bare
//     recipe with no scene (or no gate found) this check is reported SKIPPED.
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    /// <summary>
    /// Pure-math safeguard checks over a base-layout recipe
    /// (<see cref="System.Collections.Generic.List{PlacedStructureData}"/>). Each check is
    /// independent and logs every violation as <c>[LayoutValidator] FAIL: &lt;id&gt; &lt;reason&gt;</c>,
    /// then a single summary line (<c>LAYOUT_VALIDATE_OK</c> / <c>LAYOUT_VALIDATE_FAIL</c>) so a
    /// batchmode / CI run can grep one token for pass/fail.
    /// </summary>
    public static class LayoutValidator
    {
        // ── Grid constants — MIRROR PlacementGrid (DeNelle.Village). Keep in lock-step. ──
        // PlacementGrid.cs: cellSize 3, gridWidth 28, gridHeight 22, edgeMargin 0,
        // origin auto-centred to (-w*cs/2, 0, -h*cs/2) when left at Vector3.zero.
        private const float CellSize   = 3f;
        private const int   GridWidth  = 28;
        private const int   GridHeight = 22;
        private const int   EdgeMargin = 0;

        // ON-GROUND epsilon (metres). The recipe can't encode a float, so this guards the
        // grid-plane invariant only; kept tight because the math is exact.
        private const float GroundEpsilon = 0.01f;

        // Gate-lane math — MIRROR BuildModeController.IsTooCloseToGate (DeNelle.Village).
        private const float GateClearance = 3f;   // _gateClearance default
        private const float GateLaneReach = 6f;   // laneReach base (6 + clearance)

        private static Vector3 GridOrigin =>
            new Vector3(-GridWidth * CellSize * 0.5f, 0f, -GridHeight * CellSize * 0.5f);

        // ── One violation record ──────────────────────────────────────────────
        private struct Violation
        {
            public string id;       // the offending placement id (or "<recipe>")
            public string reason;
            public Violation(string id, string reason) { this.id = id; this.reason = reason; }
        }

        // ── Public batchmode / menu entry points ──────────────────────────────

        /// <summary>
        /// Batchmode-friendly entry: validate the ACTIVE GameState's BaseLayout (the live
        /// recipe), resolving gates from the active scene. Logs each violation + a single
        /// LAYOUT_VALIDATE_OK / LAYOUT_VALIDATE_FAIL token. Returns true when clean.
        /// Invoke headless via -executeMethod DeNelle.Editor.LayoutValidator.ValidateActive
        /// </summary>
        [MenuItem("Defenders/Verify/Validate Active Base Layout")]
        public static bool ValidateActive()
        {
            List<PlacedStructureData> recipe = ResolveActiveRecipe();
            if (recipe == null)
            {
                Debug.LogWarning("[LayoutValidator] No active GameState/BaseLayout found — " +
                                 "nothing to validate. (Empty recipe is reported as OK.)");
                recipe = new List<PlacedStructureData>();
            }
            return Validate(recipe);
        }

        /// <summary>
        /// Run all checks over <paramref name="recipe"/>. Pure apart from the optional
        /// active-scene gate lookup (gates aren't stored in the recipe). Logs every
        /// violation and a single summary token. Returns true when no violations.
        /// </summary>
        public static bool Validate(IReadOnlyList<PlacedStructureData> recipe)
        {
            var violations = new List<Violation>();
            if (recipe == null) recipe = System.Array.Empty<PlacedStructureData>();

            // Resolve catalog presence once; warn (don't fail) if the registry is empty —
            // an empty registry means Content hasn't registered yet (not a recipe bug).
            if (CatalogRegistry.Count == 0)
            {
                Debug.LogWarning("[LayoutValidator] CatalogRegistry is EMPTY — orientation / " +
                                 "prefab checks will report every id as unresolved. Register the " +
                                 "catalog (enter Build Mode / run Content seeding) before validating " +
                                 "for a meaningful ORIENTATION result.");
            }

            CheckOnGround(recipe, violations);
            CheckInBounds(recipe, violations);
            CheckNoOverlap(recipe, violations);
            CheckOrientation(recipe, violations);
            CheckGateLane(recipe, violations);
            CheckRoundTrip(recipe, violations);

            foreach (var v in violations)
                Debug.LogError($"[LayoutValidator] FAIL: {v.id} {v.reason}");

            bool ok = violations.Count == 0;
            if (ok)
                Debug.Log($"[LayoutValidator] {recipe.Count} placement(s) checked, 0 violations. LAYOUT_VALIDATE_OK");
            else
                Debug.LogError($"[LayoutValidator] {violations.Count} violation(s) over {recipe.Count} placement(s). LAYOUT_VALIDATE_FAIL");
            return ok;
        }

        // ── CHECK 1: ON-GROUND ─────────────────────────────────────────────────
        // The recipe is a flat XZ planner: Y is supplied by the placement raycast at
        // LOAD time (PlacementGrid header), the recipe has no per-placement Y. So the
        // only float the recipe itself can encode is a grid plane that isn't ground.
        // We assert CellToWorld(cell).y == origin.y (the plane the structure seats on).
        // CellToWorld (PlacementGrid.cs:97) returns origin.y verbatim, so any non-zero
        // here would mean a mis-configured origin — surfaced rather than silently passed.
        private static void CheckOnGround(IReadOnlyList<PlacedStructureData> recipe, List<Violation> v)
        {
            float originY = GridOrigin.y;   // 0 by construction
            for (int i = 0; i < recipe.Count; i++)
            {
                var p = recipe[i];
                Vector3 world = CellToWorld(p.cellX, p.cellZ);
                if (Mathf.Abs(world.y - originY) > GroundEpsilon)
                    v.Add(new Violation(Tag(p, i),
                        $"ON-GROUND: seated Y {world.y:0.###} != ground {originY:0.###} (eps {GroundEpsilon})"));
            }
        }

        // ── CHECK 2: IN-BOUNDS ─────────────────────────────────────────────────
        // Every cell of the footprint must lie inside the buildable bounds. Mirrors
        // PlacementGrid.CanPlace bounds test (PlacementGrid.cs:124-131): margin m, the
        // block spans [cell, cell+footprint) which must satisfy minX..maxX (=W-m), exclusive.
        private static void CheckInBounds(IReadOnlyList<PlacedStructureData> recipe, List<Violation> v)
        {
            int m = Mathf.Max(0, EdgeMargin);
            int minX = m, minZ = m, maxX = GridWidth - m, maxZ = GridHeight - m;
            for (int i = 0; i < recipe.Count; i++)
            {
                var p = recipe[i];
                Vector2Int fp = FootprintCellsFor(p.itemId);
                int x0 = p.cellX, z0 = p.cellZ;
                int x1 = p.cellX + fp.x - 1, z1 = p.cellZ + fp.y - 1;
                if (x0 < minX || z0 < minZ || x1 >= maxX || z1 >= maxZ)
                    v.Add(new Violation(Tag(p, i),
                        $"IN-BOUNDS: footprint cells [{x0}..{x1}],[{z0}..{z1}] (size {fp.x}x{fp.y}) " +
                        $"outside buildable [{minX}..{maxX - 1}],[{minZ}..{maxZ - 1}]"));
            }
        }

        // ── CHECK 3: NO-OVERLAP ────────────────────────────────────────────────
        // No two footprints may share a cell. Mirrors PlacementGrid.Occupy/CanPlace
        // cell-occupancy (each placement claims [cell, cell+footprint) cells). AABB on
        // the integer cell block — exact, no float fuzz. First claimer wins; any later
        // placement touching a claimed cell is the violation (both ids named).
        private static void CheckNoOverlap(IReadOnlyList<PlacedStructureData> recipe, List<Violation> v)
        {
            var owner = new Dictionary<Vector2Int, string>();
            for (int i = 0; i < recipe.Count; i++)
            {
                var p = recipe[i];
                Vector2Int fp = FootprintCellsFor(p.itemId);
                bool flagged = false;
                for (int dx = 0; dx < fp.x && !flagged; dx++)
                {
                    for (int dz = 0; dz < fp.y; dz++)
                    {
                        var cell = new Vector2Int(p.cellX + dx, p.cellZ + dz);
                        if (owner.TryGetValue(cell, out string other))
                        {
                            v.Add(new Violation(Tag(p, i),
                                $"NO-OVERLAP: cell ({cell.x},{cell.y}) already claimed by '{other}'"));
                            flagged = true;   // one report per placement is enough
                            break;
                        }
                    }
                }
                // Claim this placement's cells (even if flagged, so we don't double-count
                // the same pair from the other side).
                for (int dx = 0; dx < fp.x; dx++)
                    for (int dz = 0; dz < fp.y; dz++)
                        owner[new Vector2Int(p.cellX + dx, p.cellZ + dz)] = Tag(p, i);
            }
        }

        // ── CHECK 4: ORIENTATION ───────────────────────────────────────────────
        // The id must resolve to a real CatalogEntry, that entry must name a visual
        // (visualPrefabPath, the LOOK — StructureFactory.Create:69), and discrete yaw
        // must be a valid 0..3 step (PlacedStructureData: "World yaw = yawSteps * 90").
        // We do NOT fail on a missing OrientationFix: per CatalogEntry's doc + Structure-
        // Factory:90, only manual=true fixes are applied and most assets are authored
        // upright (orientation == null is the NORMAL, valid case) — flagging it would be
        // a false positive. A non-null fix with a malformed euler array IS flagged.
        private static void CheckOrientation(IReadOnlyList<PlacedStructureData> recipe, List<Violation> v)
        {
            for (int i = 0; i < recipe.Count; i++)
            {
                var p = recipe[i];
                var entry = CatalogRegistry.Get(p.itemId);
                if (entry == null)
                {
                    v.Add(new Violation(Tag(p, i),
                        $"ORIENTATION: id '{p.itemId}' not in CatalogRegistry (no prefab resolves)"));
                    continue;
                }
                if (entry.kind != EntryKind.Composite && string.IsNullOrEmpty(entry.visualPrefabPath))
                    v.Add(new Violation(Tag(p, i),
                        $"ORIENTATION: '{p.itemId}' has no visualPrefabPath (no LOOK to skin)"));

                if (p.yawSteps < 0 || p.yawSteps > 3)
                    v.Add(new Violation(Tag(p, i),
                        $"ORIENTATION: yawSteps {p.yawSteps} out of 0..3 (yaw = steps*90)"));

                if (entry.orientation != null && entry.orientation.manual)
                {
                    var o = entry.orientation;
                    if (o.euler == null || o.euler.Length != 3)
                        v.Add(new Violation(Tag(p, i),
                            $"ORIENTATION: '{p.itemId}' manual OrientationFix has malformed euler[] " +
                            $"(len {(o.euler == null ? 0 : o.euler.Length)}, expected 3)"));
                    if (o.scale <= 0f)
                        v.Add(new Violation(Tag(p, i),
                            $"ORIENTATION: '{p.itemId}' manual OrientationFix scale {o.scale} <= 0"));
                }
            }
        }

        // ── CHECK 5: GATE-LANE CLEAR ───────────────────────────────────────────
        // Nothing may block a gate's spawn→Heart lane. Gates aren't in the recipe and
        // the Editor asmdef can't reference the Village Gate type, so we find gates in
        // the ACTIVE SCENE by name ("Gate") and replicate BuildModeController.
        // IsTooCloseToGate's lane math (BuildModeController.cs:591-624): the lane is the
        // gate OPENING (shorter horizontal span + clearance) extended along the gate→Heart
        // (origin) axis by laneReach. We test each footprint's world AABB against it.
        // SKIPPED (reported, not failed) when no scene / no gate object is found.
        private static void CheckGateLane(IReadOnlyList<PlacedStructureData> recipe, List<Violation> v)
        {
            List<Bounds> gateBounds = FindGateBoundsInActiveScene();
            if (gateBounds.Count == 0)
            {
                Debug.LogWarning("[LayoutValidator] GATE-LANE: no gate object found in the active " +
                                 "scene — check SKIPPED. (Open the village scene with its gates to " +
                                 "run the spawn→Heart corridor check.)");
                return;
            }

            foreach (var gb in gateBounds)
            {
                Bounds lane = GateLane(gb);
                for (int i = 0; i < recipe.Count; i++)
                {
                    var p = recipe[i];
                    Vector2Int fp = FootprintCellsFor(p.itemId);
                    Bounds foot = FootprintWorldBounds(p.cellX, p.cellZ, fp, GridOrigin.y);
                    if (OverlapsXZ(foot, lane))
                        v.Add(new Violation(Tag(p, i),
                            $"GATE-LANE: footprint blocks the spawn→Heart corridor of gate at " +
                            $"({gb.center.x:0.#},{gb.center.z:0.#})"));
                }
            }
        }

        // ── CHECK 6: ROUND-TRIP ────────────────────────────────────────────────
        // Serialize→deserialize the recipe through the same JSON the save layer uses
        // (PlacedStructureData is [Serializable], JSON-friendly per its header). A
        // mismatch means a field drops/aliases on persist — the recipe is the server-
        // replayable unit, so this must be lossless. Cheap, so always run.
        private static void CheckRoundTrip(IReadOnlyList<PlacedStructureData> recipe, List<Violation> v)
        {
            for (int i = 0; i < recipe.Count; i++)
            {
                var p = recipe[i];
                string json = JsonUtility.ToJson(p);
                PlacedStructureData rt = JsonUtility.FromJson<PlacedStructureData>(json);
                if (rt.itemId != p.itemId || rt.cellX != p.cellX || rt.cellZ != p.cellZ ||
                    rt.yawSteps != p.yawSteps || rt.level != p.level ||
                    !Mathf.Approximately(rt.yawOffset, p.yawOffset))
                    v.Add(new Violation(Tag(p, i),
                        $"ROUND-TRIP: recipe field drift after JSON serialize→deserialize ({json})"));
            }
        }

        // ── Replicated grid math (lock-step with PlacementGrid) ────────────────

        // PlacementGrid.CellToWorld (PlacementGrid.cs:97): cell centre, Y = origin.y.
        private static Vector3 CellToWorld(int cx, int cz)
        {
            Vector3 o = GridOrigin;
            return new Vector3(o.x + (cx + 0.5f) * CellSize, o.y, o.z + (cz + 0.5f) * CellSize);
        }

        // PlacementGrid.FootprintCells (PlacementGrid.cs:179) over the entry's
        // placement.footprint metres. Mirrors the loader/validity path which uses
        // MeasureUprightFootprintMetres (the corrected mesh) at runtime — that needs a
        // live skinned mesh we can't build headless, so we fall back to the AUTHORED
        // placement.footprint (the same fallback StructureFactory uses when a visual
        // can't be measured, StructureFactory.cs:207). Unknown id → 1 cell.
        private static Vector2Int FootprintCellsFor(string itemId)
        {
            float metres = 3f;
            var entry = CatalogRegistry.Get(itemId);
            if (entry != null && entry.repo != null && entry.repo.placement != null)
                metres = Mathf.Max(1f, entry.repo.placement.footprint);
            int cells = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0.01f, metres) / CellSize));
            return new Vector2Int(cells, cells);
        }

        // BuildModeController.FootprintWorldBounds (BuildModeController.cs:536).
        private static Bounds FootprintWorldBounds(int cx, int cz, Vector2Int fp, float y)
        {
            int fw = Mathf.Max(1, fp.x);
            int fh = Mathf.Max(1, fp.y);
            Vector3 minC = CellToWorld(cx, cz);
            Vector3 maxC = CellToWorld(cx + fw - 1, cz + fh - 1);
            Vector3 min = new Vector3(Mathf.Min(minC.x, maxC.x) - CellSize * 0.5f, y - 0.5f, Mathf.Min(minC.z, maxC.z) - CellSize * 0.5f);
            Vector3 max = new Vector3(Mathf.Max(minC.x, maxC.x) + CellSize * 0.5f, y + 0.5f, Mathf.Max(minC.z, maxC.z) + CellSize * 0.5f);
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        // BuildModeController.IsTooCloseToGate lane (BuildModeController.cs:600-620).
        private static Bounds GateLane(Bounds gb)
        {
            Vector3 c = gb.center;
            Vector3 toHeart = new Vector3(-c.x, 0f, -c.z);
            bool throughIsX = Mathf.Abs(toHeart.x) >= Mathf.Abs(toHeart.z);
            float openWidth = throughIsX ? gb.size.z : gb.size.x;
            float halfWidth = openWidth * 0.5f + GateClearance;
            float laneReach = GateLaneReach + GateClearance;

            var lane = new Bounds(c, Vector3.one);
            if (throughIsX)
                lane.SetMinMax(
                    new Vector3(c.x - laneReach, c.y - 0.5f, c.z - halfWidth),
                    new Vector3(c.x + laneReach, c.y + 0.5f, c.z + halfWidth));
            else
                lane.SetMinMax(
                    new Vector3(c.x - halfWidth, c.y - 0.5f, c.z - laneReach),
                    new Vector3(c.x + halfWidth, c.y + 0.5f, c.z + laneReach));
            return lane;
        }

        // BuildModeController.OverlapsXZ (BuildModeController.cs:649).
        private static bool OverlapsXZ(Bounds a, Bounds b) =>
            a.min.x < b.max.x && a.max.x > b.min.x &&
            a.min.z < b.max.z && a.max.z > b.min.z;

        // ── Scene / state helpers ──────────────────────────────────────────────

        // The active recipe = the live GameState.BaseLayout via reflection-free access.
        // GameStateService is in Core but is a runtime service that may be absent in the
        // editor (not playing). We read the static Instance.State.BaseLayout if present.
        private static List<PlacedStructureData> ResolveActiveRecipe()
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            return state != null ? state.BaseLayout : null;
        }

        // Find gate world-AABBs in the active scene by GameObject name ("Gate" — how
        // StructureFactory names a Gate entry / catalog displayName). Renderer bounds
        // first (displacement-trap safe, mirrors BuildModeController.TryWorldBounds),
        // collider fallback. Searches all root objects of all loaded scenes.
        private static List<Bounds> FindGateBoundsInActiveScene()
        {
            var result = new List<Bounds>();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.IndexOf("Gate", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (TryWorldBounds(t.gameObject, out Bounds b)) result.Add(b);
                    }
                }
            }
            return result;
        }

        private static bool TryWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            if (go == null) return false;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends != null && rends.Length > 0)
            {
                bounds = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
                return true;
            }
            var col = go.GetComponentInChildren<Collider>(true);
            if (col != null) { bounds = col.bounds; return true; }
            return false;
        }

        // A stable, human-readable tag for a placement in the violation log.
        private static string Tag(PlacedStructureData p, int index) =>
            $"[{index}]'{(string.IsNullOrEmpty(p.itemId) ? "<no-id>" : p.itemId)}'@({p.cellX},{p.cellZ})";
    }
}
