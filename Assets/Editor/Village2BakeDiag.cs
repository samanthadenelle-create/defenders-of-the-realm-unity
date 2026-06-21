// =============================================================================
//  Village2BakeDiag — read-only diagnostic for the Village2 navmesh bake.
//
//  WHY: the Village2 bake produces TWO coplanar OVERLAPPING navmesh sheets in
//  the courtyard (disconnected islands at ~the same XZ, y~0) and bakes NO mesh
//  on the keep platform. Per CLAUDE.md §12 we do NOT guess — we DUMP the bake
//  inputs (the surface settings + every collider/renderer feeding the bake) and
//  PROBE the resulting navmesh at courtyard XZ points to reveal stacked layers,
//  then read the data to root-cause.
//
//  This is READ-ONLY: it opens the scene, inspects, samples the EXISTING baked
//  navmesh, and logs. It never re-bakes, never edits, never saves.
//
//  NavMeshSurface is referenced directly (the DeNelle.Editor asmdef references
//  Unity.AI.Navigation, same as EnemyStrongholdBuilder). If that reference ever
//  breaks, swap the typed access for reflection on "Unity.AI.Navigation.NavMeshSurface".
// =============================================================================
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class Village2BakeDiag
    {
        private const string ScenePath = "Assets/Scenes/Village2.unity";
        private const string RootName  = "StrongholdRoot";
        private const string Tag       = "[V2BakeDiag]";

        // A collider/renderer is "floor-like" if it is wide in XZ, thin in Y, and low.
        private const float FloorMinXZ   = 8f;    // big horizontal footprint
        private const float FloorMaxY    = 2.0f;  // thin slab
        private const float FloorMaxTopY = 3.0f;  // sits low in the world

        // ---------------------------------------------------------------------
        // Gate-approach probe: name WHAT carves the ~4m no-navmesh strip in front
        // of the south MainGate (arrival navmesh ends at z~-18; wall/gate at z~-14).
        // Dumps every collider + NavMeshModifier intersecting the approach box, and
        // samples the navmesh along the gate centerline so we see exactly where it dies.
        [MenuItem("Defenders/Village2/Diagnose Gate Approach")]
        public static void DumpGateApproach()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"{Tag} ==== GATE APPROACH PROBE (south MainGate) ====");

            // Approach box: south gate centerline, from outside (z=-22) to inside the wall (z=-10).
            var boxMin = new Vector3(-8f, -2f, -22f);
            var boxMax = new Vector3( 8f, 10f, -10f);
            var box = new Bounds((boxMin + boxMax) * 0.5f, boxMax - boxMin);

            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (col == null) continue;
                if (!col.bounds.Intersects(box)) continue;
                var nm = col.GetComponent("Unity.AI.Navigation.NavMeshModifier") ?? col.GetComponent<NavMeshModifier>();
                string area = "(no modifier)";
                if (nm is NavMeshModifier mod) area = $"NavMeshModifier override={mod.overrideArea} area={mod.area}";
                Debug.Log($"{Tag}   COL '{PathGA(col.transform)}' type={col.GetType().Name} enabled={col.enabled} trigger={col.isTrigger} " +
                          $"center={col.bounds.center} size={col.bounds.size} | {area}");
            }

            // Sample the navmesh along the gate centerline (x=0) and a touch left (x=-4.7, where the path dead-ended).
            foreach (float xLine in new[] { 0f, -4.7f })
            {
                Debug.Log($"{Tag}   -- navmesh along x={xLine} (z -22..-10) --");
                for (float z = -22f; z <= -10f; z += 0.5f)
                {
                    bool on = NavMesh.SamplePosition(new Vector3(xLine, 0.2f, z), out NavMeshHit h, 1.0f, NavMesh.AllAreas);
                    Debug.Log($"{Tag}      x={xLine} z={z:F1}: {(on ? $"NAVMESH y={h.position.y:F2}" : "---- no navmesh ----")}");
                }
            }
            Debug.Log($"{Tag} ==== done ====");
        }

        private static string PathGA(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        [MenuItem("Defenders/Village2/Diagnose Bake Sources")]
        public static void Run()
        {
            Log("==================== Village2 bake-source diagnostic START ====================");
            try
            {
                var scene = OpenScene();
                if (!scene.IsValid())
                {
                    Err("Could not open scene; aborting (no re-bake, no edits).");
                    return;
                }

                var root = FindRoot();
                if (root == null)
                    Warn($"No '{RootName}' found in scene. Will still dump scene-wide surfaces/colliders.");

                DumpSurfaces();
                DumpColliders(root);
                DumpRenderers(root);
                ProbeCourtyardOverlap();
                DumpKeyHeights(root);
            }
            catch (System.Exception ex)
            {
                // Defensive: never throw out of a diagnostic.
                Err($"UNEXPECTED top-level exception (logged, not thrown): {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            Log("==================== Village2 bake-source diagnostic END ====================");
        }

        // ---------------------------------------------------------------------
        //  Scene + root
        // ---------------------------------------------------------------------
        private static Scene OpenScene()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Log($"Opened scene (read-only, NO re-bake): {ScenePath}  valid={scene.IsValid()} loaded={scene.isLoaded}");
                return scene;
            }
            catch (System.Exception ex)
            {
                Err($"OpenScene failed: {ex.Message}");
                return default;
            }
        }

        private static GameObject FindRoot()
        {
            try
            {
                foreach (var go in EnumerateAllRoots())
                    if (go != null && go.name == RootName)
                        return go;
            }
            catch (System.Exception ex) { Warn($"FindRoot: {ex.Message}"); }
            return null;
        }

        private static IEnumerable<GameObject> EnumerateAllRoots()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) yield break;
            foreach (var go in scene.GetRootGameObjects())
                yield return go;
        }

        // ---------------------------------------------------------------------
        //  1/2.  NavMeshSurface settings
        // ---------------------------------------------------------------------
        private static void DumpSurfaces()
        {
            Log("---- SECTION: NavMeshSurface settings ----");
            NavMeshSurface[] surfaces;
            try
            {
                surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            catch (System.Exception ex)
            {
                Err($"FindObjectsByType<NavMeshSurface> failed: {ex.Message}");
                return;
            }

            if (surfaces == null || surfaces.Length == 0)
            {
                Warn("No NavMeshSurface components found in scene.");
                return;
            }
            Log($"Found {surfaces.Length} NavMeshSurface component(s).");

            foreach (var s in surfaces)
            {
                if (s == null) continue;
                try
                {
                    Log($"  --- Surface on '{Path(s.transform)}' ---");
                    Log($"    collectObjects = {s.collectObjects}  (All=whole scene, Volume=bounds, Children=this hierarchy)");
                    Log($"    useGeometry    = {s.useGeometry}  (RenderMeshes vs PhysicsColliders — which inputs feed the bake)");
                    Log($"    layerMask      = {s.layerMask.value} (int)  -> layers: {LayerNames(s.layerMask)}");
                    Log($"    defaultArea    = {s.defaultArea}");
                    Log($"    agentTypeID    = {s.agentTypeID}");
                    Log($"    center/size    = {s.center} / {s.size} (only used when collectObjects=Volume)");
                    Log($"    overrideVoxelSize = {s.overrideVoxelSize}  voxelSize = {SafeVoxel(s)}");
                    Log($"    overrideTileSize  = {s.overrideTileSize}   tileSize  = {SafeTile(s)}");
                    Log($"    minRegionArea     = {s.minRegionArea}  (smaller islands than this are discarded; bears on the keep-platform 'no mesh' case)");
                    Log($"    buildHeightMesh   = {s.buildHeightMesh}");
                    Log($"    navMeshData       = {(s.navMeshData == null ? "NULL (nothing baked!)" : s.navMeshData.name)}");

                    DumpAgentSettings(s.agentTypeID);
                }
                catch (System.Exception ex)
                {
                    Err($"  Surface dump failed: {ex.Message}");
                }
            }
        }

        private static void DumpAgentSettings(int agentTypeID)
        {
            try
            {
                var settings = NavMesh.GetSettingsByID(agentTypeID);
                Log($"    -- NavMeshBuildSettings (agentTypeID {agentTypeID}) --");
                Log($"       agentRadius = {settings.agentRadius}  (key suspect: a large radius erodes thin slabs / narrow ledges -> keep platform may vanish; mismatched radius vs a stacked floor splits islands)");
                Log($"       agentHeight = {settings.agentHeight}");
                Log($"       agentSlope  = {settings.agentSlope}  (if the keep ramp/platform exceeds this, no mesh bakes there)");
                Log($"       agentClimb  = {settings.agentClimb}  (step height — if two stacked floors differ by MORE than this, they bake as DISCONNECTED islands instead of one merged sheet — prime overlap suspect)");
                Log($"       minRegionArea (settings) = {settings.minRegionArea}");
                Log($"       overrideVoxelSize = {settings.overrideVoxelSize}  voxelSize = {settings.voxelSize}");
                Log($"       overrideTileSize  = {settings.overrideTileSize}   tileSize  = {settings.tileSize}");
            }
            catch (System.Exception ex)
            {
                Warn($"    GetSettingsByID({agentTypeID}) failed: {ex.Message}");
            }
        }

        private static string SafeVoxel(NavMeshSurface s)
        {
            try { return s.voxelSize.ToString(); } catch { return "?"; }
        }
        private static string SafeTile(NavMeshSurface s)
        {
            try { return s.tileSize.ToString(); } catch { return "?"; }
        }

        // ---------------------------------------------------------------------
        //  3.  Colliders under root (floor-like first)
        // ---------------------------------------------------------------------
        private static void DumpColliders(GameObject root)
        {
            Log("---- SECTION: Colliders under StrongholdRoot (FLOOR-LIKE listed first) ----");
            if (root == null)
            {
                Warn("No root; skipping collider dump.");
                return;
            }
            Collider[] cols;
            try { cols = root.GetComponentsInChildren<Collider>(true); }
            catch (System.Exception ex) { Err($"GetComponentsInChildren<Collider> failed: {ex.Message}"); return; }

            if (cols == null || cols.Length == 0) { Warn("No colliders under root."); return; }

            var records = new List<BoundsRec>();
            foreach (var c in cols)
            {
                if (c == null) continue;
                try
                {
                    var b = c.bounds;
                    bool floor = IsFloorLike(b);
                    records.Add(new BoundsRec
                    {
                        path    = Path(c.transform),
                        kind    = c.GetType().Name,
                        enabled = c.enabled,
                        trigger = c.isTrigger,
                        layer   = LayerMask.LayerToName(c.gameObject.layer),
                        navStat = IsNavStatic(c.gameObject),
                        center  = b.center,
                        size    = b.size,
                        floor   = floor
                    });
                }
                catch (System.Exception ex) { Warn($"  collider '{SafeName(c)}': {ex.Message}"); }
            }

            // Floor-like first, then by ascending Y so stacked sheets read top-to-bottom.
            var sorted = records.OrderByDescending(r => r.floor).ThenBy(r => r.center.y).ToList();

            int floorCount = sorted.Count(r => r.floor);
            Log($"Total colliders: {records.Count}  |  FLOOR-LIKE: {floorCount}");
            if (floorCount > 1)
                Warn($"MULTIPLE floor-like colliders ({floorCount}) detected — candidate for the stacked/overlapping navmesh sheets. Compare their center.y below.");

            foreach (var r in sorted)
                Log("  " + Format(r));
        }

        // ---------------------------------------------------------------------
        //  4.  Renderers under root (in case useGeometry == RenderMeshes)
        // ---------------------------------------------------------------------
        private static void DumpRenderers(GameObject root)
        {
            Log("---- SECTION: MeshRenderers under StrongholdRoot (FLOOR-LIKE listed first; relevant if useGeometry=RenderMeshes) ----");
            if (root == null) { Warn("No root; skipping renderer dump."); return; }
            MeshRenderer[] rends;
            try { rends = root.GetComponentsInChildren<MeshRenderer>(true); }
            catch (System.Exception ex) { Err($"GetComponentsInChildren<MeshRenderer> failed: {ex.Message}"); return; }
            if (rends == null || rends.Length == 0) { Warn("No MeshRenderers under root."); return; }

            var records = new List<BoundsRec>();
            foreach (var rend in rends)
            {
                if (rend == null) continue;
                try
                {
                    var b = rend.bounds;
                    if (!IsFloorLike(b)) continue; // only floor-like renderers matter here
                    records.Add(new BoundsRec
                    {
                        path    = Path(rend.transform),
                        kind    = "MeshRenderer",
                        enabled = rend.enabled,
                        trigger = false,
                        layer   = LayerMask.LayerToName(rend.gameObject.layer),
                        navStat = IsNavStatic(rend.gameObject),
                        center  = b.center,
                        size    = b.size,
                        floor   = true
                    });
                }
                catch (System.Exception ex) { Warn($"  renderer '{SafeName(rend)}': {ex.Message}"); }
            }

            Log($"FLOOR-LIKE MeshRenderers: {records.Count}");
            foreach (var r in records.OrderBy(r => r.center.y))
                Log("  " + Format(r));
        }

        // ---------------------------------------------------------------------
        //  5.  Probe the baked navmesh for stacked layers at courtyard XZ
        // ---------------------------------------------------------------------
        private static void ProbeCourtyardOverlap()
        {
            Log("---- SECTION: Courtyard navmesh probe (reveals stacked layers at same XZ) ----");
            Vector2[] xz =
            {
                new Vector2(0f, 0f),
                new Vector2(10f, -10f),
                new Vector2(-10f, 10f),
                new Vector2(0f, -20f),
                new Vector2(20f, -38f), // near the recipe target mentioned in the builder (20.6,?,-38.3)
            };

            foreach (var p in xz)
            {
                try
                {
                    var found = new List<float>();
                    // Sample from HIGH down and from LOW up so we catch BOTH stacked sheets.
                    SampleAndCollect(new Vector3(p.x, 5f, p.y),  found);
                    SampleAndCollect(new Vector3(p.x, -1f, p.y), found);
                    // Also sample at a few discrete heights to separate close-but-distinct sheets.
                    SampleAndCollect(new Vector3(p.x, 0.0f, p.y),  found);
                    SampleAndCollect(new Vector3(p.x, 1.0f, p.y),  found);
                    SampleAndCollect(new Vector3(p.x, 3.0f, p.y),  found);

                    var distinct = Distinct(found, 0.05f);
                    if (distinct.Count == 0)
                        Log($"  XZ({p.x},{p.y}): NO navmesh found nearby (gap / unbaked here).");
                    else
                    {
                        string ys = string.Join(", ", distinct.Select(v => v.ToString("0.000")));
                        string flag = distinct.Count > 1 ? "  <-- MULTIPLE distinct navmesh heights = STACKED SHEETS" : "";
                        Log($"  XZ({p.x},{p.y}): {distinct.Count} navmesh height(s) [{ys}]{flag}");
                    }
                }
                catch (System.Exception ex) { Warn($"  probe XZ({p.x},{p.y}) failed: {ex.Message}"); }
            }
        }

        private static void SampleAndCollect(Vector3 from, List<float> found)
        {
            try
            {
                if (NavMesh.SamplePosition(from, out var hit, 4f, NavMesh.AllAreas))
                    found.Add(hit.position.y);
            }
            catch (System.Exception ex) { Warn($"    SamplePosition from {from}: {ex.Message}"); }
        }

        private static List<float> Distinct(List<float> values, float tol)
        {
            var outv = new List<float>();
            foreach (var v in values.OrderBy(x => x))
                if (!outv.Any(o => Mathf.Abs(o - v) <= tol))
                    outv.Add(v);
            return outv;
        }

        // ---------------------------------------------------------------------
        //  6.  Key object heights
        // ---------------------------------------------------------------------
        private static void DumpKeyHeights(GameObject root)
        {
            Log("---- SECTION: Key object world-Y (floor / ground / foundation / keep platform) ----");
            if (root == null) { Warn("No root; skipping key-height dump."); return; }
            string[] needles = { "Floor_Stronghold", "Floor", "Ground", "Foundation", "Platform_Keep", "Platform", "Keep" };
            try
            {
                var all = root.GetComponentsInChildren<Transform>(true);
                foreach (var needle in needles)
                {
                    bool any = false;
                    foreach (var t in all)
                    {
                        if (t == null || t.name.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                        any = true;
                        float topY = TopY(t.gameObject);
                        Log($"  '{needle}' match: '{Path(t)}'  worldPos.y={t.position.y:0.000}  rendererTopY={topY:0.000}");
                    }
                    if (!any) Log($"  '{needle}': (no match under root)");
                }
            }
            catch (System.Exception ex) { Err($"DumpKeyHeights failed: {ex.Message}"); }
        }

        private static float TopY(GameObject go)
        {
            try
            {
                var rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends == null || rends.Length == 0) return float.NaN;
                float max = float.NegativeInfinity;
                foreach (var r in rends)
                    if (r != null) max = Mathf.Max(max, r.bounds.max.y);
                return max;
            }
            catch { return float.NaN; }
        }

        // ---------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------
        private struct BoundsRec
        {
            public string path, kind, layer;
            public bool enabled, trigger, navStat, floor;
            public Vector3 center, size;
        }

        private static string Format(BoundsRec r)
        {
            string tag = r.floor ? "[FLOOR?] " : "";
            return $"{tag}{r.kind}  '{r.path}'  enabled={r.enabled} trigger={r.trigger} layer='{r.layer}' " +
                   $"navStatic={r.navStat}  centerY={r.center.y:0.000}  size=({r.size.x:0.0},{r.size.y:0.0},{r.size.z:0.0})";
        }

        private static bool IsFloorLike(Bounds b)
        {
            return b.size.x >= FloorMinXZ
                && b.size.z >= FloorMinXZ
                && b.size.y <= FloorMaxY
                && b.center.y <= FloorMaxTopY;
        }

        private static bool IsNavStatic(GameObject go)
        {
            try
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                return (flags & StaticEditorFlags.NavigationStatic) != 0;
            }
            catch { return false; }
        }

        private static string LayerNames(LayerMask mask)
        {
            var names = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                if ((mask.value & (1 << i)) == 0) continue;
                string n = LayerMask.LayerToName(i);
                names.Add(string.IsNullOrEmpty(n) ? $"layer{i}" : n);
            }
            return names.Count == 0 ? "(none)" : string.Join(", ", names);
        }

        private static string Path(Transform t)
        {
            if (t == null) return "(null)";
            var sb = new System.Text.StringBuilder(t.name);
            var p = t.parent;
            int guard = 0;
            while (p != null && guard++ < 64)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }

        private static string SafeName(Object o)
        {
            try { return o == null ? "(null)" : o.name; } catch { return "(?)"; }
        }

        private static void Log(string m)  => Debug.Log($"{Tag} {m}");
        private static void Warn(string m) => Debug.LogWarning($"{Tag} {m}");
        private static void Err(string m)  => Debug.LogError($"{Tag} {m}");
    }
}
