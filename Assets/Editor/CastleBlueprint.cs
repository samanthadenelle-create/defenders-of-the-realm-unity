// =============================================================================
// CastleBlueprint — SCIENTIFIC coordinate extractor for MainCastle_Hall.
// Reads the ACTUAL placed geometry (no extrapolation) and dumps every key point's
// exact world coordinates, bounds, and facing so the castle has a deterministic
// spatial blueprint — the spatial source-of-truth, alongside the code MASTER_CATALOG.
// Also resolves the exit bug by reporting CanStreamedLevelBeLoaded for every build
// scene (artifact-vs-real test) and the baked NavMesh extents.
//
// Batchmode: -executeMethod DeNelle.Editor.CastleBlueprint.Extract
// Writes Builds/castle-blueprint.txt (raw) + logs each line with a [BP] prefix.
// Read-only on the scene (opens, never saves).
// =============================================================================
using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Editor
{
    public static class CastleBlueprint
    {
        private const string ScenePath = "Assets/Scenes/MainCastle_Hall.unity";
        private static StringBuilder _sb;

        public static void Extract()
        {
            _sb = new StringBuilder();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Line("===== CASTLE BLUEPRINT — MainCastle_Hall (exact, measured) =====");
            Line("origin (Heart of Elarion) = (0, 0, 0)");

            // ── SCENE-LOADABLE TEST (resolves the exit bug) ─────────────────────
            Line("");
            Line("-- scene loadable (CanStreamedLevelBeLoaded) — exit-bug test --");
            foreach (var s in new[] { "OuterWorld", "Village2", "MainCastle_Hall", "ATBBattle", "Dungeon_HealersCottage" })
                Line($"   {s,-22} = {Application.CanStreamedLevelBeLoaded(s)}");

            // ── GATES (the 4 entrances) ─────────────────────────────────────────
            Line("");
            Line("-- gates (4 entrances): center, bounds, opening axis --");
            foreach (var side in new[] { "South", "West", "North", "East" })
            {
                var root = GameObject.Find("CastleSide_" + side);
                if (root == null) { Line($"   {side}: CastleSide_{side} NOT FOUND"); continue; }
                var gate = FindChildContaining(root.transform, "Gate");
                if (gate == null) { Line($"   {side}: no Gate child"); continue; }
                if (TryBounds(gate, out Bounds b))
                {
                    bool xWide = b.size.x >= b.size.z;
                    float openW = xWide ? b.size.x : b.size.z;
                    float depth = xWide ? b.size.z : b.size.x;
                    string axis = xWide ? "X (travel Z)" : "Z (travel X)";
                    Line($"   {side,-6} center=({b.center.x:F2},{b.center.y:F2},{b.center.z:F2}) " +
                         $"openingWidth={openW:F2} depth={depth:F2} floorY={b.min.y:F2} topY={b.max.y:F2} openingAxis={axis}");
                }
            }

            // ── WALLS + TOWERS per side ─────────────────────────────────────────
            Line("");
            Line("-- walls + corner towers (per side) --");
            foreach (var side in new[] { "South", "West", "North", "East" })
            {
                var root = GameObject.Find("CastleSide_" + side);
                if (root == null) continue;
                foreach (Transform child in root.transform)
                {
                    if (child.name.IndexOf("Gate", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (TryBounds(child, out Bounds b))
                        Line($"   {side}/{child.name,-20} pos=({child.position.x:F2},{child.position.y:F2},{child.position.z:F2}) " +
                             $"size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) yaw={child.eulerAngles.y:F1}");
                }
            }

            // ── KEY STRUCTURES ──────────────────────────────────────────────────
            Line("");
            Line("-- key structures --");
            DumpNamed("MainKeep_CastleWithTwoLevels_Home", "keep");
            DumpNamed("GrandStair_CourtyardToBattlements", "grand stair");
            DumpNamed("CourtyardFloor_Nav", "courtyard floor (nav)");
            DumpNamed("UpperBattlements_Nav", "upper battlements (nav)");
            DumpNamed("KeepInterior_Nav", "keep interior (nav)");
            foreach (var side in new[] { "South", "West", "North", "East" })
                DumpNamed("GateExit_" + side + "_Nav", "gate exit strip " + side);

            // ── KEY POINTS (spawn, exit seam) ───────────────────────────────────
            Line("");
            Line("-- key points --");
            var spawn = GameObject.Find("HeroStartPoint_PlayerSpawn") ?? GameObject.Find("Capsule");
            Line("   spawn = " + (spawn != null ? Pos(spawn.transform) : "<none>"));
            var hero = GameObject.Find("Hero (Blaise)");
            Line("   hero(edit) = " + (hero != null ? Pos(hero.transform) + " tag=" + SafeTag(hero) : "<runtime-spawned>"));

            var transType = FindType("DeNelle.Village.SceneTransitionTrigger");
            if (transType != null)
            {
                var comps = UnityEngine.Object.FindObjectsByType(transType, FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in comps)
                {
                    var mb = c as MonoBehaviour; if (mb == null) continue;
                    string tgt = transType.GetField("targetSceneName")?.GetValue(mb) as string ?? "?";
                    object rad = transType.GetField("ProximityRadius")?.GetValue(mb);
                    object wto = transType.GetField("targetPosition")?.GetValue(mb);
                    Line($"   exitSeam '{mb.name}' pos={Pos(mb.transform)} active={mb.gameObject.activeInHierarchy} radius={rad} target={tgt} warpTo={wto}");
                }
            }

            // ── NAVMESH EXTENTS ─────────────────────────────────────────────────
            Line("");
            Line("-- baked NavMesh extents (committed surface) --");
            ReloadCommittedNavMesh();
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices != null && tri.vertices.Length > 0)
            {
                Vector3 mn = tri.vertices[0], mx = tri.vertices[0];
                foreach (var v in tri.vertices) { mn = Vector3.Min(mn, v); mx = Vector3.Max(mx, v); }
                Line($"   navmesh verts={tri.vertices.Length} tris={tri.indices.Length / 3} " +
                     $"min=({mn.x:F1},{mn.y:F1},{mn.z:F1}) max=({mx.x:F1},{mx.y:F1},{mx.z:F1})");
                // does the navmesh reach each gate's OUTSIDE?
                foreach (var side in new[] { "South", "West", "North", "East" })
                {
                    var root = GameObject.Find("CastleSide_" + side);
                    var gate = root != null ? FindChildContaining(root.transform, "Gate") : null;
                    if (gate != null && TryBounds(gate, out Bounds gb))
                    {
                        Vector3 outside = gb.center + (gb.center.normalized * 8f); outside.y = 0.2f;
                        bool on = NavMesh.SamplePosition(outside, out NavMeshHit h, 1.5f, NavMesh.AllAreas);
                        Line($"   {side}: 8m-outside {V(outside)} onNavMesh(tol1.5)={on}" + (on ? " @" + V(h.position) : ""));
                    }
                }
            }
            else Line("   navmesh triangulation EMPTY (no baked surface loaded)");

            Line("");
            Line("===== END BLUEPRINT =====");

            string outPath = "Builds/castle-blueprint.txt";
            System.IO.Directory.CreateDirectory("Builds");
            System.IO.File.WriteAllText(outPath, _sb.ToString());
            Debug.Log("[BP] wrote " + outPath + "\n" + _sb.ToString());
        }

        // STEP 1 of the spatial-contract ladder: does the castle's warp-landing point
        // actually sit on OuterWorld's navmesh? If not, the seam fires but drops the hero
        // off-mesh -> "nothing happens" after transition. Reports the nearest valid point,
        // which is what OuterWorld's blueprint entry SHOULD declare.
        public static void VerifyOuterWorldEntry()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/OuterWorld.unity", OpenSceneMode.Single);
            int n = ReloadCommittedNavMesh();
            Debug.Log("[OWEntry] opened OuterWorld — navmesh surfaces with committed data = " + n);

            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices != null && tri.vertices.Length > 0)
            {
                Vector3 mn = tri.vertices[0], mx = tri.vertices[0];
                foreach (var v in tri.vertices) { mn = Vector3.Min(mn, v); mx = Vector3.Max(mx, v); }
                Debug.Log($"[OWEntry] OuterWorld navmesh verts={tri.vertices.Length} tris={tri.indices.Length / 3} min={V(mn)} max={V(mx)}");
            }
            else Debug.Log("[OWEntry] OuterWorld navmesh EMPTY — there is NO baked surface here (warp lands off-mesh -> stuck). SCORE: FAIL.");

            Vector3 entry = new Vector3(0f, 0.5f, -80f); // the castle seam's warpTo
            bool on = NavMesh.SamplePosition(entry, out NavMeshHit h, 3f, NavMesh.AllAreas);
            if (on)
                Debug.Log($"[OWEntry] SCORE: PASS — warp entry {V(entry)} is on navmesh; nearest {V(h.position)} dist={Vector3.Distance(entry, h.position):F2}m. Landing is walkable.");
            else
            {
                Debug.Log($"[OWEntry] SCORE: FAIL — warp entry {V(entry)} is NOT on navmesh within 3m (hero lands off-mesh -> stuck).");
                bool on20 = NavMesh.SamplePosition(entry, out NavMeshHit h20, 25f, NavMesh.AllAreas);
                if (on20)
                    Debug.Log($"[OWEntry] nearest walkable point within 25m = {V(h20.position)} (dist={Vector3.Distance(entry, h20.position):F2}m) — THIS is the entry coord the blueprint should declare.");
                else
                    Debug.Log("[OWEntry] no navmesh within 25m of the entry either — OuterWorld needs a nav bake at the castle-approach (run OuterWorldNavBake).");
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────────
        private static void Line(string s) { _sb.AppendLine(s); Debug.Log("[BP] " + s); }
        private static string V(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";
        private static string Pos(Transform t) => V(t.position);

        private static void DumpNamed(string name, string label)
        {
            var go = GameObject.Find(name);
            if (go == null) { Line($"   {label,-26} = <not found> ({name})"); return; }
            if (TryBounds(go.transform, out Bounds b))
                Line($"   {label,-26} pos={Pos(go.transform)} boundsCenter={V(b.center)} size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2})");
            else
                Line($"   {label,-26} pos={Pos(go.transform)} (no renderers)");
        }

        private static Transform FindChildContaining(Transform root, string token)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return t;
            return null;
        }

        private static bool TryBounds(Transform t, out Bounds b)
        {
            b = new Bounds(t.position, Vector3.zero);
            var rends = t.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return false;
            b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return true;
        }

        private static string SafeTag(GameObject go)
        {
            try { return go.tag; } catch { return "<untagged>"; }
        }

        private static int ReloadCommittedNavMesh()
        {
            NavMesh.RemoveAllNavMeshData();
            var surfType = FindType("Unity.AI.Navigation.NavMeshSurface");
            if (surfType == null) return 0;
            var dataProp = surfType.GetProperty("navMeshData");
            var surfaces = UnityEngine.Object.FindObjectsByType(surfType, FindObjectsSortMode.None);
            int n = 0;
            foreach (var s in surfaces)
            {
                var data = dataProp != null ? dataProp.GetValue(s) as NavMeshData : null;
                if (data != null) { NavMesh.AddNavMeshData(data); n++; }
            }
            return n;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
