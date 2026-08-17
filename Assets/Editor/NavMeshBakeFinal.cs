// =============================================================================
// NavMeshBakeFinal — the navmesh bake as a SEPARATE, ALWAYS-LAST step.
// -----------------------------------------------------------------------------
// OWNER DIAGNOSIS 2026-08-17, and it is the whole reason this file exists:
//   "its because when you originally run the script you bake it, then you rotate
//    the buildings, so the bake moves with the rotation"
//   "need to rotate and bake last"
//   "even easier run a seperate bake script always at exit script"
//
// THE DEFECT. The scene builders bake the navmesh INSIDE the build, partway
// through — CastleHubBuilder's BATCH-BAKE runs BuildNavMesh() and then keeps
// going. Anything that moves or rotates a building AFTERWARDS leaves the carve
// behind at the old pose: an unwalkable footprint where nothing stands, and a
// walkable gap where the building now is. That is the "baked footprint" the
// owner reported — not an object, not terrain paint, and not something a cache
// clear can touch, because it is baked nav data inside the scene that ships.
//
// This is the SAME failure class as every other one found today: the pet's -90°
// yaw, the .tripo-extracted marker, the WizardTower_1 art path, the five deleted
// music clips. A value computed correctly, welded in place, and outliving the
// thing it described. Here the stale value is a whole navmesh.
//
// ⛔ WHY A SEPARATE SCRIPT AND NOT "MOVE THE BAKE DOWN A FEW LINES".
// Reordering inside one builder fixes that builder until the next step is
// appended after it — and the bug is invisible until someone walks into an empty
// square, so it would not be noticed for weeks. A bake that is structurally the
// LAST thing cannot be leapfrogged: there is nothing after it to leapfrog.
//
// ⚠ WHAT THIS DOES NOT FIX, STATED PLAINLY SO IT IS NOT ASSUMED.
// It bakes the scene as it stands when it runs. Structures whose rotation is
// applied at RUNTIME — the catalog `orientation` block via StructureFactory, and
// the Offset Forge dev store at persistentDataPath/structure-orientations.json —
// move AFTER any editor bake, by construction. No bake ordering can cover those;
// they need a runtime carve (NavMeshObstacle) or a runtime rebuild. This closes
// the scene-baked half, which is the half the owner is looking at.
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    /// <summary>Bakes every NavMeshSurface in a scene as a standalone final step.</summary>
    public static class NavMeshBakeFinal
    {
        /// <summary>Marker printed on success — grep the SHAPE, per the project's marker rule.</summary>
        private const string OkMarker = "NAVMESH_BAKE_OK";

        /// <summary>The home hub. Default target when no scene is named.</summary>
        private const string HubScene = "Assets/Scenes/Main_Castle_Overworld.unity";

        [MenuItem("Defenders/World/Bake NavMesh (ALWAYS LAST)")]
        public static void BakeOpenSceneMenu() => BakeOpenScene();

        /// <summary>
        /// Batchmode entry: -executeMethod DeNelle.Editor.NavMeshBakeFinal.Run
        /// Opens the hub scene, bakes, persists and saves. This is the step that must run AFTER
        /// every builder, every rotation pass and every layout edit — never before one.
        /// </summary>
        public static void Run()
        {
            string scenePath = HubScene;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-bakeScene") { scenePath = args[i + 1]; break; }

            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogError($"[NavMeshBakeFinal] scene NOT FOUND: '{scenePath}' — nothing baked. " +
                               "Pass -bakeScene <path> or restore the hub scene.");
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"[NavMeshBakeFinal] opened {scenePath}");

            long sizeBefore = new System.IO.FileInfo(scenePath).Length;

            int baked = BakeOpenScene();
            if (baked <= 0) return;

            // MarkSceneDirty is what makes SaveOpenScenes actually write. Without it the save is a
            // silent no-op (see the note in BakeOpenScene) — belt and braces alongside SetDirty on
            // each surface, because the cost of getting this wrong is a bake that reports success
            // and ships the stale carve.
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ⛔ VERIFY THE ARTIFACT, NOT THE PROCESS. Every step above can "succeed" while the
            // scene on disk keeps its old navmesh. Prove the scene was rewritten before claiming a
            // bake happened — this is the check whose absence let the first run report OK on a
            // completely inert pass.
            long sizeAfter = new System.IO.FileInfo(scenePath).Length;
            var writeTime = System.IO.File.GetLastWriteTimeUtc(scenePath);
            bool rewritten = writeTime > System.DateTime.UtcNow.AddMinutes(-5);

            Debug.Log($"[NavMeshBakeFinal] scene bytes {sizeBefore:N0} -> {sizeAfter:N0}, " +
                      $"lastWrite={writeTime:O}");

            if (!rewritten)
            {
                Debug.LogError("[NavMeshBakeFinal] BAKE NOT PERSISTED — the scene file was NOT rewritten. " +
                               "The navmesh data may be an orphaned asset while the scene keeps its stale " +
                               "carve. Do NOT treat this run as a bake.");
                return;
            }

            Debug.Log($"[NavMeshBakeFinal] scene + assets saved.");
            Debug.Log($"{OkMarker} {baked} surface(s) — {System.IO.Path.GetFileName(scenePath)}");
        }

        /// <summary>
        /// Bakes every NavMeshSurface in the currently-open scene. Returns the number baked,
        /// or -1 on a hard failure. Reflection is used for the NavMeshSurface API to match the
        /// surrounding builders (CastleHubBuilder does the same) rather than introduce a second
        /// way of reaching the same type.
        /// </summary>
        public static int BakeOpenScene()
        {
            var surfType = System.Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (surfType == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    surfType = asm.GetType("Unity.AI.Navigation.NavMeshSurface");
                    if (surfType != null) break;
                }
            }
            if (surfType == null)
            {
                Debug.LogError("[NavMeshBakeFinal] NavMeshSurface type not resolved — NOTHING WAS BAKED. " +
                               "Do NOT treat this run as a bake; the stale carve is still in the scene.");
                return -1;
            }

            var surfaces = Object.FindObjectsByType(surfType, FindObjectsSortMode.None);
            if (surfaces == null || surfaces.Length == 0)
            {
                Debug.LogError("[NavMeshBakeFinal] ZERO NavMeshSurfaces in the open scene — nothing to bake. " +
                               "A scene with no surface has no navmesh at all, which is a different and worse " +
                               "problem than a stale one; not reported as success.");
                return -1;
            }

            // Log the pose of every rotated structure BEFORE baking. This is the evidence that the
            // bake saw the FINAL orientation — the exact fact that was missing when the stale carve
            // shipped. Without it, a future "did the bake run after the rotation?" is unanswerable.
            LogStructurePoses();

            var built = new List<string>();
            foreach (var s in surfaces)
            {
                var comp = s as Component;
                string name = comp != null ? comp.gameObject.name : s.name;

                // Match CastleHubBuilder's configuration exactly: renderer-off planes must still be
                // collected, so geometry comes from PHYSICS COLLIDERS, not render meshes.
                var ug = surfType.GetProperty("useGeometry");
                if (ug != null) ug.SetValue(s, System.Enum.ToObject(ug.PropertyType, 1)); // PhysicsColliders
                var co = surfType.GetProperty("collectObjects");
                if (co != null) co.SetValue(s, System.Enum.ToObject(co.PropertyType, 0)); // All

                var build = surfType.GetMethod("BuildNavMesh", System.Type.EmptyTypes);
                if (build == null)
                {
                    Debug.LogError("[NavMeshBakeFinal] BuildNavMesh() not found on NavMeshSurface — nothing baked.");
                    return -1;
                }
                build.Invoke(s, null);

                // Persist the freshly-built data as an asset or it does not survive the scene save —
                // an unpersisted bake looks fine in the editor and ships as nothing.
                var dataProp = surfType.GetProperty("navMeshData");
                var data = dataProp != null ? dataProp.GetValue(s) as Object : null;
                if (data == null)
                {
                    Debug.LogError($"[NavMeshBakeFinal] '{name}' baked but produced NULL navMeshData — " +
                                   "surface has no walkable geometry, or the bake silently failed.");
                    continue;
                }
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(data)))
                {
                    string dir = "Assets/Scenes/NavMesh";
                    if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Scenes", "NavMesh");
                    AssetDatabase.CreateAsset(data, $"{dir}/NavMesh_{name}.asset");
                }

                // ⛔ MARK THE COMPONENT DIRTY OR THE WHOLE BAKE IS DISCARDED.
                // CreateAsset turns the data into an asset, but the SURFACE's reference to it lives
                // in the SCENE, and Unity will not write a scene it does not believe changed.
                // The first run of this script proved it the expensive way: it printed
                // NAVMESH_BAKE_OK, wrote a 1.6 MB asset, left the .unity file BYTE-IDENTICAL, and
                // the asset was ORPHANED — the scene still referenced the old, stale navmesh. A
                // green marker on a run that changed nothing is the worst possible outcome, because
                // it retires the bug in the reader's head while leaving it in the build. Verified by
                // grepping the new asset's GUID in the .unity file — the check now lives in Run().
                if (comp != null) EditorUtility.SetDirty(comp);
                built.Add(name);
                Debug.Log($"[NavMeshBakeFinal] baked '{name}' (data persisted).");
            }

            Debug.Log($"[NavMeshBakeFinal] {built.Count}/{surfaces.Length} surface(s) baked: {string.Join(", ", built)}");
            return built.Count;
        }

        /// <summary>
        /// Records the yaw of every structure-looking root before the bake. The stale-carve bug is
        /// exactly "the bake did not see this rotation", so the poses at bake time are the one piece
        /// of evidence that settles it — cheap to log, impossible to reconstruct afterwards.
        /// </summary>
        private static void LogStructurePoses()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(false))
                {
                    // Only report things carrying a collider — those are what the bake voxelizes,
                    // so they are the only rotations that can move a carve.
                    if (t.GetComponent<Collider>() == null) continue;
                    Vector3 e = t.rotation.eulerAngles;
                    if (Mathf.Approximately(e.x, 0f) && Mathf.Approximately(e.y, 0f) && Mathf.Approximately(e.z, 0f))
                        continue;   // identity poses carry no information here
                    if (n++ < 40)
                        Debug.Log($"[NavMeshBakeFinal] pose-at-bake '{t.name}' euler=({e.x:F1},{e.y:F1},{e.z:F1})");
                }
            }
            Debug.Log($"[NavMeshBakeFinal] {n} rotated collider(s) present at bake time " +
                      "(these are the poses the carve will match — if a building is rotated AFTER this, " +
                      "its carve goes stale and this bake must be re-run).");
        }
    }
}
