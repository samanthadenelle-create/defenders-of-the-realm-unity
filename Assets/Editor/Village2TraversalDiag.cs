// Village2TraversalDiag — headless RCA for "Village2 still not traversable".
// Loads Village2, samples the navmesh at the hero spawn + key interior points,
// and runs CalculatePath spawn->each. PathComplete = walkable; Partial/Invalid =
// blocked (a collider over the opening kept the navmesh from connecting). Read-only.
// Run: DeNelle.Editor.Village2TraversalDiag.Run
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class Village2TraversalDiag
    {
        private const string ScenePath = "Assets/Scenes/Village2.unity";

        [MenuItem("Defenders/Village2/Diag Traversal (path spawn->stronghold)")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log("=== Village2 traversal diag ===");

            var spawnGo = GameObject.Find("HeroStartPoint_PlayerSpawn");
            Vector3 spawn = spawnGo != null ? spawnGo.transform.position : Vector3.zero;
            Log($"spawn marker = {(spawnGo != null ? spawn.ToString() : "<not found, using origin>")}");

            if (!NavMesh.SamplePosition(spawn, out NavMeshHit s, 6f, NavMesh.AllAreas))
            { Err($"NO navmesh near spawn {spawn} (r=6) — hero can't even stand. Bake/floor problem."); }
            else Log($"spawn on navmesh @ {s.position}");

            // Targets: a few named markers + the stronghold root + a couple interior spawn points.
            string[] names = { "Spawn_Gate", "Spawn_Chokepoint", "Spawn_Keep", "Spawn_Rear", "StrongholdRoot" };
            foreach (var n in names)
            {
                var go = GameObject.Find(n);
                if (go == null) { Log($"  target '{n}' not found."); continue; }
                Vector3 tp = go.transform.position;
                if (!NavMesh.SamplePosition(tp, out NavMeshHit t, 8f, NavMesh.AllAreas))
                { Warn($"  '{n}' {tp}: NO navmesh within 8m (target area not walkable)."); continue; }
                var path = new NavMeshPath();
                bool ok = NavMesh.SamplePosition(spawn, out NavMeshHit ss, 6f, NavMesh.AllAreas)
                          && NavMesh.CalculatePath(ss.position, t.position, NavMesh.AllAreas, path);
                Log($"  spawn -> '{n}' {t.position}: status={path.status} corners={path.corners.Length} (calc={ok})");
            }

            // Colliders near the gate (the suspected blocker) — list them so the fix can target the right one.
            var gate = GameObject.Find("Spawn_Gate");
            if (gate != null)
            {
                Log($"--- colliders within 8m of Spawn_Gate {gate.transform.position} ---");
                int c = 0;
                foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                {
                    if (col == null || col.isTrigger) continue;
                    if (Vector3.Distance(col.bounds.center, gate.transform.position) > 8f) continue;
                    if (c++ > 25) { Log("  ...(truncated)"); break; }
                    Log($"  COLLIDER '{Path(col.transform)}' type={col.GetType().Name} center={col.bounds.center} size={col.bounds.size}");
                }
                Log($"  colliders near gate: {c}");
            }
            Log("=== done ===");
        }

        private static string Path(Transform t)
        {
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }
        private static void Log(string m)  => Debug.Log("[V2Traversal] " + m);
        private static void Warn(string m) => Debug.LogWarning("[V2Traversal] " + m);
        private static void Err(string m)  => Debug.LogError("[V2Traversal] " + m);
    }
}
