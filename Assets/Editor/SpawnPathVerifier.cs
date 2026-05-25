// =============================================================================
// SpawnPathVerifier - headless WO-27 loop verification. Opens the baked Village
// scene and, for each WaveSpawnPoint, checks (a) the spawn position samples onto
// the baked NavMesh within 8 m (SpawnOne's snap radius -> enemy will NOT freeze
// off-mesh) and (b) a NavMesh path from the spawn to the Heart is continuous
// (the apron -> corridor -> gate -> interior -> Heart march route solves).
//
//   -executeMethod DeNelle.Editor.SpawnPathVerifier.VerifySpawnPaths
//
// Edit-mode note: the baked (legacy) NavMesh loads with the scene, so NavMesh
// queries work without entering play mode. Gate force-field blockers carve the
// mesh only at RUNTIME (NavMeshObstacle), so an edit-mode path is expected to be
// Complete through the gate opening -- that confirms the route geometry; the gate
// gating itself is a runtime behaviour verified by playtest.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class SpawnPathVerifier
    {
        [MenuItem("Defenders/Week 3/Verify Spawn Paths (NavMesh)")]
        public static void VerifySpawnPaths()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Village.unity", OpenSceneMode.Single);

            var spawns = new List<Transform>();
            Transform heart = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("WaveSpawnPoint")) spawns.Add(t);
                    if (heart == null && (t.name.Contains("Elarion") || t.name.Contains("Heart")))
                        heart = t;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("[SpawnPathVerify] === WO-27 NavMesh march-route check ===");
            sb.AppendLine($"[SpawnPathVerify] spawn points found: {spawns.Count}; heart: " +
                          (heart != null ? heart.name + " @ " + heart.position : "NOT FOUND"));

            if (heart == null || spawns.Count == 0)
            {
                sb.AppendLine("[SpawnPathVerify] RESULT: FAIL - missing spawn points or heart.");
                Debug.Log(sb.ToString());
                return;
            }

            int onMesh = 0, complete = 0;
            foreach (var s in spawns)
            {
                Vector3 p = s.position;
                bool on = NavMesh.SamplePosition(p, out NavMeshHit hit, 8f, NavMesh.AllAreas);
                Vector3 start = on ? hit.position : p;
                if (on) onMesh++;

                var path = new NavMeshPath();
                bool calc = NavMesh.CalculatePath(start, heart.position, NavMesh.AllAreas, path);
                float len = 0f;
                for (int i = 1; i < path.corners.Length; i++)
                    len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                bool full = calc && path.status == NavMeshPathStatus.PathComplete;
                if (full) complete++;

                float distOut = Vector3.Distance(p, Vector3.zero);
                sb.AppendLine(
                    $"[SpawnPathVerify] {s.name}: pos={p} (~{distOut:F1} m from centre) | " +
                    $"onNavMesh={on} (snap {hit.distance:F2} m) | " +
                    $"path={path.status} corners={path.corners.Length} len={len:F1} m");
            }

            sb.AppendLine($"[SpawnPathVerify] RESULT: {onMesh}/{spawns.Count} spawns on NavMesh, " +
                          $"{complete}/{spawns.Count} have a complete route to the Heart.");
            sb.AppendLine(onMesh == spawns.Count && complete == spawns.Count
                ? "[SpawnPathVerify] PASS - every spawn is on the mesh with a continuous march route."
                : "[SpawnPathVerify] CHECK - see per-spawn lines above (a Partial path can be a runtime gate carve, but off-mesh spawns will freeze).");
            Debug.Log(sb.ToString());
        }
    }
}
