// Village2NavRCA — map Village2's navmesh + navlinks so we can SEE why spawn->stronghold
// isn't traversable (owner couldn't walk through; no gizmos). READ-ONLY.
//  - Dumps every NavMeshLink: both world endpoints, each on-navmesh?, gap length, area/active.
//  - Samples a line from the hero spawn to the stronghold center, every 4m, reporting where the
//    navmesh BREAKS (the gap) — and whether spawn-island and stronghold-island are path-connected.
// Run: DeNelle.Editor.Village2NavRCA.Run
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Editor
{
    public static class Village2NavRCA
    {
        private const string ScenePath = "Assets/Scenes/Village2.unity";

        [MenuItem("Defenders/Village2/Nav RCA (map navmesh + navlinks)")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();   // ensure a current bake to read
            Log("=== Village2 NAV RCA ===");

            var spawnGo = GameObject.Find("HeroStartPoint_PlayerSpawn");
            var keepGo  = GameObject.Find("Spawn_Keep") ?? GameObject.Find("StrongholdRoot");
            Vector3 spawn = spawnGo != null ? spawnGo.transform.position : new Vector3(27.3f, 0f, -45.6f);
            Vector3 keep  = keepGo  != null ? keepGo.transform.position  : Vector3.zero;
            Log($"spawn={spawn}  stronghold={keep}");

            // --- NavMeshLinks ---
            foreach (var link in Object.FindObjectsByType<NavMeshLink>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (link == null) continue;
                Vector3 a = link.transform.TransformPoint(link.startPoint);
                Vector3 b = link.transform.TransformPoint(link.endPoint);
                bool aOn = NavMesh.SamplePosition(a, out _, 1.5f, NavMesh.AllAreas);
                bool bOn = NavMesh.SamplePosition(b, out _, 1.5f, NavMesh.AllAreas);
                Log($"LINK '{link.name}' A={a} (onMesh={aOn}) B={b} (onMesh={bOn}) gap={Vector3.Distance(a,b):F1} " +
                    $"width={link.width} bidir={link.bidirectional} active={link.isActiveAndEnabled}");
            }

            // --- navmesh continuity along spawn->stronghold ---
            int steps = 14;
            int onCount = 0, firstBreak = -1;
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(spawn, keep, i / (float)steps);
                bool on = NavMesh.SamplePosition(p, out NavMeshHit h, 3f, NavMesh.AllAreas);
                if (on) onCount++; else if (firstBreak < 0) firstBreak = i;
                Log($"  step {i,2}/{steps} {p} onMesh={on}{(on ? " @"+h.position : "")}");
            }
            Log($"navmesh coverage spawn->stronghold: {onCount}/{steps+1} samples on-mesh; first GAP at step {firstBreak}.");

            // ISLAND SEAM finder: for each adjacent on-mesh pair, is there a COMPLETE path between them?
            // The first pair that ISN'T connected is the island boundary (where to bridge).
            for (int i = 0; i < steps; i++)
            {
                Vector3 pa = Vector3.Lerp(spawn, keep, i / (float)steps);
                Vector3 pb = Vector3.Lerp(spawn, keep, (i + 1) / (float)steps);
                if (!NavMesh.SamplePosition(pa, out NavMeshHit ha, 3f, NavMesh.AllAreas)) continue;
                if (!NavMesh.SamplePosition(pb, out NavMeshHit hb, 3f, NavMesh.AllAreas)) continue;
                var pp = new NavMeshPath();
                NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, pp);
                if (pp.status != NavMeshPathStatus.PathComplete)
                    Log($"  *** ISLAND SEAM between step {i} ({ha.position}) and {i+1} ({hb.position}) — status={pp.status}. BRIDGE HERE.");
            }

            // --- path connectivity ---
            if (NavMesh.SamplePosition(spawn, out NavMeshHit s, 6f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(keep, out NavMeshHit k, 8f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                NavMesh.CalculatePath(s.position, k.position, NavMesh.AllAreas, path);
                Log($"PATH spawn->stronghold: status={path.status} corners={path.corners.Length}");
            }
            Log("=== done (read-only) ===");
        }

        private static void Log(string m) => Debug.Log("[V2NavRCA] " + m);
    }
}
