// =============================================================================
// OuterWorldNavBake — bakes a NavMeshSurface for OuterWorld so the
// Castle -> OuterWorld warp (to ~0,0.5,-80) lands on WALKABLE ground.
//
// ROOT CAUSE of the "zone doesn't connect to OuterWorld" bug: OuterWorld had NO
// baked navmesh at all (the castle has one; OuterWorld never did). The hero's
// WarpTo does NavMesh.SamplePosition near the seam and, finding no OuterWorld
// mesh, snaps back onto the castle edge / void.
//
// Mirrors CastleHubBuilder.BatchAddFloorAndBakeCastle (reflection on
// Unity.AI.Navigation.NavMeshSurface — no hard package dependency) for a
// consistent surface-based navmesh. Touches ONLY OuterWorld.unity — NOT the
// corruption-cursed Village.unity that the legacy BakeWorldNavMesh opens.
//
// Editor-closed batchmode:  Defenders > World > Bake OuterWorld NavMesh (solo surface)
// =============================================================================
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class OuterWorldNavBake
    {
        private const string ScenePath = "Assets/Scenes/OuterWorld.unity";
        private const string AssetDir  = "Assets/Scenes/OuterWorld";
        private const string AssetPath = "Assets/Scenes/OuterWorld/NavMesh-OuterWorld.asset";

        [MenuItem("Defenders/World/Bake OuterWorld NavMesh (solo surface)")]
        public static void Bake()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[OuterWorldNavBake] opened " + ScenePath);

            // Level the (flat) terrain so the WALKABLE PATH CORRIDOR sits at Y=0, flush with the
            // castle floor. FIX 2026-06-20 (navlink RCA): the old sample point (42,0,0) was on the
            // ORIGIN-centered terrain; after the un-stack the terrain spans z=-72..-1072, so (42,0,0)
            // is OFF the terrain → SampleHeight returns garbage → the whole terrain (and the cave
            // navmesh at z≈-684) gets mis-leveled → SEAM-OFF-MESH. Sample a point that is actually ON
            // the flat corridor (x=0, z in [-76,-700]) so the corridor — and the cave — land at Y=0.
            var levelSample = new Vector3(0f, 0f, -200f);   // on the un-stacked corridor
            foreach (var go in scene.GetRootGameObjects())
                foreach (var terr in go.GetComponentsInChildren<Terrain>(true))
                {
                    float edge = terr.transform.position.y + terr.SampleHeight(levelSample);
                    float delta = 0f - edge;
                    if (Mathf.Abs(delta) > 0.001f)
                    {
                        terr.transform.position += new Vector3(0f, delta, 0f);
                        Debug.Log("[OuterWorldNavBake] leveled terrain by " + delta.ToString("0.000") + " -> Y=0.");
                    }
                }

            var surfType = ResolveType("Unity.AI.Navigation.NavMeshSurface");
            if (surfType == null)
            {
                Debug.LogError("[OuterWorldNavBake] NavMeshSurface type not found — bake skipped (do it in-editor).");
                return;
            }

            var found = Object.FindObjectsByType(surfType, FindObjectsSortMode.None);
            Object surf;
            if (found.Length > 0)
            {
                surf = found[0];
                Debug.Log("[OuterWorldNavBake] reusing existing NavMeshSurface.");
            }
            else
            {
                var host = new GameObject("OuterWorld_NavMeshSurface");
                surf = host.AddComponent(surfType);
                Debug.Log("[OuterWorldNavBake] created NavMeshSurface host.");
            }

            SetEnum(surfType, surf, "collectObjects", 0); // All
            SetEnum(surfType, surf, "useGeometry", 1);    // PhysicsColliders (terrain has a TerrainCollider)

            var build = surfType.GetMethod("BuildNavMesh", System.Type.EmptyTypes);
            if (build == null) { Debug.LogError("[OuterWorldNavBake] BuildNavMesh() not found."); return; }
            build.Invoke(surf, null);
            Debug.Log("[OuterWorldNavBake] BuildNavMesh() invoked.");

            var dataProp = surfType.GetProperty("navMeshData");
            var data = dataProp != null ? dataProp.GetValue(surf) as Object : null;
            if (data == null)
            {
                Debug.LogError("[OuterWorldNavBake] navMeshData NULL after bake — nothing collected. " +
                               "Retry with useGeometry=RenderMeshes, or confirm ExteriorTerrain has a collider.");
            }
            else
            {
                if (!System.IO.Directory.Exists(AssetDir))
                    AssetDatabase.CreateFolder("Assets/Scenes", "OuterWorld");
                if (!AssetDatabase.Contains(data))
                {
                    var prior = AssetDatabase.LoadAssetAtPath<Object>(AssetPath);
                    if (prior != null) AssetDatabase.DeleteAsset(AssetPath);
                    AssetDatabase.CreateAsset(data, AssetPath);
                    Debug.Log("[OuterWorldNavBake] navmesh asset -> " + AssetPath);
                }
                else Debug.Log("[OuterWorldNavBake] navMeshData already an asset (updated in place).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[OuterWorldNavBake] saved scene + asset. Done.");
        }

        private static System.Type ResolveType(string fullName)
        {
            var t = System.Type.GetType(fullName);
            if (t != null) return t;
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static void SetEnum(System.Type type, Object obj, string prop, int val)
        {
            var p = type.GetProperty(prop);
            if (p != null) p.SetValue(obj, System.Enum.ToObject(p.PropertyType, val));
        }
    }
}
