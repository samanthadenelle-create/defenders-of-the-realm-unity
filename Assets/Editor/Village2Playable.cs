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
using UnityEngine.AI;
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
        // PHASE C - combined Village2 + OuterWorld navmesh (the walled-in risk)
        // Mirrors OuterWorldBuilder.BakeWorldNavMesh but for Village2: flags
        // Village2 walls/houses/towers NavigationStatic as OBSTACLES, keeps gate
        // arches + roads walkable, levels the OuterWorld terrain to Y=0 flush, and
        // bakes ONE surface so the hero walks Village2 <-> terrain through the gates.
        // =====================================================================
        const string OuterWorldScenePath = "Assets/Scenes/OuterWorld.unity";

        // Names whose renderers must STAY walkable (NOT flagged obstacle): the gate
        // arch openings + the ground roads. Everything else under the village root
        // (walls, ramparts, corner towers, houses, the tree) becomes an obstacle.
        static readonly string[] KeepWalkable = { "Wall_Arch", "Floor_Brick" };

        [MenuItem("Defenders/Village2/C. Bake Combined NavMesh")]
        public static void C_BakeNavMesh()
        {
            Log("=== PHASE C: Bake combined Village2 + OuterWorld navmesh START ===");
            if (!System.IO.File.Exists(Village2ScenePath) || !System.IO.File.Exists(OuterWorldScenePath))
            {
                Err($"Missing {Village2ScenePath} or {OuterWorldScenePath}. Run Phase A + the world build first. Aborting.");
                return;
            }

            // Open Village2 (active) + OuterWorld additive — both contribute to one bake.
            Scene village2 = EditorSceneManager.OpenScene(Village2ScenePath, OpenSceneMode.Single);
            Scene outer = EditorSceneManager.OpenScene(OuterWorldScenePath, OpenSceneMode.Additive);
            Log($"Opened '{Village2ScenePath}' (active) + '{OuterWorldScenePath}' (additive).");

            // --- Flag Village2 obstacle geometry NavigationStatic --------------------
            GameObject root = FindRoot(village2, "Village2");
            int obstacles = 0, skipped = 0;
            if (root != null)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (NameOrAncestorContainsAny(r.transform, KeepWalkable)) { skipped++; continue; }
                    var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.NavigationStatic);
                    obstacles++;
                }
            }
            Log($"Flagged {obstacles} Village2 renderer(s) NavigationStatic (obstacles); kept {skipped} walkable (gate arches/roads).");

            // --- Level the OuterWorld terrain to Y=0 (flush) + flag it static --------
            const float villageFloorY = 0f;
            int terrains = 0;
            foreach (var go in outer.GetRootGameObjects())
                foreach (var terr in go.GetComponentsInChildren<Terrain>(true))
                {
                    float edgeSurface = terr.transform.position.y + terr.SampleHeight(new Vector3(42f, 0f, 0f));
                    float delta = villageFloorY - edgeSurface;
                    terr.transform.position += new Vector3(0f, delta, 0f);
                    var flags = GameObjectUtility.GetStaticEditorFlags(terr.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(terr.gameObject, flags | StaticEditorFlags.NavigationStatic);
                    EditorUtility.SetDirty(terr.gameObject);
                    terrains++;
                    Log($"Terrain '{terr.name}' leveled by {delta:F3} -> Y=0 flush; flagged NavigationStatic.");
                }
            if (terrains == 0)
                Err("No Terrain found in OuterWorld — the hero will have NO walkable ground outside the gates. " +
                    "Run Defenders/World/Build Outer World + Build Exterior Terrain first.");

            // --- Bake ONE combined surface ------------------------------------------
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(village2);
            EditorSceneManager.MarkSceneDirty(outer);
            EditorSceneManager.SaveScene(village2, Village2ScenePath);
            EditorSceneManager.SaveScene(outer, OuterWorldScenePath);
            AssetDatabase.SaveAssets();

            Log($"VERIFY: obstacles={obstacles} walkableKept={skipped} terrains={terrains}. " +
                "Baked ONE combined navmesh across Village2 + OuterWorld; saved both scenes.");
            Log("=== PHASE C DONE ===");
        }

        // =====================================================================
        // PHASE D - THE SWAP (Option 2, owner-approved 2026-06-04): keep the scene
        // NAMED "Village" so the 15+ systems that gate on scene.name=="Village" all
        // keep working with ZERO code edits. Back up the live hand-authored
        // Village.unity -> Village_Legacy.unity (rollback, kept on disk), then write
        // the generated Village2 content (Heart + nav flags already in it) into
        // Village.unity. NEVER deletes; backup is verified BEFORE the overwrite.
        // After this: run OuterWorldBuilder.BakeWorldNavMesh (targets Village.unity)
        // then D2 verify.
        // =====================================================================
        const string LegacyScenePath = "Assets/Scenes/Village_Legacy.unity";

        [MenuItem("Defenders/Village2/D. Swap Generated Village Into Village.unity")]
        public static void D_SwapIntoLiveVillage()
        {
            Log("=== PHASE D: Swap generated village INTO Village.unity START ===");

            // Guard 1: source must exist AND be wired (has a Heart) — never swap a bare shell.
            if (!File.Exists(Village2ScenePath)) { Err($"{Village2ScenePath} missing. Aborting."); return; }
            if (!File.Exists(VillageScenePath)) { Err($"{VillageScenePath} missing — nothing to back up. Aborting."); return; }

            Scene src = EditorSceneManager.OpenScene(Village2ScenePath, OpenSceneMode.Single);
            System.Type heartType = FindType(TypeHeartController);
            int srcHearts = heartType != null ? Object.FindObjectsByType(heartType, FindObjectsSortMode.None).Length : 0;
            GameObject srcRoot = FindRoot(src, "Village2");
            int srcObjs = srcRoot != null ? srcRoot.transform.childCount : 0;
            if (srcHearts < 1) { Err($"Source {Village2ScenePath} has no HeartController — refusing to swap a bare shell. Run B1 first. Aborting."); return; }
            Log($"Source verified: {srcObjs} objects, {srcHearts} Heart. OK to swap.");

            // Guard 2: BACK UP the live Village.unity FIRST and confirm the copy exists.
            if (File.Exists(LegacyScenePath))
            {
                Log($"{LegacyScenePath} already exists (prior backup) — leaving it as the original rollback, not overwriting it.");
            }
            else
            {
                bool copied = AssetDatabase.CopyAsset(VillageScenePath, LegacyScenePath);
                AssetDatabase.SaveAssets();
                if (!copied || !File.Exists(LegacyScenePath))
                {
                    Err($"Backup CopyAsset {VillageScenePath} -> {LegacyScenePath} FAILED. Refusing to overwrite Village.unity without a rollback. Aborting.");
                    return;
                }
                Log($"BACKED UP live village -> {LegacyScenePath} (rollback preserved).");
            }

            // Now safe: write the generated content into Village.unity (keeps its GUID/path,
            // so Build Settings + every asset reference stay valid; scene.name stays "Village").
            bool ok = EditorSceneManager.SaveScene(src, VillageScenePath, /*saveAsCopy:*/ false);
            if (!ok) { Err($"SaveScene to {VillageScenePath} FAILED. Aborting (Village_Legacy.unity holds the original)."); return; }
            Log($"Wrote generated village -> {VillageScenePath} (scene.name stays 'Village').");

            // Remove the now-redundant Village2.unity from Build Settings (keep Village.unity).
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int removed = scenes.RemoveAll(s => s.path == Village2ScenePath);
            if (removed > 0) { EditorBuildSettings.scenes = scenes.ToArray(); Log($"Removed redundant {Village2ScenePath} from Build Settings."); }

            // Verify Village.unity now holds the generated village.
            Scene check = EditorSceneManager.OpenScene(VillageScenePath, OpenSceneMode.Single);
            int liveHearts = heartType != null ? Object.FindObjectsByType(heartType, FindObjectsSortMode.None).Length : -1;
            GameObject liveRoot = FindRoot(check, "Village2");
            Log($"VERIFY: Village.unity scene='{check.name}' rootObj='{(liveRoot != null ? liveRoot.name : "?")}' heart={liveHearts}. Village_Legacy.unity = rollback.");
            Log("=== PHASE D DONE — next: OuterWorldBuilder.BakeWorldNavMesh, then D2 verify ===");
        }

        [MenuItem("Defenders/Village2/D2. Verify Live Village Walkable")]
        public static void D2_VerifyLiveVillage()
        {
            Log("=== PHASE D2: Verify Village.unity navmesh connectivity START ===");
            EditorSceneManager.OpenScene(VillageScenePath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(OuterWorldScenePath, OpenSceneMode.Additive);
            VerifyConnectivityFromPlaza();
            Log("=== PHASE D2 DONE ===");
        }

        // =====================================================================
        // PHASE C-verify - headless walled-in check. Opens the baked scenes, samples
        // the navmesh in the plaza + far out on the terrain in all 4 directions, and
        // runs CalculatePath. A COMPLETE path = the hero can walk village -> terrain
        // through that side. Confirms the owner-flagged risk WITHOUT a playtest.
        // =====================================================================
        [MenuItem("Defenders/Village2/C2. Verify Walkable (navmesh connectivity)")]
        public static void C2_VerifyWalkable()
        {
            Log("=== PHASE C2: Verify navmesh connectivity START ===");
            EditorSceneManager.OpenScene(Village2ScenePath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(OuterWorldScenePath, OpenSceneMode.Additive);
            VerifyConnectivityFromPlaza();
            Log("=== PHASE C2 DONE ===");
        }

        // Samples the navmesh near the plaza + far out on the terrain in all 4 cardinals,
        // runs CalculatePath, logs how many sides are COMPLETE (hero can walk out). Assumes
        // the village scene + OuterWorld are already open with the combined navmesh baked.
        static void VerifyConnectivityFromPlaza()
        {
            if (!NavMesh.SamplePosition(new Vector3(6f, 0f, 0f), out NavMeshHit plaza, 12f, NavMesh.AllAreas))
            {
                Err("No navmesh near the village plaza (sample @ (6,0,0) r=12 failed). Bake produced no walkable ground inside.");
                return;
            }
            Log($"Plaza navmesh point: {plaza.position}");

            (string dir, Vector3 p)[] farPoints =
            {
                ("EAST",  new Vector3( 90f, 0f,   0f)),
                ("WEST",  new Vector3(-90f, 0f,   0f)),
                ("NORTH", new Vector3(  0f, 0f,  90f)),
                ("SOUTH", new Vector3(  0f, 0f, -90f)),
            };

            int complete = 0;
            foreach (var (dir, p) in farPoints)
            {
                if (!NavMesh.SamplePosition(p, out NavMeshHit far, 30f, NavMesh.AllAreas))
                {
                    Warn($"  {dir}: no terrain navmesh near {p} (r=30) — terrain may not extend here.");
                    continue;
                }
                var path = new NavMeshPath();
                bool ok = NavMesh.CalculatePath(plaza.position, far.position, NavMesh.AllAreas, path);
                Log($"  {dir}: target {far.position} -> path status={path.status} corners={path.corners.Length} (ok={ok})");
                if (path.status == NavMeshPathStatus.PathComplete) complete++;
            }

            Log($"VERIFY: {complete}/4 cardinal directions reachable village -> terrain (COMPLETE path).");
            if (complete == 0) Err("WALLED IN: hero cannot reach the terrain on any side. Gate openings/terrain bake need work.");
            else if (complete < 4) Warn($"Partial: {complete}/4 sides open — usable, but some gates may be sealed.");
            else Log("ALL 4 sides open — hero can walk out every gate onto the terrain.");
        }

        static bool NameOrAncestorContainsAny(Transform t, string[] fragments)
        {
            for (Transform cur = t; cur != null; cur = cur.parent)
                foreach (var f in fragments)
                    if (cur.name.Contains(f)) return true;
            return false;
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
