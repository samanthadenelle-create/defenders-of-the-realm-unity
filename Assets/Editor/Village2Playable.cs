// Village2Playable.cs - turn the Village2 ART shell into a playable village by
// adding gameplay components ONE AT A TIME, each step idempotent + verbose so a
// batchmode log confirms it before the next step runs. Owner-directed cadence
// (2026-06-04): "copy components one at a time, methodical is key."
//
// Editor-only (DeNelle.Editor asmdef). Like Village2Build, it CANNOT reference
// Assembly-CSharp (where the runtime gameplay types live), so it adds those
// components by REFLECTION via FindType + AddComponent(System.Type). This is
// build tooling, not a runtime cross-module bridge, so it does not violate the
// "no reflection bridges" rule (same exemption as VillageSceneBuilder).
//
// IMPORTANT: this NEVER touches Village.unity. It only authors Village2.unity.
// Village.unity stays the live, shipping scene until Phase E unhooks it.

using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class Village2Playable
    {
        const string SourceScenePath = "Assets/Scenes/Village2Test.unity";
        const string Village2ScenePath = "Assets/Scenes/Village2.unity";
        const string VillageScenePath  = "Assets/Scenes/Village.unity";

        // Runtime gameplay types (Assembly-CSharp / DeNelle.Village) resolved by reflection.
        const string TypeHeartController = "DeNelle.Village.HeartController";

        // =====================================================================
        // PHASE A - promote the generated shell to a real, build-registered scene
        // =====================================================================
        [MenuItem("Defenders/Village2/A. Promote Shell To Village2.unity")]
        public static void A_PromoteScene()
        {
            Log("=== PHASE A: Promote shell -> Village2.unity START ===");

            string src = File.Exists(SourceScenePath) ? SourceScenePath
                       : (File.Exists(Village2ScenePath) ? Village2ScenePath : null);
            if (src == null)
            {
                Err($"No source scene found ({SourceScenePath} or {Village2ScenePath}). " +
                    "Run Defenders/Village2/2. Setup + Generate Village2 first. Aborting.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(src, OpenSceneMode.Single);
            Log($"Opened source scene '{src}'. Root objects: {scene.rootCount}");

            GameObject villageRoot = FindRoot(scene, "Village2");
            if (villageRoot == null)
            {
                Err("No 'Village2' root GameObject in the scene - the art shell is missing. Aborting.");
                return;
            }
            Log($"Village2 art root found: {villageRoot.transform.childCount} child groups/objects.");

            // Save AS Village2.unity (does NOT modify the source if source was the Test scene).
            bool ok = EditorSceneManager.SaveScene(scene, Village2ScenePath, /*saveAsCopy:*/ false);
            Log(ok ? $"Saved '{Village2ScenePath}'." : $"FAILED to save '{Village2ScenePath}'.");
            if (!ok) { Err("SaveScene failed. Aborting."); return; }

            // Register Village2 in Build Settings (after Village, keep Village present).
            EnsureInBuildSettings(Village2ScenePath, afterPath: VillageScenePath);

            Log("=== PHASE A DONE - Village2.unity exists, in Build Settings, Village.unity untouched ===");
        }

        // =====================================================================
        // DIAGNOSTIC - report transform.position vs renderer-bounds center for the
        // Village2 root + the tree, so we can see the tripo-displacement reality
        // before anchoring gameplay. Read-only (does not save).
        // =====================================================================
        [MenuItem("Defenders/Village2/0. Inspect Layout")]
        public static void Inspect()
        {
            Log("=== INSPECT Village2 layout START ===");
            if (!OpenVillage2(out Scene scene)) return;

            GameObject root = FindRoot(scene, "Village2");
            if (root != null)
            {
                Log($"Village2 root transform.position = {root.transform.position}");
                if (TryWorldBounds(root, out Bounds rb))
                    Log($"Village2 root RENDERER bounds: center={rb.center} size={rb.size}");
            }

            // Tree: report the clone root transform AND its true rendered center.
            GameObject tree = FindByNameContains(scene, "TreeOfLife");
            if (tree == null) tree = FindByNameContains(scene, "Tree");
            if (tree != null)
            {
                Log($"TREE node '{tree.name}' transform.position = {tree.transform.position}");
                if (TryWorldBounds(tree, out Bounds tb))
                    Log($"TREE RENDERER bounds: center={tb.center} size={tb.size}  <-- this is where it VISUALLY sits");
                else
                    Log("TREE has no renderers?!");
            }

            // Where did B1 put the Heart?
            System.Type heartType = FindType(TypeHeartController);
            if (heartType != null)
            {
                var hearts = Object.FindObjectsByType(heartType, FindObjectsSortMode.None);
                foreach (var h in hearts)
                {
                    var mb = h as MonoBehaviour;
                    if (mb != null) Log($"HEART currently on '{mb.gameObject.name}' at {mb.transform.position}");
                }
            }

            // Quick sanity: list the direct children of the Village2 root with bounds centers.
            if (root != null)
            {
                Log("--- direct children (name : renderer-bounds center) ---");
                int n = 0;
                foreach (Transform c in root.transform)
                {
                    if (n++ > 18) { Log("  ...(truncated)"); break; }
                    string bc = TryWorldBounds(c.gameObject, out Bounds cb) ? cb.center.ToString() : "no-rend";
                    Log($"  {c.name} : tf={c.position} bounds={bc}");
                }
            }
            Log("=== INSPECT DONE ===");
        }

        static bool TryWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool have = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!have) { bounds = r.bounds; have = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return have;
        }

        // =====================================================================
        // PHASE B1 - HeartController onto the Tree of Life at origin
        // (mirrors VillageSceneBuilder.Content.cs heart wiring: authored transform)
        // =====================================================================
        [MenuItem("Defenders/Village2/B1. Wire Heart")]
        public static void B1_WireHeart()
        {
            Log("=== PHASE B1: Wire HeartController START ===");
            if (!OpenVillage2(out Scene scene)) return;

            GameObject root = FindRoot(scene, "Village2");
            if (root == null) { Err("No 'Village2' root. Aborting."); return; }

            // The visible Tree of Life is scaled up ~14x (ScaleToHeight). Putting the
            // HeartController on it would make Awake()'s auto blocker capsule (r=2,h=8
            // LOCAL) a ~28m plaza-blocking capsule -> heart-collider-scale-trap. So we
            // host the Heart on a CLEAN, scale-1 child anchor at the tree's x/z (origin),
            // leaving the scaled tree purely decorative. Same gameplay anchor (lose
            // condition + enemy target at village centre), no giant collider.
            GameObject tree = FindByNameContains(scene, "TreeOfLife");
            if (tree == null) tree = FindByNameContains(scene, "Tree");
            Vector3 centre = tree != null
                ? new Vector3(tree.transform.position.x, 0f, tree.transform.position.z)
                : Vector3.zero;
            Log(tree != null
                ? $"Tree found '{tree.name}' at {tree.transform.position} scale {tree.transform.localScale}; Heart anchor x/z from it."
                : "Tree not found; defaulting Heart anchor to origin.");

            System.Type heartType = FindType(TypeHeartController);
            if (heartType == null)
            {
                Err($"Type '{TypeHeartController}' not found (is DeNelle.Village compiled?). Aborting.");
                return;
            }

            const string AnchorName = "HeartOfElarion";
            Transform existingAnchor = root.transform.Find(AnchorName);
            GameObject anchor = existingAnchor != null ? existingAnchor.gameObject : new GameObject(AnchorName);
            if (existingAnchor == null)
            {
                anchor.transform.SetParent(root.transform, false);
                Log($"Created '{AnchorName}' anchor under Village2 root.");
            }
            anchor.transform.position = centre;          // village centre, on the ground
            anchor.transform.localScale = Vector3.one;   // clean scale -> clean blocker

            if (anchor.GetComponent(heartType) == null)
            {
                var heart = anchor.AddComponent(heartType);
                var so = new SerializedObject(heart);
                var prop = so.FindProperty("_useAuthoredTransform");
                if (prop != null) { prop.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
                else Warn("HeartController._useAuthoredTransform not found - Elarion may snap to origin at Play.");
                Log($"HeartController added on '{AnchorName}' at {centre} (scale 1).");
            }
            else Log("HeartController already on the anchor - idempotent skip.");

            EditorSceneManager.SaveScene(scene, Village2ScenePath);
            var hearts = Object.FindObjectsByType(heartType, FindObjectsSortMode.None);
            Log($"VERIFY: HeartController instances = {hearts.Length} (expect 1).");
            foreach (var h in hearts)
            {
                var mb = h as MonoBehaviour;
                if (mb != null) Log($"VERIFY: Heart on '{mb.gameObject.name}' at {mb.transform.position} scale {mb.transform.lossyScale}.");
            }
            Log("=== PHASE B1 DONE ===");
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        static bool OpenVillage2(out Scene scene)
        {
            scene = default;
            if (!File.Exists(Village2ScenePath))
            {
                Err($"{Village2ScenePath} does not exist yet - run Phase A first. Aborting.");
                return false;
            }
            scene = EditorSceneManager.OpenScene(Village2ScenePath, OpenSceneMode.Single);
            Log($"Opened '{Village2ScenePath}' ({scene.rootCount} roots).");
            return true;
        }

        static GameObject FindRoot(Scene scene, string exactName)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == exactName) return r;
            return null;
        }

        static GameObject FindByNameContains(Scene scene, string fragment)
        {
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name.Contains(fragment)) return r;
                var all = r.GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                    if (t.name.Contains(fragment)) return t.gameObject;
            }
            return null;
        }

        static void EnsureInBuildSettings(string scenePath, string afterPath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath))
            {
                Log($"Build Settings already contains '{scenePath}' - no change.");
                return;
            }
            int insertAt = scenes.FindIndex(s => s.path == afterPath);
            var entry = new EditorBuildSettingsScene(scenePath, true);
            if (insertAt >= 0) scenes.Insert(insertAt + 1, entry);
            else scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
            Log($"Added '{scenePath}' to Build Settings (enabled). Total scenes: {scenes.Count}.");
        }

        static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static void Log(string m)  => Debug.Log("[Village2Playable] " + m);
        static void Warn(string m) => Debug.LogWarning("[Village2Playable] " + m);
        static void Err(string m)  => Debug.LogError("[Village2Playable] " + m);
    }
}
