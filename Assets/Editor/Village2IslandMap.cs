// Village2IslandMap — map Village2's walkable navmesh into connected ISLANDS so we can
// hand the owner an exact interior coordinate for the crossing destination (and see if
// the interior is one patch or fragmented). Grid-samples the footprint, clusters samples
// by path-connectivity, reports each island's size + centroid + bounds. READ-ONLY.
// Run: DeNelle.Editor.Village2IslandMap.Run
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Editor
{
    public static class Village2IslandMap
    {
        private const string ScenePath = "Assets/Scenes/Village2.unity";

        [MenuItem("Defenders/Village2/Map NavMesh Islands")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Log("=== Village2 navmesh island map ===");

            // Collect on-navmesh sample points over the footprint.
            var pts = new List<Vector3>();
            for (float x = -45f; x <= 45f; x += 3f)
                for (float z = -55f; z <= 30f; z += 3f)
                    if (NavMesh.SamplePosition(new Vector3(x, 2f, z), out NavMeshHit h, 3f, NavMesh.AllAreas))
                        pts.Add(h.position);
            Log($"on-navmesh sample points: {pts.Count}");

            // Cluster by path-connectivity (union-find-ish: assign each point to the first island it connects to).
            var islands = new List<List<Vector3>>();
            foreach (var p in pts)
            {
                bool placed = false;
                foreach (var isl in islands)
                {
                    var path = new NavMeshPath();
                    if (NavMesh.CalculatePath(isl[0], p, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    { isl.Add(p); placed = true; break; }
                }
                if (!placed) islands.Add(new List<Vector3> { p });
            }
            islands.Sort((a, b) => b.Count.CompareTo(a.Count));

            Log($"ISLANDS: {islands.Count} (sorted by size)");
            int show = Mathf.Min(islands.Count, 8);
            for (int i = 0; i < show; i++)
            {
                var isl = islands[i];
                Vector3 c = Vector3.zero; var min = isl[0]; var max = isl[0];
                foreach (var p in isl) { c += p; min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                c /= isl.Count;
                Log($"  island[{i}] size={isl.Count,3}  centroid=({c.x:F1},{c.y:F1},{c.z:F1})  " +
                    $"bounds x[{min.x:F0}..{max.x:F0}] z[{min.z:F0}..{max.z:F0}]");
            }

            // Where are the key markers, and which island is each on?
            foreach (var n in new[] { "HeroStartPoint_PlayerSpawn", "Crossing_Village2Gate_Entry", "Crossing_Village2Gate_Dest", "Spawn_Keep", "Spawn_Chokepoint", "Spawn_Rear" })
            {
                var go = GameObject.Find(n);
                if (go == null) { Log($"  marker '{n}': not found"); continue; }
                int isl = IslandOf(go.transform.position, islands);
                Log($"  marker '{n}' @ {go.transform.position} -> island[{(isl < 0 ? "OFF-MESH" : isl.ToString())}]");
            }
            Log("=== done. Put the crossing DEST on the same island as Spawn_Keep/Chokepoint (the interior). ===");
        }

        private static int IslandOf(Vector3 pos, List<List<Vector3>> islands)
        {
            if (!NavMesh.SamplePosition(pos, out NavMeshHit h, 6f, NavMesh.AllAreas)) return -1;
            for (int i = 0; i < islands.Count; i++)
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(islands[i][0], h.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    return i;
            }
            return -1;
        }

        private static void Log(string m) => Debug.Log("[V2Islands] " + m);
    }
}
