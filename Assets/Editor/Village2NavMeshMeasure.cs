// Village2NavMeshMeasure — measure Village2's navmesh ISLANDS using the REAL navmesh the
// scene/runtime uses (the NavMeshSurface-baked asset) WITHOUT re-baking. Unlike
// Village2IslandMap (which calls the LEGACY NavMeshBuilder.BuildNavMesh and overwrites the
// in-memory navmesh with a different bake), this tool reads whatever navmesh is currently
// active after the scene opens — i.e. the NavMeshSurface bake the game actually uses.
// Grid-samples the footprint, clusters samples by path-connectivity, reports each island's
// size + centroid + bounds, each key marker's island, and explicit reachability paths
// between key markers. READ-ONLY (never rebuilds, never writes, never throws).
// Run: DeNelle.Editor.Village2NavMeshMeasure.Run
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Editor
{
    public static class Village2NavMeshMeasure
    {
        private const string ScenePath = "Assets/Scenes/Village2.unity";

        [MenuItem("Defenders/Village2/Measure NavMesh (existing bake)")]
        public static void Run()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (System.Exception e)
            {
                Log("FAILED to open scene '" + ScenePath + "': " + e.Message);
                return;
            }

            Log("=== Village2 navmesh measure (EXISTING bake, NO rebuild) ===");

            // Do NOT call NavMeshBuilder.BuildNavMesh() — that's the LEGACY baker and would
            // overwrite the in-memory navmesh with a different bake than the game uses.
            // The scene-open loads the NavMeshSurface baked data automatically; we just read it.
            ReportSurfaces();
            Log("reading EXISTING NavMeshSurface bake (no rebuild)");

            // Collect on-navmesh sample points over the footprint.
            var pts = new List<Vector3>();
            for (float x = -50f; x <= 50f; x += 3f)
            {
                for (float z = -60f; z <= 35f; z += 3f)
                {
                    NavMeshHit h;
                    if (SampleSafe(new Vector3(x, 2f, z), 3f, out h))
                        pts.Add(h.position);
                }
            }
            Log($"on-navmesh sample points: {pts.Count}");

            if (pts.Count == 0)
            {
                Log("WARNING: zero on-navmesh sample points — either no bake loaded or footprint is off. Aborting island clustering.");
            }

            // Cluster by path-connectivity (assign each point to the first island it connects to).
            var islands = new List<List<Vector3>>();
            foreach (var p in pts)
            {
                bool placed = false;
                foreach (var isl in islands)
                {
                    if (PathComplete(isl[0], p))
                    {
                        isl.Add(p);
                        placed = true;
                        break;
                    }
                }
                if (!placed) islands.Add(new List<Vector3> { p });
            }
            islands.Sort((a, b) => b.Count.CompareTo(a.Count));

            Log($"ISLANDS: {islands.Count} (sorted by size)");
            int show = Mathf.Min(islands.Count, 8);
            for (int i = 0; i < show; i++)
            {
                var isl = islands[i];
                Vector3 c = Vector3.zero;
                var min = isl[0];
                var max = isl[0];
                foreach (var p in isl)
                {
                    c += p;
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                c /= isl.Count;
                Log($"  island[{i}] size={isl.Count,3}  centroid=({c.x:F1},{c.y:F1},{c.z:F1})  " +
                    $"bounds x[{min.x:F0}..{max.x:F0}] z[{min.z:F0}..{max.z:F0}]");
            }

            // Where are the key markers, and which island is each on?
            foreach (var n in new[] { "HeroStartPoint_PlayerSpawn", "Crossing_Village2Gate_Entry", "Crossing_Village2Gate_Dest", "Spawn_Keep", "Spawn_Chokepoint", "Spawn_Rear" })
            {
                var go = SafeFind(n);
                if (go == null)
                {
                    Log($"  marker '{n}': not found");
                    continue;
                }
                int isl = IslandOf(go.transform.position, islands);
                Log($"  marker '{n}' @ {go.transform.position} -> island[{(isl < 0 ? "OFF-MESH" : isl.ToString())}]");
            }

            // Explicit reachability between key marker pairs — the real acceptance signal.
            Log("--- reachability (NavMesh.CalculatePath status) ---");
            ReportReach("HeroStartPoint_PlayerSpawn", "Spawn_Chokepoint");
            ReportReach("HeroStartPoint_PlayerSpawn", "Spawn_Keep");
            ReportReach("Crossing_Village2Gate_Dest", "Spawn_Keep");

            Log("=== done. The crossing DEST should be on the same island as Spawn_Keep/Chokepoint, and reachability above should read PathComplete. ===");
        }

        private static void ReportSurfaces()
        {
            // Find any NavMeshSurface in the scene (Unity.AI.Navigation.NavMeshSurface) and
            // report it — but DO NOT rebuild it. It loads its baked data on scene open.
            // Resolved by name to avoid a hard compile dependency on the AI Navigation package.
            var t = System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (t == null)
            {
                Log("NavMeshSurface type not found (Unity.AI.Navigation package) — relying on whatever navmesh is active.");
                return;
            }
            Object[] surfaces = Object.FindObjectsByType(t, FindObjectsSortMode.None);
            Log($"NavMeshSurface components in scene: {(surfaces == null ? 0 : surfaces.Length)} (not rebuilt)");
        }

        private static bool SampleSafe(Vector3 pos, float radius, out NavMeshHit hit)
        {
            hit = default(NavMeshHit);
            try
            {
                return NavMesh.SamplePosition(pos, out hit, radius, NavMesh.AllAreas);
            }
            catch (System.Exception e)
            {
                Log("SamplePosition threw: " + e.Message);
                return false;
            }
        }

        private static bool PathComplete(Vector3 a, Vector3 b)
        {
            try
            {
                var path = new NavMeshPath();
                return NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            }
            catch (System.Exception e)
            {
                Log("CalculatePath threw: " + e.Message);
                return false;
            }
        }

        private static int IslandOf(Vector3 pos, List<List<Vector3>> islands)
        {
            NavMeshHit h;
            if (!SampleSafe(pos, 6f, out h)) return -1;
            for (int i = 0; i < islands.Count; i++)
            {
                if (PathComplete(islands[i][0], h.position))
                    return i;
            }
            return -1;
        }

        private static void ReportReach(string fromName, string toName)
        {
            var from = SafeFind(fromName);
            var to = SafeFind(toName);
            if (from == null || to == null)
            {
                Log($"  reach {fromName} -> {toName}: marker missing (from={(from == null ? "NULL" : "ok")}, to={(to == null ? "NULL" : "ok")})");
                return;
            }

            NavMeshHit hf, ht;
            if (!SampleSafe(from.transform.position, 6f, out hf) || !SampleSafe(to.transform.position, 6f, out ht))
            {
                Log($"  reach {fromName} -> {toName}: one or both endpoints OFF-MESH (cannot path)");
                return;
            }

            try
            {
                var path = new NavMeshPath();
                bool ok = NavMesh.CalculatePath(hf.position, ht.position, NavMesh.AllAreas, path);
                string status = ok ? path.status.ToString() : "PathInvalid (CalculatePath returned false)";
                Log($"  reach {fromName} -> {toName}: {status}");
            }
            catch (System.Exception e)
            {
                Log($"  reach {fromName} -> {toName}: threw {e.Message}");
            }
        }

        private static GameObject SafeFind(string name)
        {
            try
            {
                return GameObject.Find(name);
            }
            catch (System.Exception e)
            {
                Log("GameObject.Find threw for '" + name + "': " + e.Message);
                return null;
            }
        }

        private static void Log(string m) => Debug.Log("[V2Measure] " + m);
    }
}
